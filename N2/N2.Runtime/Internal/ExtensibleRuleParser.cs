using System;
using System.Diagnostics;
using N2.Runtime;

//структура правила расширения.

// 0         id
// 1         размер узла
// 2         состояние разбора -1 если правило полностью разобрано.
// 3         размеры подправил
//           ...
// 3 + n     терминатор и флаги. Должен быть меньше 0
// 3 + n + 1 ссылки на AST или инлайн AST подправил
//           ...


namespace N2.Internal
{
#if !PARSER_DEBUG
  //[DebuggerStepThroughAttribute]
#endif
  public sealed partial class ExtensibleRuleParser : StartRuleParser
  {
    public static class AstOfs
    {
      public static const int Id = 0;
      public static const int Next = 1;
      public static const int State = 2;
      public static const int Sizes = 3;
    }

    public static class PrefixOfs
    {
      public static const int Id = 0;
      public static const int Next = 1;
      public static const int List = 2;
      public static const int NodeSize = 3;
    }

    public static class PostfixOfs
    {
      public static const int Id = 0;
      public static const int Next = 1;
      public static const int AstList = 2;
      public static const int FirstRuleIndex = 3;
      public static const int NodeSize = 4;
    }

    public static class PostfixAstOfs
    {
      public static const int Id = 0;
      public static const int Next = 1;
    }

    public static class PostfixMark
    {
      public static const int Bad   = 0 << 30;
      public static const int Equal = 1 << 30;
      public static const int Best  = 2 << 30;
    }

    public static class PostfixMask
    {
      public static const int Id   = ~(3 << 30);
      public static const int Mark =  (3 << 30);
    }

    public readonly int BindingPower;
    public readonly int PrefixId;
    public readonly int PostfixId;

    public readonly int FirstPostfixRule;
    public readonly int FirstPostfixRuleId;
    public readonly ExtentionRuleParser[] PrefixRules;
    public readonly ExtentionRuleParser[] PostfixRules;
    public readonly int PrefixOffset;
    public readonly int PostfixOffset;

    public ExtensibleRuleParser(ExtensibleRuleParserData parserData, int bindingPower)
      : base(parserData.Grammar, parserData.Descriptor)
    {
      BindingPower     = bindingPower;
      PrefixId         = parserData.PrefixId;
      PostfixId        = parserData.PostfixId;
      PrefixRules      = parserData.PrefixParsers;
      PostfixRules     = parserData.PostfixParsers;
      FirstPostfixRule = 0;
      var postfixRules = parserData.PostfixDescriptors;
      while (FirstPostfixRule < postfixRules.Length && bindingPower >= postfixRules[FirstPostfixRule].BindingPower)
        ++FirstPostfixRule;
      if (PostfixRules.Length > 0)
      {
        if (FirstPostfixRule == PostfixRules.Length)
          FirstPostfixRuleId = int.MaxValue;
        else
          FirstPostfixRuleId = PostfixRules[FirstPostfixRule].RuleId;
      }
      else
        FirstPostfixRuleId = int.MaxValue;
      if (PrefixRules.Length > 0)  PrefixOffset   = PrefixRules[0].RuleId;  else PrefixOffset   = 0;
      if (PostfixRules.Length > 0) PostfixOffset  = PostfixRules[0].RuleId; else PostfixOffset  = 0;
    }

    public override int Parse(int curTextPos, string text, Parser parser)
    {
      int bestPos;
      curTextPos = ParsePrefix(curTextPos, text, parser);
      if (curTextPos > 0)
      {
        do
        {
          bestPos = curTextPos;
          curTextPos = ParsePostfix(curTextPos, text, parser);
        }
        while (curTextPos > bestPos);
        return bestPos;
      }
      else
        return -1;
    }

    public int ParsePrefix(int curTextPos, string text, Parser parser)
    {
      unchecked
      {
        int prefixAst;
        int newEndPos;
        int newResult;
        int bestEndPos;
        int bestResult;
        int i;
        int j;
        char c; // временная переменная для отсечения правил по первой букве

        prefixAst = parser.memoize[curTextPos];
        for (; prefixAst > 0; prefixAst = parser.ast[prefixAst + PrefixOfs.Next])
        {
          if (parser.ast[prefixAst + PrefixOfs.Id] == PrefixId)
          {
            bestResult = parser.ast[prefixAst + PrefixOfs.List];
            if (bestResult > 0)
            {
              int state = parser.ast[bestResult + AstOfs.State];
              if (state == Parser.AstParsedState)
              {
                //TODO: убрать цикл
                i = bestResult + AstOfs.Sizes;
                for (; parser.ast[i] >= 0; ++i)
                  curTextPos += parser.ast[i];
                bestEndPos = curTextPos;
                return curTextPos;
              }
              else if (state < 0)
              {
                parser.ast[prefixAst + PrefixOfs.Next] = 0;//FIXME. обрпботать неоднозначности.
                var prefixRule = PrefixRules[parser.ast[prefixAst + PrefixOfs.Id] - PrefixOffset];
                return prefixRule.Parse(curTextPos, text, ref newResult, parser);
              }
            }
            return -1; // облом разбора
          }
        }

        assert2(parser.ParsingMode == ParsingMode.Parsing);

        //нет мемоизации префикса
        prefixAst = parser.Allocate(PrefixOfs.NodeSize, PrefixId);
        parser.ast[prefixAst + PrefixOfs.Next] = parser.memoize[curTextPos];
        parser.memoize[curTextPos] = prefixAst;
        if (curTextPos >= text.Length)
          return -1;
        i = 0;
        c = text[curTextPos];
        bestResult = 0;
        for (; i < PrefixRules.Length; ++i)
        {
          var prefixRule = PrefixRules[i];
          if (prefixRule.LowerBound <= c && c <= prefixRule.UpperBound)
          {
            newResult = -1;
            newEndPos = prefixRule.Parse(curTextPos, text, ref newResult, parser);
            if (newResult > 0)
            {
              if (bestResult > 0)
              {
                if (bestEndPos < 0) { if (newEndPos >= 0) goto prefix_new_better; }
                else                { if (newEndPos < 0)  goto prefix_best_better; }
                j = AstOfs.Sizes;
                for (; true; ++j)
                {
                  var newSize  = parser.ast[newResult + j];
                  var bestSize = parser.ast[bestResult + j];
                  if (newSize < 0)
                  {
                    if (bestSize < 0)
                      goto prefix_equal;
                    else
                      goto prefix_best_better;
                  }
                  if (bestSize < newSize)
                    goto prefix_new_better;
                  if (bestSize > newSize)
                    goto prefix_best_better;
                }
              }
              else
                goto prefix_new_better;
            prefix_equal://АСТ равен лучшему. Неоднозначность.
              parser.ast[newResult + AstOfs.Next] = bestResult;
              bestResult = newResult;
              assert(bestEndPos == newEndPos);
              continue;
            prefix_new_better://Новый АСТ лучше
              bestResult = newResult;
              bestEndPos = newEndPos;
              continue;
            prefix_best_better:
              continue;
            }
          }
        }

        parser.ast[prefixAst + PrefixOfs.List] = bestResult;

        if (bestResult <= 0 || bestEndPos < 0)// не смогли разобрать префикс
          return -1;
        return bestEndPos;
      }
    }

    public int ParsePostfix(int curTextPos, string text, Parser parser)
    {
      unchecked
      {
        int postfixAst;
        int newEndPos;
        int newResult;
        int bestEndPos= curTextPos;
        int bestResult= 0;
        int lastResult= 0;
        int i;
        int j;
        char c; // временная переменная для отсечения правил по первой букве

        if (curTextPos >= text.Length) // постфиксное правило которое не съело ни одного символа игнорируется
          return curTextPos;// при достижении конца текста есть нечего
        //ищем запомненое
        postfixAst = parser.memoize[curTextPos];
        for (; postfixAst > 0; postfixAst = parser.ast[postfixAst + PostfixOfs.Next])
        {
          if (parser.ast[postfixAst + PostfixOfs.Id] == PostfixId)//нашли
          {
            lastResult = parser.ast[postfixAst + PostfixOfs.AstList];//список разобраных с этого места правил
            bestResult = lastResult;
            i = parser.ast[postfixAst + PostfixOfs.FirstRuleIndex] - 1;//индекс первого не разобранного правила
            if (i >= FirstPostfixRule)// не всё разобрано
            {
              //ищем лучшее правило
              while (bestResult > 0 && (parser.ast[bestResult] & PostfixMask.Mark) != PostfixMark.Best)
                bestResult = parser.ast[bestResult + PostfixAstOfs.Next];
              if (bestResult > 0 && parser.ast[bestResult + AstOfs.State] == Parser.AstParsedState)//Убеждаемся что разбор успешный
              {
                bestEndPos = curTextPos;
                //TODO: убрать цикл
                //вычисляем длинну разобранного правила
                j = bestResult + AstOfs.Sizes;
                while (true)
                {
                  var size = parser.ast[j];
                  if (size >= 0)
                    bestEndPos += size;
                  else
                    break;//нашли терминатор.

                  ++j;
                }
              }
              else
                bestEndPos = -1;
              goto postfix_parse;//парсим то что не распарсили раньше
            }
            else
            {
              // пропускаем правила с низкой силой связывания.
              while (bestResult > 0 && (parser.ast[bestResult] & PostfixMask.Id) < FirstPostfixRuleId)
                bestResult = parser.ast[bestResult + PostfixAstOfs.Next];
              // ищем лучшее правило среди тех у кого подходящая сила связывания.
              while (bestResult > 0 && (parser.ast[bestResult] & PostfixMask.Mark) != PostfixMark.Best)
                bestResult = parser.ast[bestResult + PostfixAstOfs.Next];
              if (bestResult > 0 && parser.ast[bestResult + AstOfs.State] == Parser.AstParsedState)//Убеждаемся что разбор успешный
              {
                bestEndPos = curTextPos;
                //TODO: убрать цикл
                //вычисляем длинну разобранного правила
                j = bestResult + AstOfs.Sizes;
                while (true)
                {
                  var size = parser.ast[j];
                  if (size >= 0)
                    bestEndPos += size;
                  else
                    return bestEndPos;//нашли терминатор. Парсим следующее правило.

                  ++j;
                }
              }
              else
                return curTextPos;//облом. Заканчиваем разбор.
            }
          }
        }
        //нет мемоизации
        postfixAst = parser.Allocate(PostfixOfs.NodeSize, PostfixId);
        parser.ast[postfixAst + PostfixOfs.Next] = parser.memoize[curTextPos];
        parser.memoize[curTextPos] = postfixAst;
        bestResult = 0;
        lastResult = 0;
        i = PostfixRules.Length - 1;
      postfix_parse:
        parser.ast[postfixAst + PostfixOfs.FirstRuleIndex] = FirstPostfixRule;
        c = text[curTextPos];
        for (; i >= FirstPostfixRule; --i)
        {
          var postfixRule = PostfixRules[i];
          if (postfixRule.LowerBound <= c && c <= postfixRule.UpperBound)
          {
            newResult = -1;
            newEndPos = postfixRule.Parse(curTextPos, text, ref newResult, parser);
            if (newResult > 0)//АСТ создано
            {
              parser.ast[newResult + AstOfs.Next] = lastResult; lastResult = newResult;//добавляем в список
              //определяем является ли даный АСТ лучшим из тех что разобрали
              if (bestResult > 0)
              {
                if (bestEndPos < 0) { if (newEndPos >= 0) goto postfix_new_better; }
                else                { if (newEndPos < 0)  goto postfix_best_better; }
                j = AstOfs.Sizes;
                for (; true; ++j)
                {
                  var newSize  = parser.ast[newResult + j];
                  var bestSize = parser.ast[bestResult + j];
                  if (newSize < 0)
                  {
                    if (bestSize < 0)
                      goto postfix_equal;
                    else
                      goto postfix_best_better;
                  }
                  if (bestSize < newSize)
                    goto postfix_new_better;
                  if (bestSize > newSize)
                    goto postfix_best_better;
                }
              }
              else
                goto postfix_new_better;
            postfix_equal://АСТ равен лучшему. Неоднозначность.
              parser.ast[newResult] = parser.ast[newResult] | PostfixMark.Equal;
              assert(bestEndPos == newEndPos);
              continue;
            postfix_new_better://Новый АСТ лучше
              bestEndPos = newEndPos;
              bestResult = newResult;
              parser.ast[newResult] = parser.ast[newResult] | PostfixMark.Best;
              continue;
            postfix_best_better:
              continue;
            }
          }
        }

        parser.ast[postfixAst + PostfixOfs.AstList] = lastResult;

        if (bestEndPos <= curTextPos)
          return curTextPos;
        else
          return bestEndPos;
      }
    }
  }
}
