#define DebugOutput
using N2.Internal;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;

#if N2RUNTIME
namespace N2.Strategies
#else
// ReSharper disable once CheckNamespace
namespace N2.DebugStrategies
#endif
{
  using ParserData = Tuple<int, int, List<ParsedStateInfo>>;
  using ReportData = Action<RecoveryResult, List<RecoveryResult>, List<RecoveryResult>, List<RecoveryStackFrame>>;

  public class Recovery
  {
    protected List<RecoveryResult>          _candidats    = new List<RecoveryResult>();
    protected RecoveryResult                _bestResult;
    protected List<RecoveryResult>          _bestResults  = new List<RecoveryResult>();
    protected int                           _nestedLevel  = 0;
    protected HashSet<RecoveryStackFrame>   _visitedFrame = new HashSet<RecoveryStackFrame>();

    public ReportData ReportResult;

    public Recovery(ReportData reportResult)
    {
      ReportResult = reportResult;
    }

    void Reset()
    {
      _candidats.Clear();
      _bestResult = null;
      _bestResults.Clear();
      _nestedLevel = 0;
    }

    public virtual int Strategy(Parser parser)
    {
      Reset();
      var maxFailPos = parser.MaxFailPos;
      var curTextPos = startTextPos;
      var text = parser.Text;
      Debug.Assert(parser.RecoveryStacks.Count > 0);
      var frames = parser.RecoveryStacks;
      var lastStack = frames.Last();

      while (curTextPos < text.Length && _candidats.Count == 0)// && curTextPos - curTextPos < 400
      {
        var newFrames = new List<RecoveryStackFrame>();
        foreach (var frame in frames)
          ProcessFindSpeculativeFrames(newFrames, startTextPos, parser, frame, curTextPos, text, 0);

        newFrames.AddRange(frames);

        var allFrames = Parser.PrepareRecoveryStacks(newFrames);

        foreach (var frame in newFrames)
          ProcessTopFrames(startTextPos, parser, frame, curTextPos, text, 0);

        Debug.Assert(true);

        // TODO: Фильтруем результаты

        curTextPos++;
        _visitedFrame.Clear();
      }

      if (_candidats.Count > 1)
        _candidats = FilterBest(_candidats).ToList();

      parser.MaxFailPos = maxFailPos;

      if (_bestResult == null)
        AddResult(text.Length, text.Length, text.Length, -1, lastStack, text, startTextPos);

      if (ReportResult != null)
        ReportResult(_bestResult, _bestResults, _candidats, frames);

      FixAst(parser);
      Reset();
      return -1;
    }

    private RecoveryResult[] FilterBest(List<RecoveryResult> candidats)
    {
      if (candidats.Count <= 1)
        return candidats.ToArray();

      candidats.Sort(CompareRecoveryResults);

      var last = candidats[candidats.Count - 1];

      var firstIndex = candidats.FindIndex(c => CompareRecoveryResults(last, c) == 0);
      Debug.Assert(firstIndex >= 0);
      var result = new RecoveryResult[candidats.Count - firstIndex];
      candidats.CopyTo(firstIndex, result, 0, candidats.Count - firstIndex);
      return result;
    }

    private void ProcessFindSpeculativeFrames(List<RecoveryStackFrame> newFrames, int startTextPos, Parser parser, RecoveryStackFrame frame, int curTextPos, string text, int i)
    {
      ProcessStackFrameSpeculative(newFrames, startTextPos, parser, frame, curTextPos, text, 0);
    }

    private static int CompareStack(RecoveryStackFrame frame1, RecoveryStackFrame frame2)
    {
      var child1 = frame1;
      var child2 = frame2;
      for(;;)
      {
        if (frame1 == frame2)
        {
          //Debug.Assert(child1.FailState - child2.FailState != 0);
          return child1.FailState - child2.FailState;
        }

        if (frame1.Depth == frame2.Depth)
        {
          if (frame1.Parents.Count < 1)
            return 0;
          if (frame2.Parents.Count < 1)
            return 0;

          child1 = frame1;
          child2 = frame2;
          frame1 = frame1.Parents.First();
          frame2 = frame2.Parents.First();
          continue;
        }

        if (frame1.Depth < frame2.Depth)
        {
          if (frame1.Parents.Count < 1)
            return 0;

          child1 = frame1;
          frame1 = frame1.Parents.First();
          continue;
        }

        if (frame2.Parents.Count < 1)
          return 0;

        child2 = frame2;
        frame2 = frame2.Parents.First();
      }
    }

    private void ProcessStackFrame(int startTextPos, Parser parser, RecoveryStackFrame recoveryStack, int curTextPos, string text, int subruleLevel)
    {
      ProcessTopFrames(startTextPos, parser, recoveryStack, curTextPos, text, subruleLevel);
      if (_bestResult != null)
        return;
      ProcessOtherFrames(startTextPos, parser, recoveryStack, curTextPos, text, subruleLevel);
      if (_bestResult != null)
        return;
    }

    private void ProcessStackFrameSpeculative(List<RecoveryStackFrame> newFrames, int startTextPos, Parser parser, RecoveryStackFrame recoveryStack, int curTextPos, string text, int subruleLevel)
    {
      var stackFrame = recoveryStack;

      int nextState;
      for (var state = stackFrame.FailState; state >= 0; state = nextState) //subruleLevel > 0 ? stackFrame.GetNextState(stackFrame.FailState) :
      {
        nextState = stackFrame.GetNextState(state);

        if (!stackFrame.IsTokenRule) //&& stackFrame.FailState == state
          TryParseSubrules(newFrames, startTextPos, parser, recoveryStack, curTextPos, text, subruleLevel, state);
      }
    }

    private void ProcessTopFrames(int startTextPos, Parser parser, RecoveryStackFrame frame, int curTextPos, string text, int subruleLevel)
    {
      var isPrefixParsed = frame.IsPrefixParsed;
      var isNotOptional = frame.IsLoopSeparatorStart;

      int nextState;
      for (var state = frame.FailState; state >= 0; state = nextState) //subruleLevel > 0 ? stackFrame.GetNextState(stackFrame.FailState) :
      {
        parser.MaxFailPos = startTextPos;
        nextState = frame.GetNextState(state);

        List<ParsedStateInfo> parsedStates;
        int pos = TryParse(parser, frame, curTextPos, state, out parsedStates);

        if (curTextPos > 0)
          Debug.Assert(pos != 0);

        var lastPos = Math.Max(pos, parser.MaxFailPos);

        // что-то пропарсили и это что-то не пробелы
        if (lastPos > curTextPos && lastPos - curTextPos > ParsedSpacesLen(frame, parsedStates)
          || parsedStates.Count > 0 && HasParsedStaets(frame, parsedStates))
        {
          var pos1 = pos >= 0 ? pos : curTextPos;
          var pos2 = pos >= 0 ? ContinueParse(pos1, frame, parser, true) : pos;
          AddResult(pos1, lastPos, pos2, state, frame, text, startTextPos);
          break;
        }

        var isParsed = pos > curTextPos;

        if (!isPrefixParsed && isParsed && !frame.IsVoidState(state))
          isPrefixParsed = true;

        if (nextState < 0 && !isPrefixParsed) // пытаемся восстановить пропущенный разделитель списка
        {
          var separatorFrame = frame.GetLoopBodyFrameForSeparatorState(curTextPos, parser);

          if (separatorFrame != null)
          {
            // Нас просят попробовать востановить отстуствующий разделитель цикла. Чтобы знать, нужно ли это дела, или мы
            // имеем дело с банальным концом цикла мы должны
            Debug.Assert(separatorFrame.Parents.Count == 1);

            var subRecovery = CreateSubRecovery();
            subRecovery.ProcessStackFrame(startTextPos, parser, separatorFrame, curTextPos, text, subruleLevel);
            var bestResult = subRecovery._bestResult;

            if (bestResult != null && bestResult.RecoveredCount > 0)
            {
              var endPos = Math.Max(bestResult.EndPos, curTextPos);
              var ruleEndPos = Math.Max(bestResult.RuleEndPos, curTextPos);

              AddResult(curTextPos, ruleEndPos, endPos, -1, separatorFrame, text, startTextPos, true);
              return;
            }// a b
          }
        }
      }

      var pos3 = ContinueParse(curTextPos, frame, parser, true);
      if (pos3 >= 0)
        AddResult(curTextPos, curTextPos, pos3, -1, frame, text, startTextPos);
    }

    private void ProcessOtherFrames(int startTextPos, Parser parser, RecoveryStackFrame frame, int curTextPos, string text, int subruleLevel)
    {
      var pos = ContinueParse(curTextPos, frame, parser, true);
      if (pos > curTextPos)
        AddResult(curTextPos, curTextPos, pos, -1, frame, text, startTextPos);
    }

    protected virtual Recovery CreateSubRecovery()
    {
      return new Recovery(ReportResult);
    }

    // ReSharper disable once ParameterTypeCanBeEnumerable.Local
    private static int Sum(List<ParsedStateInfo> parsedStates)
    {
      var sum = 0;
// ReSharper disable once LoopCanBeConvertedToQuery
      foreach (var parsedState in parsedStates)
        sum += parsedState.Size;
      return sum;
    }

    // ReSharper disable once ParameterTypeCanBeEnumerable.Local
    private static bool HasParsedStaets(RecoveryStackFrame frame, List<ParsedStateInfo> parsedStates)
    {
// ReSharper disable once LoopCanBeConvertedToQuery
      foreach (var parsedState in parsedStates)
      {
        if (!frame.IsVoidState(parsedState.State) && parsedState.Size > 0)
          return true;
      }
      return false;
    }

// ReSharper disable once ParameterTypeCanBeEnumerable.Local
    private static int ParsedSpacesLen(RecoveryStackFrame frame, List<ParsedStateInfo> parsedStates)
    {
      var sum = 0;
// ReSharper disable once LoopCanBeConvertedToQuery
      foreach (var parsedState in parsedStates)
        sum += !frame.IsVoidState(parsedState.State) ? 0 : parsedState.Size;
      return sum;
    }

    protected virtual void TryParseSubrules(List<RecoveryStackFrame> newFrames, int startTextPos, Parser parser, RecoveryStackFrame frame, int curTextPos, string text, int subruleLevel, int state)
    {
      if (_nestedLevel > 20) // ловим зацикленную рекурсию для целей отладки
        return;

      _nestedLevel++;
      var frames = frame.GetSpeculativeFramesForState(curTextPos, parser, state);

#if !N2RUNTIME
      if (frames.Length != 0)
      {
      }
#endif

      foreach (var subFrame in frames)
      {
        if (subFrame == null)
          continue;

        if (subFrame.IsTokenRule)
          continue;

        if (!_visitedFrame.Add(subFrame))
          continue;

        newFrames.Add(subFrame);
        TryParseSubrules(newFrames, startTextPos, parser, subFrame, curTextPos, text, subruleLevel, 0);
      }

      _nestedLevel--;
    }

    private void AddResult(int startPos, int ruleEndPos, int endPos, int startState, RecoveryStackFrame stack,
      string text, int failPos, bool allowEmpty = false)
    {
      var skipedCount = startPos - failPos;
      var newResult = new RecoveryResult(startPos, ruleEndPos, endPos, startState, 0, stack, text, failPos);
      _candidats.Add(newResult);
    }

    int CompareRecoveryResults(RecoveryResult result1, RecoveryResult result2)
    {
      var skipedCount = result1.StartPos - result1.FailPos;

      if (result1.RuleEndPos >= 0 && result1.SkipedCount == result2.SkipedCount && result1.RecoveredHeadCount == result2.RecoveredHeadCount && result1.RecoveredTailCount > 0  && result2.RecoveredTailCount <= 0) goto good; // если у newResult есть продолжение, а у _bestResult нет
      if (result2.RuleEndPos >= 0 && result1.SkipedCount == result2.SkipedCount && result1.RecoveredHeadCount == result2.RecoveredHeadCount && result1.RecoveredTailCount <= 0 && result2.RecoveredTailCount > 0)  return -1;    // если у _bestResult есть продолжение, а у newResult нет

      if (result1.Stack.Parents.SetEquals(result2.Stack.Parents))
      {
        if (result1.StartState < result2.StartState && result1.SkipedCount <= result2.SkipedCount) goto good;
        if (result1.StartState > result2.StartState && result1.SkipedCount > result2.SkipedCount) return -1;
      }

      if (result1.Stack == result2.Stack)
      {
      }

      if (result1.RuleEndPos >= 0 && result2.RuleEndPos <  0) goto good; //
      if (result1.RuleEndPos <  0 && result2.RuleEndPos >= 0) return -1;

      if (result1.StartPos < result2.StartPos && result1.EndPos == result2.EndPos) goto good;
      if (result1.StartPos > result2.StartPos && result1.EndPos == result2.EndPos) return -1;

      if (skipedCount < result2.SkipedCount) goto good;
      if (skipedCount > result2.SkipedCount) return -1;

      if (result1.EndPos > result2.EndPos) goto good;
      if (result1.EndPos < result2.EndPos) return -1;

      //// Если при восстановлении ничего не было пропарсено, то побеждать должен фрейм с большим FialState, так как
      //// иначе будут возникать фантомные значени. Если же что-то спарсилось, то побеждать должен фрейм с меньшим FialState.
      var winLastState = result2.RecoveredHeadCount == 0 && result1.RecoveredHeadCount == 0;
      var newGrater = CompareStack(result1.Stack, result2.Stack);
      if (winLastState)
      {
        if (newGrater > 0) goto good;
        if (newGrater < 0) return -1;
      }
      else
      {
        if (newGrater > 0) return -1;
        if (newGrater < 0) goto good;
      }

      if (result1.EndPos > result2.EndPos) goto good;
      if (result1.EndPos < result2.EndPos) return -1;

      goto good2;
    good:
      return 1;
    good2:
      return 0;
    }

    protected virtual int ContinueParse(int startTextPos, RecoveryStackFrame recoveryStack, Parser parser, bool trySkipStates)
    {
      return ContinueParseImpl(startTextPos, recoveryStack, parser, trySkipStates);
    }

    protected int ContinueParseImpl(int curTextPos, RecoveryStackFrame recoveryStack, Parser parser, bool trySkipStates)
    {
      var parents = recoveryStack.Parents;

      if (parents.Count == 0)
        return curTextPos;

      var parsedStates = new List<ParsedStateInfo>(); ;
      var results = new List<Tuple<int, RecoveryStackFrame, List<ParsedStateInfo>>>();
      var bestPos = curTextPos;
      foreach (var stackFrame in parents)
      {
        var state = stackFrame.FailState;
        do
        {
          state = stackFrame.GetNextState(state);
          var pos = stackFrame.TryParse(state, curTextPos, true, parsedStates, parser);

          if (pos > bestPos)
          {
            results.Clear();
            bestPos = pos;
          }
          if (pos >= bestPos)
          {
            if (pos > 1)
            { 
            }
            results.Add(Tuple.Create(pos, stackFrame, parsedStates));
            break;
          }
        }
        while (state >= 0);
      }

// ReSharper disable once LoopCanBeConvertedToQuery
      foreach (var result in results)
      {
        var pos = ContinueParseImpl(result.Item1, result.Item2, parser, trySkipStates);
        if (pos > bestPos)
          bestPos = pos;
      }

      if (bestPos > curTextPos)
        return bestPos;

      return Math.Max(parser.MaxFailPos, curTextPos);
    }

    protected virtual int TryParse(Parser parser, RecoveryStackFrame recoveryStack, int curTextPos, int state, out List<ParsedStateInfo> parsedStates)
    {
      parsedStates = new List<ParsedStateInfo>();
      return recoveryStack.TryParse(state, curTextPos, false, parsedStates, parser);
    }

    private void FixAst(Parser parser)
    {
    //  // TODO: Надо переписать. Пока закоментил.
    //  Debug.Assert(_bestResult != null);

    //  var frame = _bestResult.Stack.Head;

    //  if (frame.AstStartPos < 0)
    //    Debug.Assert(frame.AstPtr >= 0);

    //  var error = new ParseErrorData(new NToken(_bestResult.FailPos, _bestResult.StartPos), _bestResults.ToArray());
    //  var errorIndex = parser.ErrorData.Count;
    //  parser.ErrorData.Add(error);

    //  frame.RuleParser.PatchAst(_bestResult.StartPos, _bestResult.StartState, errorIndex, _bestResult.Stack, parser);

    //  for (var stack = _bestResult.Stack.Tail as RecoveryStack; stack != null; stack = stack.Tail as RecoveryStack)
    //  {
    //    if (stack.Head.RuleParser is ExtensibleRuleParser)
    //      continue;
    //    Debug.Assert(stack.Head.FailState >= 0);
    //    stack.Head.RuleParser.PatchAst(stack.Head.AstStartPos, -2, -1, stack, parser);
    //  }
    }

  }
}
