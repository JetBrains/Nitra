//#define DebugOutput
using N2.Internal;

using Nemerle.Collections;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using RecoveryStack = Nemerle.Core.list<N2.Internal.RecoveryStackFrame>.Cons;
using System.Diagnostics;

#if N2RUNTIME
namespace N2.Strategies
#else
namespace N2.DebugStrategies
#endif
{
  using PrseData = Tuple<int, int, List<ParsedStateInfo>>;

  public sealed class Recovery
  {
    List<RecoveryResult>         _candidats = new List<RecoveryResult>();
    RecoveryResult               _bestResult;
    List<RecoveryResult>         _bestResults = new List<RecoveryResult>();
    Dictionary<object, PrseData> _visited = new Dictionary<object, PrseData>();
    RecoveryStack                _recoveryStack;
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

    public Action<RecoveryResult, List<RecoveryResult>, List<RecoveryResult>, List<RecoveryStack>> ReportResult;

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
      _visited = new Dictionary<object, PrseData>();
    }

    public void Strategy(int startTextPos, Parser parser)
    {
      Reset();
#if !N2RUNTIME
      Timer.Start();
      Count++;
      var before = parser.Text.Substring(0, startTextPos); // DEBUG
#endif
      var maxFailPos = parser.MaxFailPos;
      var curTextPos = startTextPos;
      var text = parser.Text;
      Debug.Assert(parser.RecoveryStacks.Count > 0);
      var lastStack = (RecoveryStack)parser.RecoveryStacks.Last();
      var stacks = PrepareStacks(parser);

      do
      {
        foreach (var stack in stacks)
        {
          _recoveryStack = stack;
          ProcessStackFrameImpl(startTextPos, parser, _recoveryStack, curTextPos, text, 0);
        }

        if (_bestResult != null)
          break;

        foreach (var stack in stacks)
        {
          _recoveryStack = stack;
          ProcessStackFrameSpeculative(startTextPos, parser, _recoveryStack, curTextPos, text, 0);
          if (_bestResult != null)
            break;
        }

#if !N2RUNTIME && DebugOutput
        Debug.WriteLine((curTextPos - startTextPos) + "%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%");
#endif
        curTextPos++;
        _visitedFrame.Clear();
      }
      while (curTextPos <= text.Length && _bestResult == null);// && curTextPos - startTextPos < 400

      parser.MaxFailPos = maxFailPos;

      if (_bestResult == null)
        AddResult(text.Length, text.Length, text.Length, -1, lastStack, text, startTextPos);

#if !N2RUNTIME
      ReportResult(_bestResult, _bestResults, _candidats, stacks);
#endif
      FixAst(parser);
      Reset();
#if !N2RUNTIME
      Timer.Stop();
#endif
    }

    private void ProcessStackFrame(int startTextPos, Parser parser, RecoveryStack recoveryStack, int curTextPos, string text, int subruleLevel)
    {
      ProcessStackFrameImpl(startTextPos, parser, recoveryStack, curTextPos, text, subruleLevel);
      if (_bestResult == null)
        ProcessStackFrameSpeculative(startTextPos, parser, recoveryStack, curTextPos, text, subruleLevel);
    }

    private void ProcessStackFrameSpeculative(int startTextPos, Parser parser, RecoveryStack recoveryStack, int curTextPos, string text, int subruleLevel)
    {
      var stackFrame = recoveryStack.hd;
      var ruleParser = stackFrame.RuleParser;

#if !N2RUNTIME && DebugOutput
      var indent = subruleLevel;
      Debug.WriteLine(new string(' ', subruleLevel * 2) + "Begin frame --------------------------- subruleLevel=" + subruleLevel);
      foreach (var frame in recoveryStack.Reverse())
        Debug.WriteLine(string.Format("{0}{1}{2}", new string(' ', indent++ * 2), frame.AstPtr < 0 ? "$$ " : "", ToString(frame)));
#endif

      int nextState;
      for (var state = stackFrame.FailState; state >= 0; state = nextState) //subruleLevel > 0 ? ruleParser.GetNextState(stackFrame.FailState) :
      {
        nextState = ruleParser.GetNextState(state);
        if (_bestResult != null)
          return;

        if (!stackFrame.RuleParser.IsTokenRule /*&& stackFrame.FailState == state*/)
          TryParseSubrules(startTextPos, parser, recoveryStack, curTextPos, text, subruleLevel, state);
        else
        {
        }
      }
#if !N2RUNTIME && DebugOutput
      Debug.WriteLine(new string(' ', subruleLevel * 2) + "End frame --------------------------- subruleLevel=" + subruleLevel);
#endif
    }

    private void ProcessStackFrameImpl(int startTextPos, Parser parser, RecoveryStack recoveryStack, int curTextPos, string text, int subruleLevel)
    {
      var stackFrame = recoveryStack.hd;
      var ruleParser = stackFrame.RuleParser;
      var isPrefixParsed = !ruleParser.IsStartState(stackFrame.FailState);
      var isOptional = ruleParser.IsLoopSeparatorStart(stackFrame.FailState);
      List<ParsedStateInfo> parsedStates;

      int nextState;
      for (var state = stackFrame.FailState; state >= 0; state = nextState) //subruleLevel > 0 ? ruleParser.GetNextState(stackFrame.FailState) :
      {
        parser.MaxFailPos = startTextPos;
        nextState = ruleParser.GetNextState(state);

        int pos = TryParse(parser, recoveryStack, curTextPos, ruleParser, state, out parsedStates);

        if (curTextPos > 0)
          Debug.Assert(pos != 0);

        var lastPos = Math.Max(pos, parser.MaxFailPos);

        if (parser.MaxFailPos > curTextPos && parser.MaxFailPos - curTextPos > ParsedSpacesLen(ruleParser, parsedStates)) // что-то пропарсили и это что-то не пробелы
        {
          //var stack = recoveryStack;
          //if (IsBetterStack(stack))
          {
            var pos2 = pos == lastPos ? ContinueParse(pos, recoveryStack, parser, text, !isOptional) : lastPos;
            AddResult(curTextPos, lastPos, pos2, state, recoveryStack, text, startTextPos);
            break;
          }
        }

        var isParsed = pos > curTextPos;

        if (!isPrefixParsed && isParsed && !ruleParser.IsVoidState(state))
          isPrefixParsed = isParsed;

        if (nextState < 0 && !isPrefixParsed) //
        {
          int itemId;
          IRecoveryRuleParser itemRuleParser;
          var loopBodyStartStgate = ruleParser.GetBodyStartStateForSeparator(state, out itemRuleParser, out itemId);
          if (loopBodyStartStgate >= 0)
          {
            // Нас просят попробовать востановить отстуствующий разделитель цикла. Чтобы знать, нужно ли это дела, или мы
            // имеем дело с банальным концом цикла мы должны
            var elemFrame = new RecoveryStackFrame(itemRuleParser, itemId, stackFrame.AstPtr, stackFrame.AstStartPos, loopBodyStartStgate, stackFrame.Counter, 0, 0, stackFrame.IsRootAst, stackFrame.Info);
            var loopStack = (RecoveryStack)recoveryStack.Tail;
            var loopFrame = loopStack.hd;
            var newLoopFrame = new RecoveryStackFrame(loopFrame.RuleParser, loopFrame.RuleId, loopFrame.AstPtr, loopFrame.AstStartPos, loopFrame.FailState, loopFrame.Counter, loopFrame.ListStartPos, loopFrame.ListEndPos, loopFrame.IsRootAst, FrameInfo.LoopBody);
            var newStack = new RecoveryStack(elemFrame, new RecoveryStack(newLoopFrame, loopStack.Tail));

            var old_bestResult    = _bestResult;
            var old_bestResults   = _bestResults;
            var old__candidats    = _candidats;
            var old__visitedFrame = _visitedFrame;

            _bestResult   = null;
            _bestResults  = new List<RecoveryResult>();
            _candidats    = new List<RecoveryResult>();
            _visitedFrame = new HashSet<RecoveryStackFrame>();

            ProcessStackFrame(startTextPos, parser, newStack, curTextPos, text, subruleLevel);

            _bestResults  = old_bestResults;
            _candidats    = old__candidats;
            _visitedFrame = old__visitedFrame;

            if (_bestResult != null && _bestResult.RecoveredCount > 0)
            {
              var endPos = Math.Max(_bestResult.EndPos, curTextPos);
              var ruleEndPos = Math.Max(_bestResult.RuleEndPos, curTextPos);
              _bestResult  = old_bestResult;

              AddResult(curTextPos, ruleEndPos, endPos, -1, recoveryStack, text, startTextPos, true);
              return;
            }

            _bestResult = old_bestResult;
          }
        }

        if (pos > curTextPos && HasParsedStaets(ruleParser, parsedStates) || pos == text.Length)
        {
          if (isOptional)
          {
          }
          var pos2 = ContinueParse(pos, recoveryStack, parser, text, !isOptional);
          if (!(isOptional && pos == pos2))
          AddResult(curTextPos, pos, pos2, state, recoveryStack, text, startTextPos);
        }
        else if (pos == curTextPos && nextState < 0 && !stackFrame.RuleParser.IsTokenRule)
        {
          var pos2 = ContinueParse(pos, recoveryStack, parser, text, !isOptional);
          if (!(isOptional && pos == pos2))
          if (!(stackFrame.AstPtr == -1 && !isPrefixParsed)) // Спекулятивный фрэйм стека не спарсивший ничего полезного. Игнорируем его.
          if (pos2 > curTextPos || isPrefixParsed)
          {
            AddResult(curTextPos, pos, pos2, state, recoveryStack, text, startTextPos);
          }
        }
        else if (parsedStates.Count > 0 && HasParsedStaets(ruleParser, parsedStates))
        {
          Debug.Assert(pos < 0);
          // Мы сфайлили но прпарсили часть правил. Надо восстанавливаться на первом сбойнувшем правиле.
          var successParseLen = Sum(parsedStates);
          var ruleEndPos = curTextPos + successParseLen;
          //var pos2 = ContinueParse(ruleEndPos, recoveryStack, parser, text, !isOptional);
          //AddResult(curTextPos, ruleEndPos, Math.Max(parser.MaxFailPos, pos2), state, recoveryStack, text, startTextPos);
          AddResult(curTextPos, ruleEndPos, parser.MaxFailPos, state, recoveryStack, text, startTextPos);
          //break;
        }
        else if (pos < 0 && nextState < 0 && !(stackFrame.AstPtr == -1 && !isPrefixParsed))
        {
          // последнее состояние. Надо попытаться допарсить
          var pos2 = ContinueParse(curTextPos, recoveryStack, parser, text, !isOptional);
          if (!(isOptional && !isPrefixParsed)) // необязательное правило не спрасившее ни одного не пробельного символа нужно игнорировать
          if (!(stackFrame.AstPtr == -1 && !isPrefixParsed)) // Спекулятивный фрэйм стека не спарсивший ничего полезного. Игнорируем его.
          if (pos2 > curTextPos || isPrefixParsed)
          {
            AddResult(curTextPos, pos, pos2, -1, recoveryStack, text, startTextPos);
          }
        }
      }
    }

    private static int Sum(List<ParsedStateInfo> parsedStates)
    {
      return parsedStates.Sum(x => x.Size);
    }

    private static bool HasParsedStaets(IRecoveryRuleParser ruleParser, List<ParsedStateInfo> parsedStates)
    {
      return parsedStates.Any(x => !ruleParser.IsVoidState(x.State) && x.Size > 0);
    }

    private static int ParsedSpacesLen(IRecoveryRuleParser ruleParser, List<ParsedStateInfo> parsedStates)
    {
      return parsedStates.Sum(x => !ruleParser.IsVoidState(x.State) ? 0 : x.Size);
    }

    void TryParseSubrules(int startTextPos, Parser parser, RecoveryStack recoveryStack, int curTextPos, string text, int subruleLevel, int state)
    {
      if (_nestedLevel > 20) // ловим зацикленную рекурсию для целей отладки
        return;

#if !N2RUNTIME
      TryParseSubrulesCount++;
      var time = Timer.Elapsed;
#endif

      if (recoveryStack.tl.Contains(recoveryStack.hd))
        return;

      _nestedLevel++;
      var stackFrame = recoveryStack.hd;
      var parsers = stackFrame.RuleParser.GetParsersForState(state);

#if !N2RUNTIME
      if (!parsers.IsEmpty())
      {
      }
#endif

      foreach (var subRuleParser in parsers)
      {
        if (subRuleParser.IsTokenRule)
          continue;

        int subRuleParserId = -1;
        if (subRuleParser is StartRuleParser) subRuleParserId = ((StartRuleParser)subRuleParser).StartRuleId;
        if (subRuleParser is ExtentionRuleParser) subRuleParserId = ((ExtentionRuleParser)subRuleParser).RuleId;
        Debug.Assert(subRuleParserId != -1);

        var old = recoveryStack;
        var newFrame = new RecoveryStackFrame(subRuleParser, subRuleParserId, -1, startTextPos, subRuleParser.StartState, 0, 0, 0, true, FrameInfo.None);

        if (!_visitedFrame.Add(newFrame))
          continue;

        recoveryStack = recoveryStack.Push(newFrame);

#if !N2RUNTIME && DebugOutput
        Debug.WriteLine(string.Format("{0}## {1}", new string(' ', (_nestedLevel + recoveryStack.Length) * 2), ToString(newFrame)));
#endif

        ProcessStackFrame(startTextPos, parser, recoveryStack, curTextPos, text, subruleLevel + 1);
        recoveryStack = old; // remove top element
      }

      _nestedLevel--;

#if !N2RUNTIME
      if (_nestedLevel == 0)
        TryParseSubrulesTime += Timer.Elapsed - time;
#endif
    }

#if !N2RUNTIME
    static string ToString(RecoveryStackFrame frame)
    {
      return frame + "  RuleId=" + frame.RuleId + " AstStartPos=" + frame.AstStartPos + " RuleParser=" + frame.RuleParser.GetHashCode();
    }
#endif

    void AddResult(int startPos, int ruleEndPos, int endPos, int startState, RecoveryStack stack, string text, int failPos, bool allowEmpty = false)
    {
      int stackLength = stack.Length;
      var skipedCount = startPos - failPos;
      var newResult = new RecoveryResult(startPos, ruleEndPos, endPos, startState, stackLength, stack, text, failPos);
      _candidats.Add(newResult);

      if (newResult.SkipedCount > 0)
      {
      }

      if (stack.hd.AstPtr == 18)
      {
      }

      if (!allowEmpty && startPos == endPos && endPos != text.Length) return;

      if (_bestResult == null)                   goto good;

      if (stack.hd.AstPtr == -1 && _bestResult.Stack.hd.AstPtr != -1) // спекулятивный фрейм стека
      {
        return;
      }

      if (newResult.RuleEndPos   >= 0 && newResult.SkipedCount == _bestResult.SkipedCount && newResult.RecoveredHeadCount == _bestResult.RecoveredHeadCount && newResult.RecoveredTailCount > 0  && _bestResult.RecoveredTailCount <= 0) goto good; // если у newResult есть продолжение, а у _bestResult нет
      if (_bestResult.RuleEndPos >= 0 && newResult.SkipedCount == _bestResult.SkipedCount && newResult.RecoveredHeadCount == _bestResult.RecoveredHeadCount && newResult.RecoveredTailCount <= 0 && _bestResult.RecoveredTailCount > 0) return;    // если у _bestResult есть продолжение, а у newResult нет

      if (stack.Tail == _bestResult.Stack.Tail)
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

      stackLength = stack.Length;

      // Если при восстановлении ничего не было пропарсено, то побеждать должен фрейм с большим FialState, так как
      // иначе будут возникать фантомные значени. Если же что-то спарсилось, то побеждать должен фрейм с меньшим FialState.
      var winLastState = _bestResult.RecoveredCount == 0 && newResult.RecoveredCount == 0;
      var result = CompareStack(stack, _bestResult.Stack, winLastState);
      if (result > 0)  goto good;
      if (result < 0) return;

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
      return;
    }

    /// <param name="winLastState">Если true - будет побеждать фрейм с большим FailState и наоборот.</param>
    /// <returns>0 - стеки равны или несравнимы, 1 - первый стек лучше второго, -1 второй стек лучше.</returns>
    public static int CompareStack(RecoveryStack stack1, RecoveryStack stack2, bool winLastState)
    {
      var len1 = stack1.Length;
      var len2 = stack2.Length;
      var len = Math.Min(len1, len2);

      if (len1 != len2) // отбрасываем "лишние" элементы самого длинного цикла.
        if (len1 == len)
          stack2 = SkipN(stack2, len2 - len1);
        else
          stack1 = SkipN(stack1, len1 - len2);

      var result = CompareStackImpl(stack1, stack2, winLastState);

      if (result == 0)
        return len2 - len1; // если корни стеков равны, то лучше более короткий стек, так как более длинный является спекулятивным (более корткие постеки выкидываются вначале обработки)

      return result;
    }

    private static int CompareStackImpl(RecoveryStack stack1, RecoveryStack stack2, bool winLastState)
    {
      if (stack1.tl.IsEmpty)
        return 0;
      else
      {
        var result = CompareStackImpl((RecoveryStack)stack1.tl, (RecoveryStack)stack2.tl, winLastState);

        if (result == 0)
        {
          var x = stack1.hd;
          var y = stack2.hd;

          if (!object.ReferenceEquals(x.RuleParser, y.RuleParser))
            return 0; // стеки несравнимы

          if (winLastState)
            return x.FailState - y.FailState; // лучше фрэйм с большим значением FailState
          else
            return y.FailState - x.FailState; // лучше фрэйм с меньшим значением FailState
        }
        else
          return result;
      }
    }

    private static RecoveryStack SkipN(RecoveryStack stack, int n)
    {
      for (int i = 0; i < n; i++)
        stack = (RecoveryStack)stack.tl;
      return stack;
    }

    int ContinueParse(int startTextPos, RecoveryStack recoveryStack, Parser parser, string text, bool trySkipStates)
    {
#if !N2RUNTIME
      var stratTime = Timer.Elapsed;
      ContinueParseCount++;
#endif
      var result = ContinueParseImpl(startTextPos, recoveryStack, parser, text, trySkipStates);
#if !N2RUNTIME
      ContinueParseTime += Timer.Elapsed - stratTime;
#endif
      return result;
    }

    int ContinueParseImpl(int startTextPos, RecoveryStack recoveryStack, Parser parser, string text, bool trySkipStates)
    {
      var tail = recoveryStack.Tail as RecoveryStack;

      if (tail == null)
        return startTextPos;

      var stackFrame = tail.Head;
      var ruleParser = stackFrame.RuleParser;
      List<ParsedStateInfo> parsedStates;
      var pos = TryParse(parser, tail, startTextPos, ruleParser, -2, out parsedStates); // -2 - предлагаем парсеру вычислить следующее состояние для допарсивания

      if (pos >= 0)
        return ContinueParseImpl(pos, tail, parser, text, trySkipStates);
      else
      {
        if (trySkipStates)
        {
          var pos2 = startTextPos;
          // Если неудалось продолжить парсинг напрямую пытаемся скипнуть одно или более состояние и продолжить парсинг.
          // Это позволяет нам продолжить допарсивание даже в условиях когда непосредственно за местом восстановления находится повторная ошибка.
          for (var state = ruleParser.GetNextState(stackFrame.FailState); state >= 0; state = ruleParser.GetNextState(state))
          {
            parsedStates.Clear();
            pos2 = TryParse(parser, tail, startTextPos, ruleParser, state, out parsedStates);
            if (pos2 >= 0 && !ruleParser.IsVoidState(state))
              return ContinueParseImpl(pos2, tail, parser, text, trySkipStates);
          }
        }

        return Math.Max(parser.MaxFailPos, startTextPos);
      }
    }

    private int TryParse(Parser parser, RecoveryStack recoveryStack, int curTextPos, IRecoveryRuleParser ruleParser, int state, out List<ParsedStateInfo> parsedStates)
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
        result = ruleParser.TryParse(recoveryStack, state, curTextPos, parsedStates, parser);
#if !N2RUNTIME
        TryParseNoCacheTime += Timer.Elapsed - timer2;
#endif
      }
      else
      {
        var key = Tuple.Create(curTextPos, ruleParser, state);
        PrseData data;
        if (_visited.TryGetValue(key, out data))
        {
          if (parser.MaxFailPos < data.Item2)
            parser.MaxFailPos = data.Item2;
          parsedStates = data.Item3;
          result = data.Item1;
        }
        else
        {
#if !N2RUNTIME
          TryParseNoCacheCount++;
          var timer2 = Timer.Elapsed;
#endif
          parsedStates = new List<ParsedStateInfo>();
          int pos = ruleParser.TryParse(recoveryStack, state, curTextPos, parsedStates, parser);
#if !N2RUNTIME
          TryParseNoCacheTime += Timer.Elapsed - timer2;
#endif
          _visited[key] = Tuple.Create(pos, parser.MaxFailPos, parsedStates);
          result = pos;
        }
      }

#if !N2RUNTIME
      TryParseTime += Timer.Elapsed - timer;
#endif

      return result;
    }

    private void FixAst(Parser parser)
    {
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

    private static List<RecoveryStack> PrepareStacks(Parser parser)
    {
      var stacks = new List<RecoveryStack>();

      foreach (var stack in parser.RecoveryStacks)
        UpdateStacks(stacks, stack as RecoveryStack);

      return stacks;
    }

    private static void UpdateStacks(List<RecoveryStack> stacks, RecoveryStack stack)
    {
      var index = stacks.FindIndex(s => IsSubStack(stack, s)); // is nStack SubStack of s
      if (index >= 0)
        stacks[index] = stack;
      else if (stacks.FindIndex(s => IsSubStack(s, stack)) < 0) // is new stack?
        stacks.Add(stack);
      // else -> better stack in list
    }

    private static bool IsSubStack(RecoveryStack stack1, RecoveryStack stack2)
    {
      if (stack2.Length > stack1.Length)
        return false;

      var ary1 = stack1.ToArray();
      var ary2 = stack2.ToArray();

      Array.Reverse(ary1);
      Array.Reverse(ary2);

      for (int i = 0; i < ary2.Length; i++)
      {
        var head1 = ary1[i];
        var head2 = ary2[i];
        if (!object.ReferenceEquals(head1.RuleParser, head2.RuleParser) || head1.FailState != head2.FailState)
          return false;
      }

      return true;
    }
  }

  internal static class Utils
  {
    public static bool IsRestStatesCanParseEmptyString(this IRecoveryRuleParser ruleParser, int state)
    {
      bool ok = true;

      for (; state >= 0; state = ruleParser.GetNextState(state))
        ok &= ruleParser.IsRestStatesCanParseEmptyString(state);

      return ok;
    }

    public static RecoveryStack Push(this RecoveryStack stack, RecoveryStackFrame elem)
    {
      return new RecoveryStack(elem, stack);
    }
  }
}
