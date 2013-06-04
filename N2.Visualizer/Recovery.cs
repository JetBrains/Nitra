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
    List<RecoveryResult> _candidats = new List<RecoveryResult>();
    RecoveryResult       _bestResult;
    List<RecoveryResult> _bestResults = new List<RecoveryResult>();
    int                  _parseCount;
    int                  _recCount;
    int                  _bestResultsCount;
    int                  _nestedLevel;
    Dictionary<int, int> _allacetionsInfo = new Dictionary<int, int>();
    Dictionary<object, PrseData> _visited = new Dictionary<object, PrseData>();
    Dictionary<string, int> _parsedRules = new Dictionary<string, int>();
    RecoveryStack        _recoveryStack;

    void Reset()
    {
      _candidats.Clear();
      _bestResult = null;
      _bestResults.Clear();
      _parseCount = 0;
      _recCount = 0;
      _bestResultsCount = 0;
      _nestedLevel = 0;
      _allacetionsInfo.Clear();
      _visited = new Dictionary<object, PrseData>();
      _parsedRules = new Dictionary<string, int>();
    }

    public void Strategy(int startTextPos, Parser parser)
    {
      Reset();
      var maxFailPos = parser.MaxFailPos;
      var timer = System.Diagnostics.Stopwatch.StartNew();
      _recoveryStack     = parser.RecoveryStack.NToList() as RecoveryStack;
      var curTextPos    = startTextPos;
      var text          = parser.Text;

      parser.ParsingMode = ParsingMode.Parsing;

      var before = parser.Text.Substring(0, startTextPos);

      if (before == "[\r\n  { : 1},\r\n  { a },\r\n  { a: },\r\n  { a:, },\r\n  { \r\n  'a':, \r\n  a:1\r\n  },\r\n  {a::2,:},\r\n  {a# :1}, \r\n  {a")
      {
        Debug.Assert(true);
      }
        
      do
      {
        //for (var stack = _recoveryStack; stack != null; stack = stack.Tail as RecoveryStack)
        //  ProcessStackFrame(startTextPos, parser, stack, curTextPos, text, 0);
        ProcessStackFrame(startTextPos, parser, _recoveryStack, curTextPos, text, 0);
        curTextPos++;
      }
      while (curTextPos - startTextPos < 800 && /*_bestResult == null && _bestResult == null && (res.Count == 0 || curTextPos - startTextPos < 10) &&*/ curTextPos <= text.Length);

      timer.Stop();

      if (_bestResult != null)
      {
        FixAst(parser);
        parser.ParsingMode = ParsingMode.EndRecovery;
        parser.MaxFailPos = _bestResult.EndPos; // HACK!!!
      }
      else
      {
        parser.ParsingMode = ParsingMode.Recovery;
        parser.MaxFailPos = maxFailPos;
      }

      Reset();
    }

    private void ProcessStackFrame(int startTextPos, Parser parser, RecoveryStack recoveryStack, int curTextPos, string text, int subruleLevel)
    {
      var stackFrame = recoveryStack.hd;
      var ruleParser = stackFrame.RuleParser;
      var isPrefixParsed = !ruleParser.IsStartState(stackFrame.FailState);
      var isOptional = ruleParser.IsLoopSeparatorStart(stackFrame.FailState);
      List<ParsedStateInfo> parsedStates;

      int nextState;
      for (var state = subruleLevel > 0 ? ruleParser.GetNextState(stackFrame.FailState) : stackFrame.FailState; state >= 0; state = nextState)
      {
        parser.MaxFailPos = startTextPos;
        nextState = ruleParser.GetNextState(state);
        //var needSkip = nextState < 0 && ruleParser.IsVoidState(state);
        //if (nextState < 0 && ruleParser.IsVoidState(state))
        //  continue;
        int pos = TryParse(parser, recoveryStack, curTextPos, ruleParser, state, out parsedStates);

        var isParsed = pos > curTextPos;

        if (!isPrefixParsed && isParsed && !ruleParser.IsVoidState(state))
          isPrefixParsed = isParsed;

        //if (!isPrefixParsed)
        //  continue;

        if (nextState < 0 && !isPrefixParsed)
        {
          var loopBodyStartStgate = ruleParser.GetBodyStartStateForSeparator(state);
          if (loopBodyStartStgate >= 0)
          {
            // Нас просят попробовать востановить отстуствующий разделитель цикла. Чтобы знать, нужно ли это дела, или мы 
            // имеем дело с банальным концом цикла мы должны
            //var pos2 = ContinueParse(pos, recoveryStack, parser, text, !isOptional);
            var elemFrame = new RecoveryStackFrame(stackFrame.RuleParser, stackFrame.AstPtr, stackFrame.AstStartPos, loopBodyStartStgate, stackFrame.Counter, 0, 0, stackFrame.IsRootAst, stackFrame.Info);
            var loopStack = (RecoveryStack)recoveryStack.Tail;
            var loopFrame = loopStack.hd;
            var newLoopFrame = new RecoveryStackFrame(loopFrame.RuleParser, loopFrame.AstPtr, loopFrame.AstStartPos, loopFrame.FailState, loopFrame.Counter, loopFrame.ListStartPos, loopFrame.ListEndPos, loopFrame.IsRootAst, FrameInfo.LoopBody);
            var newStack = new RecoveryStack(elemFrame, new RecoveryStack(newLoopFrame, loopStack.Tail));
            var old_bestResult = _bestResult;
            var old_bestResults = _bestResults;
            _bestResult = null;
            _bestResults = new List<RecoveryResult>();

            ProcessStackFrame(startTextPos, parser, newStack, curTextPos, text, subruleLevel);

            if (_bestResult != null && _bestResult.RecoveredCount > 0)
            {
              _bestResult = old_bestResult;
              _bestResults = old_bestResults;
              AddResult(curTextPos, curTextPos, curTextPos, state, recoveryStack, text, startTextPos, true);
              return;
            }

            _bestResult = old_bestResult;
            _bestResults = old_bestResults;
          }
        }

        if (pos > curTextPos && HasParsedStaets(ruleParser, parsedStates) || pos == text.Length /*&& isPrefixParsed*/)
        {
          if (isOptional)
          {
          }
          var pos2 = ContinueParse(pos, recoveryStack, parser, text, !isOptional);
          if (isOptional && pos == pos2)
            continue;
          if (stackFrame.AstPtr == -1 && !isPrefixParsed) // Спекулятивный фрэйм стека не спарсивший ничего полезного. Игнорируем его.
            continue;
          AddResult(curTextPos, pos, pos2, state, recoveryStack, text, startTextPos);
        }
        else if (pos == curTextPos && nextState < 0 /*&& isPrefixParsed*/)
        {
          var pos2 = ContinueParse(pos, recoveryStack, parser, text, !isOptional);
          if (isOptional && pos == pos2)
            continue;
          if (stackFrame.AstPtr == -1 && !isPrefixParsed) // Спекулятивный фрэйм стека не спарсивший ничего полезного. Игнорируем его.
            continue;
          if (pos2 > curTextPos || isPrefixParsed)
            AddResult(curTextPos, pos, pos2, state, recoveryStack, text, startTextPos);
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
        }
        else if (parser.MaxFailPos > curTextPos)
          AddResult(curTextPos, pos, parser.MaxFailPos, state, recoveryStack, text, startTextPos);
        else if (pos < 0 && nextState < 0)
        {
          // последнее состояние. Надо попытаться допарсить
          var pos2 = ContinueParse(curTextPos, recoveryStack, parser, text, !isOptional);
          if (isOptional && !isPrefixParsed) // необязательное правило не спрасившее ни одного не пробельного символа нужно игнорировать
            continue;
          if (stackFrame.AstPtr == -1 && !isPrefixParsed) // Спекулятивный фрэйм стека не спарсивший ничего полезного. Игнорируем его.
            continue;
          if (pos2 > curTextPos || isPrefixParsed)
            AddResult(curTextPos, pos, pos2, -1, recoveryStack, text, startTextPos);
        }
        else if (stackFrame.FailState == state && subruleLevel <= 0)
          TryParseSubrules(startTextPos, parser, recoveryStack, curTextPos, text, subruleLevel);
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

    void TryParseSubrules(int startTextPos, Parser parser, RecoveryStack recoveryStack, int curTextPos, string text, int subruleLevel)
    {
      if (_nestedLevel > 20) // ловим зацикленную рекурсию для целей отладки
        return;
      _nestedLevel++;
      var stackFrame = recoveryStack.hd;
      var parsers = stackFrame.RuleParser.GetParsersForState(stackFrame.FailState);

      if (!parsers.IsEmpty())
      {
      }

      foreach (var subRuleParser in parsers)
      {
        var old = recoveryStack;
        recoveryStack = recoveryStack.Push(new RecoveryStackFrame(subRuleParser, -1, startTextPos, subRuleParser.StartState, 0, 0, 0, true, FrameInfo.None));
        _recCount++;
        ProcessStackFrame(startTextPos, parser, recoveryStack, curTextPos, text, subruleLevel + 1);
        recoveryStack = old; // remove top element
      }

      _nestedLevel--;
    }

    void AddResult(int startPos, int ruleEndPos, int endPos, int startState, RecoveryStack stack, string text, int failPos, bool allowEmpty = false)
    {
      _bestResultsCount++;

      int stackLength = stack.Length;
      var skipedCount = startPos - failPos;
      var newResult = new RecoveryResult(startPos, ruleEndPos, endPos, startState, stackLength, stack, text, failPos);
      _candidats.Add(newResult);

      if (newResult.SkipedCount > 0)
      {
      }

      if (!allowEmpty && startPos == endPos && endPos != text.Length) return;

      if (_bestResult == null)                   goto good;

      if (stack.hd.AstPtr == -1 && _bestResult.Stack.hd.AstPtr != -1) // спекулятивный фрейм стека
      {
        return;
      }

      if (newResult.RuleEndPos   >= 0 && newResult.RecoveredHead == _bestResult.RecoveredHead && newResult.RecoveredTailCount > 0 && _bestResult.RecoveredTailCount <= 0) goto good; // если у newResult есть продолжение, а у _bestResult нет
      if (_bestResult.RuleEndPos >= 0 && newResult.RecoveredHead == _bestResult.RecoveredHead && newResult.RecoveredTailCount <= 0 && _bestResult.RecoveredTailCount > 0) return;    // если у _bestResult есть продолжение, а у newResult нет

      if (stack.Tail == _bestResult.Stack.Tail)
      {
        if (startState < _bestResult.StartState && newResult.SkipedCount <= _bestResult.SkipedCount) goto good;
        if (startState > _bestResult.StartState && newResult.SkipedCount >  _bestResult.SkipedCount) return;
      }

      if (stack == _bestResult.Stack)
      {
      }

      //if (ruleEndPos - startPos > _bestResult.RecoveredHeadCount) goto good;
      //if (ruleEndPos - startPos < _bestResult.RecoveredHeadCount) return;

      if (newResult.RuleEndPos >= 0 && _bestResult.RuleEndPos <  0) goto good; // 
      if (newResult.RuleEndPos <  0 && _bestResult.RuleEndPos >= 0) return;

      if (startPos < _bestResult.StartPos && endPos == _bestResult.EndPos) goto good;
      if (startPos   > _bestResult.StartPos && endPos == _bestResult.EndPos) return;

      if (skipedCount < _bestResult.SkipedCount) goto good;
      if (skipedCount > _bestResult.SkipedCount) return;

      if (endPos > _bestResult.EndPos) goto good;
      if (endPos < _bestResult.EndPos) return;

      stackLength = stack.Length;
      var bestResultStackLength = this._bestResult.StackLength;

      //if (stack.Head.AstPtr == 0 && _bestResult.Stack.Head.AstPtr != 0) return;

      if (stackLength < bestResultStackLength) goto good;
      if (stackLength > bestResultStackLength)    return;

      if (startState < _bestResult.StartState) goto good;
      if (startState > _bestResult.StartState) return;

      if (stack.Head.FailState > _bestResult.Stack.Head.FailState) goto good;
      if (stack.Head.FailState < _bestResult.Stack.Head.FailState) return;

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
      return;
    }

    int ContinueParse(int startTextPos, RecoveryStack recoveryStack, Parser parser, string text, bool trySkipStates)
    {
      var tail = recoveryStack.Tail as RecoveryStack;

      if (tail == null)
        return startTextPos;

      var stackFrame = tail.Head;
      var ruleParser = stackFrame.RuleParser;
      List<ParsedStateInfo> parsedStates;
      var pos = TryParse(parser, tail, startTextPos, ruleParser, -2, out parsedStates); // -2 - предлагаем парсеру вычислить следующее состояние для допарсивания

      if (pos >= 0)
        return ContinueParse(pos, tail, parser, text, trySkipStates);
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
              return ContinueParse(pos2, tail, parser, text, trySkipStates);
          }
        }

        return Math.Max(parser.MaxFailPos, startTextPos);
      }
    }

    private int TryParse(Parser parser, RecoveryStack recoveryStack, int curTextPos, IRecoveryRuleParser ruleParser, int state, out List<ParsedStateInfo> parsedStates)
    {
      _parseCount++;
      if (state < 0)
      {
        parsedStates = new List<ParsedStateInfo>();
        return ruleParser.TryParse(recoveryStack, state, curTextPos, parsedStates, parser);
      }

      PrseData data;
      var key = Tuple.Create(curTextPos, ruleParser, state);
      if (_visited.TryGetValue(key, out data))
      {
        if (parser.MaxFailPos < data.Item2)
          parser.MaxFailPos = data.Item2;
        parsedStates = data.Item3;
        return data.Item1;
      }
      else
      {
        parsedStates = new List<ParsedStateInfo>();
        int pos = ruleParser.TryParse(recoveryStack, state, curTextPos, parsedStates, parser);
        _visited[key] = Tuple.Create(pos, parser.MaxFailPos, parsedStates);
        return pos;
      }
    }

    private void FixAst(Parser parser)
    {
      Debug.Assert(_bestResult != null);

      var frame = _bestResult.Stack.Head;

      if (frame.AstStartPos < 0)
      {
        Debug.Assert(frame.AstPtr >= 0);
      }

      var error = new ParseErrorData(new NToken(_bestResult.FailPos, _bestResult.StartPos), _bestResults.ToArray());
      var errorIndex = parser.ErrorData.Count;
      parser.ErrorData.Add(error);

      frame.RuleParser.PatchAst(_bestResult.StartPos, _bestResult.StartState, errorIndex, _bestResult.Stack, parser);

      for (var stack = _bestResult.Stack.Tail as RecoveryStack; stack != null; stack = stack.Tail as RecoveryStack)
      {
        if (stack.Head.RuleParser is ExtensibleRuleParser)
          continue;
        var state = stack.Head.FailState;
        Debug.Assert(state >= 0);
        if (stack.Head.AstPtr > 0)
          parser.ast[stack.Head.AstPtr + 2] = ~state;
      }
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
