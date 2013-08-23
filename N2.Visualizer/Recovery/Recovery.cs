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
    public ReportData ReportResult;

    private Dictionary<RecoveryStackFrame, ParseAlternative[]> _visited = new Dictionary<RecoveryStackFrame, ParseAlternative[]>();

    public Recovery(ReportData reportResult)
    {
      ReportResult = reportResult;
    }

    public virtual int Strategy(Parser parser)
    {
      var failPos = parser.MaxFailPos;
      var skipCount = 0;
      var text = parser.Text;

      Debug.Assert(parser.RecoveryStacks.Count > 0);

      var bestFrames = new List<RecoveryStackFrame>();

      for (;failPos + skipCount < text.Length && bestFrames.Count == 0; ++skipCount)
      {
        var frames = parser.RecoveryStacks.PrepareRecoveryStacks();
        var newFrames = new HashSet<RecoveryStackFrame>(frames);
        foreach (var frame in frames)
          if (frame.Depth == 0)
            FindSpeculativeFrames(newFrames, parser, frame, failPos, skipCount);

        var allFrames = newFrames.PrepareRecoveryStacks();

        FindBestFrames(parser, bestFrames, allFrames, skipCount);

        _visited.Clear();
      }

      return -1;
    }

    // ReSharper disable once ParameterTypeCanBeEnumerable.Local
    private void FindBestFrames(Parser parser, List<RecoveryStackFrame> bestFrames, List<RecoveryStackFrame> allFrames, int skipCount)
    {
      bestFrames.Clear();

      foreach (var frame in allFrames)
      {
        if (frame.Parents.Count == 0) // is root
          ProcessFrame(parser, bestFrames, frame, skipCount);
      }

      foreach (var frame in allFrames)
      {
        if (frame.Parents.Count == 0) // is root
          ChoosingTheBestFrame(bestFrames, frame, -1);
      }
    }

    private void ChoosingTheBestFrame(List<RecoveryStackFrame> bestFrames, RecoveryStackFrame frame, int parentStart)
    {
      frame.Best = true;

      if (frame.Children.Count == 0)
      {
        bestFrames.Add(frame);
        return;
      }

      var res0 = parentStart >= 0 ? frame.ParseAlternatives.Where(p => p.End == parentStart).ToArray() : frame.ParseAlternatives;
      var res1 = res0.FilterMax(f => f.End);
      var res2 = res1.FilterMin(f => f.State < 0 ? int.MaxValue : f.State); // побеждает меньшее состояние

      if (frame.ToString().Contains("Attribute =") && frame.ToString().Contains("FailState=3"))
      {
      }

      foreach (var alternative in res2)
      {
        var start = alternative.Start;
        var children = FilterBetterEmptyIfAllEmpty(frame.Children, start);

        if (children.Count == 0)
          Debug.Assert(false);

        foreach (var child in children)
        {
          if (child.ParseAlternatives.Any(p => p.End == start))
            ChoosingTheBestFrame(bestFrames, child, start);
        }
      }
    }

    private static List<RecoveryStackFrame> FilterBetterEmptyIfAllEmpty(List<RecoveryStackFrame> children, int start)
    {
      var xs = children
        .SelectMany(c => c.ParseAlternatives)
        .Where(p => p.End == start).ToList();
      var needFilterEmpty = xs.All(p => p.Start == p.End)
                            && xs.Any(p => p.State < 0);

      if (needFilterEmpty)
      {
      }

      return needFilterEmpty
        ? children.Where(c => c.ParseAlternatives.Any(p => p.Start == p.End && p.State < 0)).ToList()//.FilterMin()
        : children;
    }

    private static IEnumerable<T> FilterIfExists<T>(List<T> res2, Func<T, bool> predicate)
    {
      return res2.Any(predicate) ? res2.Where(predicate) : res2;
    }

    private void ProcessFrame(Parser parser, List<RecoveryStackFrame> bestFrames, RecoveryStackFrame frame, int skipCount)
    {
      if (frame.ParseAlternatives != null)
        return;

      if (frame.Children.Count == 0)
      {
        // разбираемся с головами
        if (frame.ToString().Contains("NewArray_2"))
        {
        }
        else if (frame.ToString().Contains("AttributeArguments") && frame.ToString().Contains("FailState=3"))
        {
        }

        var end = ParseTopFrame(parser, frame, skipCount);
        var xx = new [] { end };
        frame.ParseAlternatives = xx;
        _visited.Add(frame, xx);
      }
      else
      {
        // разбираемся с промежуточными ветками

        //// В надежде не то, что пользователь просто забыл ввести некоторые токены, пробуем пропарсить фрэйм с позиции облома.
        //ParseNonTopFrame(parser, frame, frame.TextPos, skipCount);
        var childEnds = new HashSet<ParseAlternative>();
        ProcessChildren(childEnds, parser, bestFrames, frame, skipCount);

        var curentEnds = new HashSet<ParseAlternative>();
        foreach (var start in childEnds)
          curentEnds.Add(ParseNonTopFrame(parser, frame, start.End, skipCount));

        var xx = curentEnds.ToArray();
        frame.ParseAlternatives = xx;
        _visited.Add(frame, xx);

        if (frame.ToString().Contains("AttributeSection = "))
        {
        }
        else if (frame.ToString().Contains("Class = "))
        {
        }


        Debug.Assert(true);
      }
    }

    private void ProcessChildren(HashSet<ParseAlternative> ends, Parser parser, List<RecoveryStackFrame> bestFrames, RecoveryStackFrame frame, int skipCount)
    {
      foreach (var child in frame.Children)
      {
        ProcessFrame(parser, bestFrames, child, skipCount);
        ends.UnionWith(child.ParseAlternatives);
      }
    }

    private static ParseAlternative ParseAlternative(int startPos, int endPos, int state)
    {
      return new ParseAlternative(startPos, endPos, state);
    }

    /// <returns>Посиция окончания парсинга</returns>
    private ParseAlternative ParseTopFrame(Parser parser, RecoveryStackFrame frame, int skipCount)
    {
      var curTextPos = frame.TextPos + skipCount;
      for (var state = frame.FailState; state >= 0; state = frame.GetNextState(state))
      {
        parser.MaxFailPos = curTextPos;
        var parsedStates = new List<ParsedStateInfo>();
        var pos = frame.TryParse(state, curTextPos, false, parsedStates, parser);
        if (NonVoidParsed(frame, curTextPos, pos, parsedStates, parser))
        {
          frame.StartState = state;
          frame.StartParsePos = curTextPos;
          frame.EndParsePos = pos;
          frame.MaxFailPos = parser.MaxFailPos;
          return ParseAlternative(curTextPos, pos >= 0 ? pos : parser.MaxFailPos, state); // TODO: Подумать как быть с parser.MaxFailPos
        }
      }

      // Если ни одного состояния не пропарсились, то считаем, что пропарсилось состояние "за концом правила".
      // Это соотвтствует полному пропуску остатка подправил данного правила.
      frame.StartState = -1;
      frame.StartParsePos = curTextPos;
      frame.EndParsePos = curTextPos;
      frame.MaxFailPos = curTextPos;

      return ParseAlternative(curTextPos, curTextPos, -1);
    }

    private static ParseAlternative ParseNonTopFrame(Parser parser, RecoveryStackFrame frame, int failPos, int skipCount)
    {
      if (failPos < 0)
      {
      }
      var startParsePos = failPos + skipCount;
      var maxFailPos = failPos; // TODO: Возможно имеет смысл использовать startParsePos для инициализации maxFailPos.

      var state = frame.GetNextState(frame.FailState);

      frame.StartState = -2; // не начинало парситься, т.е. не нашло ни одного состояния с которого возможно допарсивание
      frame.StartParsePos = startParsePos;
      frame.EndParsePos = -1;
      frame.MaxFailPos = maxFailPos;

      for (; state >= 0; state = frame.GetNextState(state))
      {
        parser.MaxFailPos = failPos;
        var parsedStates = new List<ParsedStateInfo>();
        var pos = frame.TryParse(state, startParsePos, true, parsedStates, parser);
        // TODO: Возможно здесь надо проверять с поммощью NonVoidParsed(), как в ProcessTopFrame
        if (pos > 0)
        {
          frame.StartState = state;
          frame.StartParsePos = startParsePos;
          frame.EndParsePos = pos;
          frame.MaxFailPos = parser.MaxFailPos;
          return ParseAlternative(startParsePos, pos, state);
        }
      }

      return ParseAlternative(startParsePos, startParsePos, -1);
    }

    private bool NonVoidParsed(RecoveryStackFrame frame, int curTextPos, int pos, List<ParsedStateInfo> parsedStates, Parser parser)
    {
      var lastPos = Math.Max(pos, parser.MaxFailPos);
      return lastPos > curTextPos && lastPos - curTextPos > ParsedSpacesLen(frame, parsedStates)
          || parsedStates.Count > 0 && HasParsedStaets(frame, parsedStates);
    }

    private void FindSpeculativeFrames(HashSet<RecoveryStackFrame> newFrames, Parser parser, RecoveryStackFrame frame, int failPos, int skipCount)
    {
      if (frame.IsTokenRule)
        return;

      //if (InitFrame(parser, frame, false, failPos, skipCount))
      var res = ParseTopFrame(parser, frame, skipCount);
      if (res.State >= 0)
      {
        newFrames.Add(frame);
        return;
      }

      if (!frame.IsPrefixParsed) // пытаемся восстановить пропущенный разделитель списка
      {
        var separatorFrame = frame.GetLoopBodyFrameForSeparatorState(failPos + skipCount, parser);

        if (separatorFrame != null)
        {
          // Нас просят попробовать востановить отстуствующий разделитель цикла. Чтобы знать, нужно ли это дела, или мы
          // имеем дело с банальным концом цикла мы должны
          Debug.Assert(separatorFrame.Parents.Count == 1);
          var newFramesCount = newFrames.Count;
          FindSpeculativeFrames(newFrames, parser, separatorFrame, failPos, failPos + skipCount);
          if (newFrames.Count > newFramesCount)
            return;
        }
      }

      for (var state = frame.FailState; state >= 0; state = frame.GetNextState(state))
        FindSpeculativeSubframes(newFrames, parser, frame, failPos + skipCount, state);
    }

    protected virtual void FindSpeculativeSubframes(HashSet<RecoveryStackFrame> newFrames, Parser parser, RecoveryStackFrame frame, int curTextPos, int state)
    {
      foreach (var subFrame in frame.GetSpeculativeFramesForState(curTextPos, parser, state))
      {
        if (subFrame.IsTokenRule)
          continue;

        if (!newFrames.Add(subFrame))
          continue;

        FindSpeculativeSubframes(newFrames, parser, subFrame, curTextPos, subFrame.FailState);
      }
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

  internal static class RecoveryUtils
  {
    public static List<T> FilterMax<T>(this ICollection<T> candidates, Func<T, int> selector)
    {
      var max1 = candidates.Max(selector);
      var res2 = candidates.Where(c => selector(c) == max1);
      return res2.ToList();
    }

    public static List<T> FilterMin<T>(this ICollection<T> candidates, Func<T, int> selector)
    {
      var min = candidates.Min(selector);
      var res2 = candidates.Where(c => selector(c) == min);
      return res2.ToList();
    }

    public static List<RecoveryStackFrame> PrepareRecoveryStacks(this ICollection<RecoveryStackFrame> heads)
    {
      var allRecoveryStackFrames = new List<RecoveryStackFrame>();

      foreach (var stack in heads)
        stack.ClearAndCollectFrames(allRecoveryStackFrames);
      foreach (var stack in heads)
        stack.Depth = 0;
      foreach (var stack in heads)
        stack.UpdateFrameDepth();

      allRecoveryStackFrames.SortByDepth();

      for (int i = 0; i < allRecoveryStackFrames.Count; ++i)
      {
        var frame = allRecoveryStackFrames[i];
        frame.Index = i;
        frame.Best = false;
        frame.Children.Clear();
      }

      foreach (var frame in allRecoveryStackFrames)
        foreach (var parent in frame.Parents)
        {
          if (parent.Children.Contains(frame))
            Debug.Assert(false);

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
          UpdateFrameDepth(parent);
        }
    }
  }
}
