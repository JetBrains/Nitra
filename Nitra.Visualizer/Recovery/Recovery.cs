//#region Пролог
//#define DebugOutput
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
using SCG = System.Collections.Generic;

using SubruleParses = System.Collections.Generic.Dictionary<Nitra.Internal.Recovery.ParsedSubrule, int>;
using ParsedSequenceAndSubrule = Nemerle.Builtins.Tuple</*Inserted tokens*/int, Nitra.Internal.Recovery.ParsedSequence, Nitra.Internal.Recovery.ParsedSubrule>;
using ParsedSequenceAndSubrule2 = Nemerle.Builtins.Tuple<string, Nitra.Internal.Recovery.ParsedSubrule, /*Inserted tokens*/int, Nitra.Internal.Recovery.ParsedSequence>;

#if NITRA_RUNTIME
namespace Nitra.Strategies
#else
// ReSharper disable once CheckNamespace

static class RecoveryDebug
{
  public static string CurrentTestName;
}

namespace Nitra.DebugStrategies
#endif
{
  using ParsedSequenceAndSubrules = Nemerle.Core.list<ParsedSequenceAndSubrule>;
  using FlattenSequences = List<Nemerle.Core.list<ParsedSequenceAndSubrule>>;
  using SubruleParsesAndEnd = Nemerle.Builtins.Tuple<SubruleParses, int>;

  using ParsedList = Nemerle.Core.list<ParsedNode>;
  using Nitra.Runtime;

//#endregion

  public class Recovery
  {
    public const int NumberOfTokensForSpeculativeDeleting = int.MaxValue;
    public const int Fail = int.MaxValue;
    private readonly Dictionary<ParsedNode, bool> _deletedToken = new Dictionary<ParsedNode, bool>();

    public virtual int Strategy(ParseResult parseResult)
    {
      //Debug.Assert(parseResult.RecoveryStacks.Count > 0);

#if DebugOutput
      Debug.IndentSize = 1;
      var timer = Stopwatch.StartNew();
      Debug.WriteLine(RecoveryDebug.CurrentTestName + " -----------------------------------------------------------");
#endif

      _deletedToken.Clear();
      var textLen = parseResult.Text.Length;
      var rp = new RecoveryParser(parseResult);
      rp.StartParse(parseResult.RuleParser);//, parseResult.MaxFailPos);
      var startSeq = rp.Sequences.First().Value;

      UpdateEarleyParseTime();
#if DebugOutput
      timer.Stop();
      Debug.WriteLine("Earley parse took: " + timer.Elapsed);
      timer.Restart();
#endif

      RecoverAllWays(rp);

      UpdateRecoverAllWaysTime();
#if DebugOutput
      timer.Stop();
      Debug.WriteLine("RecoverAllWays took: " + timer.Elapsed);
      timer.Restart();
#endif

      var memiozation = new Dictionary<ParsedSeqKey, SubruleParsesAndEnd>();
      FindBestPath(startSeq, textLen, memiozation);

      UpdateFindBestPathTime();
#if DebugOutput
      timer.Stop();
      Debug.WriteLine("FindBestPath took: " + timer.Elapsed);
      timer.Restart();
#endif

      var results = FlattenSequence(new FlattenSequences() { Nemerle.Collections.NList.ToList(new ParsedSequenceAndSubrule[0]) }, 
      parseResult, startSeq, textLen, memiozation[new ParsedSeqKey(startSeq, textLen)].Field1, memiozation);

      UpdateFlattenSequenceTime();
#if DebugOutput
      timer.Stop();
      Debug.WriteLine("FlattenSequence took: " + timer.Elapsed);
#endif

      CollectError(rp, results);
      return parseResult.Text.Length;
    }

    private void CollectError(RecoveryParser rp, FlattenSequences results)
    {
      var text         = rp.ParseResult.Text;
      var expected     = new Dictionary<NSpan, HashSet<ParsedNode>>();
      var failSeq      = default(ParsedSequence);
      var failSubrule  = default(ParsedSubrule);
      var skipRecovery = false;

      foreach (var result in results)
      {
        var reverse = result.ToArray().Reverse();
        foreach (var x in reverse)
        {
          var ins     = x.Field0;
          var seq     = x.Field1;
          var subrule = x.Field2;

          if (skipRecovery)
          {
            if (ins == 0 && seq.ParsingSequence.RuleName != "s")
            {
              skipRecovery = false;
              //Debug.WriteLine(x);
              HashSet<ParsedNode> parsedNodes;
              var span = new NSpan(failSubrule.Begin, subrule.Begin);
              if (!expected.TryGetValue(span, out parsedNodes))
              {
                parsedNodes = new HashSet<ParsedNode>();
                expected[span] = parsedNodes;
              }

              if (failSubrule.IsEmpty)
                parsedNodes.Add(new ParsedNode(failSeq, failSubrule));
              else
                parsedNodes.Add(new ParsedNode(seq, subrule));
            }
          }
          else
          {
            if (ins != 0)
            {
              failSeq      = seq;
              failSubrule  = subrule;
              skipRecovery = true;
            }
          }
        }
      }

      var parseResult = rp.ParseResult;
      foreach (var e in expected)
        parseResult.ReportError(new ExpectedSubrulesError(new Location(parseResult.OriginalSource, e.Key.StartPos, e.Key.EndPos), e.Value));
    }

    private FlattenSequences FlattenSubrule(FlattenSequences prevs, ParseResult parseResult, ParsedSequence seq, SubruleParses parses, ParsedSubrule subrule, int subruleInsertedTokens, Dictionary<ParsedSeqKey, SubruleParsesAndEnd> memiozation)
    {
      Begin:

      var txt = parseResult.Text.Substring(subrule.Begin, subrule.End - subrule.Begin);
      var stateIndex = subrule.State;
      var state = stateIndex < 0 ? null : seq.ParsingSequence.States[stateIndex];

      if (subrule.End == 11)
      {}

      var currentNodes = new FlattenSequences();
      //var subruledDesc = seq.GetSubruleDescription(subrule.State);
      if (subrule.IsEmpty)
      {
        //if (subruleInsertedTokens > 0)
        //  Debug.WriteLine("Inserted = " + subruleInsertedTokens + "  -  " + subruledDesc + "  Seq: " + seq);
      }
      else
      {
        var sequences = seq.GetSequencesForSubrule(subrule).ToArray();

        if (sequences.Length > 1)
        { }

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
          currentNodes.Add(new ParsedSequenceAndSubrules.Cons(new ParsedSequenceAndSubrule(subruleInsertedTokens, seq, subrule), prev));
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
      var seqTxt = parseResult.Text.Substring(seq.StartPos, end - seq.StartPos);

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

      var total = new FlattenSequences();

      foreach (var firstSubrule in firstSubrules)
      {
        var txt = parseResult.Text.Substring(firstSubrule.Begin, firstSubrule.End - firstSubrule.Begin);
        var stateIndex = firstSubrule.State;
        var state = stateIndex < 0 ? null : seq.ParsingSequence.States[stateIndex];

        var insertedTokens = parses[firstSubrule];
        if (insertedTokens == Fail)
          continue;

        var result = FlattenSubrule(prevs, parseResult, seq, parses, firstSubrule, insertedTokens, memiozation);
        total.AddRange(result);
      }

      return total;
    }

    private int FindBestPath(ParsedSequence seq, int end, Dictionary<ParsedSeqKey, SubruleParsesAndEnd> memiozation)
    {
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
        if (_deletedToken.ContainsKey(new ParsedNode(seq, subrule)))
          localMin = 1; // оцениваем удаление как одну вставку
        else
          localMin = LocalMinForSubSequence(seq, memiozation, subrule, localMin);

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

    private int LocalMinForSubSequence(ParsedSequence seq, Dictionary<ParsedSeqKey, SubruleParsesAndEnd> memiozation, ParsedSubrule subrule, int localMin)
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
        if (subrule.IsEmpty)
          localMin = seq.SubruleMandatoryTokenCount(subrule);
        else
          localMin = 0;
      }
      return localMin;
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

    private static int AddOrFail(int source, int addition)
    {
      return source == Fail || addition == Fail ? Fail : source + addition;
    }

    private List<Tuple<int, ParsedSequence>> FindMaxFailPos(RecoveryParser rp)
    {
      // В следстии особенностей работы основного парсере некоторые правила могут с
      var result = new List<Tuple<int, ParsedSequence>>(3);
      int maxPos;
      do
      {
        maxPos = rp.MaxPos;
        int count;
        do
        {
          var records = rp.Records[maxPos].ToArray(); // to materialize collection

          // Среди текущих состояний могут быть эски. Находим их и засовываем их кишки в Эрли.
          foreach (var record in records)
            if (record.State >= 0)
            {
              var state = record.ParsingState;
              if (state.IsToken)
              {
                var simple = state as ParsingState.Simple;
                if (simple == null || simple.RuleParser.RuleName != "S" && simple.RuleParser.RuleName != "s")
                  continue;
                rp.PredictionOrScanning(maxPos, record, false);
              }
            }

          count = records.Length;
          var sequences = GetSequences(rp, maxPos).ToArray();
          foreach (var sequence in sequences)
          {
            if (sequence.IsToken)
            {
              // если последовательность - это эска, пробуем удалить за ней грязь или добавить ее в result для дальнешей попытки удаления токенов.
              if (sequence.ParsingSequence.RuleName == "s")
              {
                if (TryDeleteGarbage(rp, maxPos, sequence))
                  continue;
                result.Add(Tuple.Create(maxPos, sequence));
                continue;
              }

              if (sequence.ParsingSequence.RuleName != "S")
                continue;
            }
            // Если в последовательнсости есть пропарсивания оканчивающиеся на место падения, добавляем кишки этого состояния в Эрли.
            // Это позволит, на следующем шаге, поискать в них эски.
            foreach (var subrule in sequence.ParsedSubrules)
              if (subrule.State >= 0 && subrule.End == maxPos && sequence.ParsingSequence.SequenceInfo != null)
              {
                var state = sequence.ParsingSequence.States[subrule.State];
                if (state.IsToken)
                {
                  var simple = state as ParsingState.Simple;
                  if (simple == null || simple.RuleParser.RuleName != "S" && simple.RuleParser.RuleName != "s")
                    continue;
                }
                rp.PredictionOrScanning(subrule.Begin, new ParseRecord(sequence, subrule.State, subrule.Begin), false);
              }
          }
          rp.Parse();
        }
        while (count < rp.Records[maxPos].Count);
      }
      while (maxPos < rp.MaxPos);

      return result;
    }

    private static HashSet<ParsedSequence> GetSequences(RecoveryParser rp, int maxPos)
    {
      return new SCG.HashSet<ParsedSequence>(rp.Records[maxPos].Select(r => r.Sequence));
    }

    const int s_loopState = 0;

    /// <summary>
    /// В позиции облома может находиться "грязь", т.е. набор символов которые не удается разобрать ни одним правилом токена
    /// доступным в CompositeGrammar в данном месте. Ни одно правило не сможет спарсить этот код, так что просто ищем 
    /// следующий корректный токе и пропускаем все что идет до него (грязь).
    /// </summary>
    /// <returns>true - если грязь была удалена</returns>
    private bool TryDeleteGarbage(RecoveryParser rp, int maxPos, ParsedSequence sequence)
    {
      var text = rp.ParseResult.Text;
      if (maxPos >= text.Length)
        return false;
      var parseResult = rp.ParseResult;
      var grammar = parseResult.RuleParser.Grammar;
      var res = grammar.ParseAllGrammarTokens(maxPos, parseResult);
      RemoveEmpty(res, maxPos);

      if (res.Count == 0)
      {
        var i = maxPos + 1;
        for (; i < text.Length; i++) // крутимся пока не будет распознан токен или достигнут конец строки
        {
          var res2 = grammar.ParseAllGrammarTokens(i, parseResult);
          RemoveEmpty(res2, i);
          if (res2.Count > 0)
            break;
        }

        _deletedToken[new ParsedNode(sequence, new ParsedSubrule(maxPos, i, s_loopState))] = true;
        rp.SubruleParsed(maxPos, i, new ParseRecord(sequence, 0, maxPos));
        return true;
      }

      return false;
    }

    private void DeleteTokens(RecoveryParser rp, int pos, ParsedSequence sequence, int tokensToDelete)
    {
      if (tokensToDelete <= 0)
        return;

      var text = rp.ParseResult.Text;
      var parseResult = rp.ParseResult;
      var grammar = parseResult.RuleParser.Grammar;
      var res = grammar.ParseAllNonVoidGrammarTokens(pos, parseResult);
      RemoveEmpty(res, pos);

      if (res.Count == 0)
        return;

      foreach (var nextPos in res)
        if (CanDelete(text, pos, nextPos))
          ContinueDeleteTokens(rp, sequence, pos, nextPos, tokensToDelete);
    }

    private bool CanDelete(string text, int pos, int nextPos)
    {
      // TODO: Надо неализовать эту функцию на базе метаинформации из грамматик.
      switch (text.Substring(pos, nextPos - pos))
      {
        case ",":
        case ";": return false;
        default: return true;
      }
    }

    private void ContinueDeleteTokens(RecoveryParser rp, ParsedSequence sequence, int pos, int nextPos, int tokensToDelete)
    {
      _deletedToken[new ParsedNode(sequence, new ParsedSubrule(pos, nextPos, s_loopState))] = false;
      rp.SubruleParsed(pos, nextPos, new ParseRecord(sequence, 0, pos));

      var parseResult = rp.ParseResult;
      var grammar = parseResult.RuleParser.Grammar;
      var res2 = grammar.ParseAllVoidGrammarTokens(nextPos, parseResult);
      RemoveEmpty(res2, nextPos);

      if (res2.Count == 0)
        DeleteTokens(rp, nextPos, sequence, tokensToDelete - 1);
      foreach (var nextPos2 in res2)
      {
        //_deletedToken[new ParsedNode(sequence, new ParsedSubrule(pos, nextPos2, s_loopState))] = false;
        rp.SubruleParsed(nextPos, nextPos2, new ParseRecord(sequence, s_loopState, pos));
        DeleteTokens(rp, nextPos2, sequence, tokensToDelete - 1);
      }
    }

    private void RecoverAllWays(RecoveryParser rp)
    {
// ReSharper disable once RedundantAssignment
      int maxPos = rp.MaxPos;
      var failPositions = new HashSet<int>();
      var deleted = new List<Tuple<int, ParsedSequence>>();

      do
      {
        deleted.AddRange(FindMaxFailPos(rp));
        if (rp.MaxPos != rp.ParseResult.Text.Length)
          UpdateParseErrorCount();
        maxPos = rp.MaxPos;
        failPositions.Add(maxPos);

        var records = new SCG.Queue<ParseRecord>(rp.Records[maxPos]);
        var prevRecords = new SCG.HashSet<ParseRecord>(rp.Records[maxPos]);

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
            rp.PredictionOrScanning(maxPos, record, false);
          }

          rp.Parse();

          foreach (var record in rp.Records[maxPos])
            if (!prevRecords.Contains(record))
              records.Enqueue(record);
          prevRecords.UnionWith(rp.Records[maxPos]);
        }
        while (records.Count > 0);
      }
      while (rp.MaxPos > maxPos);

      foreach (var del in deleted)
        DeleteTokens(rp, del.Item1, del.Item2, NumberOfTokensForSpeculativeDeleting);
      rp.Parse();
    }

    private static void RemoveEmpty(HashSet<int> res, int maxPos)
    {
      res.RemoveWhere(x => x <= maxPos);
    }

    protected virtual void UpdateEarleyParseTime() { }
    protected virtual void UpdateRecoverAllWaysTime() { }
    protected virtual void UpdateFindBestPathTime() { }
    protected virtual void UpdateFlattenSequenceTime() { }
    protected virtual void UpdateParseErrorCount() { }
  }

#region Utility methods

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
  color: rgb(243, 212, 166);
  background: rgb(170, 134, 80);
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
    static readonly XAttribute _skipedStateClass  = new XAttribute("class", "skipedState");
    static readonly XAttribute _default           = new XAttribute("class", "default");

    public static void PrintPaths(ParseResult parseResult, Dictionary<ParsedNode, bool> deletedToken, FlattenSequences paths)
    {
      var results = new List<XNode> { new XText(RecoveryDebug.CurrentTestName + "\r\n" + parseResult.DebugText + "\r\n\r\n") };

      foreach (var path in paths)
        PrintPath(results, parseResult.Text, deletedToken, path);

      var template = XElement.Parse(HtmlTemplate);
      var content = template.Descendants("content").First();
      Debug.Assert(content.Parent != null);
      content.Parent.ReplaceAll(results);
      var filePath = Path.ChangeExtension(Path.GetTempFileName(), ".html");
      template.Save(filePath);
      Process.Start(filePath);
    }

    public static void PrintPath(List<XNode> results, string text, Dictionary<ParsedNode, bool> deletedToken, ParsedSequenceAndSubrules path)
    {
      var isPrevInsertion = false;

      foreach (var node in path.Reverse())
      {
        //var str = node.Field0;
        var insertedTokens = node.Field0;
        var seq            = node.Field1;
        var subrule        = node.Field2;

        bool isGarbage;
        if (deletedToken.TryGetValue(new ParsedNode(seq, subrule), out isGarbage))
        {
          isPrevInsertion = false;
          var title = new XAttribute("title", "Deleted token;  Subrule: " + subrule + ";  Sequence: " + seq + ";");
          results.Add(new XElement("span", isGarbage ? _garbageClass : _deletedClass, 
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
          results.Add(new XElement("span", title, text.Substring(subrule.Begin, subrule.End - subrule.Begin)));

          isPrevInsertion = false;
        }
      }

      results.Add(new XText("\r\n"));
    }
  }

#endregion
}
