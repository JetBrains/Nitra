//#region Пролог
//#define DebugOutput
//#define DebugThreading
using Nitra.Internal.Recovery;
using Nitra.Runtime.Errors;
using Nitra.Runtime.Reflection;

using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Xml.Linq;

using NB = Nemerle.Builtins;
using SCG = System.Collections.Generic;

//using ParsedSequenceAndSubrule2 = Nemerle.Builtins.Tuple</*Inserted tokens*/int, Nitra.Internal.Recovery.ParsedSequence, Nitra.Internal.Recovery.ParsedSubrule>;

#if NITRA_RUNTIME
namespace Nitra.Strategies
#else
// ReSharper disable once CheckNamespace

namespace Nitra.DebugStrategies
#endif
{
  using SubrulesTokenChanges = Dictionary<ParsedSubrule, TokenChanges>;
  using ParsedSequenceAndSubrules = Nemerle.Core.list<SubruleTokenChanges>;
  using FlattenSequences = List<Nemerle.Core.list<SubruleTokenChanges>>;
  using ParsedList = Nemerle.Core.list<ParsedSequenceAndSubrule>;
  using Nitra.Runtime;

  //#endregion

  public static class RecoveryDebug
  {
    public static string CurrentTestName;
  }

  public class Recovery
  {
    public const int NumberOfTokensForSpeculativeDeleting = 4;
    public const int Fail = int.MaxValue;
    private readonly Dictionary<ParsedSequenceAndSubrule, bool> _deletedToken = new Dictionary<ParsedSequenceAndSubrule, bool>();
    private ParseResult _parseResult;
    private RecoveryParser _recoveryParser;
#if DebugThreading
    private static int _counter = 0;
    private int _id;
    public int ThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId;

    public Recovery()
    {
      _id = System.Threading.Interlocked.Increment(ref _counter);
      Debug.WriteLine("Recovery() " + _id + " ThreadId=" + System.Threading.Thread.CurrentThread.ManagedThreadId);
    }
#endif

    public virtual int Strategy(ParseResult parseResult)
    {
#if DebugThreading
      if (ThreadId != System.Threading.Thread.CurrentThread.ManagedThreadId)
        Debug.Assert(false);
      Debug.WriteLine(">>>> Strategy " + _id + " ThreadId=" + System.Threading.Thread.CurrentThread.ManagedThreadId);
#endif
      //Debug.Assert(parseResult.RecoveryStacks.Count > 0);

      _parseResult = parseResult;

#if DebugOutput
      Debug.IndentSize = 1;
      var timer = Stopwatch.StartNew();
      Debug.WriteLine(RecoveryDebug.CurrentTestName + " -----------------------------------------------------------");
#endif

      _deletedToken.Clear();
      var textLen = parseResult.Text.Length;
      var rp = new RecoveryParser(parseResult);
      _recoveryParser = rp;
      rp.StartParse(parseResult.RuleParser);//, parseResult.MaxFailPos);
      var startSeq = rp.Sequences.First().Value;

      UpdateEarleyParseTime();
#if DebugOutput
      timer.Stop();
      Debug.WriteLine("Earley parse took: " + timer.Elapsed);
      timer.Restart();
#endif

      RecoverAllWays(rp);

      //AstPatcher.FindBestPath(startSeq, rp, _deletedToken);
      UpdateRecoverAllWaysTime();
#if DebugOutput
      timer.Stop();
      Debug.WriteLine("RecoverAllWays took: " + timer.Elapsed);
      timer.Restart();
#endif

      if (parseResult.TerminateParsing)
        throw new OperationCanceledException();

      var memiozation = new Dictionary<ParsedSequenceKey, SequenceTokenChanges>();
      FindBestPath(startSeq, textLen, memiozation);

      UpdateFindBestPathTime();
#if DebugOutput
      timer.Stop();
      Debug.WriteLine("FindBestPath took: " + timer.Elapsed);
      timer.Restart();
#endif

      if (parseResult.TerminateParsing)
        throw new OperationCanceledException();

      var results = FlattenSequence(new FlattenSequences() { Nemerle.Collections.NList.ToList(new SubruleTokenChanges[0]) },
        parseResult, startSeq, textLen, memiozation[new ParsedSequenceKey(startSeq, textLen)].TotalTokenChanges, memiozation);

      //ParsePathsVisializer.PrintPaths(parseResult, _deletedToken, results);

      if (parseResult.TerminateParsing)
        throw new OperationCanceledException();

      UpdateFlattenSequenceTime();
#if DebugOutput
      timer.Stop();
      Debug.WriteLine("FlattenSequence took: " + timer.Elapsed);
#endif

      CollectError(rp, results);
#if DebugThreading
      Debug.WriteLine("<<<< Strategy " + _id + " ThreadId=" + System.Threading.Thread.CurrentThread.ManagedThreadId);
#endif

      if (parseResult.TerminateParsing)
        throw new OperationCanceledException();

      AstPatcher.Patch(startSeq, rp, memiozation);

      _parseResult = null;

      return parseResult.Text.Length;
    }

    private void CollectError(RecoveryParser rp, FlattenSequences results)
    {
      //var text = rp.ParseResult.Text;
      var expected = new Dictionary<NSpan, HashSet<ParsedSequenceAndSubrule>>();
      var failSeq = default(ParsedSequence);
      var failSubrule = default(ParsedSubrule);
      var skipRecovery = false;

      foreach (var result in results)
      {
        var reverse = result.ToArray().Reverse();
        foreach (var x in reverse)
        {
          var ins = x.TokenChanges;
          var seq = x.Seq;
          var subrule = x.Subrule;

          if (skipRecovery)
          {
            if (!ins.HasChanges && seq.ParsingSequence.RuleName != "s")
            {
              skipRecovery = false;
              //Debug.WriteLine(x);
              HashSet<ParsedSequenceAndSubrule> parsedNodes;
              var span = new NSpan(failSubrule.Begin, subrule.Begin);
              if (!expected.TryGetValue(span, out parsedNodes))
              {
                parsedNodes = new HashSet<ParsedSequenceAndSubrule>();
                expected[span] = parsedNodes;
              }

              if (failSubrule.IsEmpty)
                parsedNodes.Add(new ParsedSequenceAndSubrule(failSeq, failSubrule));
              else
                parsedNodes.Add(new ParsedSequenceAndSubrule(seq, subrule));
            }
          }
          else
          {
            if (ins.HasChanges)
            {
              failSeq = seq;
              failSubrule = subrule;
              skipRecovery = true;
            }
          }
        }
      }

      var parseResult = rp.ParseResult;
      foreach (var e in expected)
        parseResult.ReportError(new ExpectedSubrulesError(new Location(parseResult.OriginalSource, e.Key.StartPos, e.Key.EndPos), e.Value));
    }

    private FlattenSequences FlattenSubrule(FlattenSequences prevs, ParseResult parseResult, ParsedSequence seq, SubrulesTokenChanges parses, ParsedSubrule subrule, TokenChanges tokenChanges, Dictionary<ParsedSequenceKey, SequenceTokenChanges> memiozation)
    {
    Begin:

      //var txt = parseResult.Text.Substring(subrule.Begin, subrule.End - subrule.Begin);
      //var stateIndex = subrule.State;
      //var state = stateIndex < 0 ? null : seq.ParsingSequence.States[stateIndex];

      if (subrule.End == 11)
      { }

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
          var result = FlattenSequence(prevs, parseResult, subSequences, subrule.End, tokenChanges, memiozation);
          currentNodes.AddRange(result);
        }
      }

      if (currentNodes.Count == 0) // если не было сабсиквенсов, надо создать продолжения из текущего сабруля
      {
        foreach (var prev in prevs)
          currentNodes.Add(new ParsedSequenceAndSubrules.Cons(new SubruleTokenChanges(seq, subrule, tokenChanges), prev));
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

            tokenChanges = parses[nextSubrule];
            if (tokenChanges.IsFail)
              return currentNodes;
            // recursive self call...
            prevs = currentNodes;
            subrule = nextSubrule;
            goto Begin;
            return null;
          }
        default:
          {
            var resultNodes = new FlattenSequences();

            foreach (var nextSubrule in nextSubrules)
            {
              var newSubruleInsertedTokens = parses[nextSubrule];
              if (newSubruleInsertedTokens.IsFail)
                continue;

              var result = FlattenSubrule(currentNodes, parseResult, seq, parses, nextSubrule, newSubruleInsertedTokens, memiozation);
              resultNodes.AddRange(result);
            }

            return resultNodes;
          }
      }
    }

    private FlattenSequences FlattenSequence(
      FlattenSequences prevs,
      ParseResult parseResult,
      ParsedSequence seq,
      int end,
      TokenChanges sequenceInsertedTokens,
      Dictionary<ParsedSequenceKey, SequenceTokenChanges> memiozation)
    {
      //var seqTxt = parseResult.Text.Substring(seq.StartPos, end - seq.StartPos);

      if (seq.StartPos == 8 && end == 15)
        Debug.Assert(true);

      SequenceTokenChanges first;
      var key = new ParsedSequenceKey(seq, end);
      if (!memiozation.TryGetValue(key, out first))
        Debug.Assert(false);

      var parses = first.SubrulesTokenChanges;

      if (first.TotalTokenChanges.IsFail)
        return new FlattenSequences();

      if (sequenceInsertedTokens != first.TotalTokenChanges)
      {
        //Debug.Assert(false);
        return new FlattenSequences();
      }

      var firstSubrules = seq.GetFirstSubrules(parses.Keys).ToArray();

      var total = new FlattenSequences();

      foreach (var firstSubrule in firstSubrules)
      {
        //var txt = parseResult.Text.Substring(firstSubrule.Begin, firstSubrule.End - firstSubrule.Begin);
        //var stateIndex = firstSubrule.State;
        //var state = stateIndex < 0 ? null : seq.ParsingSequence.States[stateIndex];

        var insertedTokens = parses[firstSubrule];
        if (insertedTokens.IsFail)
          continue;

        var result = FlattenSubrule(prevs, parseResult, seq, parses, firstSubrule, insertedTokens, memiozation);
        total.AddRange(result);
      }

      return total;
    }

    private TokenChanges FindBestPath(ParsedSequence seq, int end, Dictionary<ParsedSequenceKey, SequenceTokenChanges> memiozation)
    {
      if (_parseResult.TerminateParsing)
        throw new OperationCanceledException();

      SequenceTokenChanges result;

      var key = new ParsedSequenceKey(seq, end);

      if (memiozation.TryGetValue(key, out result))
        return result.TotalTokenChanges;

      if (seq.StartPos == end)
      {
        var tokenChanges = new TokenChanges(seq.ParsingSequence.MandatoryTokenCount, 0);
        memiozation.Add(key, new SequenceTokenChanges(new SubrulesTokenChanges(), tokenChanges));
        return tokenChanges;
      }

      var results = new SubrulesTokenChanges();
      var validSubrules = seq.GetValidSubrules(end).ToList();
      if (validSubrules.Count == 0)
      {
        var tokenChanges = new TokenChanges();
        memiozation.Add(key, new SequenceTokenChanges(results, tokenChanges));
        return tokenChanges;
      }
      memiozation.Add(key, new SequenceTokenChanges(results, TokenChanges.Fail));

      foreach (var subrule in validSubrules)
      {
        TokenChanges localMin = TokenChanges.Fail;
        if (_deletedToken.ContainsKey(new ParsedSequenceAndSubrule(seq, subrule)))
          localMin = new TokenChanges(0, 1);
        else
          localMin = LocalMinForSubSequence(seq, memiozation, subrule, localMin);

        results[subrule] = localMin;
      }

      TokenChanges comulativeMin;
      if (results.Count == 0)
      { }
      var bestResults = RemoveWorstPaths(seq, end, results, out comulativeMin);
      var result2 = new SequenceTokenChanges(bestResults, comulativeMin);
      memiozation[key] = result2;

      return result2.TotalTokenChanges;
    }

    private TokenChanges LocalMinForSubSequence(ParsedSequence seq, Dictionary<ParsedSequenceKey, SequenceTokenChanges> memiozation, ParsedSubrule subrule, TokenChanges localMin)
    {
      var subSeqs = seq.GetSequencesForSubrule(subrule).ToArray();
      var hasSequence = false;

      foreach (var subSeq in subSeqs)
      {
        hasSequence = true;
        var localRes = FindBestPath(subSeq, subrule.End, memiozation);

        localMin = TokenChanges.Min(localMin, localRes);
      }

      if (!hasSequence)
      {
        if (subrule.IsEmpty)
          localMin = new TokenChanges(seq.SubruleMandatoryTokenCount(subrule), 0);
        else
          localMin = new TokenChanges();
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

    private static SubrulesTokenChanges RemoveWorstPaths(ParsedSequence seq, int end, SubrulesTokenChanges parses, out TokenChanges comulativeMin)
    {
      var comulativeCost = new SubrulesTokenChanges();
      bool updated = true;
      while (updated)
      {
        updated = false;
        foreach (var parse in parses)
        {
          var subrule = parse.Key;
          TokenChanges oldCount;
          if (!comulativeCost.TryGetValue(subrule, out oldCount))
            updated = true;
          TokenChanges min;
          if (seq.StartPos == subrule.Begin && seq.ParsingSequence.StartStates.Contains(subrule.State))
            min = new TokenChanges();
          else
          {
            min = TokenChanges.Fail;
            TokenChanges prevCount;
            foreach (var prevSubrule in seq.GetPrevSubrules(subrule, parses.Keys))
              if (comulativeCost.TryGetValue(prevSubrule, out prevCount))
                min = TokenChanges.Min(min, prevCount);
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
      var good = new SubrulesTokenChanges();
      while (toProcess.Count > 0)
      {
        var subrule = toProcess.Dequeue();
        if (good.ContainsKey(subrule))
          continue;
        good.Add(subrule, parses[subrule]);
        var prev = seq.GetPrevSubrules(subrule, parses.Keys).ToList();
        if (prev.Count > 0)
        {
          TokenChanges min;
          if (seq.StartPos == subrule.Begin && seq.ParsingSequence.StartStates.Contains(subrule.State))
            min = new TokenChanges();
          else
            min = prev.Min(s => comulativeCost[s]);
          foreach (var prevSubrule in prev)
            if (comulativeCost[prevSubrule] == min)
              toProcess.Enqueue(prevSubrule);
        }
      }
      return good;
    }

    private static TokenChanges AddOrFail(TokenChanges source, TokenChanges addition)
    {
      return source.IsFail || addition.IsFail 
        ? TokenChanges.Fail
        : new TokenChanges(source.Inserted + addition.Inserted, source.Deleted + addition.Deleted);
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
                if (simple == null || simple.RuleParser.Descriptor.Name != "S" && simple.RuleParser.Descriptor.Name != "s")
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
                  if (simple == null || simple.RuleParser.Descriptor.Name != "S" && simple.RuleParser.Descriptor.Name != "s")
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

        _deletedToken[new ParsedSequenceAndSubrule(sequence, new ParsedSubrule(maxPos, i, s_loopState))] = true;
        rp.SubruleParsed(maxPos, i, new ParseRecord(sequence, 0, maxPos));
        return true;
      }

      return false;
    }

    private List<TokenParserApplication> GetCurrentTokens(RecoveryParser rp, int pos)
    {
      var parseResult = rp.ParseResult;
      var grammar = parseResult.RuleParser.Grammar;
      var res = grammar.ParseNonVoidTokens(pos, parseResult);
      return res;
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
      _deletedToken[new ParsedSequenceAndSubrule(sequence, new ParsedSubrule(pos, nextPos, s_loopState))] = false;
      rp.SubruleParsed(pos, nextPos, new ParseRecord(sequence, 0, pos));

      var parseResult = rp.ParseResult;
      var grammar = parseResult.RuleParser.Grammar;
      var res2 = grammar.ParseAllVoidGrammarTokens(nextPos, parseResult);
      RemoveEmpty(res2, nextPos);

      if (res2.Count == 0)
        DeleteTokens(rp, nextPos, sequence, tokensToDelete - 1);
      foreach (var nextPos2 in res2)
      {
        //_deletedToken[new ParsedSequenceAndSubrule(sequence, new ParsedSubrule(pos, nextPos2, s_loopState))] = false;
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

        var tmpDeleted = FindMaxFailPos(rp);
        if (rp.MaxPos != rp.ParseResult.Text.Length)
          UpdateParseErrorCount();

        if (!CheckUnclosedToken(rp))
          deleted.AddRange(tmpDeleted);
        else
        {
        }

        maxPos = rp.MaxPos;
        failPositions.Add(maxPos);
        var records = new SCG.Queue<ParseRecord>(rp.Records[maxPos]);

        var tokens = GetCurrentTokens(rp, rp.MaxPos);

        if (tokens.Count > 0)
        {
          var visited = new Dictionary<ParsingCallerInfo, ParseRecord?>();
          var roots = new Dictionary<ParsingCallerInfo, bool>();
          foreach (var record in records)
            if (!record.IsComplete)
            {
              if (!(record.Sequence.IsToken && record.ParsingState.IsStart))
                AddRoot(roots, record.Sequence, record.State, maxPos);
              else
              { }
            }

            foreach (var token in tokens)
            {
              var yyy = rp.ParseResult.Text.Substring(token.Start, token.Length);
              foreach (var callerInfo in token.Token.Callers)
                FindAllCallers(visited, roots, callerInfo, maxPos);
            }

            rp.Parse();
        }
        else if (rp.MaxPos == rp.ParseResult.Text.Length)
        {
          SkipAllStates(rp, maxPos, records);
        }
      }
      while (rp.MaxPos > maxPos);

      foreach (var del in deleted)
        DeleteTokens(rp, del.Item1, del.Item2, NumberOfTokensForSpeculativeDeleting);
      rp.Parse();
    }

    private void SkipAllStates(RecoveryParser rp, int maxPos, Queue<ParseRecord> records)
    {
      var records2 = rp.Records[maxPos];
      do
      {
        while (records.Count > 0)
          SkipAllStates(records.Dequeue(), maxPos, records2);

        foreach (var tuple in rp.RecordsToProcess)
          records.Enqueue(tuple.Field1);

        rp.Parse();
      } while (records.Count > 0);
    }

    private void SkipAllStates(ParseRecord record, int pos, HashSet<ParseRecord> records)
    {
      foreach (var caller in record.Sequence.Callers)
        if (!records.Contains(caller))
          SkipAllStates(caller, pos, records);

      if (!record.IsComplete)
      {
        _recoveryParser.SubruleParsed(pos, pos, record);

        foreach (var nextState in record.ParsingState.Next)
        {
          if (nextState >= 0)
          {
            var next = record.Next(nextState);
            if (!records.Contains(next))
              SkipAllStates(next, pos, records);
          }
        }
      }
    }

    private void AddRoot(Dictionary<ParsingCallerInfo, bool> roots, ParsedSequence parsedSequence, int state, int textPos)
    {
      var parsingSequence = parsedSequence.ParsingSequence;

      if (roots.ContainsKey(new ParsingCallerInfo(parsedSequence.ParsingSequence, state)))
        return;

      var toProcess = new SCG.Stack<int>();
      toProcess.Push(state);
      while (toProcess.Count > 0)
      {
        var curState = toProcess.Pop();
        var key = new ParsingCallerInfo(parsedSequence.ParsingSequence, curState);
        if (!roots.ContainsKey(key))
        {
          foreach (var nextState in parsingSequence.States[curState].Next)
            if (nextState >= 0)
              toProcess.Push(nextState);
          roots.Add(key, false);
          _recoveryParser.SubruleParsed(textPos, textPos, new ParseRecord(parsedSequence, curState, textPos));
        }
      }

      foreach (var caller in parsedSequence.Callers)
        AddRoot(roots, caller.Sequence, caller.State, textPos);
    }

    //этот метод создаёт стек от рута до токена
    private ParseRecord? FindAllCallers(Dictionary<ParsingCallerInfo, ParseRecord?> visited, Dictionary<ParsingCallerInfo, bool> roots, ParsingCallerInfo callerInfo, int textPos)
    {
      ParseRecord? caller;
      if (visited.TryGetValue(callerInfo, out caller))
        return caller;

      var addParses = false;
      if (roots.ContainsKey(callerInfo) && !roots[callerInfo])
      {
        roots[callerInfo] = true;
        var seq = _recoveryParser.StartParseSequence(textPos, callerInfo.Sequence);
        caller = new ParseRecord(seq, callerInfo.State, textPos);
        addParses = true;
      }
      else
        caller = null;

      visited.Add(callerInfo, caller);

      foreach (var seqCaller in callerInfo.Sequence.Callers)
      {
        var newCaller = FindAllCallers(visited, roots, seqCaller, textPos);
        if (newCaller.HasValue)
        {
          _recoveryParser.StartParseSequence(newCaller.Value, textPos, callerInfo.Sequence);
          addParses = true;
        }
      }

      if (addParses)
      {
        var seq = _recoveryParser.StartParseSequence(textPos, callerInfo.Sequence);
        var toProcess = new SCG.Stack<int>();
        var visitedStates = new SCG.HashSet<int>();
        foreach (var prevState in callerInfo.Sequence.States[callerInfo.State].Prev)
          toProcess.Push(prevState);
        while (toProcess.Count > 0)
        {
          var state = toProcess.Pop();
          if (visitedStates.Add(state))
          {
            foreach (var prevState in callerInfo.Sequence.States[state].Prev)
              toProcess.Push(prevState);
            _recoveryParser.SubruleParsed(textPos, textPos, new ParseRecord(seq, state, textPos));
          }
        }
      }

      return caller;
    }

    private bool CheckUnclosedToken(RecoveryParser rp)
    {
      var maxPos  = rp.MaxPos;
      var grammar = rp.ParseResult.RuleParser.Grammar;
      var records = rp.Records[maxPos].ToArray();
      var result  = new SCG.Dictionary<ParseRecord, bool>();
      var unclosedTokenFound = false;

      foreach (var record in records)
      {
        if (record.IsComplete)
          continue;

        if (record.Sequence.StartPos >= maxPos)
          continue;

        if (record.Sequence.ParsingSequence.IsNullable)
          continue;

        var res = IsInsideToken(result, grammar, record);

        if (!res)
          continue;

        unclosedTokenFound = true;
        rp.SubruleParsed(maxPos, maxPos, record);
      }

      return unclosedTokenFound;
    }

    private static bool IsInsideToken(SCG.Dictionary<ParseRecord, bool> memoization, CompositeGrammar compositeGrammar, ParseRecord record)
    {
      bool res;
      if (memoization.TryGetValue(record, out res))
        return res;

      if (record.Sequence.ParsingSequence.SequenceInfo is SequenceInfo.Ast)
      {
        var parser = record.Sequence.ParsingSequence.SequenceInfo.Parser;
        res = compositeGrammar.Tokens.ContainsKey(parser) || compositeGrammar.VoidTokens.ContainsKey(parser);
        memoization[record] = res;
        if(res)
          return res;
      }

      foreach (var caller in record.Sequence.Callers)
      {
        res = IsInsideToken(memoization, compositeGrammar, caller);
        if (res)
        {
          memoization[record] = true;
          return true;
        }
      }

      memoization[record] = false;
      return false;
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
    static readonly XAttribute _skipedStateClass = new XAttribute("class", "skipedState");
    static readonly XAttribute _default = new XAttribute("class", "default");

    public static void PrintPaths(ParseResult parseResult, Dictionary<ParsedSequenceAndSubrule, bool> deletedToken, FlattenSequences paths)
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

    public static void PrintPath(List<XNode> results, string text, Dictionary<ParsedSequenceAndSubrule, bool> deletedToken, ParsedSequenceAndSubrules path)
    {
      var isPrevInsertion = false;

      foreach (var node in path.Reverse())
      {
        var tokenChanges = node.TokenChanges;
        var seq = node.Seq;
        var subrule = node.Subrule;
        
        bool isGarbage;
        if (deletedToken.TryGetValue(new ParsedSequenceAndSubrule(seq, subrule), out isGarbage))// TODO: Возможно здесь надо проверять значение tokenChanges.Deleted
        {
          isPrevInsertion = false;
          var title = new XAttribute("title", "Deleted token;  Subrule: " + subrule + ";  Sequence: " + seq + ";");
          results.Add(new XElement("span", isGarbage ? _garbageClass : _deletedClass,
            title, text.Substring(subrule.Begin, subrule.End - subrule.Begin)));
        }
        else if (tokenChanges.HasChanges)
        {
          var desc = seq.ParsingSequence.States[subrule.State].Description;
          if (!subrule.IsEmpty)
          { }
          var title = new XAttribute("title", "Inserted tokens: " + tokenChanges + ";  Subrule: " + subrule + ";  Sequence: " + seq + ";");
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
