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
    Dictionary<object, int> _visited = new Dictionary<object, int>();
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
      _visited = new Dictionary<object, int>();
      _parsedRules = new Dictionary<string, int>();
    }

    public void Strategy(int startTextPos, Parser parser)
    {
      Reset();
      var timer = System.Diagnostics.Stopwatch.StartNew();
      _recoveryStack     = parser.RecoveryStack.NToList() as RecoveryStack;
      var curTextPos    = startTextPos;
      var text          = parser.Text;

      parser.ParsingMode = ParsingMode.Parsing;
        
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
        parser.ParsingMode = ParsingMode.Recovery;

      Reset();
    }

    private void ProcessStackFrame(int startTextPos, Parser parser, RecoveryStack recoveryStack, int curTextPos, string text, int subruleLevel)
    {
      var stackFrame = recoveryStack.Head;
      var ruleParser = stackFrame.RuleParser;
      var isPrefixParsed = !ruleParser.IsStartState(stackFrame.FailState);
      var isOptional = ruleParser.IsLoopSeparatorStart(stackFrame.FailState);

      int nextSate;
      for (var state = subruleLevel > 0 ? ruleParser.GetNextState(stackFrame.FailState) : stackFrame.FailState; state >= 0; state = nextSate)
      {
        parser.MaxFailPos = startTextPos;
        nextSate = ruleParser.GetNextState(state);
        //var needSkip = nextSate < 0 && ruleParser.IsVoidState(state);
        //if (nextSate < 0 && ruleParser.IsVoidState(state))
        //  continue;

        int pos;

        {
          _parseCount++;
          var key = Tuple.Create(curTextPos, ruleParser, state);
          if (!_visited.TryGetValue(key, out pos))
          {
            _visited[key] = pos = ruleParser.TryParse(recoveryStack, state, curTextPos, false, parser);
          }
        }

        var isParsed = pos > curTextPos;

        if (!isPrefixParsed && isParsed && !ruleParser.IsVoidState(state))
          isPrefixParsed = isParsed;

        //if (!isPrefixParsed)
        //  continue;

        if (pos > curTextPos || pos == text.Length /*&& isPrefixParsed*/)
        {
          if (isOptional)
          {
          }
          var pos2 = ContinueParse(pos, recoveryStack, parser, text);
          if (isOptional && pos == pos2)
            continue;
          AddResult(curTextPos,   pos, pos2, state, recoveryStack, text, startTextPos);
        }
        else if (pos == curTextPos && nextSate < 0 /*&& isPrefixParsed*/)
        {
          var pos2 = ContinueParse(pos, recoveryStack, parser, text);
          if (isOptional && pos == pos2)
            continue;
          if (pos2 > curTextPos || isPrefixParsed)
            AddResult(curTextPos, pos, pos2, state, recoveryStack, text, startTextPos);
        }
        else if (parser.MaxFailPos > curTextPos)
          AddResult(curTextPos,   pos, parser.MaxFailPos, state, recoveryStack, text, startTextPos);
        else if (pos < 0 && nextSate < 0)
        {
          // последнее состояние. Надо попытаться допарсить
          var pos2 = ContinueParse(curTextPos, recoveryStack, parser, text);
          if (isOptional && !isPrefixParsed) // необязательное правило не спрасившее ни одного не пробельного символа нужно игнорировать
            continue;
          if (stackFrame.AstPtr == -1 && !isPrefixParsed) // Спекулятивный фрэйм стека не спарсивший ничего полезного. Игнорируем его.
            continue;
          if (pos2 > curTextPos || isPrefixParsed)
            AddResult(curTextPos, pos, pos2, int.MaxValue, recoveryStack, text, startTextPos);
        }
        else if (stackFrame.FailState == state && subruleLevel <= 0)
          TryParseSubrules(startTextPos, parser, recoveryStack, curTextPos, text, subruleLevel);
      }
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

    void AddResult(int startPos, int ruleEndPos, int endPos, int startState, RecoveryStack stack, string text, int failPos)
    {
      _bestResultsCount++;

      int stackLength = stack.Length;
      var skipedCount = startPos - failPos;

      var newResult = new RecoveryResult(startPos, ruleEndPos < 0 ? startPos : ruleEndPos, endPos, startState, stackLength, stack, text, failPos);
      _candidats.Add(newResult);

      if (newResult.SkipedCount > 0)
      {
      }

      if (startPos == endPos && endPos != text.Length) return;

      if (_bestResult == null)                   goto good;

      if (ruleEndPos >= 0 && newResult.RecoveredTailCount > _bestResult.RecoveredTailCount) goto good;
      if (ruleEndPos >= 0 && newResult.RecoveredTailCount < _bestResult.RecoveredTailCount) return;

      if (stack.Tail == _bestResult.Stack.Tail)
      {
        if (startState < _bestResult.StartState) goto good;
        if (startState > _bestResult.StartState) return;
      }

      if (stack == _bestResult.Stack)
      {
      }

      //if (ruleEndPos - startPos > _bestResult.RecoveredHeadCount) goto good;
      //if (ruleEndPos - startPos < _bestResult.RecoveredHeadCount) return;

      if (endPos > _bestResult.EndPos) goto good;
      if (endPos < _bestResult.EndPos) return;


      if (startPos   < _bestResult.StartPos && endPos == _bestResult.EndPos) goto good;
      if (startPos   > _bestResult.StartPos && endPos == _bestResult.EndPos) return;

      if (skipedCount < _bestResult.SkipedCount) goto good;
      if (skipedCount > _bestResult.SkipedCount) return;

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

    int ContinueParse(int startTextPos, RecoveryStack recoveryStack, Parser parser, string text)
    {
      var tail = recoveryStack.Tail as RecoveryStack;

      if (tail == null)
        return startTextPos;

      var stackFrame = tail.Head;
      var pos = stackFrame.RuleParser.TryParse(tail, -2, startTextPos, false, parser);

      if (pos >= 0)
        return ContinueParse(pos, tail, parser, text);
      else
        return Math.Max(parser.MaxFailPos, startTextPos);
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
        while (!stack.Head.IsRootAst)
          stack = stack.Tail as RecoveryStack;
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
