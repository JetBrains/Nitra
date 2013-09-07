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
      Debug.Assert(parser.RecoveryStacks.Count > 0);

      while (parser.RecoveryStacks.Count > 0)
      {
        var failPos = parser.MaxFailPos;
        var skipCount = 0;
        var bestFrames = CollectBestFrames(failPos, ref skipCount, parser);
        FixAst(bestFrames, failPos, skipCount, parser);
      }

      return parser.Text.Length;
    }

    private List<RecoveryStackFrame> CollectBestFrames(int failPos, ref int skipCount, Parser parser)
    {
      var text = parser.Text;

      var bestFrames = new List<RecoveryStackFrame>();

      for (; failPos + skipCount < text.Length; ++skipCount)
      {
        var frames = parser.RecoveryStacks.PrepareRecoveryStacks();

        foreach (var frame in frames) // reset ParseAlternatives
          frame.ParseAlternatives = null;

        var newFrames = new HashSet<RecoveryStackFrame>(frames);
        foreach (var frame in frames)
          if (frame.Depth == 0)
          {
            if (frame.TextPos != failPos)
              Debug.Assert(false);
            FindSpeculativeFrames(newFrames, parser, frame, failPos, skipCount);
          }

        var allFrames = newFrames.PrepareRecoveryStacks();

        bestFrames.Clear();

        ParseFrames(parser, skipCount, allFrames);

        UpdateParseFramesAlternatives(allFrames);
        SelectBestFrames(bestFrames, allFrames);

        if (IsAllFramesParseEmptyString(allFrames))
          bestFrames.Clear();
        else
        {
        }

        if (bestFrames.Count != 0)
          break;
      }

      return bestFrames;
    }

    private bool IsAllFramesParseEmptyString(IEnumerable<RecoveryStackFrame> allFrames)
    {
      return allFrames.All(f => f.ParseAlternatives.All(a => a.ParentsEat == 0));
    }

    #endregion

    #region Выбор лучшего фрейма

    private static void UpdateParseFramesAlternatives(List<RecoveryStackFrame> allFrames)
    {
      var root = allFrames[allFrames.Count - 1];
      root.Best = true; // единственный корень гарантированно последний
      root.ParseAlternatives = FilterMaxEndOrFail(root.ParseAlternatives.ToList()).ToArray();

      for (int i = allFrames.Count - 1; i >= 0; --i)
      {
        var frame = allFrames[i];

        if (!frame.Best)
          continue;

        if (frame.Children.Count == 0)
          continue;

        switch (frame.Id)
        {
          case 189: break;
        }

        var children = frame.Children;

        if (children.Count == 0)
          Debug.Assert(false);

        var alternatives0 = FilterParseAlternativesWichEndsEqualsParentsStarts(frame);
        var alternatives9 = FilterMinState(alternatives0);

        frame.ParseAlternatives = alternatives9.ToArray();

        foreach (var alternative in alternatives9)
        {
          var start = alternative.Start;

          foreach (var child in children)
            if (EndWith(child, start))
              child.Best = true;
        }
      }
    }

    private static List<RecoveryStackFrame> FilterNotEmpyPrefixChildren(RecoveryStackFrame frame, List<RecoveryStackFrame> children)
    {
      if (frame is RecoveryStackFrame.ExtensiblePrefix && children.Count > 1)
      {
        if (children.Any(c => c.ParseAlternatives.Any(a => a.State < 0)) && children.Any(c => c.ParseAlternatives.Any(a => a.State >= 0)))
          return children.Where(c => c.ParseAlternatives.Any(a => a.State >= 0)).ToList();
      }

      return children;
    }

    private static bool EndWith(RecoveryStackFrame child, int end)
    {
      return child.ParseAlternatives.Any(p => p.End < 0 ? p.Fail == end : p.End == end);
    }

    private static List<ParseAlternative> FilterMinState(List<ParseAlternative> alternatives)
    {
      if (alternatives.Count <= 1)
        return alternatives.ToList();

      var result = alternatives.FilterMin(f => f.State < 0 ? int.MaxValue : f.State);

      if (result.Count != alternatives.Count)
        Debug.Assert(true);

      return result;
    }

    private static List<ParseAlternative> FilterMaxEndOrFail(List<ParseAlternative> alternatives)
    {
      if (alternatives.Count <= 1)
        return alternatives.ToList();

      return alternatives.FilterMax(f => f.End >= 0 ? f.End : f.Fail);
    }

    private static void SelectBestFrames(List<RecoveryStackFrame> bestFrames, List<RecoveryStackFrame> allFrames)
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

        switch (frame.Id)
        {
          case 189: break;
        }

        // Отбрасывает всех потомков у которых свойство Best == false
        var children0 = OnlyBastFrames(frame);
        // отбрасывает потомков не съедающих символов, в случае если они ростут из состяния допускающего пустую строку (цикл или необязательное правило)
        var children1 = FilterEmptyChildrenWhenFailSateCanParseEmptySting(frame, children0);
        // отберат фреймы которые которые продолжают парсинг с состояния облом. Такое может случиться если была пропущена грязь, а сразу за ней 
        // идет корректная конструкция. Пример из джейсона: {a:.2}. Здесь "." - это грязь за которой идет корректное Value. Фильтрация производится
        // только если среди потомков есть подпадающие под условия.
        var children2 = FilterTopFramesWhichRecoveredOnFailStateIfExists(children1); // TODO: Похоже это дело дублирует FilterFailSateEqualsStateIfExists
        // Если все потомки парсят пустую строку (во всех путях пропарсивания васех потомков ParentsEat == 0), то отбираем потомков с наименьшей глубиной (Depth).
        var children3 = children2.FilterBetterEmptyIfAllEmpty();
        // Если среди потомков есть фреймы пропарсившие код (у которых End >= 0), то отбираем их, отбрасывая фреймы пропарсившие с Fail-мо. 
        // TODO: Возожно нужно делать это более осторожно, так как при наличии нескольких ошибок Fail-фреймы могут оказаться более предпочтительным. Но возможно они отфильтруются раньше.
        var children4 = FilterNonFailedFrames(children3);
        // Для каждой группы потомков с одинаковым местом фэйла (TextPos) отбираем такие которые начали парситься с меньшего состояния (подправила).
        var children5 = SelectMinFailSateIfTextPosEquals(children4);
        // Отбрасываем потомков все альтеративы которых пропарсили пустую строку.
        var children6 = FilterEmptyChildren(children5);
        var children9 = children6;//FilterNotEmpyPrefixChildren(frame, children6);

        var bettreChildren = children9;
        var poorerChildren = SubstractSet(frame.Children, bettreChildren);

        if (poorerChildren.Count > 0)
          ResetBestProperty(poorerChildren);

        if (bettreChildren.Count == 0)
          bestFrames.Add(frame);
      }

      FilterFailSateEqualsStateIfExists(bestFrames);
    }

    private static List<RecoveryStackFrame> FilterEmptyChildren(List<RecoveryStackFrame> children5)
    {
      return SubstractSet(children5, children5.Where(f => f.ParseAlternatives.All(a => f.TextPos == a.Start && a.ParentsEat == 0 && a.State < 0 && f.FailState == 0)).ToList());
    }

    private static void FilterFailSateEqualsStateIfExists(List<RecoveryStackFrame> bestFrames)
    {
      if (bestFrames.Any(f => f.ParseAlternatives.Any(a => f.FailState == a.State)))
        for (int index = bestFrames.Count - 1; index >= 0; index--)
        {
          var f = bestFrames[index];
          if (!f.ParseAlternatives.Any(a => f.FailState == a.State))
            bestFrames.RemoveAt(index);
        }
    }

    private static List<RecoveryStackFrame> SelectMinFailSateIfTextPosEquals(List<RecoveryStackFrame> children4)
    {
      return children4.GroupBy(f => f.TextPos).SelectMany(fs => fs.ToList().FilterMin(f => f.FailState)).ToList();
    }

    private static List<RecoveryStackFrame> FilterNonFailedFrames(List<RecoveryStackFrame> children3)
    {
      return children3.FilterIfExists(f => f.ParseAlternatives.Any(a => a.End >= 0)).ToList();
    }

    private static List<RecoveryStackFrame> FilterEmptyChildrenWhenFailSateCanParseEmptySting(RecoveryStackFrame frame, List<RecoveryStackFrame> frames)
    {
      if (frame.IsSateCanParseEmptyString(frame.FailState))
      {
        var result = frames.Where(f => f.ParseAlternatives.Any(a => a.ParentsEat != 0 || frame.TextPos < a.Start)).ToList();
        return result;
      }

      return frames;
    }

    private static List<RecoveryStackFrame> OnlyBastFrames(RecoveryStackFrame frame)
    {
      return frame.Children.Where(f => f.Best).ToList();
    }

    private static List<RecoveryStackFrame> RemoveSpeculativeFrames(List<RecoveryStackFrame> frames)
    {
      if (frames.Count <= 1)
        return frames;

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

    private static List<RecoveryStackFrame> SubstractSet(List<RecoveryStackFrame> set1, ICollection<RecoveryStackFrame> set2)
    {
      return set1.Where(c => !set2.Contains(c)).ToList();
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

    private static List<ParseAlternative> FilterParseAlternativesWichEndsEqualsParentsStarts(RecoveryStackFrame frame)
    {
      List<ParseAlternative> res0;
      if (frame.Parents.Count == 0)
        res0 = frame.ParseAlternatives.ToList();
      else
      {
        var parentStarts = new HashSet<int>();
        foreach (var parent in frame.Parents)
          if (parent.Best)
            foreach (var alternative in parent.ParseAlternatives)
              parentStarts.Add(alternative.Start);
        res0 = frame.ParseAlternatives.Where(alternative => parentStarts.Contains(alternative.End)).ToList();
        if (res0.Count == 0)
          res0 = frame.ParseAlternatives.Where(alternative => parentStarts.Contains(alternative.Fail)).ToList();
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

        if (frame.Id == 50)
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
                curentEnds.Add(new ParseAlternative(alternative.Fail, -1, alternative.ParentsEat, alternative.Fail, frame.FailState));

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
      //if (frame.Id == 187)
      //  Debug.Assert(false);
      var parentsEat = frame.Children.Max(c => c.ParseAlternatives.Length == 0 
                                              ? 0
                                              : c.ParseAlternatives.Max(a => a.End == curTextPos ? a.ParentsEat : 0));
      var maxfailPos = curTextPos;

      // Мы должны попытаться пропарсить даже если состояние полученное в первый раз от frame.GetNextState(state) 
      // меньше нуля, так как при этом производится попытка пропарсить следующий элемент цикла.
      var state      = frame.FailState;
      do
      {
        state = frame.GetNextState(state);
        parser.MaxFailPos = maxfailPos;
        var parsedStates = new List<ParsedStateInfo>();
        var pos = frame.TryParse(state, curTextPos, true, parsedStates, parser);
        if (frame.NonVoidParsed(curTextPos, pos, parsedStates, parser))
          return new ParseAlternative(curTextPos, pos, (pos < 0 ? parser.MaxFailPos : pos) - curTextPos + parentsEat, pos < 0 ? parser.MaxFailPos : 0, state);
      }
      while (state >= 0);

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
        var bodyFrame = frame.GetLoopBodyFrameForSeparatorState(failPos, parser);

        if (bodyFrame != null)
        {
          // Нас просят попробовать востановить отстуствующий разделитель цикла. Чтобы знать, нужно ли это дела, или мы
          // имеем дело с банальным концом цикла мы должны
          Debug.Assert(bodyFrame.Parents.Count == 1);
          var newFramesCount = newFrames.Count;
          FindSpeculativeFrames(newFrames, parser, bodyFrame, failPos, skipCount);
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
    private void FixAst(List<RecoveryStackFrame> bestFrames, int failPos, int skipCount, Parser parser)
    {
      var allBestFrames = bestFrames.UpdateDepthAndCollectAllFrames();
      allBestFrames.RemoveAll(frame => !frame.Best);
      foreach (var frame in allBestFrames)
        if (frame.ParseAlternatives.Length != 1)
          Debug.Assert(false);

      parser.RecoveryStacks.Clear();

      var errorIndex = parser.ErrorData.Count;
      parser.ErrorData.Add(new ParseErrorData(new NToken(failPos, failPos + skipCount), allBestFrames.ToArray()));

      var parents = new HashSet<RecoveryStackFrame>();
      foreach (var frame in bestFrames)
      {
        if (frame.PatchAst(errorIndex, parser))
          foreach (var parent in frame.Parents)
            if (parent.Best)
              parents.Add(parent);
      }

      while (parents.Count > 0 && !parents.Contains(allBestFrames[allBestFrames.Count - 1]))//пока не содержит корень
      {
        var newParents = new HashSet<RecoveryStackFrame>();
        foreach (var frame in parents)
        {
          if (frame.ContinueParse(parser))
            foreach (var parent in frame.Parents)
              if (parent.Best)
                newParents.Add(parent);
        }
        parents = newParents;
      }
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

    public static List<RecoveryStackFrame> UpdateDepthAndCollectAllFrames(this ICollection<RecoveryStackFrame> heads)
    {
      var allRecoveryStackFrames = new List<RecoveryStackFrame>();

      foreach (var stack in heads)
        stack.ClearAndCollectFrames(allRecoveryStackFrames);
      foreach (var stack in heads)
        stack.Depth = 0;
      foreach (var stack in heads)
        stack.UpdateFrameDepth();

      allRecoveryStackFrames.SortByDepth();

      return allRecoveryStackFrames;
    }

    public static List<RecoveryStackFrame> PrepareRecoveryStacks(this ICollection<RecoveryStackFrame> heads)
    {
      var allRecoveryStackFrames = heads.UpdateDepthAndCollectAllFrames();

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
