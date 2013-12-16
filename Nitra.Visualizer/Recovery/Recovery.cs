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
      var startSeq = rp.Sequences.First();

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

      return parseResult.Text.Length;
    }

    private FlattenSequences FlattenSubrule(FlattenSequences prevs, ParseResult parseResult, ParsedSequence seq, SubruleParses parses, ParsedSubrule subrule, int subruleCumulativeInsertedTokens, int prevSubruleCumulativeInsertedTokens, int sequenceInsertedTokens, Dictionary<ParsedSeqKey, SubruleParsesAndEnd> memiozation)
    {
      Begin:

      if (subruleCumulativeInsertedTokens == Fail && prevSubruleCumulativeInsertedTokens == Fail)
        Debug.Assert(false);

      var subruleInsertedTokens = SubOrFail(subruleCumulativeInsertedTokens, prevSubruleCumulativeInsertedTokens);

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
          var newSubruleCumulativeInsertedTokens = parses[nextSubrule];
          if (newSubruleCumulativeInsertedTokens == Fail)
            return currentNodes;
          // recursive self call...
          prevs = currentNodes;
          subrule = nextSubrule;
          prevSubruleCumulativeInsertedTokens = subruleCumulativeInsertedTokens;
          subruleCumulativeInsertedTokens = newSubruleCumulativeInsertedTokens;
          goto Begin;
        }
        default:
        {
          var resultNodes = new FlattenSequences();

          foreach (var nextSubrule in nextSubrules)
          {
            var newSubruleCumulativeInsertedTokens = parses[nextSubrule];
            if (newSubruleCumulativeInsertedTokens == Fail)
              continue;

            var result = FlattenSubrule(currentNodes, parseResult, seq, parses, nextSubrule, newSubruleCumulativeInsertedTokens, subruleCumulativeInsertedTokens, sequenceInsertedTokens, memiozation);
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
      SubruleParsesAndEnd first;
      var key = new ParsedSeqKey(seq, end);
      if (!memiozation.TryGetValue(key, out first))
        Debug.Assert(false);

      var parses = first.Field0;

      if (sequenceInsertedTokens != first.Field1 || first.Field1 == Fail)
        return new FlattenSequences();

      var firstSubrules = seq.GetFirstSubrules(parses.Keys).ToArray();

      if (firstSubrules.Length > 1)
      { }

      var total = new FlattenSequences();

      foreach (var firstSubrule in firstSubrules)
      {
        var insertedTokens = parses[firstSubrule];
        if (insertedTokens == Fail)
          continue;

        var result = FlattenSubrule(prevs, parseResult, seq, parses, firstSubrule, insertedTokens, 0, sequenceInsertedTokens, memiozation);
        total.AddRange(result);
      }

      return total;
    }

    private static int FindBestPath(ParsedSequence seq, int end, Dictionary<ParsedSeqKey, SubruleParsesAndEnd> memiozation)
    {
      SubruleParsesAndEnd result;

      var key = new ParsedSeqKey(seq, end);

      if (memiozation.TryGetValue(key, out result))
        return result.Field1;

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
        if (subrule.State == 4 && subrule.Begin == 0 && subrule.End == 29)
        { }
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
            if (subrule.IsEmpty)
              localMin = seq.SubruleMandatoryTokenCount(subrule);
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
      var comulativeCost = new SubruleParses(parses);
      bool updated = true;
      while (updated)
      {
        updated = false;
        foreach (var parse in parses)
        {
          var subrule = parse.Key;
          var oldCount = comulativeCost[subrule];
          int min;
          if (seq.StartPos == subrule.Begin && seq.ParsingSequence.StartStates.Contains(subrule.State))
            min = 0;
          else
            min = seq.GetPrevSubrules(subrule, parses.Keys).Min(s => comulativeCost[s]);
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
          var min = prev.Min(s => comulativeCost[s]);
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

    private static IEnumerable<ParsedSubrule> NextSubrules(ParsedSequence seq, SubruleParses parses, ParsedSubrule curr, int next)
    {
      return seq.GetNextSubrules(curr, parses.Keys).Where(s => s.State == next);
    }

    private static int AddOrFail(int source, int addition)
    {
      return source == Fail || addition == Fail ? Fail : source + addition;
    }

    private static int SubOrFail(int source, int addition)
    {
      return source == Fail || addition == Fail ? Fail : source - addition;
    }

    private void RecoverAllWays(RecoveryParser rp)
    {
// ReSharper disable once RedundantAssignment
      int maxPos = rp.MaxPos;

      do
      {
        maxPos = rp.MaxPos;
        var records = new SCG.Queue<ParseRecord>(rp.Records[maxPos]);

        do
        {
          rp.AddedSequences.Clear();

          while (records.Count > 0)
          {
            var record = records.Dequeue();

            if (record.IsComplete || record.Sequence.IsToken)
              continue;

            var predicate = record.ParsingState as ParsingState.Predicate;
            if (predicate != null)
              if (!predicate.HeadPredicate.apply(maxPos, rp.ParseResult.Text, rp.ParseResult))
                continue;

            foreach (var state in record.ParsingState.Next)
            {
              var newRecord = new ParseRecord(record.Sequence, state, maxPos);
              if (!rp.Records[maxPos].Contains(newRecord))
                records.Enqueue(newRecord);
            }

            rp.SubruleParsed(maxPos, maxPos, record);
            
            if (rp.ParseResult.Text.Length != maxPos)
              rp.PredictionOrScanning(maxPos, record, false);
          }

          rp.Parse();

          foreach (var parsedSequence in rp.AddedSequences)
          {
            if (parsedSequence.StartPos == maxPos && !parsedSequence.IsToken)
              records.Enqueue(new ParseRecord(parsedSequence, MaxSubruleIndex(parsedSequence.ParsedSubrules), maxPos));
          }
        } while (records.Count > 0);

        //maxPos = Array.FindIndex(rp.Records, maxPos + 1, IsNotNull);

      } while (rp.MaxPos > maxPos); //while (maxPos >= 0 && maxPos < textLen);
    }

    private bool IsNotNull(HashSet<ParseRecord> x)
    {
      return x != null;
    }

    private int MaxSubruleIndex(HashSet<ParsedSubrule> parsedSubrules)
    {
      //if (parsedSubrules.Count == 0)
        return 0;

      //return parsedSubrules.Max(x => x.Index);
    }

    private bool IsUncomplate(ParseRecord obj)
    {
      return obj.IsComplete;
    }

    private static int FindLast(RecoveryParser rp)
    {
      return Array.FindLastIndex(rp.Records, x => x != null);
    }

  }

#region Utility methods

  public static class RecoveryUtils
  {
    public static int MinOrDefault<T>(this IEnumerable<T> seq, Func<T, int> selector, int defaultValue)
    {
      using (var enumerable = seq.GetEnumerator())
      {
        if (enumerable.MoveNext())
        {
          var min = selector(enumerable.Current);

          while (enumerable.MoveNext())
          {
            var cur = selector(enumerable.Current);
            if (cur < min)
              min = cur;
          }
          return min;
        }

        return defaultValue;
      }
    }

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

    public static void RemoveFramesUnnecessaryAlternatives(List<RecoveryStackFrame> allFrames, RecoveryStackFrame head)
    {
      // reset IsMarked
      foreach (var frame in allFrames)
        frame.IsMarked = false;

      // set IsMarked on parents of head

      CheckGraph(allFrames);

      RemoveOthrHeads(allFrames, head);

      CheckGraph(allFrames);

      // удаляем ParseAlternatives-ы с которых не может быть начат парсинг фрейма head.
      UpdateParseAlternativesTopToDown(allFrames);

      // выбрать самые длинные пропарсивания в префиксных и постфиксных правилах
      for (int index = allFrames.Count - 1; index >= 0; index--)
      {
        var frame = allFrames[index];

        if (!frame.Best)
          continue;

        if (frame.IsMarked)
        {
          frame.IsMarked = false;
          var alternatives0 = FilterParseAlternativesWichEndsEqualsParentsStarts(frame);

          if (frame.ParseAlternatives.Length != alternatives0.Count)
          {
            frame.ParseAlternatives = alternatives0.ToArray();
            MarkChildren(frame);
            if (alternatives0.Count == 0)
            {
              if (frame.Id == 321)
              {
              }
              frame.Best = false;
            }
          }
        }

        if ((frame is RecoveryStackFrame.ExtensiblePostfix || frame is RecoveryStackFrame.ExtensiblePrefix) && frame.ParseAlternatives.Length > 1)
        {
          var parseAlternatives = FilterMaxStop(frame);

          if (frame.ParseAlternatives.Length != parseAlternatives.Count)
          {
            frame.ParseAlternatives = parseAlternatives.ToArray();
            MarkChildren(frame);
          }
        }
      }

      UpdateParseAlternativesTopToDown(allFrames);
    }

    public static void RemoveOthrHeads(List<RecoveryStackFrame> allFrames, RecoveryStackFrame livingHead)
    {
      livingHead.IsMarked = true;

      PropageteMarkeds(allFrames);
    }

    public static void RemoveOthrHeads(List<RecoveryStackFrame> allFrames, List<RecoveryStackFrame> livingHeads)
    {
      foreach (var livingHead in livingHeads)
        livingHead.IsMarked = true;

      PropageteMarkeds(allFrames);
    }

    private static void PropageteMarkeds(List<RecoveryStackFrame> allFrames)
    {
      foreach (var frame in allFrames)
      {
        if (!frame.IsMarked)
          continue;

        if (frame.Parents.Count == 0)
          continue;

        foreach (var parent in frame.Parents)
          if (parent.Best && !parent.IsMarked)
            parent.IsMarked = true;
      }

      // update Best by Marked
      foreach (var frame in allFrames)
      {
        if (!frame.IsMarked && frame.Id == 321)
        {
        }

        frame.Best = frame.IsMarked;
        frame.IsMarked = false;
      }
    }

    public static void UpdateParseAlternativesDownToTop(List<RecoveryStackFrame> allFrames)
    {
      if (allFrames.Count == 0)
        return;
      
      int index = allFrames.Count - 1;
      var frame = allFrames[index];

      frame.IsMarked = true;

      for (; index >= 0; index--)
      {
        frame = allFrames[index];

        if (!frame.Best)
          continue;

        if (frame.IsMarked)
        {
          frame.IsMarked = false;
          var alternatives0 = FilterParseAlternativesWichEndsEqualsParentsStarts(frame);

          if (frame.ParseAlternatives.Length != alternatives0.Count)
          {
            frame.ParseAlternatives = alternatives0.ToArray();
            MarkChildren(frame);
            if (alternatives0.Count == 0)
            {
              if (frame.Id == 321)
              {
              }
              frame.Best = false;
            }
          }
        }
      }
    }

    public static void CheckGraph(List<RecoveryStackFrame> allFrames, List<RecoveryStackFrame> bestFrames = null)
    {
      var setBest = new HashSet<RecoveryStackFrame>();

      if (bestFrames != null)
      {
        setBest.UnionWith(bestFrames);

        foreach (var frame in bestFrames)
        {
          if (!frame.IsTop)
            Debug.Assert(false);
        }
      }

      var setAll = new HashSet<RecoveryStackFrame>();

      foreach (var frame in allFrames)
        if (frame.Best)
          if (!setAll.Add(frame))
            Debug.Assert(false);


      foreach (var frame in allFrames)
      {
        if (!frame.Best)
          continue;

        var hasNoChildren = true;

        foreach (var child in frame.Children)
        {
          if (!child.Best)
            continue;

          hasNoChildren = false;

          if (!setAll.Contains(child))
            Debug.Assert(false);

          if (!child.Parents.Contains(frame))
            Debug.Assert(false);
        }

        if (hasNoChildren && bestFrames != null)
          if (!setBest.Contains(frame))
            Debug.Assert(false);
      }
    }

    public static void UpdateParseAlternativesTopToDown(List<RecoveryStackFrame> allFrames)
    {
      if (allFrames.Count == 0)
        return;
      
      var starts       = new HashSet<int>();
      var alternatives = new List<ParseAlternative>();

      // удаляем ParseAlternatives-ы с которых не может быть начат парсинг фрейма head.
      foreach (var frame in allFrames)
      {
        if (!frame.Best)
          continue;

        var children = frame.Children;

        if (children.Count == 0)
          continue;

        starts.Clear();

        if (frame.Id == 308)
        {
        }

        // собираем допустимые стартовые позиции для текущего фрейма
        foreach (var child in children)
        {
          if (!child.Best)
            continue;

          foreach (var a in child.ParseAlternatives)
            starts.Add(a.Stop);
        }

        if (starts.Count == 0) // это верхний фрейм
          continue;

        // удаляем ParseAlternatives-ы не начинающиеся с starts.
        alternatives.Clear();

        foreach (var a in frame.ParseAlternatives)
        {
          if (starts.Contains(a.Start))
            alternatives.Add(a);
        }

        if (alternatives.Count != frame.ParseAlternatives.Length)
          frame.ParseAlternatives = alternatives.ToArray();
      }
    }

    public static void MarkChildren(RecoveryStackFrame frame)
    {
      foreach (var child in frame.Children)
        if (frame.Best)
          child.IsMarked = true;
    }

    private static List<ParseAlternative> FilterMaxStop(RecoveryStackFrame frame)
    {
      return FilterMax(frame.ParseAlternatives, a => a.Stop);
    }

    public static List<ParseAlternativeNode> UpdateReverseDepthAndCollectAllNodes(this SCG.ICollection<ParseAlternativeNode> heads)
    {
      var allNodes = new List<ParseAlternativeNode>();

      foreach (var node in heads)
        node.ClearAndCollectNodes(allNodes);
      foreach (var node in heads)
        node.UpdateNodeReverseDepth();

      allNodes.Sort((l, r) => -l.Depth.CompareTo(r.Depth));

      return allNodes;
    }

    public static List<ParseAlternativeNode> UpdateDepthAndCollectAllNodes(this List<ParseAlternativeNode> heads)
    {
      var allNodes = new List<ParseAlternativeNode>();

      foreach (var node in heads)
        node.ClearAndCollectNodes(allNodes);
      foreach (var node in heads)
        node.Depth = 0;
      foreach (var node in heads)
        node.UpdateDepth();

      allNodes.Sort((l, r) => l.Depth.CompareTo(r.Depth));

      return allNodes;
    }

    public static void UpdateDepth(this ParseAlternativeNode node)
    {
      foreach (var parent in node.Parents)
        if (parent.Depth <= node.Depth + 1)
        {
          parent.Depth = node.Depth + 1;
          UpdateDepth(parent);
        }
    }

    public static List<RecoveryStackFrame> UpdateDepthAndCollectAllFrames(this SCG.ICollection<RecoveryStackFrame> heads)
    {
      var allRecoveryStackFrames = new List<RecoveryStackFrame>();

      foreach (var stack in heads)
        stack.ClearAndCollectFrames(allRecoveryStackFrames);
      foreach (var stack in heads)
        stack.Depth = 0;
      foreach (var stack in heads)
        stack.UpdateFrameDepth();

      allRecoveryStackFrames.SortByDepth();

      return allRecoveryStackFrames;
    }

    public static List<RecoveryStackFrame> PrepareRecoveryStacks(this SCG.ICollection<RecoveryStackFrame> heads)
    {
      var allRecoveryStackFrames = heads.UpdateDepthAndCollectAllFrames();

      foreach (var frame in allRecoveryStackFrames)
      {
        frame.Best = false;
        frame.Children.Clear();
      }

      foreach (var frame in allRecoveryStackFrames)
        foreach (var parent in frame.Parents)
        {
          if (parent.Children.Contains(frame))
            Debug.Assert(false);
          else
            parent.Children.Add(frame);
        }

      return allRecoveryStackFrames;
    }

    private static void SortByDepth(this List<RecoveryStackFrame> allRecoveryStackFrames)
    {
      allRecoveryStackFrames.Sort((l, r) => l.Depth.CompareTo(r.Depth));
    }

    private static void ClearAndCollectFrames(this RecoveryStackFrame frame, List<RecoveryStackFrame> allRecoveryStackFrames)
    {
      if (frame.Depth != -1)
      {
        allRecoveryStackFrames.Add(frame);
        frame.Depth = -1;
        foreach (var parent in frame.Parents)
          ClearAndCollectFrames(parent, allRecoveryStackFrames);
      }
    }

    private static void UpdateFrameDepth(this RecoveryStackFrame frame)
    {
      foreach (var parent in frame.Parents)
        if (parent.Depth <= frame.Depth + 1)
        {
          parent.Depth = frame.Depth + 1;
          if (parent.Depth > 200)
            Debug.Assert(true);
          UpdateFrameDepth(parent);
        }
    }

    private static void ClearAndCollectNodes(this ParseAlternativeNode node, List<ParseAlternativeNode> allNodes)
    {
      if (node.Depth != -1)
      {
        allNodes.Add(node);
        node.Depth = -1;
        foreach (var parent in node.Parents)
          ClearAndCollectNodes(parent, allNodes);
        //if (node.MissedSeparator != null)
        //  ClearAndCollectNodes(node.MissedSeparator, allNodes);
      }
    }

    private static void UpdateNodeReverseDepth(this ParseAlternativeNode node)
    {
      if (!node.HasParents)
        node.Depth = 0;
      else
      {
        foreach (var parent in node.Parents)
          if (parent.Depth == -1)
            UpdateNodeReverseDepth(parent);
        node.Depth = node.Parents.Max(x => x.Depth) + 1;
      }
    }

    public static List<RecoveryStackFrame> FilterBetterEmptyIfAllEmpty(this List<RecoveryStackFrame> frames)
    {
      if (frames.Count <= 1)
        return frames;

      if (frames.All(f => f.ParseAlternatives.Length == 0 || f.ParseAlternatives.Max(a => a.ParentsEat) == 0))
      {
        // Если список содержит только элементы разбирающие пустую строку и при этом имеется элементы с нулевой глубиной, то предпочитаем их.
        var res2 = frames.FilterMin(c => c.Depth).ToList();
        //if (res2.Count != result.Count)
        //  Debug.Assert(true);
        return res2;
      }

      return frames;
    }

    public static IEnumerable<T> FilterIfExists<T>(this List<T> res2, Func<T, bool> predicate)
    {
      return res2.Any(predicate) ? res2.Where(predicate) : res2;
    }

    public static bool HasParsedStaets(this RecoveryStackFrame frame, List<ParsedStateInfo> parsedStates)
    {
// ReSharper disable once LoopCanBeConvertedToQuery
      foreach (var parsedState in parsedStates)
      {
        if (!frame.IsVoidState(parsedState.State) && parsedState.Size > 0)
          return true;
      }
      return false;
    }

    public static int ParsedSpacesLen(this RecoveryStackFrame frame, List<ParsedStateInfo> parsedStates)
    {
      var sum = 0;
// ReSharper disable once LoopCanBeConvertedToQuery
      foreach (var parsedState in parsedStates)
        sum += !frame.IsVoidState(parsedState.State) ? 0 : parsedState.Size;
      return sum;
    }

    public static bool NonVoidParsed(this RecoveryStackFrame frame, int curTextPos, int pos, List<ParsedStateInfo> parsedStates, ParseResult parseResult)
    {
      var lastPos = Math.Max(pos, parseResult.MaxFailPos);
      return lastPos > curTextPos && lastPos - curTextPos > ParsedSpacesLen(frame, parsedStates)
             || parsedStates.Count > 0 && frame.HasParsedStaets(parsedStates);
    }

    public static List<ParseAlternative> FilterParseAlternativesWichEndsEqualsParentsStarts(RecoveryStackFrame frame)
    {
      List<ParseAlternative> res0;
      if (frame.Parents.Count == 0)
        res0 = frame.ParseAlternatives.ToList();
      else
      {
        var parentStarts = new HashSet<int>();
        foreach (var parent in frame.Parents)
          if (parent.Best)
            foreach (var alternative in parent.ParseAlternatives)
              parentStarts.Add(alternative.Start);
        res0 = frame.ParseAlternatives.Where(alternative => parentStarts.Contains(alternative.Stop)).ToList();
      }
      return res0;
    }

    public static List<RecoveryStackFrame> FilterNotEmpyPrefixChildren(RecoveryStackFrame frame, List<RecoveryStackFrame> children)
    {
      if (frame is RecoveryStackFrame.ExtensiblePrefix && children.Count > 1)
      {
        if (children.Any(c => c.ParseAlternatives.Any(a => a.State < 0)) && children.Any(c => c.ParseAlternatives.Any(a => a.State >= 0)))
          return children.Where(c => c.ParseAlternatives.Any(a => a.State >= 0)).ToList();
      }

      return children;
    }

    public static bool EndWith(RecoveryStackFrame child, int end)
    {
      foreach (var p in child.ParseAlternatives)
        if (p.Stop == end)
          return true;

      return false;
    }

    public static List<ParseAlternative> FilterMinState(List<ParseAlternative> alternatives)
    {
      if (alternatives.Count <= 1)
        return alternatives.ToList();

      var result = alternatives.FilterMin(f => f.State < 0 ? Int32.MaxValue : f.State);

      if (result.Count != alternatives.Count)
        Debug.Assert(true);

      return result;
    }

    public static List<ParseAlternative> FilterMaxEndOrFail(List<ParseAlternative> alternatives)
    {
      if (alternatives.Count <= 1)
        return alternatives.ToList();

      var maxEnd  = alternatives.Max(a => a.End);
      var maxFail = alternatives.Max(a => a.Fail);
      if (maxEnd >= 0 && maxEnd < maxFail)
        Debug.Assert(false);

      if (alternatives.Any(a => a.End >= 0))
        return alternatives.FilterMax(f => f.End);

      return alternatives.FilterMax(f => f.Fail);
    }

    public static List<RecoveryStackFrame> FilterEmptyChildren(List<RecoveryStackFrame> children5, int skipCount)
    {
      return SubstractSet(children5, children5.Where(f => 
        f.StartPos == f.TextPos
        && f.ParseAlternatives.All(a => f.TextPos + skipCount == a.Start && a.ParentsEat == 0 && a.State < 0 && f.FailState2 == 0)).ToList());
    }

    public static void FilterFailSateEqualsStateIfExists(List<RecoveryStackFrame> bestFrames)
    {
      if (bestFrames.Any(f => f.ParseAlternatives.Any(a => f.FailState == a.State)))
        for (int index = bestFrames.Count - 1; index >= 0; index--)
        {
          var f = bestFrames[index];
          if (!f.ParseAlternatives.Any(a => f.FailState == a.State))
            bestFrames.RemoveAt(index);
        }
    }

    public static List<RecoveryStackFrame> SelectMinFailSateIfTextPosEquals(List<RecoveryStackFrame> children4)
    {
      return children4.GroupBy(f =>  new IntRuleCallKey(f.TextPos, f.RuleKey)).SelectMany(fs => fs.ToList().FilterMin(f => f.FailState)).ToList();
    }

    public static List<RecoveryStackFrame> FilterNonFailedFrames(List<RecoveryStackFrame> children3)
    {
      return children3.FilterIfExists(f => f.ParseAlternatives.Any(a => a.End >= 0)).ToList();
    }

    public static List<RecoveryStackFrame> FilterEmptyChildrenWhenFailSateCanParseEmptySting(RecoveryStackFrame frame, List<RecoveryStackFrame> frames, int skipCount)
    {
      if (frame.IsSateCanParseEmptyString(frame.FailState))
      {
        var result = frames.Where(f => f.ParseAlternatives.Any(a => a.ParentsEat != 0 || frame.TextPos + skipCount < a.Start)).ToList();
        return result;
      }

      return frames;
    }

    public static List<RecoveryStackFrame> OnlyBastFrames(RecoveryStackFrame frame)
    {
      return frame.Children.Where(f => f.Best).ToList();
    }

    public static List<RecoveryStackFrame> FilterTopFramesWhichRecoveredOnFailStateIfExists(List<RecoveryStackFrame> bestFrames)
    {
      if (bestFrames.Any(f => f.ParseAlternatives.Any(a => a.State == f.FailState)))
        return bestFrames.Where(f => f.ParseAlternatives.Any(a => a.State == f.FailState)).ToList();

      return bestFrames;
    }

    public static List<RecoveryStackFrame> RemoveSpeculativeFrames(List<RecoveryStackFrame> frames)
    {
      if (frames.Count <= 1)
        return frames;

      var frames2 = frames.FilterMax(f => f.ParseAlternatives[0].ParentsEat).ToList();
      var frames3 = frames2.FilterMin(f => f.FailState);
      return frames3.ToList();
    }

    public static bool HasTopFramesWhichRecoveredOnFailState(RecoveryStackFrame frame)
    {
      var failState = frame.FailState;
      foreach (var a in frame.ParseAlternatives)
        if (a.State == failState)
          return true;
      return false;
    }

    public static List<RecoveryStackFrame> SubstractSet(List<RecoveryStackFrame> set1, SCG.ICollection<RecoveryStackFrame> set2)
    {
      return set1.Where(c => !set2.Contains(c)).ToList();
    }

    public static void ResetChildrenBestProperty(List<RecoveryStackFrame> poorerChildren)
    {
      foreach (var child in poorerChildren)
        if (child.Best)
        {
          if (child.Id == 321)
          {
          }
          child.Best = false;
          ResetChildrenBestProperty(child.Children);
        }
    }

    public static void ResetParentsBestProperty(IEnumerable<ParseAlternativeNode> parents)
    {
      foreach (var node in parents)
        if (node.Best)
        {
          node.Best = false;
          ResetParentsBestProperty(node.Parents);
        }
    }

    public static bool StartWith(RecoveryStackFrame parent, HashSet<int> ends)
    {
      return parent.ParseAlternatives.Any(a => ends.Contains(a.Start));
    }

    public static HashSet<int> Ends(RecoveryStackFrame frame)
    {
      return new HashSet<int>(frame.ParseAlternatives.Select(a => a.Stop));
    }

    public static bool StartWith(RecoveryStackFrame parent, ParseAlternative a)
    {
      return parent.ParseAlternatives.Any(p => p.Start == a.End);
    }

    public static HashSet<int> Stops(this RecoveryStackFrame frame)
    {
      var stops = new HashSet<int>();
      foreach (var a in frame.ParseAlternatives)
        stops.Add(a.Stop);

      return stops;
    }

    public static bool IsExistsNotFailedAlternatives(RecoveryStackFrame frame)
    {
      return frame.Children.Any(f => f.ParseAlternatives.Any(a => a.End >= 0));
    }

    public static List<ParseAlternative> FilterNotFailedParseAlternatives(List<ParseAlternative> alternatives0)
    {
      return alternatives0.Where(a => a.End >= 0).ToList();
    }
  }

  public static class ParseAlternativesVisializer
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

    static readonly XAttribute _garbageClass      = new XAttribute("class", "garbage");
    static readonly XAttribute _topClass          = new XAttribute("class", "parsed");
    static readonly XAttribute _prefixClass       = new XAttribute("class", "prefix");
    static readonly XAttribute _postfixClass      = new XAttribute("class", "postfix");
    static readonly XAttribute _skipedStateClass  = new XAttribute("class", "skipedState");
    static readonly XAttribute _default           = new XAttribute("class", "default");

    static readonly XElement  _start              = new XElement("span", _default, "▸");
    static readonly XElement  _end                = new XElement("span", _default, "◂");

    /// <summary>
    /// Формирует HTML-файл графически описывающий варианты продолжения прасинга из графа и открывает его в бруозере исползуемом по умолчанию.
    /// </summary>
    public static void PrintParseAlternatives(List<RecoveryStackFrame> bestFrames, List<RecoveryStackFrame> allFrames, ParseResult parseResult, int skipCount, string msg = null)
    {
      RecoveryUtils.UpdateParseAlternativesTopToDown(allFrames);
      var nodes = ParseAlternativeNode.MakeGraph(bestFrames);

      PrintParseAlternatives(parseResult, nodes, skipCount, msg);
    }

    public static void PrintParseAlternatives(ParseResult parseResult, List<ParseAlternativeNode> nodes, int skipCount, string msg = null)
    {
      var results = new List<XNode> { new XText(parseResult.DebugText + "\r\n\r\n") };
      var alternativesCount = 0;
      var topNodes = nodes.Where(n => n.IsTop).ToList();

      foreach (var g in topNodes.GroupBy(n => n.Frame))
      {
        results.Add(new XText("\r\n"));
        results.Add(new XElement("span", g.Key, ":\r\n"));

        foreach (var node in g)
        {
          if (!node.Best)
            continue;

          var result = node.GetHtml(skipCount);
          results.AddRange(result);
          alternativesCount += result.Count;
        }
      }

      results.Insert(0, new XText(msg + " " + alternativesCount + " alternatives.\r\n\r\n"));

      var template = XElement.Parse(HtmlTemplate);
      var content = template.Descendants("content").First();
      Debug.Assert(content.Parent != null);
      content.Parent.ReplaceAll(results);
      var filePath = Path.ChangeExtension(Path.GetTempFileName(), ".html");
      template.Save(filePath);
      Process.Start(filePath);
    }

    public static List<XElement> GetHtml(this ParseAlternativeNode node, int skipCount)
    {
      var results = new List<XElement>();
      var paths = node.GetFlatParseAlternatives();

      if (paths.Count == 2)
      {
        var x = paths[0];
        var y = paths[1];
        for (; !x.IsEmpty && !y.IsEmpty; x = x.Tail, y = y.Tail)
        {
          if (x.Head != y.Head)
          {
          }
        }
      }

      if (node.Frame.Id == 83)
      {
      }

      foreach (var path in paths)
        results.Add(MakeHtml(path, skipCount));

      return results;
    }

    private static XElement MakeHtml(ParseAlternativeNodes nodes, int skipCount)
    {
      XElement content = null;
      XElement missedSeparator = null;
      var skippedTokenCount = 0;
      var id = nodes.IsEmpty ? "???" : nodes.Head.Id.ToString(CultureInfo.InvariantCulture);
      var minSkip = nodes.IsEmpty ? 0 : nodes.Head.MinSkipedMandatoryTokenCount;
      

      while (true)
      {
        if (nodes.IsEmpty)
          return new XElement("span", id + " " + skippedTokenCount + " (" + minSkip + ") " + " skipped ", content);

        var node = nodes.Head;
        var a = node.ParseAlternative;
        var frame = node.Frame;
        var parsingFailAtState = frame.FailState2;
        var recursionState = frame.FailState;
        var isTop = node.IsTop;
        var text = frame.ParseResult.Text;

        skippedTokenCount += node.SkipedMandatoryTokenCount;
        if (a.Skip > 0)
          skippedTokenCount++;

        var parsedClass = isTop ? _topClass : _postfixClass;

        var title = MakeTitle(node);

        var prefixText = text.Substring(frame.StartPos, frame.TextPos - frame.StartPos);
        var prefix = String.IsNullOrEmpty(prefixText) ? null : new XElement("span", _prefixClass, prefixText);

        var postfixText = text.Substring(a.Start, a.Stop - a.Start);
        var postfix = String.IsNullOrEmpty(postfixText) ? null : new XElement("span", isTop ? _topClass : _postfixClass, postfixText);

        XElement skippedPrefix = null;
        XElement skippedPostfix = null;

        var endState = a.State;

        var skippedText = a.Skip + skipCount > 0 ? new XElement("span", _garbageClass, text.Substring(frame.TextPos, a.Skip + skipCount)) : null;

        if (a.Skip > 0)
        {

        }

        if (isTop)
        {
          if (recursionState != endState)
            skippedPrefix = new XElement("span", _skipedStateClass, SkipedStatesCode(frame, parsingFailAtState, endState));
        }
        else
        {
          if (parsingFailAtState < recursionState)
          {
            skippedPrefix = new XElement("span", _skipedStateClass, SkipedStatesCode(frame, parsingFailAtState, recursionState));
          }

          var startState = frame.GetNextState(recursionState);

          if (startState >= 0 && (startState < endState || endState < 0))
            skippedPostfix = new XElement("span", _skipedStateClass, SkipedStatesCode(frame, startState, endState));
        }

        var fail = a.End < 0 ? new XElement("span", _garbageClass, "<FAIL>") : null;
        var span = new XElement("span", parsedClass, title, _start, prefix, skippedText, missedSeparator, skippedPrefix, content, skippedPostfix, postfix, _end, fail);

        var missed = node.MissedSeparator;

        if (missed != null)
        {
          if (node.Id == 4200)
          {
          }
          missedSeparator = new XElement("span", _skipedStateClass, _start, MakeTitle(missed), SkipedStatesCode(frame, missed.Frame.FirstState, -1), _end);
        }
        else
          missedSeparator = null;

        if (!node.HasParents)
          span.Add("\r\n");

        nodes = nodes.Tail;
        content = span;
      }

      throw new Exception("MakeHtml failed");
    }

    private static string SkipedStatesCode(RecoveryStackFrame frame, int startState, int endState)
    {
      return String.Join(" ", frame.CodeForStates(startState, endState, true));
    }

    private static XAttribute MakeTitle(ParseAlternativeNode node)
    {
      return new XAttribute("title", node);
    }
  }

#endregion
}
