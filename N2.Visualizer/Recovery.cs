using N2.Internal;

using Nemerle.Collections;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using RecoveryStack = Nemerle.Core.list<N2.Internal.RecoveryStackFrame>.Cons;
using System.Diagnostics;

namespace N2.Visualizer
{
  class ErrorException : Exception
  {
    public ErrorException(RecoveryResult[] recovery)
    {
      Recovery = recovery;
    }

    public RecoveryResult[] Recovery { get; private set; }
  }

  static class Utils
  {
    public static RecoveryStack Push(this RecoveryStack stack, RecoveryStackFrame elem)
    {
      return new RecoveryStack(elem, stack);
    }

    public static int Inc<T>(this Dictionary<T, int> heshtable, T key)
    {
      int value;
      heshtable.TryGetValue(key, out value);
      heshtable[key] = value;
      return value;
    }
  }

  class Recovery
  {
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

    public RecoveryResult Strategy(int startTextPos, Parser parser)
    {
      Reset();
      var timer = System.Diagnostics.Stopwatch.StartNew();
      _recoveryStack     = parser.RecoveryStack.NToList() as RecoveryStack;
      var curTextPos    = startTextPos;
      var text          = parser.Text;

      parser.ParsingMode = ParsingMode.Parsing;
        
      do
      {
        for (var stack = _recoveryStack as RecoveryStack; stack != null; stack = stack.Tail as RecoveryStack)
          ProcessStackFrame(startTextPos, parser, stack, curTextPos, text, 0);
        curTextPos++;
      }
      while (curTextPos - startTextPos < 800 && /*_bestResult == null && _bestResult == null && (res.Count == 0 || curTextPos - startTextPos < 10) &&*/ curTextPos <= text.Length);

      timer.Stop();

      //ProcessStackFrame(startTextPos, parser, _bestResult.Stack, _bestResult.StartPos, text, 0);
      FixAst(curTextPos, parser);

      //var ex = new ErrorException(_bestResults.ToArray());
      Reset();
      //throw ex;
      return _bestResult;
    }

    private void FixAst(int startPos, Parser parser)
    {
      var frame = _bestResult.Stack.Head;

      var tail = _bestResult.Stack.Tail as RecoveryStack;

      if (tail != null)
      {
        var subFrame = tail.Head;
      }

      Debug.Assert(frame.AstPtr >= 0);

      //var fieldSize = parser.ast[frame.AstPtr + 3 + frame.State];
      var error = new Error(new NToken(startPos, startPos + _bestResult.SkipedCount), _bestResults);
      var errorIndex = parser.Errors.Count;
      parser.Errors.Add(error);

      // АСТ - У нас нет пересчета из состояний в индексы полей.
      // Нужна информация о том что фрейм принадлежит перавилу, а не подправилу. Т.е. каким подтипом RuleStruct он является (Ast или еще каким-то).
      // 
      // 
      // 
      // 
      // 

      frame.RuleParser.PatchAst(_bestResult.FailPos, _bestResult.StartPos, _bestResult.StartState, errorIndex, _bestResult.Stack, parser);

      for (var stack = _bestResult.Stack.Tail as RecoveryStack; stack != null; stack = stack.Tail as RecoveryStack)
      {
        if (stack.Head.IsRootAst)
        {
          var state = parser.ast[stack.Head.AstPtr + 2];
          Debug.Assert(state >= 0);
          parser.ast[stack.Head.AstPtr + 2] = ~state;
        }
      }
    }

    private void ProcessStackFrame(
      int startTextPos, 
      Parser parser, 
      RecoveryStack recoveryStack, 
      int curTextPos, 
      string text,
      int subruleLevel)
    {
      var stackFrame = recoveryStack.Head;
      var ruleParser = stackFrame.RuleParser;

      for (var state = stackFrame.FailState; state >= 0; state = stackFrame.RuleParser.GetNextState(state))
      {
        parser.MaxTextPos = startTextPos;
        _parseCount++;
        var startAllocated = parser.allocated;
        int pos;

        {
          var key = Tuple.Create(curTextPos, ruleParser, state);
          if (!_visited.TryGetValue(key, out pos))
          {
            _visited[key] = pos = ruleParser.TryParse(recoveryStack, state, curTextPos, false, parser);
          }
        }

        if (pos > curTextPos || pos == text.Length)
        {
          var pos2 = ContinueParse(pos, recoveryStack, parser, text);
          AddResult(curTextPos,              pos2, state, recoveryStack, text, startTextPos);
        }
        else if (pos == curTextPos && stackFrame.RuleParser.GetNextState(state) == -1)
        {
          var pos2 = ContinueParse(pos, recoveryStack, parser, text);
          AddResult(curTextPos, pos2, state, recoveryStack, text, startTextPos);
        }
        else if (parser.MaxTextPos > curTextPos)
          AddResult(curTextPos, parser.MaxTextPos, state, recoveryStack, text, startTextPos);
        else
        {
          if (object.ReferenceEquals(_recoveryStack, recoveryStack))
          if (subruleLevel <= 0)
          {
            if (_nestedLevel > 20) // ловим зацикленную рекурсию для целей отладки
              continue;
            _nestedLevel++;

            var parsers = ruleParser.GetParsersForState(state);

            if (!parsers.IsEmpty())
            {
            }

            foreach (var subRuleParser in parsers)
            {
              var old = recoveryStack;
              recoveryStack = recoveryStack.Push(new RecoveryStackFrame(subRuleParser, -1, true, -1/*stackFrame.AstPtr*/, subRuleParser.StartState, 0, FrameInfo.None));
              _recCount++;
              ProcessStackFrame(startTextPos, parser, recoveryStack, curTextPos, text, subruleLevel + 1);
              recoveryStack = old; // remove top element
            }

            _nestedLevel--;
          }
        }
      }
    }

    void AddResult(int startPos, int endPos, int startState, RecoveryStack stack, string text, int failPos)
    {
      _bestResultsCount++;

      int stackLength = stack.Length;
      var skipedCount = startPos - failPos;

      var newResult = new RecoveryResult(startPos, endPos, startState, stackLength, stack, text, failPos);

      if (newResult.SkipedCount > 0)
      {
      }

      if (_bestResult == null)                   goto good;

      if (endPos     > _bestResult.EndPos)       goto good;
      if (endPos     < _bestResult.EndPos)       return;

      if (stack.Length == _bestResult.Stack.Length) // это халтура :(
      {
        if (stack.Head.AstPtr == 0 && _bestResult.Stack.Head.AstPtr != 0)
          return;
        if (stack.Head.AstPtr != 0 && _bestResult.Stack.Head.AstPtr == 0)
          goto good;
      }

      if (startPos   < _bestResult.StartPos)     goto good;
      if (startPos   > _bestResult.StartPos)     return;

      if (skipedCount < _bestResult.SkipedCount) goto good;
      if (skipedCount > _bestResult.SkipedCount) return;

      stackLength = stack.Length;
      var bestResultStackLength = this._bestResult.StackLength;

      //if (stack.Head.AstPtr == 0 && _bestResult.Stack.Head.AstPtr != 0) return;

      if (stackLength > bestResultStackLength) goto good;
      if (stackLength < bestResultStackLength)    return;

      if (startState < _bestResult.StartState) goto good;
      if (startState > _bestResult.StartState) return;

      if (stack.Head.FailState > _bestResult.Stack.Head.FailState) goto good;
      if (stack.Head.FailState < _bestResult.Stack.Head.FailState) return;

      goto good2;
    good:
      _bestResult = new RecoveryResult(startPos, endPos, startState, stackLength, stack, text, failPos);
      _bestResults.Clear();
      _bestResults.Add(_bestResult);
      return;
    good2:
      _bestResults.Add(new RecoveryResult(startPos, endPos, startState, stackLength, stack, text, failPos));
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
        return Math.Max(parser.MaxTextPos, startTextPos);
    }
  }
}
