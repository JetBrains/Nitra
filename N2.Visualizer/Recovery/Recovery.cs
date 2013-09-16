//#region Пролог
#define DebugOutput
using N2.Internal;

using IntRuleCallKey = Nemerle.Builtins.Tuple<int, N2.Internal.RuleCallKey>;

using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Xml.Linq;

#if N2RUNTIME
namespace N2.Strategies
#else
// ReSharper disable once CheckNamespace
namespace N2.DebugStrategies
#endif
{
  using ParserData = Tuple<int, int, List<ParsedStateInfo>>;
  using ReportData = Action<RecoveryResult, List<RecoveryResult>, List<RecoveryResult>, List<RecoveryStackFrame>>;
  
//#endregion

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

        var allFrames = CollectSpeculativeFrames(failPos, skipCount, parser, frames);

        bestFrames.Clear();

        ParseFrames(parser, skipCount, allFrames);

        UpdateParseFramesAlternatives(allFrames);
        foreach (var f in allFrames)
          if (f.IsTop && !f.IsSpeculative)
            Debug.WriteLine(f);

        RecoveryUtils.UpdateParseAlternativesTopToDown(allFrames);

        ParseAlternativesVisializer.PrintParseAlternatives(allFrames, allFrames, parser);

        SelectBestFrames(bestFrames, allFrames);

        //RecoveryUtils.UpdateParseAlternativesTopToDown(allFrames);
        ParseAlternativesVisializer.PrintParseAlternatives(bestFrames, allFrames, parser);

        if (IsAllFramesParseEmptyString(allFrames))
          bestFrames.Clear();
        else
        {
        }

        if (bestFrames.Count != 0)
          break;
        else
        {
        }
      }

      return bestFrames;
    }



    private List<RecoveryStackFrame> CollectSpeculativeFrames(int failPos, int skipCount, Parser parser, List<RecoveryStackFrame> frames)
    {
      var newFrames = new HashSet<RecoveryStackFrame>(frames);
      foreach (var frame in frames)
      {
        if (frame.Depth == 0 && frame.TextPos != failPos)
          Debug.Assert(false);
        FindSpeculativeFrames(newFrames, parser, frame, failPos, skipCount);
      }

      var allFrames = newFrames.PrepareRecoveryStacks();
      UpdateIsSpeculative(frames, allFrames);
      return allFrames;
    }


    private static void UpdateIsSpeculative(List<RecoveryStackFrame> frames, List<RecoveryStackFrame> allFrames)
    {
      var frameSet = new HashSet<RecoveryStackFrame>(frames);
      foreach (var frame in allFrames)
        frame.IsSpeculative = !frameSet.Contains(frame);
    }

    static List<RecoveryStackFrame> Top(List<RecoveryStackFrame> allFrames)
    {
      return allFrames.Where(f => f.IsTop).ToList();
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
      root.ParseAlternatives = RecoveryUtils.FilterMaxEndOrFail(root.ParseAlternatives.ToList()).ToArray();

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

        var alternatives0 = RecoveryUtils.FilterParseAlternativesWichEndsEqualsParentsStarts(frame);
        //var alternatives9 = RecoveryUtils.FilterMinState(alternatives0);
        var alternatives9 = alternatives0;

        frame.ParseAlternatives = alternatives9.ToArray();

        foreach (var alternative in alternatives9)
        {
          var start = alternative.Start;

          foreach (var child in children)
            if (RecoveryUtils.EndWith(child, start))
              child.Best = true;
        }
      }
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
          case 65: break;
          case 23: break;
        }

        // Отбрасывает всех потомков у которых свойство Best == false
        var children0 = RecoveryUtils.OnlyBastFrames(frame);
        // отбрасывает потомков не съедающих символов, в случае если они ростут из состяния допускающего пустую строку (цикл или необязательное правило)
        var children1 = RecoveryUtils.FilterEmptyChildrenWhenFailSateCanParseEmptySting(frame, children0);
        // отберат фреймы которые которые продолжают парсинг с состояния облом. Такое может случиться если была пропущена грязь, а сразу за ней 
        // идет корректная конструкция. Пример из джейсона: {a:.2}. Здесь "." - это грязь за которой идет корректное Value. Фильтрация производится
        // только если среди потомков есть подпадающие под условия.
        var children2 = RecoveryUtils.FilterTopFramesWhichRecoveredOnFailStateIfExists(children1); // TODO: Похоже это дело дублирует FilterFailSateEqualsStateIfExists
        // Если все потомки парсят пустую строку (во всех путях пропарсивания васех потомков ParentsEat == 0), то отбираем потомков с наименьшей глубиной (Depth).
        var children3 = children2.FilterBetterEmptyIfAllEmpty();
        // Если среди потомков есть фреймы пропарсившие код (у которых End >= 0), то отбираем их, отбрасывая фреймы пропарсившие с Fail-мо. 
        // TODO: Возожно нужно делать это более осторожно, так как при наличии нескольких ошибок Fail-фреймы могут оказаться более предпочтительным. Но возможно они отфильтруются раньше.
        var children4 = RecoveryUtils.FilterNonFailedFrames(children3);
        // Для каждой группы потомков с одинаковым местом фэйла (TextPos) отбираем такие которые начали парситься с меньшего состояния (подправила).
        //var children5 = RecoveryUtils.SelectMinFailSateIfTextPosEquals(children4);
        var children5 = children4;
        // Отбрасываем потомков все альтеративы которых пропарсили пустую строку.
        var children6 = RecoveryUtils.FilterEmptyChildren(children5);
        var children9 = children6;//FilterNotEmpyPrefixChildren(frame, children6);

        var bettreChildren = children9;
        var poorerChildren = RecoveryUtils.SubstractSet(frame.Children, bettreChildren);

        if (poorerChildren.Count > 0)
          RecoveryUtils.ResetChildrenBestProperty(poorerChildren);

        if (bettreChildren.Count == 0)
          bestFrames.Add(frame);
      }

      // Реализовано не корректно. Выбирать FS==S можно только если у нас скипается грязь и допарсивание не пропускает состояний. Как-то так.
      //RecoveryUtils.FilterFailSateEqualsStateIfExists(bestFrames);
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
      for (var state = frame.Depth == 0 ? frame.FailState : frame.GetNextState(frame.FailState); state >= 0; state = frame.GetNextState(state))
        FindSpeculativeSubframes(newFrames, parser, frame, failPos, state, skipCount);
    }

    protected virtual void FindSpeculativeSubframes(HashSet<RecoveryStackFrame> newFrames, Parser parser, RecoveryStackFrame frame, int curTextPos, int state, int skipCount)
    {
      foreach (var subFrame in frame.GetSpeculativeFramesForState(frame.TextPos + skipCount, parser, state))
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
      parser.MaxFailPos = failPos;
      parser.RecoveryStacks.Clear();
      if (bestFrames.Count == 0)
        return;

      var allFrames = bestFrames.UpdateDepthAndCollectAllFrames();
      var cloned = RecoveryStackFrame.CloneGraph(allFrames);
      var first = bestFrames[0];
      var firstBestFrame = new[] { first };
      RecoveryUtils.RemoveFramesUnnecessaryAlternatives(allFrames, first);

      allFrames = allFrames.UpdateReverseDepthAndCollectAllFrames();

      foreach (var frame in firstBestFrame)
      {
        var errorIndex = parser.ErrorData.Count;
        parser.ErrorData.Add(new ParseErrorData(new NToken(failPos, failPos + skipCount), cloned.ToArray(), parser.ErrorData.Count));
        if (!frame.PatchAst(errorIndex, parser))
          RecoveryUtils.ResetParentsBestProperty(frame.Parents);
        frame.Best = false;
      }

      for (int i = 0; i < allFrames.Count - 1; ++i)//последним идет корень. Его фиксить не надо
      {
        var frame = allFrames[i];
        if (frame.Best)
          if (!frame.ContinueParse(parser))
            RecoveryUtils.ResetParentsBestProperty(frame.Parents);
      }
    }

    #endregion
  }

#region Utility methods

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

    public static void RemoveFramesUnnecessaryAlternatives(List<RecoveryStackFrame> allFrames, RecoveryStackFrame head)
    {
      // reset IsMarked
      foreach (var frame in allFrames)
        frame.IsMarked = false;

      // set IsMarked on parents of head

      RemoveOthrHeads(allFrames, head);

      // удаляем ParseAlternatives-ы с которых не может быть начат парсинг фрейма head.
      UpdateParseAlternativesTopToDown(allFrames);

      // выбрать самые длинные пропарсивания в префиксных и постфиксных правилах
      for (int index = allFrames.Count - 1; index >= 0; index--)
      {
        var frame = allFrames[index];

        if (!frame.Best)
          continue;

        if (frame.IsMarked)
        {
          frame.IsMarked = false;
          var alternatives0 = FilterParseAlternativesWichEndsEqualsParentsStarts(frame);

          if (frame.ParseAlternatives.Length != alternatives0.Count)
          {
            frame.ParseAlternatives = alternatives0.ToArray();
            MarkChildren(frame);
            if (alternatives0.Count == 0)
              frame.Best = false;
          }
        }

        if ((frame is RecoveryStackFrame.ExtensiblePostfix || frame is RecoveryStackFrame.ExtensiblePrefix) && frame.ParseAlternatives.Length > 1)
        {
          var parseAlternatives = FilterMaxStop(frame);

          if (frame.ParseAlternatives.Length != parseAlternatives.Count)
          {
            frame.ParseAlternatives = parseAlternatives.ToArray();
            MarkChildren(frame);
          }
        }
      }

      UpdateParseAlternativesTopToDown(allFrames);
    }

	  private static void RemoveOthrHeads(List<RecoveryStackFrame> allFrames, RecoveryStackFrame livingHead)
	  {
	    livingHead.IsMarked = true;

	    foreach (var frame in allFrames)
	    {
	      if (!frame.IsMarked)
	        continue;

	      if (frame.Parents.Count == 0)
	        continue;

	      foreach (var parent in frame.Parents)
	        if (parent.Best && !parent.IsMarked)
	          parent.IsMarked = true;
	    }

	    // update Best by Marked
	    foreach (var frame in allFrames)
	    {
	      frame.Best = frame.IsMarked;
	      frame.IsMarked = false;
	    }
	  }

	  public static void UpdateParseAlternativesDownToTop(List<RecoveryStackFrame> allFrames)
	  {
      if (allFrames.Count == 0)
        return;
      
      int index = allFrames.Count - 1;
      var frame = allFrames[index];

      frame.IsMarked = true;

      for (; index >= 0; index--)
      {
        frame = allFrames[index];

        if (!frame.Best)
          continue;

        if (frame.IsMarked)
        {
          frame.IsMarked = false;
          var alternatives0 = FilterParseAlternativesWichEndsEqualsParentsStarts(frame);

          if (frame.ParseAlternatives.Length != alternatives0.Count)
          {
            frame.ParseAlternatives = alternatives0.ToArray();
            MarkChildren(frame);
            if (alternatives0.Count == 0)
              frame.Best = false;
          }
        }
      }
    }

	  public static void UpdateParseAlternativesTopToDown(List<RecoveryStackFrame> allFrames)
    {
      if (allFrames.Count == 0)
        return;
      
      var starts       = new HashSet<int>();
      var alternatives = new List<ParseAlternative>();

      // удаляем ParseAlternatives-ы с которых не может быть начат парсинг фрейма head.
      foreach (var frame in allFrames)
      {
        if (!frame.Best)
          continue;

        var children = frame.Children;

        if (children.Count == 0)
          continue;

        starts.Clear();

        // собираем допустимые стартовые позиции для текущего фрейма
        foreach (var child in children)
        {
          if (!child.Best)
            continue;

          foreach (var a in child.ParseAlternatives)
            starts.Add(a.Stop);
        }

        if (starts.Count == 0) // это верхний фрейм
          continue;

        // удаляем ParseAlternatives-ы не начинающиеся с starts.
        alternatives.Clear();

        foreach (var a in frame.ParseAlternatives)
        {
          if (starts.Contains(a.Start))
            alternatives.Add(a);
        }

        if (alternatives.Count != frame.ParseAlternatives.Length)
          frame.ParseAlternatives = alternatives.ToArray();
      }
    }

	  public static void MarkChildren(RecoveryStackFrame frame)
	  {
	    foreach (var child in frame.Children)
	      if (frame.Best)
	        child.IsMarked = true;
	  }

	  private static List<ParseAlternative> FilterMaxStop(RecoveryStackFrame frame)
	  {
	    return FilterMax(frame.ParseAlternatives, a => a.Stop);
	  }

	  private static void RemoveUnnecessaryAlternatives(RecoveryStackFrame frame, HashSet<int> starts)
    {
      if (frame.ParseAlternatives.Length > 1)
        frame.ParseAlternatives = frame.ParseAlternatives.Where(a => starts.Contains(a.Start)).ToArray();
      else if (frame.ParseAlternatives.Length == 1)
        Debug.Assert(starts.Contains(frame.ParseAlternatives[0].Start));
    }

    public static List<RecoveryStackFrame> UpdateReverseDepthAndCollectAllFrames(this ICollection<RecoveryStackFrame> heads)
    {
      var allRecoveryStackFrames = new List<RecoveryStackFrame>();

      foreach (var stack in heads)
        stack.ClearAndCollectFrames(allRecoveryStackFrames);
      foreach (var stack in heads)
        stack.UpdateFrameReverseDepth();

      allRecoveryStackFrames.SortByDepth();
      allRecoveryStackFrames.Reverse();

      return allRecoveryStackFrames;
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

      foreach (var frame in allRecoveryStackFrames)
      {
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

    private static void UpdateFrameReverseDepth(this RecoveryStackFrame frame)
    {
      if (frame.Parents.Count == 0)
        frame.Depth = 0;
      else
      {
        foreach (var parent in frame.Parents)
          if (parent.Depth == -1)
            UpdateFrameReverseDepth(parent);
        frame.Depth = frame.Parents.Max(x => x.Depth) + 1;
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

	  public static List<ParseAlternative> FilterParseAlternativesWichEndsEqualsParentsStarts(RecoveryStackFrame frame)
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

	  public static List<RecoveryStackFrame> FilterNotEmpyPrefixChildren(RecoveryStackFrame frame, List<RecoveryStackFrame> children)
	  {
	    if (frame is RecoveryStackFrame.ExtensiblePrefix && children.Count > 1)
	    {
	      if (children.Any(c => c.ParseAlternatives.Any(a => a.State < 0)) && children.Any(c => c.ParseAlternatives.Any(a => a.State >= 0)))
	        return children.Where(c => c.ParseAlternatives.Any(a => a.State >= 0)).ToList();
	    }

	    return children;
	  }

	  public static bool EndWith(RecoveryStackFrame child, int end)
	  {
	    return child.ParseAlternatives.Any(p => p.End < 0 ? p.Fail == end : p.End == end);
	  }

	  public static List<ParseAlternative> FilterMinState(List<ParseAlternative> alternatives)
	  {
	    if (alternatives.Count <= 1)
	      return alternatives.ToList();

	    var result = alternatives.FilterMin(f => f.State < 0 ? Int32.MaxValue : f.State);

	    if (result.Count != alternatives.Count)
	      Debug.Assert(true);

	    return result;
	  }

	  public static List<ParseAlternative> FilterMaxEndOrFail(List<ParseAlternative> alternatives)
	  {
	    if (alternatives.Count <= 1)
	      return alternatives.ToList();

	    return alternatives.FilterMax(f => f.End >= 0 ? f.End : f.Fail);
	  }

	  public static List<RecoveryStackFrame> FilterEmptyChildren(List<RecoveryStackFrame> children5)
	  {
	    return SubstractSet(children5, children5.Where(f => f.ParseAlternatives.All(a => f.TextPos == a.Start && a.ParentsEat == 0 && a.State < 0 && f.FailState == 0)).ToList());
	  }

	  public static void FilterFailSateEqualsStateIfExists(List<RecoveryStackFrame> bestFrames)
	  {
	    if (bestFrames.Any(f => f.ParseAlternatives.Any(a => f.FailState == a.State)))
	      for (int index = bestFrames.Count - 1; index >= 0; index--)
	      {
	        var f = bestFrames[index];
	        if (!f.ParseAlternatives.Any(a => f.FailState == a.State))
	          bestFrames.RemoveAt(index);
	      }
	  }

	  public static List<RecoveryStackFrame> SelectMinFailSateIfTextPosEquals(List<RecoveryStackFrame> children4)
	  {
	    return children4.GroupBy(f =>  new IntRuleCallKey(f.TextPos, f.RuleKey)).SelectMany(fs => fs.ToList().FilterMin(f => f.FailState)).ToList();
	  }

	  public static List<RecoveryStackFrame> FilterNonFailedFrames(List<RecoveryStackFrame> children3)
	  {
	    return children3.FilterIfExists(f => f.ParseAlternatives.Any(a => a.End >= 0)).ToList();
	  }

	  public static List<RecoveryStackFrame> FilterEmptyChildrenWhenFailSateCanParseEmptySting(RecoveryStackFrame frame, List<RecoveryStackFrame> frames)
	  {
	    if (frame.IsSateCanParseEmptyString(frame.FailState))
	    {
	      var result = frames.Where(f => f.ParseAlternatives.Any(a => a.ParentsEat != 0 || frame.TextPos < a.Start)).ToList();
	      return result;
	    }

	    return frames;
	  }

	  public static List<RecoveryStackFrame> OnlyBastFrames(RecoveryStackFrame frame)
	  {
	    return frame.Children.Where(f => f.Best).ToList();
	  }

	  public static List<RecoveryStackFrame> FilterTopFramesWhichRecoveredOnFailStateIfExists(List<RecoveryStackFrame> bestFrames)
	  {
	    if (bestFrames.Any(f => f.ParseAlternatives.Any(a => a.State == f.FailState)))
	    {
	      // TODO: Устранить этот кабздец! Удалять фреймы прямо из массива.
	      return bestFrames.Where(f => f.ParseAlternatives.Any(a => a.State == f.FailState)).ToList();
	    }

	    return bestFrames;
	  }

	  public static List<RecoveryStackFrame> RemoveSpeculativeFrames(List<RecoveryStackFrame> frames)
	  {
	    if (frames.Count <= 1)
	      return frames;

	    var frames2 = frames.FilterMax(f => f.ParseAlternatives[0].ParentsEat).ToList();
	    var frames3 = frames2.FilterMin(f => f.FailState);
	    return frames3.ToList();
	  }

	  public static bool HasTopFramesWhichRecoveredOnFailState(RecoveryStackFrame frame)
	  {
	    var failState = frame.FailState;
	    foreach (ParseAlternative a in frame.ParseAlternatives)
	      if (a.State == failState)
	        return true;
	    return false;
	  }

	  public static List<RecoveryStackFrame> SubstractSet(List<RecoveryStackFrame> set1, ICollection<RecoveryStackFrame> set2)
	  {
	    return set1.Where(c => !set2.Contains(c)).ToList();
	  }

	  public static void ResetChildrenBestProperty(List<RecoveryStackFrame> poorerChildren)
	  {
	    foreach (var child in poorerChildren)
	      if (child.Best)
	      {
	        child.Best = false;
	        ResetChildrenBestProperty(child.Children);
	      }
	  }

	  public static void ResetParentsBestProperty(HashSet<RecoveryStackFrame> parents)
	  {
	    foreach (var parent in parents)
	      if (parent.Best)
	      {
	        parent.Best = false;
	        ResetParentsBestProperty(parent.Parents);
	      }
	  }


	  public static bool StartWith(RecoveryStackFrame parent, HashSet<int> ends)
	  {
	    return parent.ParseAlternatives.Any(a => ends.Contains(a.Start));
	  }

	  public static HashSet<int> Ends(RecoveryStackFrame frame)
	  {
	    return new HashSet<int>(frame.ParseAlternatives.Select(a => a.Stop));
	  }

	  public static bool StartWith(RecoveryStackFrame parent, ParseAlternative a)
	  {
	    return parent.ParseAlternatives.Any(p => p.Start == a.End);
	  }
  }

  class ParseAlternativesVisializer
  {
    private const string HtmlTemplate = @"
<html>
<head>
    <title>Pretty Print</title>
    <meta http-equiv='Content-Type' content='text/html;charset=utf-8'/>
    <style type='text/css'>
pre
{
  color: black;
  font-weight: normal;
  font-size: 12pt;
  font-family: Consolas, Courier New, Monospace;
}

.default
{
  color: black;
  background: white;
}

.garbage
{
  color: red;
  background: lightpink;
}

.parsed
{
  color: Green;
  background: LightGreen;
}

.prefix
{
  color: Indigo;
  background: Plum;
}

.postfix
{
  color: blue;
  background: lightgray;
}

.skipedState
{
  color: darkgray;
  background: lightgray;
}
.currentRulePrefix
{
  color: darkgoldenrod;
  background: lightgoldenrodyellow;
}
</style>
</head>
<body>
<pre>
<content/>
</pre>
</body>
</html>
";

    static readonly XAttribute _garbageClass = new XAttribute("class", "garbage");
    static readonly XAttribute _parsedClass = new XAttribute("class", "parsed");
    static readonly XAttribute _prefixClass = new XAttribute("class", "prefix");
    static readonly XAttribute _postfixClass = new XAttribute("class", "postfix");
    static readonly XAttribute _skipedStateClass = new XAttribute("class", "skipedState");
    static readonly XAttribute _currentRulePrefixClass = new XAttribute("class", "currentRulePrefix");
    static readonly XAttribute _default = new XAttribute("class", "default");

    static readonly XElement _start = new XElement("span", _default, "▸");
    static readonly XElement _end = new XElement("span", _default, "◂");
    static readonly Regex _removePA = new Regex(@" PA=\[.*\]", RegexOptions.Compiled);

    [Conditional("Visualize")]
    public static void PrintParseAlternatives(List<RecoveryStackFrame> bestFrames, List<RecoveryStackFrame> allFrames, Parser parser)
    {
      RecoveryUtils.UpdateParseAlternativesTopToDown(allFrames);
      var results = new List<List<XElement>>();

      foreach (var frame in bestFrames)
      {
        if (!frame.IsTop)
          continue;

        results.Add(new List<XElement> { new XElement("span", frame) });
        var prefixs = MakeAlternativesPrefixs(frame, parser);
        var postfixs = MakeAlternativesPostfixs(null, frame, parser, frame.ParseAlternatives[0].Start);

        foreach (var prefix in prefixs)
        {
          foreach (var postfix in postfixs)
          {
            var all = new List<XElement> { prefix.Item2, postfix };
            results.Add(all);
          }
        }
      }

      var ps = new List<XNode>(results.Count);
      foreach (var result in results)
      {
        if (result.Count == 1)
        {
          ps.Add(new XText("\r\n"));
          ps.Add(result[0]);
        }
        else
          ps.Add(new XElement("span", result));
        ps.Add(new XText("\r\n"));
      }

      var template = XElement.Parse(HtmlTemplate);
      var content = template.Descendants("content").First();
      Debug.Assert(content.Parent != null);
      content.Parent.ReplaceAll(ps);
      var filePath = Path.ChangeExtension(Path.GetTempFileName(), ".html");
      template.Save(filePath);
      Process.Start(filePath);
    }

    private static List<XElement> MakeAlternativesPostfixs(XElement prefix, RecoveryStackFrame frame, Parser parser, int start)
    {
      var results = new List<XElement>();

      if (frame.Parents.Count == 0) // это корневой фрейм
      {
        if (prefix != null)
          results.Add(prefix);
        return results;
      }

      var isTop = frame.IsTop;
      var parsedClass = isTop ? _parsedClass : _postfixClass;

      foreach (var a in frame.ParseAlternatives)
      {
        if (a.Start != start)
          continue;

        var title = MakeTitle(frame, a);
        var text = parser.Text.Substring(a.Start, a.Stop - a.Start);
        var startState = isTop ? frame.FailState : frame.GetNextState(frame.FailState);
        var endState = a.State;

        var span = new XElement("span", parsedClass, title, prefix);

        if (startState != endState)
          span.Add(new XElement("span", _skipedStateClass, SkipedStatesCode(frame, startState, endState)));

        span.Add(text);
        span.Add(_end);

        if (a.End < 0)
        {
          span.Add(new XElement("span", _garbageClass, "<FAIL>"));
          results.Add(span);
          continue;
        }

        foreach (var parent in frame.Parents)
        {
          if (!parent.Best)
            continue;

          if (RecoveryUtils.StartWith(parent, a))
          {
            var intermediate = MakeAlternativesPostfixs(span, parent, parser, a.End);
            results.AddRange(intermediate);
          }
        }
      }

      return results;
    }

    private static List<Tuple<int, XElement>> MakeAlternativesPrefixs(RecoveryStackFrame frame, Parser parser)
    {
      if (frame.Parents.Count == 0) // это корневой фрейм
        return new List<Tuple<int, XElement>> { Tuple.Create(0, new XElement("span", MakeTitle(frame, null), _start)) };

      var isTop = frame.IsTop;
      var parsedClass = isTop ? _parsedClass : _prefixClass;
      var title = MakeTitle(frame, null);
      var skipedStatesCode =
          frame.FailState2 < frame.FailState
            ? new XElement("span", _skipedStateClass, SkipedStatesCode(frame, frame.FailState2, frame.FailState))
            : null;

      var results = new List<Tuple<int, XElement>>();
      var ends = RecoveryUtils.Ends(frame);

      foreach (var parent in frame.Parents)
      {
        if (!parent.Best && RecoveryUtils.StartWith(parent, (HashSet<int>)ends))
          continue;

        var intermediate = MakeAlternativesPrefixs(parent, parser);

        foreach (var result in intermediate)
        {
          var pos = result.Item1;
          var prefix = result.Item2;
          var text = parser.Text.Substring(pos, frame.TextPos - pos);
          var span = new XElement("span", _start, title, new XElement("span", parsedClass, text));

          if (skipedStatesCode != null)
            span.Add(skipedStatesCode);

          prefix.Add(span);

          results.Add(Tuple.Create(frame.TextPos, prefix));
        }
      }

      return results;
    }

    private static string SkipedStatesCode(RecoveryStackFrame frame, int startState, int endState)
    {
      return string.Join(" ", frame.CodeForStates(startState, endState));
    }

    private static XAttribute MakeTitle(RecoveryStackFrame frame, ParseAlternative? a)
    {
      return new XAttribute("title", a == null ? frame.ToString() : _removePA.Replace(frame.ToString(), " PA=" + a));
    }
  }
  
  #endregion
}
