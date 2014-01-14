//#region Пролог
#define DebugOutput
using System.Globalization;
using JetBrains.Util;
using Nitra.Internal;
using Nitra.Internal.Recovery;
using Nitra.Runtime.Errors;

using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Xml.Linq;

using ParsedSeqKey = Nemerle.Builtins.Tuple<Nitra.Internal.Recovery.ParsedSequence, int>;
using ParsedNode = Nemerle.Builtins.Tuple<Nitra.Internal.Recovery.ParsedSequence, Nitra.Internal.Recovery.ParsedSubrule>;

using NB = Nemerle.Builtins;
using IntRuleCallKey = Nemerle.Builtins.Tuple<int, Nitra.Internal.Recovery.RuleCallKey>;
using SCG = System.Collections.Generic;

using SubruleParses = System.Collections.Generic.Dictionary<Nitra.Internal.Recovery.ParsedSubrule, int>;
using ParsedSequenceAndSubrule = Nemerle.Builtins.Tuple<Nitra.Internal.Recovery.ParsedSubrule, /*Inserted tokens*/int, Nitra.Internal.Recovery.ParsedSequence>;

#if NITRA_RUNTIME
namespace Nitra.Strategies
#else
// ReSharper disable once CheckNamespace
namespace Nitra.DebugStrategies
#endif
{
  using ParsedSequenceAndSubrules = Nemerle.Core.list<ParsedSequenceAndSubrule>;
  using FlattenSequences = List<Nemerle.Core.list<ParsedSequenceAndSubrule>>;
  using SubruleParsesAndEnd = Nemerle.Builtins.Tuple<SubruleParses, int>;
  using ParserData = Tuple<int, int, List<ParsedStateInfo>>;
  using ReportData = Action<RecoveryResult, List<RecoveryResult>, List<RecoveryResult>, List<RecoveryStackFrame>>;
  using ParseAlternativeNodes = Nemerle.Core.list<ParseAlternativeNode>;

  using ParsedList = Nemerle.Core.list<ParsedNode>;
  using Nitra.Runtime;

//#endregion

  public class Recovery
  {
    public const int Fail = int.MaxValue;
    public ReportData ReportResult;

    public Recovery(ReportData reportResult)
    {
      ReportResult = reportResult;
    }

    public virtual int Strategy(ParseResult parseResult)
    {
      Debug.IndentSize = 1;
      //Debug.Assert(parseResult.RecoveryStacks.Count > 0);

      var timer = Stopwatch.StartNew();

      var textLen = parseResult.Text.Length;
      var rp = new RecoveryParser(parseResult);
      rp.StartParse(parseResult.RuleParser);//, parseResult.MaxFailPos);
      var startSeq = rp.Sequences.First().Value;

      timer.Stop();
      Debug.WriteLine("FindNextError took: " + timer.Elapsed);
      timer.Restart();

      RecoverAllWays(rp);

      timer.Stop();
      Debug.WriteLine("RecoverAllWays took: " + timer.Elapsed);

      var memiozation = new Dictionary<ParsedSeqKey, SubruleParsesAndEnd>();
      FindBestPath(startSeq, textLen, memiozation);
      var results = FlattenSequence(new FlattenSequences() { ParsedSequenceAndSubrules.Nil._N_constant_object }, 
        parseResult, startSeq, textLen, memiozation[new ParsedSeqKey(startSeq, textLen)].Field1, memiozation);
      ParsePathsVisializer.PrintPaths(parseResult, results);
      return parseResult.Text.Length;
    }

    private FlattenSequences FlattenSubrule(FlattenSequences prevs, ParseResult parseResult, ParsedSequence seq, SubruleParses parses, ParsedSubrule subrule, int subruleInsertedTokens, Dictionary<ParsedSeqKey, SubruleParsesAndEnd> memiozation)
    {
      Begin:

      if (subrule.State == 9 && subrule.Begin == 8 && subrule.End == 15)
      {}

      var currentNodes = new FlattenSequences();
      //var subruledDesc = seq.GetSubruleDescription(subrule.State);
      if (subrule.IsEmpty || seq.IsSubruleVoid(subrule))
      {
        //if (subruleInsertedTokens > 0)
        //  Debug.WriteLine("Inserted = " + subruleInsertedTokens + "  -  " + subruledDesc + "  Seq: " + seq);
      }
      else
      {
        var sequences = seq.GetSequencesForSubrule(subrule).ToArray();

        if (sequences.Length > 1)
        {
        }

        foreach (var subSequences in sequences)
        {
          //Debug.WriteLine(subruledDesc);
          var result = FlattenSequence(prevs, parseResult, subSequences, subrule.End, subruleInsertedTokens, memiozation);
          currentNodes.AddRange(result);
        }
      }

      if (currentNodes.Count == 0) // если не было сабсиквенсов, надо создать продолжения из текущего сабруля
      {
        foreach (var prev in prevs)
          currentNodes.Add(new ParsedSequenceAndSubrules.Cons(new ParsedSequenceAndSubrule(subrule, subruleInsertedTokens, seq), prev));
      }

      var nextSubrules = seq.GetNextSubrules(subrule, parses.Keys).ToArray();
      switch (nextSubrules.Length)
      {
        case 0:
          return currentNodes;
        case 1:
        {
          var nextSubrule = nextSubrules[0];
          if (nextSubrule.State == 9 && nextSubrule.Begin == 8 && nextSubrule.End == 15)
          { }

          subruleInsertedTokens = parses[nextSubrule];
          if (subruleInsertedTokens == Fail)
            return currentNodes;
          // recursive self call...
          prevs = currentNodes;
          subrule = nextSubrule;
          goto Begin;
        }
        default:
        {
          var resultNodes = new FlattenSequences();

          foreach (var nextSubrule in nextSubrules)
          {
            var newSubruleInsertedTokens = parses[nextSubrule];
            if (newSubruleInsertedTokens == Fail)
              continue;

            var result = FlattenSubrule(currentNodes, parseResult, seq, parses, nextSubrule, newSubruleInsertedTokens, memiozation);
            resultNodes.AddRange(result);
          }

          return resultNodes;
        }
      }
    }

    private FlattenSequences FlattenSequence(
      FlattenSequences                              prevs,
      ParseResult                                   parseResult,
      ParsedSequence                                seq,
      int                                           end,
      int                                           sequenceInsertedTokens,
      Dictionary<ParsedSeqKey, SubruleParsesAndEnd> memiozation)
    {
      if (seq.StartPos == 8 && end == 15)
        Debug.Assert(true);

      SubruleParsesAndEnd first;
      var key = new ParsedSeqKey(seq, end);
      if (!memiozation.TryGetValue(key, out first))
        Debug.Assert(false);

      var parses = first.Field0;

      if (first.Field1 == Fail)
        return new FlattenSequences();

      if (sequenceInsertedTokens != first.Field1)
      {
        //Debug.Assert(false);
        return new FlattenSequences();
      }

      var firstSubrules = seq.GetFirstSubrules(parses.Keys).ToArray();

      if (firstSubrules.Length > 1)
      { }

      var total = new FlattenSequences();

      foreach (var firstSubrule in firstSubrules)
      {
        var insertedTokens = parses[firstSubrule];
        if (insertedTokens == Fail)
          continue;

        var result = FlattenSubrule(prevs, parseResult, seq, parses, firstSubrule, insertedTokens, memiozation);
        total.AddRange(result);
      }

      return total;
    }

    private static int FindBestPath(ParsedSequence seq, int end, Dictionary<ParsedSeqKey, SubruleParsesAndEnd> memiozation)
    {
      if (end == 39)
      { }
      if (end == 86)
      { }
      SubruleParsesAndEnd result;

      var key = new ParsedSeqKey(seq, end);

      if (memiozation.TryGetValue(key, out result))
        return result.Field1;

      if (seq.StartPos == end)
      {
        memiozation.Add(key, new SubruleParsesAndEnd(new Dictionary<ParsedSubrule, int>(), seq.ParsingSequence.MandatoryTokenCount));
        return seq.ParsingSequence.MandatoryTokenCount;
      }

      var results = new Dictionary<ParsedSubrule, int>();
      var validSubrules = seq.GetValidSubrules(end).ToList();
      if (validSubrules.Count == 0)
      {
        memiozation.Add(key, new SubruleParsesAndEnd(results, 0));
        return 0;
      }
      memiozation.Add(key, new SubruleParsesAndEnd(results, Fail));

      foreach (var subrule in validSubrules)
      {
        var localMin = Fail;

        if (seq.IsSubruleVoid(subrule))
          localMin = 0;
        else
        {
          var subSeqs = seq.GetSequencesForSubrule(subrule).ToArray();
          var hasSequence = false;
          foreach (var subSeq in subSeqs)
          {
            hasSequence = true;
            var localRes = FindBestPath(subSeq, subrule.End, memiozation);

            if (localRes < localMin)
              localMin = localRes;
          }

          if (!hasSequence)
          {
            if (subrule.State == ParsedSequence.DeletedTokenState)
              localMin = 3; // оцениваем удаление на треть дороже вставки
            if (subrule.State == ParsedSequence.DeletedGarbageState)
              localMin = 0; // грязь не оценивается
            else if (subrule.IsEmpty)
              localMin = seq.SubruleMandatoryTokenCount(subrule) * 2;
            else
              localMin = 0;
          }
        }

        results[subrule] = localMin;
      }

      int comulativeMin;
      if (results.Count == 0)
      {}
      var bestResults = RemoveWorstPaths2(seq, end, results, out comulativeMin);
      var result2 = new SubruleParsesAndEnd(bestResults, comulativeMin);
      memiozation[key] = result2;

      return result2.Field1;
    }

 
    public static int GetNodeWeight(Dictionary<ParsedSubrule, int> prevCumulativeMinMap, ParsedSubrule key)
    {
      int weight;
      if (prevCumulativeMinMap.TryGetValue(key, out weight))
        return weight;
      return int.MaxValue;
    }

    private static SubruleParses RemoveWorstPaths2(ParsedSequence seq, int end, SubruleParses parses, out int comulativeMin)
    {
      var comulativeCost = new SubruleParses();
      bool updated = true;
      while (updated)
      {
        updated = false;
        foreach (var parse in parses)
        {
          var subrule = parse.Key;
          int oldCount;
          if (!comulativeCost.TryGetValue(subrule, out oldCount))
            updated = true;
          int min;
          if (seq.StartPos == subrule.Begin && seq.ParsingSequence.StartStates.Contains(subrule.State))
            min = 0;
          else
          {
            min = Fail;
            int prevCount;
            foreach (var prevSubrule in seq.GetPrevSubrules(subrule, parses.Keys))
              if (comulativeCost.TryGetValue(prevSubrule, out prevCount))
                min = Math.Min(min, prevCount);
          }
          var newCount = AddOrFail(min, parses[subrule]);
          comulativeCost[subrule] = newCount;
          updated = updated || oldCount != newCount;
        }
      }

      var toProcess = new SCG.Queue<ParsedSubrule>(seq.GetLastSubrules(parses.Keys, end));
      var comulativeMin2 = toProcess.Min(s => comulativeCost[s]);
      comulativeMin = comulativeMin2;
      toProcess = new SCG.Queue<ParsedSubrule>(toProcess.Where(s => comulativeCost[s] == comulativeMin2));
      var good = new SubruleParses();
      while (toProcess.Count > 0)
      {
        var subrule = toProcess.Dequeue();
        if (good.ContainsKey(subrule))
          continue;
        good.Add(subrule, parses[subrule]);
        var prev = seq.GetPrevSubrules(subrule, parses.Keys).ToList();
        if (prev.Count > 0)
        {
          int min;
          if (seq.StartPos == subrule.Begin && seq.ParsingSequence.StartStates.Contains(subrule.State))
            min = 0;
          else
            min = prev.Min(s => comulativeCost[s]);
          foreach (var prevSubrule in prev)
            if (comulativeCost[prevSubrule] == min)
              toProcess.Enqueue(prevSubrule);
        }
      }
      return good;
    }

    private static SubruleParses RemoveWorstPaths(ParsedSequence seq, int end, SubruleParses parses)
    {
      //if (parses.Count <= 1)
      //  return parses;

      var prevCumulativeMinMap = new Dictionary<ParsedSubrule, int>(); // ParsedSubrule -> CumulativeMin
      var subrules = parses.Keys.ToList();
      subrules.Add(new ParsedSubrule(end, end, -1));
      var currs = new SCG.Queue<ParsedSubrule>(seq.GetFirstSubrules(subrules));
      foreach (var startSubrule in currs)
        prevCumulativeMinMap.Add(startSubrule, 0);

      var good  = new SubruleParses();

      while (currs.Count > 0)
      {
        var curr = currs.Dequeue();

        if (curr.State == -1)
          continue;

        var prevCumulativeMin = prevCumulativeMinMap[curr];
        var nexts = seq.GetNextSubrules(curr, subrules).ToArray();
        var delta = parses[curr];
        var currCumulativeMin = delta + prevCumulativeMin;

        foreach (var next in nexts)
        {
          var cumulativeMin = GetNodeWeight(prevCumulativeMinMap, next);
          if (currCumulativeMin < cumulativeMin)
          {
            prevCumulativeMinMap[next] = currCumulativeMin;
            currs.Enqueue(next);
          }
        }
      }

      return good;
    }

    private static int AddOrFail(int source, int addition)
    {
      return source == Fail || addition == Fail ? Fail : source + addition;
    }

    private void FindMaxFailPos(RecoveryParser rp)
    {
      int maxPos;
      do
      {
        maxPos = rp.MaxPos;
        int count;
        do
        {
          count = rp.Records[maxPos].Count;
          foreach (var sequence in rp.Records[maxPos].Select(r => r.Sequence).Distinct())
          {
            if (sequence.IsToken)
              continue;
            foreach (var subrule in sequence.ParsedSubrules)
              if (subrule.State > ParsedSequence.DeletedTokenState && subrule.End == maxPos && !sequence.ParsingSequence.States[subrule.State].IsToken)
                rp.PredictionOrScanning(subrule.Begin, new ParseRecord(sequence, subrule.State, subrule.Begin), false);
          }
          rp.Parse();
        }
        while (count < rp.Records[maxPos].Count);
      }
      while (maxPos < rp.MaxPos);
    }

    private void RecoverAllWays(RecoveryParser rp)
    {
// ReSharper disable once RedundantAssignment
      int maxPos = rp.MaxPos;

      do
      {
        FindMaxFailPos(rp);
        maxPos = rp.MaxPos;
        var records = new SCG.Queue<ParseRecord>(rp.Records[maxPos]);
        var prevRecords = new SCG.HashSet<ParseRecord>(rp.Records[maxPos]);

        var garbage = TryDeleteTokens(rp, maxPos, records);

        if (garbage > 0)
        {
          //maxPos = rp.MaxPos;
          continue;
        }

        do
        {
          while (records.Count > 0)
          {
            var record = records.Dequeue();

            if (record.IsComplete)
            {
              rp.StartParseSubrule(maxPos, record);
              continue;
            }
            if (record.Sequence.IsToken)
              continue;

            //var predicate = record.ParsingState as ParsingState.Predicate;
            //if (predicate != null)
            //  if (!predicate.HeadPredicate.apply(maxPos, rp.ParseResult.Text, rp.ParseResult))
            //    continue;

            foreach (var state in record.ParsingState.Next)
            {
              var newRecord = new ParseRecord(record.Sequence, state, maxPos);
              if (!rp.Records[maxPos].Contains(newRecord))
              {
                records.Enqueue(newRecord);
                prevRecords.Add(newRecord);
              }
            }

            rp.SubruleParsed(maxPos, maxPos, record);
            
            //if (rp.ParseResult.Text.Length != maxPos)
            rp.PredictionOrScanning(maxPos, record, false);
          }

          rp.Parse();

          foreach (var record in rp.Records[maxPos])
            if (!prevRecords.Contains(record))
              records.Enqueue(record);
          prevRecords.UnionWith(rp.Records[maxPos]);

          //if (records.Count == 0)
          //{
          //  var count = 0;
          //  for (int i = maxPos + 1; i < rp.MaxPos; i++)
          //  {
          //    var nextRecords = rp.Records[i];
          //    if (nextRecords == null)
          //      continue;
          //    prevRecords.Clear();
          //    foreach (var record in nextRecords)
          //    {
          //      if (record.Sequence.StartPos == maxPos && !record.IsComplete)
          //      {
          //        count++;
          //        records.Enqueue(record);
          //        prevRecords.Add(record);
          //      }
          //    }
          //    maxPos = i;
          //    break;
          //  }
          //}
        } while (records.Count > 0);

        //maxPos = Array.FindIndex(rp.Records, maxPos + 1, IsNotNull);

      } while (rp.MaxPos > maxPos); //while (maxPos >= 0 && maxPos < textLen);
    }

    private static int TryDeleteTokens(RecoveryParser rp, int maxPos, SCG.Queue<ParseRecord> records)
    {
      var text = rp.ParseResult.Text;
      var parseResult = rp.ParseResult;
      var grammar = parseResult.RuleParser.Grammar;
      var res = grammar.ParseAllGrammarTokens(maxPos, parseResult);
      RemoveEmpty(res, maxPos);

      if (res.Count > 0)
      {
        foreach (var nextPos in res)
        {
          var res2 = grammar.ParseAllVoidGrammarTokens(nextPos, parseResult);
          RemoveEmpty(res2, nextPos);

          if (res2.Count == 0)
          {
            var skipToken = new ParsedSubrule(maxPos, nextPos, ParsedSequence.DeletedTokenState);
            foreach (var record in records)
              if (!record.IsComplete)
              {
                record.Sequence.ParsedSubrules.Add(skipToken);
                rp.PredictionOrScanning(nextPos, record, false);
              }
          }
          else
            foreach (var nextPos2 in res2)
            {
              var skipToken = new ParsedSubrule(maxPos, nextPos2, ParsedSequence.DeletedTokenState);

              foreach (var record in records)
                if (!record.IsComplete)
                {
                  record.Sequence.ParsedSubrules.Add(skipToken);
                  rp.PredictionOrScanning(nextPos2, record, false);
                }
            }
        }
      }
      else if (text.Length != maxPos) // грязь
      {
        var i = maxPos + 1;
        for (; i < text.Length; i++) // крутимся пока не будет распознан токен или достигнут конец строки
        {
          var res2 = grammar.ParseAllGrammarTokens(i, parseResult);
          RemoveEmpty(res2, i);
          if (res2.Count > 0)
            break;
        }

        var skipToken = new ParsedSubrule(maxPos, i, ParsedSequence.DeletedGarbageState); // сабруль пропускающий грязь
        foreach (var record in records)
          if (!record.IsComplete)
          {
            record.Sequence.ParsedSubrules.Add(skipToken); // делаем вид, что отпарсили грязь как один удаленный токен
            rp.PredictionOrScanning(i, record, false);
          }

        rp.Parse();

        return i;
      }

      return 0;
    }

    private static int RemoveEmpty(HashSet<int> res, int maxPos)
    {
      return res.RemoveWhere(x => x <= maxPos);
    }
  }

#region Utility methods

  internal static class RecoveryUtils
  {
    public static List<T> FilterMax<T>(this SCG.ICollection<T> candidates, Func<T, int> selector)
    {
      var count = candidates.Count;
      if (candidates.Count <= 1)
      {
        var lst = candidates as List<T>;
        if (lst == null)
        {
          lst = new List<T>(count);
          lst.AddRange(candidates);
        }
        return lst;
      }

      var max1 = candidates.Max(selector);
      var res2 = candidates.Where(c => selector(c) == max1);
      return res2.ToList();
    }

    public static List<T> FilterMin<T>(this SCG.ICollection<T> candidates, Func<T, int> selector)
    {
      var count = candidates.Count;
      if (candidates.Count <= 1)
      {
        var lst = candidates as List<T>;
        if (lst == null)
        {
          lst = new List<T>(count);
          lst.AddRange(candidates);
        }
        return lst;
      }

      var min = candidates.Min(selector);
      var res2 = candidates.Where(c => selector(c) == min);
      return res2.ToList();
    }

    public static IEnumerable<T> FilterIfExists<T>(this List<T> res2, Func<T, bool> predicate)
    {
      return res2.Any(predicate) ? res2.Where(predicate) : res2;
    }
  }

  public static class ParsePathsVisializer
  {
    #region HtmlTemplate
    private const string HtmlTemplate = @"
<html>
<head>
    <title>Pretty Print</title>
    <meta http-equiv='Content-Type' content='text/html;charset=utf-8'/>
    <style type='text/css'>
pre
{
  color: black;
  font-weight: normal;
  font-size: 12pt;
  font-family: Consolas, Courier New, Monospace;
}

.default
{
  color: black;
  background: white;
}

.garbage
{
  color: red;
  background: lightpink;
}

.deleted
{
  background: lightpink;
}

.parsed
{
  color: Green;
  background: LightGreen;
}

.prefix
{
  color: Indigo;
  background: Plum;
}

.postfix
{
  color: blue;
  background: lightgray;
}

.skipedState
{
  color: darkgray;
  background: lightgray;
  -webkit-print-color-adjust:exact;
}
.currentRulePrefix
{
  color: darkgoldenrod;
  background: lightgoldenrodyellow;
}
</style>
</head>
<body>
<pre>
<content/>
</pre>
</body>
</html>
"; 
    #endregion

    static readonly XAttribute _garbageClass = new XAttribute("class", "garbage");
    static readonly XAttribute _deletedClass = new XAttribute("class", "deleted");
    static readonly XAttribute _topClass          = new XAttribute("class", "parsed");
    static readonly XAttribute _prefixClass       = new XAttribute("class", "prefix");
    static readonly XAttribute _postfixClass      = new XAttribute("class", "postfix");
    static readonly XAttribute _skipedStateClass  = new XAttribute("class", "skipedState");
    static readonly XAttribute _default           = new XAttribute("class", "default");

    static readonly XElement  _start              = new XElement("span", _default, "▸");
    static readonly XElement  _end                = new XElement("span", _default, "◂");

    public static void PrintPaths(ParseResult parseResult, FlattenSequences paths)
    {
      var results = new List<XNode> { new XText(parseResult.DebugText + "\r\n\r\n") };

      foreach (var path in paths)
        PrintPath(results, parseResult.Text, path);

      var template = XElement.Parse(HtmlTemplate);
      var content = template.Descendants("content").First();
      Debug.Assert(content.Parent != null);
      content.Parent.ReplaceAll(results);
      var filePath = Path.ChangeExtension(Path.GetTempFileName(), ".html");
      template.Save(filePath);
      Process.Start(filePath);
    }

    public static void PrintPath(List<XNode> results, string text, ParsedSequenceAndSubrules path)
    {
      var isPrevInsertion = false;

      foreach (var node in path.Reverse())
      {
        var subrule = node.Field0;
        var insertedTokens = node.Field1;
        var seq = node.Field2;

        if (subrule.State <= ParsedSequence.DeletedTokenState)
        {
          isPrevInsertion = false;
          var title = new XAttribute("title", "Deleted token;  Subrule: " + subrule + ";  Sequence: " + seq + ";");
          results.Add(new XElement("span", subrule.State == ParsedSequence.DeletedTokenState ? _deletedClass : _garbageClass, 
            title, text.Substring(subrule.Begin, subrule.End - subrule.Begin)));
        }
        else if (insertedTokens > 0)
        {
          var desc = seq.ParsingSequence.States[subrule.State].Description;
          if (!subrule.IsEmpty)
          { }
          var title = new XAttribute("title", "Inserted tokens: " + insertedTokens + ";  Subrule: " + subrule + ";  Sequence: " + seq + ";");
          results.Add(new XElement("span", _skipedStateClass, title, isPrevInsertion ? " " + desc : desc));
          isPrevInsertion = true;
        }
        else
        {
          var desc = seq.ParsingSequence.States[subrule.State].Description;
          var title = new XAttribute("title", "Description: " + desc + ";  Subrule: " + subrule + ";  Sequence: " + seq + ";");
          //if (subrule.IsEmpty)
          //  results.Add(new XElement("span", title, "▴"));
          //else
          results.Add(new XElement("span", title, text.Substring(subrule.Begin, subrule.End - subrule.Begin)));

          isPrevInsertion = false;
        }
      }

      results.Add(new XText("\r\n"));
    }
  }


#endregion
}
