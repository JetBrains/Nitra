//#define DebugOutput
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

  public sealed class Recovery
  {
    List<RecoveryResult>         _candidats = new List<RecoveryResult>();
    RecoveryResult               _bestResult;
    List<RecoveryResult>         _bestResults = new List<RecoveryResult>();
    int                          _nestedLevel;
    HashSet<RecoveryStackFrame>  _visitedFrame = new HashSet<RecoveryStackFrame>();
#if !N2RUNTIME
    public Stopwatch Timer = new Stopwatch();
    public int       Count;
    public TimeSpan  ContinueParseTime;
    public int       ContinueParseCount;
    public TimeSpan  TryParseSubrulesTime;
    public int       TryParseSubrulesCount;
    public TimeSpan  TryParseTime;
    public int       TryParseCount;
    public TimeSpan  TryParseNoCacheTime;
    public int       TryParseNoCacheCount;

    public Action<RecoveryResult, List<RecoveryResult>, List<RecoveryResult>, List<RecoveryStackFrame>> ReportResult;

    public void Init()
    {
      ContinueParseTime     = TimeSpan.Zero;
      ContinueParseCount    = 0;
      TryParseSubrulesTime  = TimeSpan.Zero;
      TryParseSubrulesCount = 0;
      TryParseTime          = TimeSpan.Zero;
      TryParseCount         = 0;
      TryParseNoCacheTime   = TimeSpan.Zero;
      TryParseNoCacheCount  = 0;
      Timer.Reset();
      Count = 0;
    }
#endif

    void Reset()
    {
      _candidats.Clear();
      _bestResult = null;
      _bestResults.Clear();
      _nestedLevel = 0;
    }

    public void Strategy(int startTextPos, Parser parser)
    {
      Reset();

#if !N2RUNTIME
      Timer.Start();
      Count++;
// ReSharper disable once UnusedVariable
      var before = parser.Text.Substring(0, startTextPos); // DEBUG
#endif
      var maxFailPos = parser.MaxFailPos;
      var curTextPos = startTextPos;
      var text = parser.Text;
      Debug.Assert(parser.RecoveryStacks.Count > 0);
      var lastStack = parser.RecoveryStacks.Last();

      var stacks = PrepareStacks(parser);

      while (curTextPos < text.Length && _bestResult == null)// && curTextPos - startTextPos < 400
      {
        foreach (var stack in stacks)
        {
          ProcessStackFrameImpl(startTextPos, parser, stack, curTextPos, text, 0);
        }

        if (_bestResult != null)
          break;

        foreach (var stack in stacks)
        {
          ProcessStackFrameSpeculative(startTextPos, parser, stack, curTextPos, text, 0);
          if (_bestResult != null)
            break;
        }

#if !N2RUNTIME && DebugOutput
        Debug.WriteLine((curTextPos - startTextPos) + "%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%");
#endif
        curTextPos++;
        _visitedFrame.Clear();
      }

      parser.MaxFailPos = maxFailPos;

      if (_bestResult == null)
        AddResult(text.Length, text.Length, text.Length, -1, lastStack, text, startTextPos);

#if !N2RUNTIME
      if (ReportResult != null)
        ReportResult(_bestResult, _bestResults, _candidats, stacks);
#endif
      FixAst(parser);
      Reset();
#if !N2RUNTIME
      Timer.Stop();
#endif
    }

    private void ProcessStackFrame(int startTextPos, Parser parser, RecoveryStackFrame recoveryStack, int curTextPos, string text, int subruleLevel)
    {
      ProcessStackFrameImpl(startTextPos, parser, recoveryStack, curTextPos, text, subruleLevel);
      if (_bestResult == null)
        ProcessStackFrameSpeculative(startTextPos, parser, recoveryStack, curTextPos, text, subruleLevel);
    }

    private void ProcessStackFrameSpeculative(int startTextPos, Parser parser, RecoveryStackFrame recoveryStack, int curTextPos, string text, int subruleLevel)
    {
      var stackFrame = recoveryStack;
#if !N2RUNTIME && DebugOutput
      var indent = subruleLevel;
      Debug.WriteLine(new string(' ', subruleLevel * 2) + "Begin frame --------------------------- subruleLevel=" + subruleLevel);
      foreach (var frame in recoveryStack.Reverse())
        Debug.WriteLine(string.Format("{0}{1}{2}", new string(' ', indent++ * 2), frame.AstPtr < 0 ? "$$ " : "", ToString(frame)));
#endif

      int nextState;
      for (var state = stackFrame.FailState; state >= 0; state = nextState) //subruleLevel > 0 ? stackFrame.GetNextState(stackFrame.FailState) :
      {
        nextState = stackFrame.GetNextState(state);
        if (_bestResult != null)
          return;

        if (!stackFrame.IsTokenRule) //&& stackFrame.FailState == state
          TryParseSubrules(startTextPos, parser, recoveryStack, curTextPos, text, subruleLevel, state);
      }
#if !N2RUNTIME && DebugOutput
      Debug.WriteLine(new string(' ', subruleLevel * 2) + "End frame --------------------------- subruleLevel=" + subruleLevel);
#endif
    }

    private void ProcessStackFrameImpl(int startTextPos, Parser parser, RecoveryStackFrame frame, int curTextPos, string text, int subruleLevel)
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

        if (parser.MaxFailPos > curTextPos && parser.MaxFailPos - curTextPos > ParsedSpacesLen(frame, parsedStates)) // что-то пропарсили и это что-то не пробелы
        {
          //var stack = stackFrame;
          //if (IsBetterStack(stack))
          {
            var pos2 = pos == lastPos ? ContinueParse(pos, frame, parser, isNotOptional) : lastPos;
            AddResult(curTextPos, lastPos, pos2, state, frame, text, startTextPos);
            break;
          }
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

            var old_bestResult    = _bestResult;
            var old_bestResults   = _bestResults;
            var old__candidats    = _candidats;
            var old__visitedFrame = _visitedFrame;

            _bestResult   = null;
            _bestResults  = new List<RecoveryResult>();
            _candidats    = new List<RecoveryResult>();
            _visitedFrame = new HashSet<RecoveryStackFrame>();

            ProcessStackFrame(startTextPos, parser, separatorFrame, curTextPos, text, subruleLevel);

            _bestResults  = old_bestResults;
            _candidats    = old__candidats;
            _visitedFrame = old__visitedFrame;

            if (_bestResult != null && _bestResult.RecoveredCount > 0)
            {
              var endPos = Math.Max(_bestResult.EndPos, curTextPos);
              var ruleEndPos = Math.Max(_bestResult.RuleEndPos, curTextPos);
              _bestResult  = old_bestResult;

              AddResult(curTextPos, ruleEndPos, endPos, -1, separatorFrame, text, startTextPos, true);
              return;
            }

            _bestResult = old_bestResult;
          }
        }

        if (pos > curTextPos && HasParsedStaets(frame, parsedStates) || pos == text.Length)
        {
          if (!isNotOptional)
          {
          }
          var pos2 = ContinueParse(pos, frame, parser, isNotOptional);
          if (!(!isNotOptional && pos == pos2))
            AddResult(curTextPos, pos, pos2, state, frame, text, startTextPos);
        }
        else if (pos == curTextPos && nextState < 0 && !frame.IsTokenRule)
        {
          var pos2 = ContinueParse(pos, frame, parser, isNotOptional);
          if (!(!isNotOptional && pos == pos2))
            if (!(frame.IsSpeculative && !isPrefixParsed)) // Спекулятивный фрэйм стека не спарсивший ничего полезного. Игнорируем его.
              if (pos2 > curTextPos || isPrefixParsed)
                AddResult(curTextPos, pos, pos2, state, frame, text, startTextPos);
        }
        else if (parsedStates.Count > 0 && HasParsedStaets(frame, parsedStates))
        {
          Debug.Assert(pos < 0);
          // Мы сфайлили но прпарсили часть правил. Надо восстанавливаться на первом сбойнувшем правиле.
          var successParseLen = Sum(parsedStates);
          var ruleEndPos = curTextPos + successParseLen;
          AddResult(curTextPos, ruleEndPos, parser.MaxFailPos, state, frame, text, startTextPos);
        }
        else if (pos < 0 && nextState < 0 && !(frame.IsSpeculative && !isPrefixParsed))// Спекулятивный фрэйм стека не спарсивший ничего полезного. Игнорируем его.
        {
          // последнее состояние. Надо попытаться допарсить
          var pos2 = ContinueParse(curTextPos, frame, parser, isNotOptional);
          if (!(!isNotOptional && !isPrefixParsed)) // необязательное правило не спрасившее ни одного не пробельного символа нужно игнорировать
            if (pos2 > curTextPos || isPrefixParsed)
              AddResult(curTextPos, pos, pos2, -1, frame, text, startTextPos);
        }
      }
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

    void TryParseSubrules(int startTextPos, Parser parser, RecoveryStackFrame frame, int curTextPos, string text, int subruleLevel, int state)
    {
      if (_nestedLevel > 20) // ловим зацикленную рекурсию для целей отладки
        return;

#if !N2RUNTIME
      TryParseSubrulesCount++;
      var time = Timer.Elapsed;
#endif

      //if (recoveryStack.tl.Contains(recoveryStack.hd))
      //  return;

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

#if !N2RUNTIME && DebugOutput
        Debug.WriteLine(string.Format("{0}## {1}", new string(' ', (_nestedLevel + recoveryStack.Length) * 2), ToString(farame)));
#endif

        ProcessStackFrame(startTextPos, parser, subFrame, curTextPos, text, subruleLevel + 1);
      }

      _nestedLevel--;

#if !N2RUNTIME
      if (_nestedLevel == 0)
        TryParseSubrulesTime += Timer.Elapsed - time;
#endif
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

      if (stack.Parents == _bestResult.Stack.Parents)
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
#if !N2RUNTIME && DebugOutput
    Debug.WriteLine(string.Format("{0}  << {1} >>", new string(' ', (_nestedLevel + _bestResult.Stack.Length) * 2), _bestResult));
#endif
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

    int ContinueParse(int startTextPos, RecoveryStackFrame recoveryStack, Parser parser, bool trySkipStates)
    {
#if !N2RUNTIME
      var stratTime = Timer.Elapsed;
      ContinueParseCount++;
#endif
      var result = ContinueParseImpl(startTextPos, recoveryStack, parser, trySkipStates);
#if !N2RUNTIME
      ContinueParseTime += Timer.Elapsed - stratTime;
#endif
      return result;
    }

    int ContinueParseImpl(int startTextPos, RecoveryStackFrame recoveryStack, Parser parser, bool trySkipStates)
    {
      var parents = recoveryStack.Parents;

      if (parents.Count == 0)
        return startTextPos;

      List<ParsedStateInfo> parsedStates;
      var results = new List<Tuple<int, RecoveryStackFrame, List<ParsedStateInfo>>>();
      var bestPos = -1;
      foreach (var stackFrame in parents)
      {
        var pos = TryParse(parser, stackFrame, startTextPos, -2, out parsedStates); // -2 - предлагаем парсеру вычислить следующее состояние для допарсивания
        if (pos < 0)
          continue;

        if (pos > bestPos)
        {
          results.Clear();
          results.Add(Tuple.Create(pos, stackFrame, parsedStates));
          bestPos = pos;
        }
        else if (pos == bestPos)
          results.Add(Tuple.Create(pos, stackFrame, parsedStates));
      }

// ReSharper disable once LoopCanBeConvertedToQuery
      foreach (var result in results)
      {
        var pos = ContinueParseImpl(result.Item1, result.Item2, parser, trySkipStates);
        if (pos > bestPos)
          bestPos = pos;
      }

      if (bestPos > startTextPos)
        return startTextPos;

      if (trySkipStates)
      {
        foreach (var stackFrame in parents)
        {
          // Если неудалось продолжить парсинг напрямую пытаемся скипнуть одно или более состояние и продолжить парсинг.
          // Это позволяет нам продолжить допарсивание даже в условиях когда непосредственно за местом восстановления находится повторная ошибка.
          // TODO: Но по уму нужно тупо использовать обычный парсинг, выявлять место следующей ошибки и пытаться восстановить ее рекурсивно! 
          for (var state = stackFrame.GetNextState(stackFrame.FailState); state >= 0; state = stackFrame.GetNextState(state))
          {
            var pos = TryParse(parser, stackFrame, startTextPos, state, out parsedStates);
            if (pos > bestPos && !stackFrame.IsVoidState(state))
              bestPos = pos;
          }

          if (bestPos >= startTextPos)
            return ContinueParseImpl(bestPos, stackFrame, parser, true);
        }
      }

      return Math.Max(parser.MaxFailPos, startTextPos);
    }

    private int TryParse(Parser parser, RecoveryStackFrame recoveryStack, int curTextPos, int state, out List<ParsedStateInfo> parsedStates)
    {
#if !N2RUNTIME
      TryParseCount++;
      var timer = Timer.Elapsed;
#endif

      int result;

      if (state < 0)
      {
#if !N2RUNTIME
        TryParseNoCacheCount++;
        var timer2 = Timer.Elapsed;
#endif
        parsedStates = new List<ParsedStateInfo>();
        result = recoveryStack.TryParse(state, curTextPos, parsedStates, parser);
#if !N2RUNTIME
        TryParseNoCacheTime += Timer.Elapsed - timer2;
#endif
      }
      else
      {
#if !N2RUNTIME
        TryParseNoCacheCount++;
        var timer2 = Timer.Elapsed;
#endif
        parsedStates = new List<ParsedStateInfo>();
        int pos = recoveryStack.TryParse(state, curTextPos, parsedStates, parser);
#if !N2RUNTIME
        TryParseNoCacheTime += Timer.Elapsed - timer2;
#endif
        result = pos;
      }

#if !N2RUNTIME
      TryParseTime += Timer.Elapsed - timer;
#endif
      return result;
    }

    private void FixAst(Parser parser)
    {
      // TODO: Надо переписать. Пока закоментил.
      Debug.Assert(_bestResult != null);

      var frame = _bestResult.Stack.Head;

      if (frame.AstStartPos < 0)
        Debug.Assert(frame.AstPtr >= 0);

      var error = new ParseErrorData(new NToken(_bestResult.FailPos, _bestResult.StartPos), _bestResults.ToArray());
      var errorIndex = parser.ErrorData.Count;
      parser.ErrorData.Add(error);

      frame.RuleParser.PatchAst(_bestResult.StartPos, _bestResult.StartState, errorIndex, _bestResult.Stack, parser);

      for (var stack = _bestResult.Stack.Tail as RecoveryStack; stack != null; stack = stack.Tail as RecoveryStack)
      {
        if (stack.Head.RuleParser is ExtensibleRuleParser)
          continue;
        Debug.Assert(stack.Head.FailState >= 0);
        stack.Head.RuleParser.PatchAst(stack.Head.AstStartPos, -2, -1, stack, parser);
      }
    }

    private static List<RecoveryStackFrame> PrepareStacks(Parser parser)
    {
      //var stacks = new List<RecoveryStackFrame>();

      //foreach (var stack in parser.RecoveryStacks)
      //  UpdateStacks(stacks, stack as RecoveryStack);

      //return stacks;
      return parser.RecoveryStacks;
    }

    //private static void UpdateStacks(List<RecoveryStack> stacks, RecoveryStack stack)
    //{
    //  var index = stacks.FindIndex(s => IsSubStack(stack, s)); // is nStack SubStack of s
    //  if (index >= 0)
    //    stacks[index] = stack;
    //  else if (stacks.FindIndex(s => IsSubStack(s, stack)) < 0) // is new stack?
    //    stacks.Add(stack);
    //  // else -> better stack in list
    //}

    //private static bool IsSubStack(RecoveryStack stack1, RecoveryStack stack2)
    //{
    //  if (stack2.Length > stack1.Length)
    //    return false;

    //  var ary1 = stack1.ToArray();
    //  var ary2 = stack2.ToArray();

    //  Array.Reverse(ary1);
    //  Array.Reverse(ary2);

    //  for (int i = 0; i < ary2.Length; i++)
    //  {
    //    var head1 = ary1[i];
    //    var head2 = ary2[i];
    //    if (!object.ReferenceEquals(head1.RuleParser, head2.RuleParser) || head1.FailState != head2.FailState)
    //      return false;
    //  }

    //  return true;
    //}

  }

  //internal static class Utils
  //{
  //  public static bool IsRestStatesCanParseEmptyString(this IRecoveryRuleParser ruleParser, int state)
  //  {
  //    bool ok = true;

  //    for (; state >= 0; state = ruleParser.GetNextState(state))
  //      ok &= ruleParser.IsRestStatesCanParseEmptyString(state);

  //    return ok;
  //  }

  //  public static RecoveryStack Push(this RecoveryStack stack, RecoveryStackFrame elem)
  //  {
  //    return new RecoveryStack(elem, stack);
  //  }
  //}
}
