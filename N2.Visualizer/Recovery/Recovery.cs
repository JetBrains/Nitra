#region Пролог
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
  
#endregion

  public class Recovery
  {
    public ReportData ReportResult;
    private readonly Dictionary<RecoveryStackFrame, ParseAlternative[]> _visited = new Dictionary<RecoveryStackFrame, ParseAlternative[]>();

    #region Инициализация и старт

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

      for (; failPos + skipCount < text.Length && bestFrames.Count == 0; ++skipCount)
      {
        var frames = parser.RecoveryStacks.PrepareRecoveryStacks();

        foreach (var frame in frames) // reset ParseAlternatives
          frame.ParseAlternatives = null;

        var newFrames = new HashSet<RecoveryStackFrame>(frames);
        foreach (var frame in frames)
          if (frame.Depth == 0)
            FindSpeculativeFrames(newFrames, parser, frame, failPos, skipCount);

        var allFrames = newFrames.PrepareRecoveryStacks();

        bestFrames.Clear();

        ParseFrames(parser, skipCount, allFrames);

        UpdateParseFramesAlternatives(allFrames);
        SelectBestFrames(bestFrames, allFrames, skipCount);

        if (IsAllFramesParseEmptyString(allFrames))
          bestFrames.Clear();
        else
        {
        }

        _visited.Clear();
      }

      return -1;
    }

    private bool IsAllFramesParseEmptyString(IEnumerable<RecoveryStackFrame> allFrames)
    {
      return allFrames.All(f => f.ParseAlternatives.All(a => a.ParentsEat == 0));
    }

    #endregion

    #region Выбор лучшего фрейма

    private static void UpdateParseFramesAlternatives(List<RecoveryStackFrame> allFrames)
    {
      allFrames[allFrames.Count - 1].Best = true; // единственный корень гарантированно последний
      for (int i = allFrames.Count - 1; i >= 0; --i)
      {
        var frame = allFrames[i];

        if (!frame.Best)
          continue;

        if (frame.Children.Count == 0)
        {
          continue;
        }

        switch (frame.Id)
        {
          case 7: break;
        }

        var alternatives0 = FilterParseAlternativesWichStartsFromParentsEnds(frame);
        var alternatives1 = alternatives0.FilterMax(f => f.End >= 0 ? f.End : f.Fail);
        var alternatives2 = alternatives1.FilterMin(f => f.State < 0 ? int.MaxValue : f.State); // побеждает меньшее состояние
        frame.ParseAlternatives = alternatives2.ToArray();

        if (frame.ParseAlternatives.Length != 1)
          Debug.Assert(false);

        foreach (var alternative in alternatives2)
        {
          var start = alternative.Start;
          var children = frame.Children;

          if (children.Count == 0)
            Debug.Assert(false);

          foreach (var child in children)
          {
            if (child.ParseAlternatives.Any(p => p.End < 0 ? p.Fail == start : p.End == start))
              child.Best = true;
          }
        }
      }
    }

    private static void SelectBestFrames(List<RecoveryStackFrame> bestFrames, List<RecoveryStackFrame> allFrames, int skipCount)
    {
      for (int i = allFrames.Count - 1; i >= 0; --i)
      {
        var frame = allFrames[i];
        
        if (!frame.Best)
          continue;

        if (frame.Children.Count == 0)
        {
          bestFrames.Add(frame);
          continue;
        }

        switch (frame.Index)
        {
          case 14: break; // AttributeArguments SP=2 TP=3 FS=3 T=Rule D=0 I=14 PA=[(3, 3; E0, S-1)] OK  ◄─┐
          case 67: break; // AttributeArguments SP=2 TP=3 FS=4 T=Rule D=4 I=67 PA=[(3, 3; E0, S-1)] !!! ◄─┤
          case 75: break; // AttributeArguments SP=2 TP=3 FS=2 T=Rule D=9 I=75 PA=[(3, 3; E0, S-1)] !!! ◄─┤
          case 76: break; // Attribute SP=2 TP=2 FS=3 T=Rule D=10 I=76 PA=[(3, 3; E0, S-1)]               │
          case 79: break; // AttributeList SP=1 TP=1 FS=0 T=Rule D=13 I=79 PA=[(3, 3; E0, S-1)]           │
          case 80: break; // AttributeSection SP=0 TP=1 FS=4 T=Rule D=14 I=80 PA=[(3, 5; E2, S5)]         │
          case 28: break;
        }

        var children1 = FilterTopFramesWhichRecoveredOnFailStateIfExists(frame.Children);
        var children2 = children1.FilterBetterEmptyIfAllEmpty();
        var bettreChildren = RemoveSpeculativeFrames(children2);
        var poorerChildren = SubstractSet(frame, bettreChildren);

        if (poorerChildren.Count > 0)
          ResetBestProperty(poorerChildren);
      }
    }

    private static List<RecoveryStackFrame> RemoveSpeculativeFrames(List<RecoveryStackFrame> frames)
    {
      var frames2 = frames.FilterMax(f => f.ParseAlternatives[0].ParentsEat).ToList();
      var frames3 = frames2.FilterMin(f => f.FailState);
      return frames3.ToList();
    }

    private static List<RecoveryStackFrame> FilterTopFramesWhichRecoveredOnFailStateIfExists(List<RecoveryStackFrame> bestFrames)
    {
      if (bestFrames.Any(f => f.ParseAlternatives.Any(a => a.State == f.FailState)))
      {
        // TODO: Устранить этот кабздец! Удалять фреймы прямо из массива.
        return bestFrames.Where(f => f.ParseAlternatives.Any(a => a.State == f.FailState)).ToList();
      }

      return bestFrames;
    }

    private static bool HasTopFramesWhichRecoveredOnFailState(RecoveryStackFrame frame)
    {
      var failState = frame.FailState;
      foreach (ParseAlternative a in frame.ParseAlternatives)
        if (a.State == failState)
          return true;
      return false;
    }

    private static List<RecoveryStackFrame> SubstractSet(RecoveryStackFrame frame, List<RecoveryStackFrame> bettreChildren)
    {
      return frame.Children.Where(c => !bettreChildren.Contains(c)).ToList();
    }

    private static void ResetBestProperty(List<RecoveryStackFrame> poorerChildren)
    {
      foreach (var child in poorerChildren)
        if (child.Best)
        {
          child.Best = false;
          ResetBestProperty(child.Children);
        }
    }

    private static ParseAlternative[] FilterParseAlternativesWichStartsFromParentsEnds(RecoveryStackFrame frame)
    {
      ParseAlternative[] res0;
      if (frame.Parents.Count == 0)
        res0 = frame.ParseAlternatives;
      else
      {
        var parentStarts = new HashSet<int>();
        foreach (var parent in frame.Parents)
          if (parent.Best)
            foreach (var alternative in parent.ParseAlternatives)
              parentStarts.Add(alternative.Start);
        res0 = frame.ParseAlternatives.Where(alternative => parentStarts.Contains(alternative.End)).ToArray();
      }
      return res0;
    }

    #endregion

    #region Parsing

    private void ParseFrames(Parser parser, int skipCount, List<RecoveryStackFrame> allFrames)
    {
      for (int i = 0; i < allFrames.Count; ++i)
      {
        var frame = allFrames[i];

        if (frame.Id == 7)
          Debug.Assert(true);

        if (frame.Depth == 0)
        {
          if (frame.ParseAlternatives != null)
            continue; // пропаршено во время попытки найти спекулятивные подфреймы
          // разбираемся с головами
          ParseTopFrame(parser, frame, skipCount);
        }
        else
        {
          // разбираемся с промежуточными ветками
          // В надежде не то, что пользователь просто забыл ввести некоторые токены, пробуем пропарсить фрэйм с позиции облома.
          var curentEnds = new HashSet<ParseAlternative>();
          var childEnds = new HashSet<int>();
          foreach (var child in frame.Children)
            foreach (var alternative in child.ParseAlternatives)
              if (alternative.End >= 0)
                childEnds.Add(alternative.End);
              else
                curentEnds.Add(new ParseAlternative(alternative.Fail, alternative.Fail, alternative.ParentsEat, 0, frame.FailState));

          foreach (var end in childEnds)
            curentEnds.Add(ParseNonTopFrame(parser, frame, end));

          var xx = curentEnds.ToArray();
          frame.ParseAlternatives = xx;
        }
      }
    }

    /// <returns>Посиция окончания парсинга</returns>
    private ParseAlternative ParseTopFrame(Parser parser, RecoveryStackFrame frame, int skipCount)
    {
      ParseAlternative parseAlternative;

      if (frame.Id == 28)
        Debug.Assert(true);

      var curTextPos = frame.TextPos + skipCount;
      for (var state = frame.FailState; state >= 0; state = frame.GetNextState(state))
      {
        parser.MaxFailPos = curTextPos;
        var parsedStates = new List<ParsedStateInfo>();
        var pos = frame.TryParse(state, curTextPos, false, parsedStates, parser);
        if (frame.NonVoidParsed(curTextPos, pos, parsedStates, parser))
        {
          parseAlternative = new ParseAlternative(curTextPos, pos, (pos < 0 ? parser.MaxFailPos : pos) - curTextPos, pos < 0 ? parser.MaxFailPos : 0, state);
          frame.ParseAlternatives = new[] { parseAlternative };
          return parseAlternative;
        }
      }

      // Если ни одного состояния не пропарсились, то считаем, что пропарсилось состояние "за концом правила".
      // Это соотвтствует полному пропуску остатка подправил данного правила.
      parseAlternative = new ParseAlternative(curTextPos, curTextPos, 0, 0, -1);
      frame.ParseAlternatives = new[] { parseAlternative };
      return parseAlternative;
    }

    private static ParseAlternative ParseNonTopFrame(Parser parser, RecoveryStackFrame frame, int curTextPos)
    {
      var parentsEat = frame.Children.Max(c => c.ParseAlternatives.Length == 0 
                                              ? 0
                                              : c.ParseAlternatives.Max(a => a.End == curTextPos ? a.ParentsEat : 0));
      var maxfailPos = curTextPos;
      var state      = frame.GetNextState(frame.FailState);

      for (; state >= 0; state = frame.GetNextState(state))
      {
        parser.MaxFailPos = maxfailPos;
        var parsedStates = new List<ParsedStateInfo>();
        var pos = frame.TryParse(state, curTextPos, true, parsedStates, parser);
        if (frame.NonVoidParsed(curTextPos, pos, parsedStates, parser))
          return new ParseAlternative(curTextPos, pos, (pos < 0 ? parser.MaxFailPos : pos) - curTextPos + parentsEat, pos < 0 ? parser.MaxFailPos : 0, state);
      }

      return new ParseAlternative(curTextPos, curTextPos, parentsEat, 0, -1);
    }

    #endregion

    #region Спекулятивный поиск фреймов

    private void FindSpeculativeFrames(HashSet<RecoveryStackFrame> newFrames, Parser parser, RecoveryStackFrame frame, int failPos, int skipCount)
    {
      if (frame.IsTokenRule)
        return;

      if (frame.Id == 28)
        Debug.Assert(true);

      if (frame.Depth == 0)
      {
        var parseAlternative = ParseTopFrame(parser, frame, skipCount);
        // Не спекулировать на правилах которые что-то парсят. Такое может случиться после пропуска грязи.
        if (skipCount > 0 && parseAlternative.End > 0 && parseAlternative.ParentsEat > 0)
          return;
      }

      if (!frame.IsPrefixParsed) // пытаемся восстановить пропущенный разделитель списка
      {
        var separatorFrame = frame.GetLoopBodyFrameForSeparatorState(failPos, parser);

        if (separatorFrame != null)
        {
          // Нас просят попробовать востановить отстуствующий разделитель цикла. Чтобы знать, нужно ли это дела, или мы
          // имеем дело с банальным концом цикла мы должны
          Debug.Assert(separatorFrame.Parents.Count == 1);
          var newFramesCount = newFrames.Count;
          FindSpeculativeFrames(newFrames, parser, separatorFrame, failPos, skipCount);
          if (newFrames.Count > newFramesCount)
            return;
        }
      }

      for (var state = frame.FailState; state >= 0; state = frame.GetNextState(state))
        FindSpeculativeSubframes(newFrames, parser, frame, failPos, state, skipCount);
    }

    protected virtual void FindSpeculativeSubframes(HashSet<RecoveryStackFrame> newFrames, Parser parser, RecoveryStackFrame frame, int curTextPos, int state, int skipCount)
    {
      foreach (var subFrame in frame.GetSpeculativeFramesForState(curTextPos, parser, state))
      {
        if (subFrame.IsTokenRule)
          continue;

        if (!newFrames.Add(subFrame))
          continue;

        FindSpeculativeSubframes(newFrames, parser, subFrame, curTextPos, subFrame.FailState, skipCount);
      }
    }
    
    #endregion

    #region Модификация AST (FixAst)

    // ReSharper disable once ParameterTypeCanBeEnumerable.Local
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

    #endregion
  }

  internal static class RecoveryUtils
  {
    public static List<T> FilterMax<T>(this ICollection<T> candidates, Func<T, int> selector)
    {
      var count = candidates.Count;
      if (candidates.Count <= 1)
      {
        var lst = candidates as List<T>;
        if (lst == null)
        {
          lst = new List<T>(count);
          lst.AddRange(candidates);
        }
        return lst;
      }

      var max1 = candidates.Max(selector);
      var res2 = candidates.Where(c => selector(c) == max1);
      return res2.ToList();
    }

    public static List<T> FilterMin<T>(this ICollection<T> candidates, Func<T, int> selector)
    {
      var count = candidates.Count;
      if (candidates.Count <= 1)
      {
        var lst = candidates as List<T>;
        if (lst == null)
        {
          lst = new List<T>(count);
          lst.AddRange(candidates);
        }
        return lst;
      }

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

    public static List<RecoveryStackFrame> FilterBetterEmptyIfAllEmpty(this List<RecoveryStackFrame> frames)
    {
      if (frames.Count < 1)
        Debug.Assert(false);

      if (frames.Count <= 1)
        return frames;

      if (frames.All(f => f.ParseAlternatives.Length == 0 || f.ParseAlternatives.Max(a => a.ParentsEat) == 0))
      {
        // Если список содержит только элементы разбирающие пустую строку и при этом имеется элементы с нулевой глубиной, то предпочитаем их.
        var res2 = frames.FilterMin(c => c.Depth).ToList();
        //if (res2.Count != result.Count)
        //  Debug.Assert(true);
        return res2;
      }

      return frames;
    }

    public static IEnumerable<T> FilterIfExists<T>(this List<T> res2, Func<T, bool> predicate)
    {
      return res2.Any(predicate) ? res2.Where(predicate) : res2;
    }

    public static bool HasParsedStaets(this RecoveryStackFrame frame, List<ParsedStateInfo> parsedStates)
    {
// ReSharper disable once LoopCanBeConvertedToQuery
      foreach (var parsedState in parsedStates)
      {
        if (!frame.IsVoidState(parsedState.State) && parsedState.Size > 0)
          return true;
      }
      return false;
    }

    public static int ParsedSpacesLen(this RecoveryStackFrame frame, List<ParsedStateInfo> parsedStates)
    {
      var sum = 0;
// ReSharper disable once LoopCanBeConvertedToQuery
      foreach (var parsedState in parsedStates)
        sum += !frame.IsVoidState(parsedState.State) ? 0 : parsedState.Size;
      return sum;
    }

    public static bool NonVoidParsed(this RecoveryStackFrame frame, int curTextPos, int pos, List<ParsedStateInfo> parsedStates, Parser parser)
    {
      var lastPos = Math.Max(pos, parser.MaxFailPos);
      return lastPos > curTextPos && lastPos - curTextPos > ParsedSpacesLen(frame, parsedStates)
             || parsedStates.Count > 0 && frame.HasParsedStaets(parsedStates);
    }
  }
}
