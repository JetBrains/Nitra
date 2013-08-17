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
  using PrseData = Tuple<int, int, List<ParsedStateInfo>>;
  using ReortData = Action<RecoveryResult, List<RecoveryResult>, List<RecoveryResult>, List<RecoveryStackFrame>>;

  public class Recovery
  {
    protected List<RecoveryResult>          _candidats    = new List<RecoveryResult>();
    protected RecoveryResult                _bestResult;
    protected List<RecoveryResult>          _bestResults  = new List<RecoveryResult>();
    protected int                           _nestedLevel  = 0;
    protected HashSet<RecoveryStackFrame>   _visitedFrame = new HashSet<RecoveryStackFrame>();

    public ReortData ReportResult;

    public Recovery(ReortData reportResult)
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

    public virtual void Strategy(int startTextPos, Parser parser)
    {
      Reset();
      var maxFailPos = parser.MaxFailPos;
      var curTextPos = startTextPos;
      var text = parser.Text;
      Debug.Assert(parser.RecoveryStacks.Count > 0);
      var stacks = parser.RecoveryStacks;
      var lastStack = stacks.Last();

      while (curTextPos < text.Length && _bestResult == null)// && curTextPos - curTextPos < 400
      {
        foreach (var stack in stacks)
          ProcessTopFrames(startTextPos, parser, stack, curTextPos, text, 0);

        if (_bestResult != null)
          break;

        foreach (var stack in stacks)
          ProcessOtherFrames(startTextPos, parser, stack, curTextPos, text, 0);

        if (_bestResult != null)
          break;

        foreach (var stack in stacks)
        {
          ProcessStackFrameSpeculative(startTextPos, parser, stack, curTextPos, text, 0);
          if (_bestResult != null)
            break;
        }

        curTextPos++;
        _visitedFrame.Clear();
      }

      parser.MaxFailPos = maxFailPos;

      if (_bestResult == null)
        AddResult(text.Length, text.Length, text.Length, -1, lastStack, text, startTextPos);

      if (ReportResult != null)
        ReportResult(_bestResult, _bestResults, _candidats, stacks);

      FixAst(parser);
      Reset();
    }

    private void ProcessStackFrame(int startTextPos, Parser parser, RecoveryStackFrame recoveryStack, int curTextPos, string text, int subruleLevel)
    {
      ProcessTopFrames(startTextPos, parser, recoveryStack, curTextPos, text, subruleLevel);
      if (_bestResult != null)
        return;
      ProcessOtherFrames(startTextPos, parser, recoveryStack, curTextPos, text, subruleLevel);
      if (_bestResult != null)
        return;
      ProcessStackFrameSpeculative(startTextPos, parser, recoveryStack, curTextPos, text, subruleLevel);
    }

    private void ProcessStackFrameSpeculative(int startTextPos, Parser parser, RecoveryStackFrame recoveryStack, int curTextPos, string text, int subruleLevel)
    {
      var stackFrame = recoveryStack;

      int nextState;
      for (var state = stackFrame.FailState; state >= 0; state = nextState) //subruleLevel > 0 ? stackFrame.GetNextState(stackFrame.FailState) :
      {
        nextState = stackFrame.GetNextState(state);
        if (_bestResult != null)
          return;

        if (!stackFrame.IsTokenRule) //&& stackFrame.FailState == state
          TryParseSubrules(startTextPos, parser, recoveryStack, curTextPos, text, subruleLevel, state);
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
          AddResult(curTextPos, lastPos, lastPos, state, frame, text, startTextPos);
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
            }
          }
        }
      }
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

    protected virtual void TryParseSubrules(int startTextPos, Parser parser, RecoveryStackFrame frame, int curTextPos, string text, int subruleLevel, int state)
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
        if (subFrame.IsTokenRule)
          continue;

        if (!_visitedFrame.Add(subFrame))
          continue;

        ProcessStackFrame(startTextPos, parser, subFrame, curTextPos, text, subruleLevel + 1);
      }

      _nestedLevel--;
    }

    void AddResult(int startPos, int ruleEndPos, int endPos, int startState, RecoveryStackFrame stack, string text, int failPos, bool allowEmpty = false)
    {
      const int stackLength = 0; //stack.Length;
      var skipedCount = startPos - failPos;
      var newResult = new RecoveryResult(startPos, ruleEndPos, endPos, startState, stackLength, stack, text, failPos);
      _candidats.Add(newResult);

      if (newResult.SkipedCount > 0)
      {
      }

      if (!allowEmpty && startPos == endPos && endPos != text.Length) return;

      if (_bestResult == null)                   goto good;

      if (stack.IsSpeculative && _bestResult.Stack.IsSpeculative) // спекулятивный фрейм стека
      {
        Debug.Assert(false, "Этого не должно произойки, так как спекулятивный парсинг должен происходить в методе ProcessStackFrameSpeculative() который вызывается только если ProcessStackFrame() не нашел результат.");
// ReSharper disable once HeuristicUnreachableCode
        return;
      }

      if (newResult.RuleEndPos   >= 0 && newResult.SkipedCount == _bestResult.SkipedCount && newResult.RecoveredHeadCount == _bestResult.RecoveredHeadCount && newResult.RecoveredTailCount > 0  && _bestResult.RecoveredTailCount <= 0) goto good; // если у newResult есть продолжение, а у _bestResult нет
      if (_bestResult.RuleEndPos >= 0 && newResult.SkipedCount == _bestResult.SkipedCount && newResult.RecoveredHeadCount == _bestResult.RecoveredHeadCount && newResult.RecoveredTailCount <= 0 && _bestResult.RecoveredTailCount > 0) return;    // если у _bestResult есть продолжение, а у newResult нет

      if (stack.Parents.SetEquals(_bestResult.Stack.Parents))
      {
        if (startState < _bestResult.StartState && newResult.SkipedCount <= _bestResult.SkipedCount) goto good;
        if (startState > _bestResult.StartState && newResult.SkipedCount >  _bestResult.SkipedCount) return;
      }

      if (stack == _bestResult.Stack)
      {
      }

      if (newResult.RuleEndPos >= 0 && _bestResult.RuleEndPos <  0) goto good; //
      if (newResult.RuleEndPos <  0 && _bestResult.RuleEndPos >= 0) return;

      if (startPos < _bestResult.StartPos && endPos == _bestResult.EndPos) goto good;
      if (startPos   > _bestResult.StartPos && endPos == _bestResult.EndPos) return;

      if (skipedCount < _bestResult.SkipedCount) goto good;
      if (skipedCount > _bestResult.SkipedCount) return;

      if (endPos > _bestResult.EndPos) goto good;
      if (endPos < _bestResult.EndPos) return;

      //// Если при восстановлении ничего не было пропарсено, то побеждать должен фрейм с большим FialState, так как
      //// иначе будут возникать фантомные значени. Если же что-то спарсилось, то побеждать должен фрейм с меньшим FialState.
      //var winLastState = _bestResult.RecoveredCount == 0 && newResult.RecoveredCount == 0;
      //var result = CompareStack(stack, _bestResult.Stack, winLastState);
      //if (result > 0)  goto good;
      //if (result < 0) return;

      if (endPos > _bestResult.EndPos) goto good;
      if (endPos < _bestResult.EndPos) return;

      goto good2;
    good:
      _bestResult = new RecoveryResult(startPos, ruleEndPos, endPos, startState, stackLength, stack, text, failPos);
      _bestResults.Clear();
      _bestResults.Add(_bestResult);
      return;
    good2:
      _bestResults.Add(new RecoveryResult(startPos, ruleEndPos, endPos, startState, stackLength, stack, text, failPos));
    }

    ///// <param name="stack1">Стек для сразвнения</param>
    ///// <param name="stack2">Стек для сразвнения</param>
    ///// <param name="winLastState">Если true - будет побеждать фрейм с большим FailState и наоборот.</param>
    ///// <returns>0 - стеки равны или несравнимы, 1 - первый стек лучше второго, -1 второй стек лучше.</returns>
    //public static int CompareStack(RecoveryStackFrame stack1, RecoveryStackFrame stack2, bool winLastState)
    //{
    //  var len1 = stack1.Length;
    //  var len2 = stack2.Length;
    //  var len  = Math.Min(len1, len2);

    //  if (len1 != len2) // отбрасываем "лишние" элементы самого длинного цикла.
    //    if (len1 == len)
    //      stack2 = SkipN(stack2, len2 - len1);
    //    else
    //      stack1 = SkipN(stack1, len1 - len2);

    //  var result = CompareStackImpl(stack1, stack2, winLastState);

    //  if (result == 0)
    //    return len2 - len1; // если корни стеков равны, то лучше более короткий стек, так как более длинный является спекулятивным (более корткие постеки выкидываются вначале обработки)

    //  return result;
    //}

    //private static int CompareStackImpl(RecoveryStackFrame stack1, RecoveryStackFrame stack2, bool winLastState)
    //{
    //  if (stack1.tl.IsEmpty)
    //    return 0;

    //  var result = CompareStackImpl((RecoveryStack)stack1.tl, (RecoveryStack)stack2.tl, winLastState);

    //  if (result != 0)
    //    return result;

    //  var x = stack1.hd;
    //  var y = stack2.hd;

    //  if (!object.ReferenceEquals(x.RuleParser, y.RuleParser))
    //    return 0; // стеки несравнимы

    //  return winLastState
    //    ? x.FailState - y.FailState  // лучше фрэйм с большим значением FailState
    //    : y.FailState - x.FailState; // лучше фрэйм с меньшим значением FailState
    //}

    //private static RecoveryStack SkipN(RecoveryStack stack, int n)
    //{
    //  for (var i = 0; i < n; i++)
    //    stack = (RecoveryStack)stack.tl;
    //  return stack;
    //}

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
            results.Add(Tuple.Create(pos, stackFrame, parsedStates));
            bestPos = pos;
          }
          else if (pos == bestPos)
            results.Add(Tuple.Create(pos, stackFrame, parsedStates));
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
        return curTextPos;

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
