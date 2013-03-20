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
    }

    public override int Parse(int curEndPos, string text, ref Parser parser)
    {
      unchecked
      {
        int postfixAst;
        int prefixAst;
        int newEndPos;
        int newResult;
        int bestEndPos;
        int bestResult;
        int lastResult;
        int i;
        int j;
        char c; // временная переменная для отсечения правил по первой букве

        if (parser.IsRecoveryMode)
          goto error_recovery;

        if (curEndPos >= text.Length) // конец текста
          return -1;

        prefixAst = parser.memoize[curEndPos];
        for (; prefixAst > 0; prefixAst = parser.ast[prefixAst + PrefixOfs.Next])
        {
          if (parser.ast[prefixAst + PrefixOfs.Id] == PrefixId)
          {
            bestResult = parser.ast[prefixAst + PrefixOfs.List];
            if (bestResult > 0 && parser.ast[bestResult + AstOfs.State] == -1)
            {
              //TODO: убрать цикл
              i = bestResult + AstOfs.Sizes;
              for (; parser.ast[i] >= 0; ++i)
                curEndPos += parser.ast[i];
              bestEndPos = curEndPos;
              goto postfix_loop;
            }
            else
              return -1; // облом разбора
          }
        }

        //нет мемоизации префикса
        prefixAst = parser.Allocate(PrefixOfs.NodeSize, PrefixId);
        parser.ast[prefixAst + PrefixOfs.Next] = parser.memoize[curEndPos];
        parser.memoize[curEndPos] = prefixAst;
        i = 0;
        c = text[curEndPos];
        bestResult = 0;
        for (; i < PrefixRules.Length; ++i)
        {
          var prefixRule = PrefixRules[i];
          if (prefixRule.LowerBound <= c && c <= prefixRule.UpperBound)
          {
            newResult = -1;
            newEndPos = prefixRule.Parse(curEndPos, text, ref newResult, ref parser);
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

      postfix_loop:
        curEndPos = bestEndPos;
        if (curEndPos >= text.Length) // постфиксное правило которое не съело ни одного символа игнорируется
          return curEndPos;// при достижении конца текста есть нечего
        //ищем запомненое
        postfixAst = parser.memoize[curEndPos];
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
              if (bestResult > 0 && parser.ast[bestResult + AstOfs.State] == -1)//Убеждаемся что разбор успешный
              {
                bestEndPos = curEndPos;
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
              if (bestResult > 0 && parser.ast[bestResult + AstOfs.State] == -1)//Убеждаемся что разбор успешный
              {
                bestEndPos = curEndPos;
                //TODO: убрать цикл
                //вычисляем длинну разобранного правила
                j = bestResult + AstOfs.Sizes;
                while (true)
                {
                  var size = parser.ast[j];
                  if (size >= 0)
                    bestEndPos += size;
                  else
                    goto postfix_loop;//нашли терминатор. Парсим следующее правило.

                  ++j;
                }
              }
              else
                return curEndPos;//облом. Заканчиваем разбор.
            }
          }
        }
        //нет мемоизации
        postfixAst = parser.Allocate(PostfixOfs.NodeSize, PostfixId);
        parser.ast[postfixAst + PostfixOfs.Next] = parser.memoize[curEndPos];
        parser.memoize[curEndPos] = postfixAst;
        bestResult = 0;
        lastResult = 0;
        i = PostfixRules.Length - 1;
      postfix_parse:
        parser.ast[postfixAst + PostfixOfs.FirstRuleIndex] = FirstPostfixRule;
        c = text[curEndPos];
        for (; i >= FirstPostfixRule; --i)
        {
          var postfixRule = PostfixRules[i];
          if (postfixRule.LowerBound <= c && c <= postfixRule.UpperBound)
          {
            newResult = -1;
            newEndPos = postfixRule.Parse(curEndPos, text, ref newResult, ref parser);
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

        if (bestEndPos <= curEndPos)
          return curEndPos; // если нам не удалось продвинуться то заканчиваем разбор

        goto postfix_loop;
      }

      error_recovery:
        throw System.Exception();
      //resultPtr = ~resultPtr;

        //int bestAst = parser.ast[astPos + BestAstOfs];
      //if (bestAst < 0)
      //  return -1; // ни одно правило не съело ни одного токена // TODO: Сделать восстановление

        //i = parser.ast[bestAst] - PrefixRules[0].RuleId; // восстанавливаем состояние из Id правила которое было разобрано дальше всех.
      //if (i < PrefixRules.Length)
      //{ // префикное правило
      //  RuleParser prefixRule = PrefixRules[i];
      //  curEndPos = prefixRule.Parse(curEndPos, text, ~astPos, ref parser);
      //}
      //else
      //{ // постфиксное правило
      //  i -= PrefixRules.Length;
      //  RuleParser postfixRule = PostfixRules[i];
      //  curEndPos = curEndPos + parser.ast[parser.ast[astPos + ParsedAstOfs] + 1]; // к стартовой позиции в тексте добавляем размер уже разобранного AST
      //  curEndPos = postfixRule.Parse(curEndPos, text, ~astPos, ref parser);
      //}
      //assert(curEndPos >= 0);
      //goto postfix_loop;
      assert(false);
      return -1;
    }
  }
}
