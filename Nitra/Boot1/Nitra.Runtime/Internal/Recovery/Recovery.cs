//#region Пролог
#define DebugOutput
using System.Globalization;
using Nitra.Internal;
using Nitra.Runtime.Errors;
using NB = Nemerle.Builtins;
using IntRuleCallKey = Nemerle.Builtins.Tuple<int, Nitra.Internal.RuleCallKey>;

using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Xml.Linq;
using SCG = System.Collections.Generic;

#if NITRA_RUNTIME
namespace Nitra.Strategies
#else
// ReSharper disable once CheckNamespace
namespace Nitra.DebugStrategies
#endif
{
  using ParserData = Tuple<int, int, List<ParsedStateInfo>>;
  using ReportData = Action<RecoveryResult, List<RecoveryResult>, List<RecoveryResult>, List<RecoveryStackFrame>>;
  using ParseAlternativeNodes = Nemerle.Core.list<ParseAlternativeNode>;
  
//#endregion

  public class Recovery
  {
    public ReportData ReportResult;

    #region Инициализация и старт

    public Recovery(ReportData reportResult)
    {
      ReportResult = reportResult;
    }

    public virtual int Strategy(ParseResult parseResult)
    {
      Debug.Assert(parseResult.RecoveryStacks.Count > 0);

      while (parseResult.RecoveryStacks.Count > 0)
      {
        var failPos = parseResult.MaxFailPos;
        var skipCount = 0;
        var bestFrames = CollectBestFrames(failPos, ref skipCount, parseResult);
        FixAst(bestFrames, failPos, skipCount, parseResult);
      }

      return parseResult.Text.Length;
    }

    private List<ParseAlternativeNode> CollectBestFrames(int failPos, ref int skipCount, ParseResult parseResult)
    {
      var text = parseResult.Text;
      var beginFrames = new HashSet<RecoveryStackFrame>(parseResult.RecoveryStacks.PrepareRecoveryStacks());
      for (; failPos + skipCount < text.Length; ++skipCount)
      {
        var frames = parseResult.RecoveryStacks.PrepareRecoveryStacks();
        foreach (var frame in frames)
          if (!beginFrames.Contains(frame))
            Debug.WriteLine(frame.ToString());

        InitFrames(frames);

        var allFrames = CollectSpeculativeFrames(failPos, skipCount, parseResult, frames);

        ParseFrames(parseResult, skipCount, allFrames);

        if (IsAllFramesParseEmptyString(allFrames))
          continue;

        var nodes = ParseAlternativeNode.MakeGraph(allFrames);
        var bestNodes = SelectBestFrames2(parseResult, nodes, skipCount);

        if (IsAllFramesParseEmptyString(nodes))
          Debug.Assert(false);

        if (bestNodes.Count != 0)
          return bestNodes;
          
        Debug.Assert(false);
      }

      {
        var frames = new List<RecoveryStackFrame>();
        frames.Add(parseResult.RecoveryStacks.Last());
        frames = frames.PrepareRecoveryStacks();
        InitFrames(frames);
        ParseFrames(parseResult, skipCount, frames);
        var nodes = ParseAlternativeNode.MakeGraph(frames);
        nodes.RemoveAll(node => node.Frame.Depth != 0);
        return nodes;
      }
    }

    private static void InitFrames(List<RecoveryStackFrame> frames)
    {
      foreach (var frame in frames) // reset ParseAlternatives
      {
        frame.ParseAlternatives = null;
        frame.Best = true;
      }

      // TODO: Возможно могут быть случаи когда кишки токена также парсятся из не токен-правил. Что делать в этом случае? Выдавать ошибку?
      CalcIsInsideTokenProperty(frames);
    }

    private static void CalcIsInsideTokenProperty(List<RecoveryStackFrame> frames)
    {
      RecoveryStackFrame.DownToTop(frames, n => { if (n.Parents.Any(x => x.IsInsideToken || x.IsTokenRule)) n.IsInsideToken = true; });
    }

    private List<RecoveryStackFrame> CollectSpeculativeFrames(int failPos, int skipCount, ParseResult parseResult, List<RecoveryStackFrame> frames)
    {
      var newFrames = new HashSet<RecoveryStackFrame>(frames);
      foreach (var frame in frames)
      {
        if (frame.Depth == 0 && frame.TextPos != failPos)
          Debug.Assert(false);
        FindSpeculativeFrames(newFrames, parseResult, frame, failPos, skipCount);
      }

      var allFrames = newFrames.PrepareRecoveryStacks();
      UpdateIsSpeculative(frames, allFrames);

      foreach (var frame in allFrames)
        frame.Best = true;

      return allFrames;
    }


    // ReSharper disable once ParameterTypeCanBeEnumerable.Local
    private static void UpdateIsSpeculative(IEnumerable<RecoveryStackFrame> sourceFrames, List<RecoveryStackFrame> allFrames)
    {
      var frameSet = new HashSet<RecoveryStackFrame>(sourceFrames);
      foreach (var frame in allFrames)
        frame.IsSpeculative = !frameSet.Contains(frame);
    }

    private bool IsAllFramesParseEmptyString(IEnumerable<ParseAlternativeNode> nodes)
    {
      foreach (var node in nodes)
      {
        if (!node.Best)
          continue;

        if (!node.ParseAlternative.IsEmpty)
          return false;
      }

      return true;
    }

    private bool IsAllFramesParseEmptyString(IEnumerable<RecoveryStackFrame> frames)
    {
      foreach (var frame in frames)
        foreach (var a in frame.ParseAlternatives)
          if (!a.IsEmpty)
            return false;

      return true;
    }

    #endregion

    #region Выбор лучшего фрейма

    // ReSharper disable UnusedParameter.Local
    private static List<ParseAlternativeNode> SelectBestFrames2(ParseResult _parseResult, List<ParseAlternativeNode> nodes, int _skipCount)
    // ReSharper restore UnusedParameter.Local
    {
      //ParseAlternativesVisializer.PrintParseAlternatives(_parseResult, nodes, _skipCount, "After RemoveTheShorterAlternative.");
      //X.VisualizeFrames(nodes);

      RemoveTheShorterAlternative(nodes);
      //ParseAlternativesVisializer.PrintParseAlternatives(_parseResult, nodes, _skipCount, "AftFer RemoveTheShorterAlternative.");
      //X.VisualizeFrames(nodes);

      FilterAlternativesWithMinimumSkippedTokens(nodes);
      //ParseAlternativesVisializer.PrintParseAlternatives(_parseResult, nodes, _skipCount, "After RemoveAlternativesWithALotOfSkippedTokens.");
      //X.VisualizeFrames(nodes);
      //ParseAlternativeNode.DownToTop(nodes, CalcMinSkipedMandatoryTokenCount);

      ParseAlternativeNode.TopToDown(nodes, RemoveChildrenIfAllChildrenIsEmpty);
      //ParseAlternativesVisializer.PrintParseAlternatives(_parseResult, nodes, _skipCount, "After RemoveChildrenIfAllChildrenIsEmpty.");
      //X.VisualizeFrames(nodes);
      
      RemoveSuccessfullyParsed(nodes);
      //ParseAlternativesVisializer.PrintParseAlternatives(_parseResult, nodes, _skipCount, "After RemoveSuccessfullyParsed.");
      //X.VisualizeFrames(nodes);
      
      RemoveDuplicateNodes(nodes);
      //ParseAlternativesVisializer.PrintParseAlternatives(_parseResult, nodes, _skipCount, "After RemoveDuplicateNodes.");
      //X.VisualizeFrames(nodes);

      var bestNodes = GetTopNodes(nodes);
      //X.VisualizeFrames(bestNodes);
      //ParseAlternativesVisializer.PrintParseAlternatives(_parseResult, nodes, _skipCount, "After RemoveDuplicateNodes.");
      return bestNodes;
    }

    public static List<ParseAlternativeNode> GetRoots(List<ParseAlternativeNode> nodes)
    {
      var roots = new List<ParseAlternativeNode>();

      for (int i = nodes.Count - 1; i >= 0; i--)
      {
        var root = nodes[i];

        if (!root.Best)
          continue;

        if (!root.IsRoot)
          break;

        roots.Add(root);
      }

      return roots;
    }

    private static void RemoveTheShorterAlternative(List<ParseAlternativeNode> nodes)
    {
      var roots   = GetRoots(nodes);
      var max     = -1;
      var maxFail = -1;
      var removed = 0;

      foreach (var root in roots)
      {
        var a = root.ParseAlternative;

        if (a.End >= 0)
        {
          if (a.End > max)
            max = a.End;
        }
        else if (a.Fail > maxFail)
          maxFail = a.Fail;
      }

      Debug.Assert(max >= 0 || maxFail >= 0);

      if (max >= maxFail)
      {
        foreach (var node in roots)
          if (node.ParseAlternative.End != max)
          {
            node.Remove();
            removed++;
          }
      }
      else foreach (var node in roots)
        if (node.ParseAlternative.Fail != maxFail)
        {
          node.Remove();
          removed++;
        }

      Debug.Assert(roots.Count - removed == 1);

      //foreach (var node in roots)
      //  if (node.IsRoot && node.Best)
      //    Debug.WriteLine(node.ParseAlternative);
    }

    private static void FilterAlternativesWithMinimumSkippedTokens(List<ParseAlternativeNode> nodes)
    {
      var topNodes = GetTopNodes(nodes);
      // получаем все альтернативы пропарсивания в плоском режиме
      var alternatives = topNodes.SelectMany(n => n.GetFlatParseAlternatives()).ToList();
      // рассчитываем минимальное число пропускаемых токенов среди альтернатив.
      var min = alternatives.Min(x => SkippedTokenCount(x));
      var minAlternatives = alternatives.Where(a => SkippedTokenCount(a) == min).ToList();
      var hasAll = minAlternatives.Any(a => !a.IsEmpty && a.Head.ParseAlternative.Skip > 0)
                && minAlternatives.Any(a => !a.IsEmpty && a.Head.ParseAlternative.Skip == 0);
      var bestAlternatives = hasAll ? minAlternatives.Where(a => !a.IsEmpty && a.Head.ParseAlternative.Skip == 0).ToList()
                                    : minAlternatives;

      // помечаем все узлы альтернатив с минимальным пропуском токенов
      foreach (var a in bestAlternatives)
        foreach (var n in a)
          n.IsMarked = true;

      // удаляем все узлы за исключением помеченных на предыдущем шаге
      foreach (var node in nodes)
      {
        var isMarked = node.IsMarked;
        node.Best = isMarked;
        if (isMarked)
          node.IsMarked = false;
      }

      // TODO: Написать оптимизированную версию с нахрапа не вышло. Надо сделать это в будущем!
    }

    private static int SkippedTokenCount(ParseAlternativeNodes x)
    {
      return x.Sum(a => (a.ParseAlternative.Skip > 0 ? 1 : 0) + a.SkipedMandatoryTokenCount);
    }

    private static void CalcMinSkipedMandatoryTokenCount(ParseAlternativeNode node)
    {
      var parentMin = node.MinSkipedMandatoryTokenCount;

      foreach (var child in node.Children)
      {
        var value = child.SkipedMandatoryTokenCount + parentMin;
        if (value < child.MinSkipedMandatoryTokenCount)
          child.MinSkipedMandatoryTokenCount = value;
      }
    }

    /// <summary>
    /// Удаляет спекулятивные альтернативы, если среди результатов есть аналогичные не спекулятивные, а так же альтернативы у которых все дочерние
    /// элементы удалены как успешно спарсившиеся или пустышки, и у альтернативы несходится TP и Start.
    /// </summary>
    private static void RemoveDuplicateNodes(List<ParseAlternativeNode> nodes)
    {
      ParseAlternativeNode.DownToTop(nodes, RemoveDuplicateNodes);
    }

    private static void RemoveDuplicateNodes(ParseAlternativeNode node)
    {
      var groups = node.Children.GroupBy(c => Create(c.Frame.RuleKey, c.ParseAlternative)).ToList();

      foreach (var group in groups)
      {
        var g = group.ToList();

        if (g.Count <= 1)
          continue;

        var index = g.FindIndex(n => !n.Frame.IsSpeculative && n.Frame.FailState == n.Frame.FailState2); // Ищем индекс не спекулятивного стека.
        if (index >= 0)
        {
          // удаляем все кроме не спекулятивного стека
          for (int i = 0; i < g.Count; i++)
            if (i != index)
              g[i].Remove();

          return;
        }

        if (g.Any(n => n.Frame.TextPos < n.ParseAlternative.Start && n.IsTop))
        {
          var result = g.Where(n => n.Frame.TextPos < n.ParseAlternative.Start && n.IsTop).ToList();

          if (g.Count == result.Count)
            Debug.Assert(false, "У нас остались только невалидные альтернативы (n.Frame.TextPos < n.ParseAlternative.Start)");

          foreach (var n in result)
          {
            if (n.Id == 8300)
            { }
            n.Remove();
          }

          return;
        }
      }
    }

    private static NB.Tuple<T1, T2> Create<T1, T2>(T1 field1, T2 field2)
    {
      return new NB.Tuple<T1, T2>(field1, field2);
    }

    //private static NB.Tuple<T1, T2, T3> Create<T1, T2, T3>(T1 field1, T2 field2, T3 field3)
    //{
    //  return new NB.Tuple<T1, T2, T3>(field1, field2, field3);
    //}

    private static void RemoveSuccessfullyParsed(List<ParseAlternativeNode> nodes)
    {
      ParseAlternativeNode.TopToDown(nodes, CalcIsSuccessfullyParsed);
      ParseAlternativeNode.DownToTop(nodes, RemoveMarked);
    }

    // ReSharper disable once ParameterTypeCanBeEnumerable.Local
    private static List<ParseAlternativeNode> GetTopNodes(List<ParseAlternativeNode> nodes)
    {
      var bestNodes = new List<ParseAlternativeNode>();

      foreach (var node in nodes)
        if (node.IsTop)
          bestNodes.Add(node);

      return bestNodes;
    }

    private static void RemoveMarked(ParseAlternativeNode node)
    {
      if (node.IsMarked)
        node.Remove();
    }

    private static void CalcIsSuccessfullyParsed(ParseAlternativeNode node)
    {
      var frame = node.Frame;
      var a     = node.ParseAlternative;

      if (frame.Id == 185)
      {
      }

      if (node.IsTop)
      {
        if (frame.TextPos != a.Start)
          return;

        for (int i = frame.FailState2; i != -1; i = frame.GetNextState(i))
          if (!frame.IsSateCanParseEmptyString(i))
            return;

        node.IsMarked = true;
      }
      else
      {
        // если все чилды IsMarked то и node IsMarked = true

        foreach (var child in node.Children)
          if (!child.IsMarked)
            return;

        for (int i = frame.GetNextState(frame.FailState); i != -1; i = frame.GetNextState(i))
          if (!frame.IsSateCanParseEmptyString(i))
            return;

        node.IsMarked = true;
      }
    }

    private static void RemoveChildrenIfAllChildrenIsEmpty(ParseAlternativeNode node)
    {
      switch (node.Id)
      {
        case 1901  : break;
        case 28400: break;
        case 28500: break;
      }

      foreach (var n in node.Children)
      {
        if (!n.IsEmpty)
          return;
      }
      foreach (var child in node.Children)
        child.Remove();
    }

    #endregion

    #region Parsing

    // ReSharper disable once ParameterTypeCanBeEnumerable.Local
    private void ParseFrames(ParseResult parseResult, int skipCount, List<RecoveryStackFrame> allFrames)
    {
      foreach (var frame in allFrames)
      {
        if (frame.Id == 34)
          Debug.Assert(true);

        if (frame.Depth == 0)
        {
          // TODO: парсим вотор раз. Не хорошо.
          //if (frame.ParseAlternatives != null)
          //  continue; // пропаршено во время попытки найти спекулятивные подфреймы
          // разбираемся с головами
          ParseTopFrame(parseResult, frame, skipCount);
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
                curentEnds.Add(new ParseAlternative(alternative.Fail, -1, alternative.ParentsEat, alternative.Fail, frame.FailState, 0));

          foreach (var end in childEnds)
            curentEnds.Add(ParseNonTopFrame(parseResult, frame, end));

          var parseAlternatives = curentEnds.ToArray();
          frame.ParseAlternatives = parseAlternatives;
        }
      }
    }

    /// <returns>Посиция окончания парсинга</returns>
    private List<ParseAlternative> ParseTopFrame(ParseResult parseResult, RecoveryStackFrame frame, int skipCount)
    {
      ParseAlternative parseAlternative;

      switch (frame.Id)
      {
        case 70: break;
        case 32: break;
      }

      var curTextPos = frame.TextPos + skipCount;
      var parsedStates = new List<ParsedStateInfo>();
      var parseAlternatives = new List<ParseAlternative>(4);

      {
        var maxFailPos = parseResult.MaxFailPos;
        var ends = frame.ParseAllGrammarTokens(curTextPos);

        foreach (var end in Sort(ends))
        {
          if (end < 0 || end - curTextPos == 0)
            continue;

          parseResult.MaxFailPos = maxFailPos;
          var state = frame.FailState;
          var pos2 = frame.TryParse(state, end, false, parsedStates, parseResult);
          if (pos2 >= 0 && frame.NonVoidParsed(end, pos2, parsedStates, parseResult))
          {
            parseAlternative = new ParseAlternative(end, pos2, (pos2 < 0 ? parseResult.MaxFailPos : pos2) - end, pos2 < 0 ? parseResult.MaxFailPos : 0, state, end - curTextPos);
            parseAlternatives.Add(parseAlternative);
            parseResult.MaxFailPos = maxFailPos;
            break;
          }
        }

        parseResult.MaxFailPos = maxFailPos;
      }


      for (var state = frame.FailState; state >= 0; state = frame.GetNextState(state))
      {
        parseResult.MaxFailPos = curTextPos;
        var pos = frame.TryParse(state, curTextPos, false, parsedStates, parseResult);

        if (frame.NonVoidParsed(curTextPos, pos, parsedStates, parseResult))
        {
          parseAlternative = new ParseAlternative(curTextPos, pos, (pos < 0 ? parseResult.MaxFailPos : pos) - curTextPos, pos < 0 ? parseResult.MaxFailPos : 0, state, 0);
          parseAlternatives.Add(parseAlternative);
          frame.ParseAlternatives = parseAlternatives.ToArray();
          return parseAlternatives;
        }
      }

      // Если ни одного состояния не пропарсились, то считаем, что пропарсилось состояние "за концом правила".
      // Это соотвтствует полному пропуску остатка подправил данного правила.
      parseAlternative = new ParseAlternative(curTextPos, curTextPos, 0, 0, -1, 0);
      parseAlternatives.Add(parseAlternative);
      frame.ParseAlternatives = parseAlternatives.ToArray();
      return parseAlternatives;
    }

    private static IOrderedEnumerable<int> Sort(HashSet<int> ends)
    {
      return ends.OrderBy(x => x);
    }

    private static ParseAlternative ParseNonTopFrame(ParseResult parseResult, RecoveryStackFrame frame, int curTextPos)
    {
      switch (frame.Id)
      {
        case 19: break;
        case 34: break;
      }
      var parentsEat = ParentsMaxEat(frame, curTextPos);
      var maxfailPos = curTextPos;

      // Мы должны попытаться пропарсить даже если состояние полученное в первый раз от frame.GetNextState(state) 
      // меньше нуля, так как при этом производится попытка пропарсить следующий элемент цикла.
      var state      = frame.FailState;
      do
      {
        state = frame.GetNextState(state);
        parseResult.MaxFailPos = maxfailPos;
        var parsedStates = new List<ParsedStateInfo>();
        var pos = frame.TryParse(state, curTextPos, true, parsedStates, parseResult);
        if (frame.NonVoidParsed(curTextPos, pos, parsedStates, parseResult))
          return new ParseAlternative(curTextPos, pos, (pos < 0 ? parseResult.MaxFailPos : pos) - curTextPos + parentsEat, pos < 0 ? parseResult.MaxFailPos : 0, state, 0);
      }
      while (state >= 0);

      return new ParseAlternative(curTextPos, curTextPos, parentsEat, 0, -1, 0);
    }

    private static int ParentsMaxEat(RecoveryStackFrame frame, int curTextPos)
    {
      return frame.Children.Max(c => c.ParseAlternatives.Length == 0 
        ? 0
        : c.ParseAlternatives.Max(a => a.End == curTextPos ? a.ParentsEat : 0));
    }

    #endregion

    #region Спекулятивный поиск фреймов

    private void FindSpeculativeFrames(HashSet<RecoveryStackFrame> newFrames, ParseResult parseResult, RecoveryStackFrame frame, int failPos, int skipCount)
    {
      if (frame.IsTokenRule || frame.IsInsideToken) // не спекулируем кишки токенов
        return;

      if (frame.Id == 182)
        Debug.Assert(true);

      if (frame.Depth == 0)
      {
        var textPos = frame.TextPos;
        var parseAlternatives = ParseTopFrame(parseResult, frame, skipCount);
        foreach (var parseAlternative in parseAlternatives)
          // Не спекулировать на правилах которые что-то парсят. Такое может случиться после пропуска грязи.
          if ((skipCount > 0 || parseAlternative.Skip > 0) && parseAlternative.End > 0 && parseAlternative.End > textPos)
            return;
      }

      if (!frame.IsPrefixParsed) // пытаемся восстановить пропущенный разделитель списка
      {
        switch (frame.Id)
        {
          case 69: break;
        }
        var bodyFrame = frame.GetLoopBodyFrameForSeparatorState(failPos, parseResult);

        if (bodyFrame != null)
        {
          switch (bodyFrame.Id)
          {
            case 181: break;
            case 256: break;
          }
          // Нас просят попробовать востановить отстуствующий разделитель цикла. Чтобы знать, нужно ли это дела, или мы
          // имеем дело с банальным концом цикла мы должны
          Debug.Assert(bodyFrame.Parents.Count == 1);
          var newFramesCount = newFrames.Count;
          FindSpeculativeFrames(newFrames, parseResult, bodyFrame, failPos, skipCount);
          if (newFrames.Count > newFramesCount)
            return;
        }
      } 
      for (var state = frame.Depth == 0 ? frame.FailState : frame.GetNextState(frame.FailState); state >= 0; state = frame.GetNextState(state))
        FindSpeculativeSubframes(newFrames, parseResult, frame, failPos, state, skipCount);
    }

    protected virtual void FindSpeculativeSubframes(HashSet<RecoveryStackFrame> newFrames, ParseResult parseResult, RecoveryStackFrame frame, int failPos, int state, int skipCount)
    {
      if (failPos != frame.TextPos)
        return;

      switch (frame.Id)
      {
        case 69: break;
        case 181: break;
      }

      foreach (var subFrame in frame.GetSpeculativeFramesForState(failPos, parseResult, state))
      {
        if (subFrame.IsTokenRule)
          continue;

        switch (subFrame.Id)
        {
          case 181: break;
          case 256: break;
        }

        if (!newFrames.Add(subFrame))
          continue;

        FindSpeculativeSubframes(newFrames, parseResult, subFrame, failPos, subFrame.FailState, skipCount);
      }
    }
    
    #endregion

    #region Модификация AST (FixAst)

    void MarkRecoveryNodes(ParseAlternativeNode node, ParseAlternativeNode mark, Dictionary<ParseAlternativeNode, HashSet<ParseAlternativeNode>> markers)
    {
      HashSet<ParseAlternativeNode> markSet;
      if (markers.TryGetValue(node, out markSet))
      {
        if (markSet.Contains(mark))
          return;
      }
      else
      {
        markSet = new HashSet<ParseAlternativeNode>();
        markers[node] = markSet;
      }
      markSet.Add(mark);
      foreach (var child in node.Children)
        if (child.Best)
          MarkRecoveryNodes(child, mark, markers);
    }

    // ReSharper disable once ParameterTypeCanBeEnumerable.Local
    private void FixAst(List<ParseAlternativeNode> bestNodes, int failPos, int skipCount, ParseResult parseResult)
    {
      foreach (var node in bestNodes)
        if (node.Frame.TextPos + skipCount != node.ParseAlternative.Start - node.ParseAlternative.Skip)
          Debug.Assert(false);

      parseResult.MaxFailPos = failPos;
      parseResult.RecoveryStacks.Clear();
      if (bestNodes.Count == 0)
        return;

      var allNodes = bestNodes.UpdateDepthAndCollectAllNodes();
      var nodesForError = ParseAlternativeNode.CloneGraph(allNodes).ToArray();//сделать клонирование

      bestNodes = bestNodes.GroupBy(node => node.Frame).Select(group => group.First()).ToList();
      allNodes = bestNodes.UpdateReverseDepthAndCollectAllNodes();

      allNodes = FilterNodesForFix(ref bestNodes, allNodes);

      Debug.Assert(bestNodes.Count > 0);
      Debug.Assert(bestNodes.Aggregate((n1, n2) => n1 != null && n1.ParseAlternative.Skip == n2.ParseAlternative.Skip ? n2 : null) != null);

      // формируем ошибки начало
      var totalSkip = skipCount + bestNodes[0].ParseAlternative.Skip;
      var loc = new Location(parseResult.OriginalSource, new NSpan(failPos, failPos + totalSkip));
      if (totalSkip > 0)
        parseResult.ReportError(new UnexpectedTokenError(loc));
      TryAddErrorsForMissedSeparators(parseResult, loc, allNodes);

      // TODO: Надо пдумать об автоматическом обновлении MinSkipedMandatoryTokenCount
      ParseAlternativeNode.DownToTop(allNodes, CalcMinSkipedMandatoryTokenCount);

      if (bestNodes.All(n => n.MinSkipedMandatoryTokenCount != 0))
        parseResult.ReportError(new ExpectedRulesError(loc, nodesForError));

      // формируем ошибки конец

      foreach (var node in bestNodes)
      {
        var errorIndex = parseResult.ErrorData.Count;
        var parseErrorData = new ParseErrorData(new NSpan(failPos, failPos + skipCount + node.ParseAlternative.Skip));
        parseResult.ErrorData.Add(parseErrorData);
        if (!node.PatchAst(errorIndex, parseResult))
          RecoveryUtils.ResetParentsBestProperty(node.Parents);
        node.Best = false;
      }

      for (int i = 0; i < allNodes.Count; ++i)//последним идет корень. Его фиксить не надо
      {
        var node = allNodes[i];
        if (node.Best && !(node.Frame is RecoveryStackFrame.Root))
          if (!node.ContinueParse(parseResult))
            RecoveryUtils.ResetParentsBestProperty(node.Parents);
      }
    }

    // ReSharper disable once ParameterTypeCanBeEnumerable.Local
    /// <summary>Костыли и подпорки для обхода проблем возникающих от восстановления разделителя цикла</summary>
    private static void TryAddErrorsForMissedSeparators(ParseResult parseResult, Location loc, List<ParseAlternativeNode> allNodes)
    {
      var missedSeparators = allNodes.Where(n => n.MissedSeparator != null);

      // TODO: Может ли у нас быть более одного восстановленного разделителя? Например, в альтернативных ветках?
      foreach (var node in missedSeparators)
      {
        var missedSeparator = node.MissedSeparator;
        var errorPos = missedSeparator.Frame.StartPos;
        parseResult.ReportError(new ExpectedRulesError(loc, new[] { missedSeparator }));
        parseResult.ErrorData.Add(new ParseErrorData(new NSpan(errorPos, errorPos)));
        node.MakeMissedSeparator(parseResult);
        missedSeparator.Best = true;
      }
    }

    private List<ParseAlternativeNode> FilterNodesForFix(ref List<ParseAlternativeNode> bestNodes, List<ParseAlternativeNode> allNodes)
    {
      var markers = new Dictionary<ParseAlternativeNode, HashSet<ParseAlternativeNode>>();
      for (int i = allNodes.Count - 1; i >= 0; --i)
      {
        var node = allNodes[i];
        var frame = node.Frame;
        if (node.HasAtLeastTwoChildren)
        {
          if (frame is RecoveryStackFrame.ExtensiblePrefix || frame is RecoveryStackFrame.ExtensiblePostfix)
          {
            foreach (var group in node.Children.Where(n => n.Best).GroupBy(n => n.Frame.RuleKey))
            {
              var children = new List<ParseAlternativeNode>(group);
              if (children.Count >= 2)
                foreach (var child in children)
                  MarkRecoveryNodes(child, child, markers);
            }
          }
          else
          {
            foreach (var child in node.Children)
              MarkRecoveryNodes(child, child, markers);
          }
        }
      }

      if (markers.Count > 0)
      {
        bestNodes = bestNodes
          .GroupBy(node => markers[node], HashSet<ParseAlternativeNode>.CreateSetComparer())
          .Select(g => new List<ParseAlternativeNode>(g))
          .OrderBy(g => g.Count)
          .First();
        allNodes = bestNodes.UpdateReverseDepthAndCollectAllNodes();
      }
      return allNodes;
    }

    #endregion
  }

#region Utility methods

  public static class RecoveryUtils
  {
    public static List<T> FilterMax<T>(this SCG.ICollection<T> candidates, Func<T, int> selector)
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

    public static List<T> FilterMin<T>(this SCG.ICollection<T> candidates, Func<T, int> selector)
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

      CheckGraph(allFrames);

      RemoveOthrHeads(allFrames, head);

      CheckGraph(allFrames);

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
            {
              if (frame.Id == 321)
              {
              }
              frame.Best = false;
            }
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

    public static void RemoveOthrHeads(List<RecoveryStackFrame> allFrames, RecoveryStackFrame livingHead)
    {
      livingHead.IsMarked = true;

      PropageteMarkeds(allFrames);
    }

    public static void RemoveOthrHeads(List<RecoveryStackFrame> allFrames, List<RecoveryStackFrame> livingHeads)
    {
      foreach (var livingHead in livingHeads)
        livingHead.IsMarked = true;

      PropageteMarkeds(allFrames);
    }

    private static void PropageteMarkeds(List<RecoveryStackFrame> allFrames)
    {
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
        if (!frame.IsMarked && frame.Id == 321)
        {
        }

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
            {
              if (frame.Id == 321)
              {
              }
              frame.Best = false;
            }
          }
        }
      }
    }

    public static void CheckGraph(List<RecoveryStackFrame> allFrames, List<RecoveryStackFrame> bestFrames = null)
    {
      var setBest = new HashSet<RecoveryStackFrame>();

      if (bestFrames != null)
      {
        setBest.UnionWith(bestFrames);

        foreach (var frame in bestFrames)
        {
          if (!frame.IsTop)
            Debug.Assert(false);
        }
      }

      var setAll = new HashSet<RecoveryStackFrame>();

      foreach (var frame in allFrames)
        if (frame.Best)
          if (!setAll.Add(frame))
            Debug.Assert(false);


      foreach (var frame in allFrames)
      {
        if (!frame.Best)
          continue;

        var hasNoChildren = true;

        foreach (var child in frame.Children)
        {
          if (!child.Best)
            continue;

          hasNoChildren = false;

          if (!setAll.Contains(child))
            Debug.Assert(false);

          if (!child.Parents.Contains(frame))
            Debug.Assert(false);
        }

        if (hasNoChildren && bestFrames != null)
          if (!setBest.Contains(frame))
            Debug.Assert(false);
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

        if (frame.Id == 308)
        {
        }

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

    public static List<ParseAlternativeNode> UpdateReverseDepthAndCollectAllNodes(this SCG.ICollection<ParseAlternativeNode> heads)
    {
      var allNodes = new List<ParseAlternativeNode>();

      foreach (var node in heads)
        node.ClearAndCollectNodes(allNodes);
      foreach (var node in heads)
        node.UpdateNodeReverseDepth();

      allNodes.Sort((l, r) => -l.Depth.CompareTo(r.Depth));

      return allNodes;
    }

    public static List<ParseAlternativeNode> UpdateDepthAndCollectAllNodes(this List<ParseAlternativeNode> heads)
    {
      var allNodes = new List<ParseAlternativeNode>();

      foreach (var node in heads)
        node.ClearAndCollectNodes(allNodes);
      foreach (var node in heads)
        node.Depth = 0;
      foreach (var node in heads)
        node.UpdateDepth();

      allNodes.Sort((l, r) => l.Depth.CompareTo(r.Depth));

      return allNodes;
    }

    public static void UpdateDepth(this ParseAlternativeNode node)
    {
      foreach (var parent in node.Parents)
        if (parent.Depth <= node.Depth + 1)
        {
          parent.Depth = node.Depth + 1;
          UpdateDepth(parent);
        }
    }

    public static List<RecoveryStackFrame> UpdateDepthAndCollectAllFrames(this SCG.ICollection<RecoveryStackFrame> heads)
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

    public static List<RecoveryStackFrame> PrepareRecoveryStacks(this SCG.ICollection<RecoveryStackFrame> heads)
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
          else
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
          if (parent.Depth > 200)
            Debug.Assert(true);
          UpdateFrameDepth(parent);
        }
    }

    private static void ClearAndCollectNodes(this ParseAlternativeNode node, List<ParseAlternativeNode> allNodes)
    {
      if (node.Depth != -1)
      {
        allNodes.Add(node);
        node.Depth = -1;
        foreach (var parent in node.Parents)
          ClearAndCollectNodes(parent, allNodes);
        //if (node.MissedSeparator != null)
        //  ClearAndCollectNodes(node.MissedSeparator, allNodes);
      }
    }

    private static void UpdateNodeReverseDepth(this ParseAlternativeNode node)
    {
      if (!node.HasParents)
        node.Depth = 0;
      else
      {
        foreach (var parent in node.Parents)
          if (parent.Depth == -1)
            UpdateNodeReverseDepth(parent);
        node.Depth = node.Parents.Max(x => x.Depth) + 1;
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

    public static bool NonVoidParsed(this RecoveryStackFrame frame, int curTextPos, int pos, List<ParsedStateInfo> parsedStates, ParseResult parseResult)
    {
      var lastPos = Math.Max(pos, parseResult.MaxFailPos);
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
        res0 = frame.ParseAlternatives.Where(alternative => parentStarts.Contains(alternative.Stop)).ToList();
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
      foreach (var p in child.ParseAlternatives)
        if (p.Stop == end)
          return true;

      return false;
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

      var maxEnd  = alternatives.Max(a => a.End);
      var maxFail = alternatives.Max(a => a.Fail);
      if (maxEnd >= 0 && maxEnd < maxFail)
        Debug.Assert(false);

      if (alternatives.Any(a => a.End >= 0))
        return alternatives.FilterMax(f => f.End);

      return alternatives.FilterMax(f => f.Fail);
    }

    public static List<RecoveryStackFrame> FilterEmptyChildren(List<RecoveryStackFrame> children5, int skipCount)
    {
      return SubstractSet(children5, children5.Where(f => 
        f.StartPos == f.TextPos
        && f.ParseAlternatives.All(a => f.TextPos + skipCount == a.Start && a.ParentsEat == 0 && a.State < 0 && f.FailState2 == 0)).ToList());
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

    public static List<RecoveryStackFrame> FilterEmptyChildrenWhenFailSateCanParseEmptySting(RecoveryStackFrame frame, List<RecoveryStackFrame> frames, int skipCount)
    {
      if (frame.IsSateCanParseEmptyString(frame.FailState))
      {
        var result = frames.Where(f => f.ParseAlternatives.Any(a => a.ParentsEat != 0 || frame.TextPos + skipCount < a.Start)).ToList();
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
        return bestFrames.Where(f => f.ParseAlternatives.Any(a => a.State == f.FailState)).ToList();

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
      foreach (var a in frame.ParseAlternatives)
        if (a.State == failState)
          return true;
      return false;
    }

    public static List<RecoveryStackFrame> SubstractSet(List<RecoveryStackFrame> set1, SCG.ICollection<RecoveryStackFrame> set2)
    {
      return set1.Where(c => !set2.Contains(c)).ToList();
    }

    public static void ResetChildrenBestProperty(List<RecoveryStackFrame> poorerChildren)
    {
      foreach (var child in poorerChildren)
        if (child.Best)
        {
          if (child.Id == 321)
          {
          }
          child.Best = false;
          ResetChildrenBestProperty(child.Children);
        }
    }

    public static void ResetParentsBestProperty(IEnumerable<ParseAlternativeNode> parents)
    {
      foreach (var node in parents)
        if (node.Best)
        {
          node.Best = false;
          ResetParentsBestProperty(node.Parents);
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

    public static HashSet<int> Stops(this RecoveryStackFrame frame)
    {
      var stops = new HashSet<int>();
      foreach (var a in frame.ParseAlternatives)
        stops.Add(a.Stop);

      return stops;
    }

    public static bool IsExistsNotFailedAlternatives(RecoveryStackFrame frame)
    {
      return frame.Children.Any(f => f.ParseAlternatives.Any(a => a.End >= 0));
    }

    public static List<ParseAlternative> FilterNotFailedParseAlternatives(List<ParseAlternative> alternatives0)
    {
      return alternatives0.Where(a => a.End >= 0).ToList();
    }
  }

  public static class ParseAlternativesVisializer
  {
    #region HtmlTemplate
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
    #endregion

    static readonly XAttribute _garbageClass      = new XAttribute("class", "garbage");
    static readonly XAttribute _topClass          = new XAttribute("class", "parsed");
    static readonly XAttribute _prefixClass       = new XAttribute("class", "prefix");
    static readonly XAttribute _postfixClass      = new XAttribute("class", "postfix");
    static readonly XAttribute _skipedStateClass  = new XAttribute("class", "skipedState");
    static readonly XAttribute _default           = new XAttribute("class", "default");

    static readonly XElement  _start              = new XElement("span", _default, "▸");
    static readonly XElement  _end                = new XElement("span", _default, "◂");

    /// <summary>
    /// Формирует HTML-файл графически описывающий варианты продолжения прасинга из графа и открывает его в бруозере исползуемом по умолчанию.
    /// </summary>
    public static void PrintParseAlternatives(List<RecoveryStackFrame> bestFrames, List<RecoveryStackFrame> allFrames, ParseResult parseResult, int skipCount, string msg = null)
    {
      RecoveryUtils.UpdateParseAlternativesTopToDown(allFrames);
      var nodes = ParseAlternativeNode.MakeGraph(bestFrames);

      PrintParseAlternatives(parseResult, nodes, skipCount, msg);
    }

    public static void PrintParseAlternatives(ParseResult parseResult, List<ParseAlternativeNode> nodes, int skipCount, string msg = null)
    {
      var results = new List<XNode> { new XText(parseResult.DebugText + "\r\n\r\n") };
      var alternativesCount = 0;
      var topNodes = nodes.Where(n => n.IsTop).ToList();

      foreach (var g in topNodes.GroupBy(n => n.Frame))
      {
        results.Add(new XText("\r\n"));
        results.Add(new XElement("span", g.Key, ":\r\n"));

        foreach (var node in g)
        {
          if (!node.Best)
            continue;

          var result = node.GetHtml(skipCount);
          results.AddRange(result);
          alternativesCount += result.Count;
        }
      }

      results.Insert(0, new XText(msg + " " + alternativesCount + " alternatives.\r\n\r\n"));

      var template = XElement.Parse(HtmlTemplate);
      var content = template.Descendants("content").First();
      Debug.Assert(content.Parent != null);
      content.Parent.ReplaceAll(results);
      var filePath = Path.ChangeExtension(Path.GetTempFileName(), ".html");
      template.Save(filePath);
      Process.Start(filePath);
    }

    public static List<XElement> GetHtml(this ParseAlternativeNode node, int skipCount)
    {
      var results = new List<XElement>();
      var paths = node.GetFlatParseAlternatives();

      if (paths.Count == 2)
      {
        var x = paths[0];
        var y = paths[1];
        for (; !x.IsEmpty && !y.IsEmpty; x = x.Tail, y = y.Tail)
        {
          if (x.Head != y.Head)
          {
          }
        }
      }

      if (node.Frame.Id == 83)
      {
      }

      foreach (var path in paths)
        results.Add(MakeHtml(path, skipCount));

      return results;
    }

    private static XElement MakeHtml(ParseAlternativeNodes nodes, int skipCount)
    {
      XElement content = null;
      XElement missedSeparator = null;
      var skippedTokenCount = 0;
      var id = nodes.IsEmpty ? "???" : nodes.Head.Id.ToString(CultureInfo.InvariantCulture);
      var minSkip = nodes.IsEmpty ? 0 : nodes.Head.MinSkipedMandatoryTokenCount;
      

      while (true)
      {
        if (nodes.IsEmpty)
          return new XElement("span", id + " " + skippedTokenCount + " (" + minSkip + ") " + " skipped ", content);

        var node = nodes.Head;
        var a = node.ParseAlternative;
        var frame = node.Frame;
        var parsingFailAtState = frame.FailState2;
        var recursionState = frame.FailState;
        var isTop = node.IsTop;
        var text = frame.ParseResult.Text;

        skippedTokenCount += node.SkipedMandatoryTokenCount;
        if (a.Skip > 0)
          skippedTokenCount++;

        var parsedClass = isTop ? _topClass : _postfixClass;

        var title = MakeTitle(node);

        var prefixText = text.Substring(frame.StartPos, frame.TextPos - frame.StartPos);
        var prefix = string.IsNullOrEmpty(prefixText) ? null : new XElement("span", _prefixClass, prefixText);

        var postfixText = text.Substring(a.Start, a.Stop - a.Start);
        var postfix = string.IsNullOrEmpty(postfixText) ? null : new XElement("span", isTop ? _topClass : _postfixClass, postfixText);

        XElement skippedPrefix = null;
        XElement skippedPostfix = null;

        var endState = a.State;

        var skippedText = a.Skip + skipCount > 0 ? new XElement("span", _garbageClass, text.Substring(frame.TextPos, a.Skip + skipCount)) : null;

        if (a.Skip > 0)
        {

        }

        if (isTop)
        {
          if (recursionState != endState)
            skippedPrefix = new XElement("span", _skipedStateClass, SkipedStatesCode(frame, parsingFailAtState, endState));
        }
        else
        {
          if (parsingFailAtState < recursionState)
          {
            skippedPrefix = new XElement("span", _skipedStateClass, SkipedStatesCode(frame, parsingFailAtState, recursionState));
          }

          var startState = frame.GetNextState(recursionState);

          if (startState >= 0 && (startState < endState || endState < 0))
            skippedPostfix = new XElement("span", _skipedStateClass, SkipedStatesCode(frame, startState, endState));
        }

        var fail = a.End < 0 ? new XElement("span", _garbageClass, "<FAIL>") : null;
        var span = new XElement("span", parsedClass, title, _start, prefix, skippedText, missedSeparator, skippedPrefix, content, skippedPostfix, postfix, _end, fail);

        var missed = node.MissedSeparator;

        if (missed != null)
        {
          if (node.Id == 4200)
          {
          }
          missedSeparator = new XElement("span", _skipedStateClass, _start, MakeTitle(missed), SkipedStatesCode(frame, missed.Frame.FirstState, -1), _end);
        }
        else
          missedSeparator = null;

        if (!node.HasParents)
          span.Add("\r\n");

        nodes = nodes.Tail;
        content = span;
      }

      throw new Exception("MakeHtml failed");
    }

    private static string SkipedStatesCode(RecoveryStackFrame frame, int startState, int endState)
    {
      return string.Join(" ", frame.CodeForStates(startState, endState, true));
    }

    private static XAttribute MakeTitle(ParseAlternativeNode node)
    {
      return new XAttribute("title", node);
    }
  }

#endregion
}
