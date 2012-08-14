using Nemerle;
using Nemerle.Imperative;
using Nemerle.Collections;
using Nemerle.Text;
using Nemerle.Utility;

using System;
using System.Collections.Generic;
using System.Linq;

//структура правила расширения.

//id
//размер узла
//состояние разбора -1 если правило полностью разобрано.
//размеры подправил
//...
//терминатор и флаги. Должен быть меньше 0
//ссылки на ast подправил
//...

namespace N2.Internal
{
  [Record]
  public sealed class ExtensibleRuleParser : RuleParser
  {
    public static const int ParsedAstOfs = 0; //ссылка на разобранное правило
    public static const int BestAstOfs   = 1; //ссылка на лучшее текущее правило
    public static const int AstSize      = 2;

    public int          SubrulesOffset { get; }
    public RuleParser[] PrefixRules { get; }
    public RuleParser[] PostfixRules { get; }

    public sealed override int Parse(int pos, string text, int astPos, ref int[] ast)
    {
      unchecked
      {
        int curEndPos;
        int newEndPos;
        int bestEndPos;
        int i;
        char c; // временная переменная для отсечения правил по первой букве

        if (astPos >= 0)
          goto start_parsing;
        else
          goto error_recovery;

start_parsing: // чистый разбор
        //AST расширяемого правила проинлайнин в AST того правила которое его вызывает
        ast[astPos + ParsedAstOfs] = -1;
        ast[astPos + BestAstOfs]   = -1;

        if (pos >= text.Length) // конец текста
          return -1;

        goto prefix_loop;

error_recovery: // восстановление после ошибки
        astPos = ~astPos;

        int bestAst = ast[astPos + BestAstOfs];
        if (bestAst < 0)
          return -1; // не смогли восстановиться

        i = ast[bestAst] - SubrulesOffset; // восстанавливаем состояние из Id правила которое было разобрано дальше всех.
        if (i < PrefixRules.Length)
        { // префикное правило.
          RuleParser prefixRule = PrefixRules[i];
          curEndPos = prefixRule.Parse(pos, text, ~astPos, ref ast);
        }
        else
        { // постфиксное правило
          i -= PrefixRules.Length;
          RuleParser postfixRule = PostfixRules[i];
          curEndPos = pos + ast[ast[astPos + ParsedAstOfs] + 1]; // к стартовой позиции в тексте добавляем размер уже разобранного AST
          curEndPos = postfixRule.Parse(curEndPos, text, ~astPos, ref ast);
        }
        goto postfix_loop;

prefix_loop:
        i = 0;
        c = text[pos];
        for (; i < PrefixRules.Length; ++i)
        {
          RuleParser prefixRule = PrefixRules[i];
          if (prefixRule.LowerBound <= c && c <= prefixRule.UpperBound)
          {
            newEndPos = prefixRule.Parse(pos, text, astPos, ref ast);
            if (newEndPos > 0)
              curEndPos = newEndPos;
          }
        }

        ast[astPos + StateOfs] = i;
        ast[astPos + SizeOfs]  = curEndPos - pos;

postfix_loop:
        if (curEndPos < 0)// не смогли разобрать префикс
          return -1;

        bestEndPos = curEndPos;
        while (curEndPos < text.Length) // постфиксное правило которое не съело ни одного символа игнорируется
                                        // при достижении конца текста есть нечего
        {
          i = 0;
          c = text[curEndPos];
          for (; i < PostfixRules.Length; ++i)
          {
            RuleParser postfixRule = PostfixRules[i];
            if (postfixRule.LowerBound <= c && c <= postfixRule.UpperBound)
            {
              newEndPos = postfixRule.Parse(curEndPos, text, astPos, ref ast);
              if (newEndPos > 0)
                bestEndPos = newEndPos;
            }
          }

          if (bestEndPos == curEndPos)
            break; // если нам не удалось продвинутся то заканчиваем разбор

          curEndPos = bestEndPos;
        }

        return curEndPos;
      }
    }
  }
}
