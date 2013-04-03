using N2.Internal;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace N2.Visualizer
{
  class Recovery
  {
    RecoveryResult _bestResult;
    int            _parseCount;
    int            _bestResultsCount;

    public RecoveryResult Strategy(int startTextPos, Parser parser)
    {
      _bestResult = null;
      _parseCount = 0;
      _bestResultsCount = 0;

      var recoveryStack = parser.RecoveryStack.ToArray();
      var curTextPos    = startTextPos;
      var visited       = new Dictionary<object, int>();
      var text          = parser.Text;

      parser.ParsingMode = ParsingMode.Parsing;
        
      do
      {
        for (var level = 0; level < recoveryStack.Length; level++)
        {
          var info       = recoveryStack[level];
          var ruleParser = info.RuleParser;
          var key        = Tuple.Create(curTextPos, ruleParser);
          int startState;
          if (visited.TryGetValue(key, out startState) && startState <= info.State)
            continue;
          visited[key] = info.State;
            
          var lastState = info.StatesCount - 1;
            
          for (var state = info.State; state <= lastState; state++)
          {
            parser.MaxTextPos = startTextPos;
            _parseCount++;
            var pos = ruleParser.TryParse(info.AstPtr, curTextPos, text, ref parser, state);
            if (pos > curTextPos || pos == text.Length)
            {
              var pos2 = ContinueParse(level + 1, pos, recoveryStack, ref parser);
              AddResult(curTextPos, pos2,              state, level, info, text, startTextPos);
            }
            else if (parser.MaxTextPos > curTextPos)
              AddResult(curTextPos, parser.MaxTextPos, state, level, info, text, startTextPos);
            else
            {
            }
          }
          level++;
        }

        curTextPos++;
      }
      while (/*res.Count == 0 && */ /*(res.Count == 0 || curTextPos - startTextPos < 10) &&*/ curTextPos <= text.Length);

      return _bestResult;
    }

    void AddResult(int startPos, int endPos, int startState, int stackLevel, RecoveryInfo info, string text, int fail)
    {
      _bestResultsCount++;

      if (_bestResult == null || endPos > _bestResult.EndPos || startPos < _bestResult.StartPos || stackLevel < _bestResult.StackLevel || startState < _bestResult.StartState)
        _bestResult = new RecoveryResult(startPos, endPos, startState, stackLevel, info, text, fail);
    }

    int ContinueParse(int level, int startTextPos, RecoveryInfo[] recoveryStack, ref Parser parser)
    {
      if (level >= recoveryStack.Length)
        return startTextPos;

      var recoveryInfo = recoveryStack[level];
      var pos3 = 
        recoveryInfo.State + 1 >= recoveryInfo.StatesCount
          ? startTextPos
          : recoveryInfo.RuleParser.TryParse(recoveryInfo.AstPtr, startTextPos, parser.Text, ref parser, recoveryInfo.State + 1);

      if (pos3 >= 0)
        return ContinueParse(level + 1, pos3, recoveryStack, ref parser);
      else
        return Math.Max(parser.MaxTextPos, startTextPos);
    }
  }
}
