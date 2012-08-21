using System;

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
  public sealed class ExtensibleRuleParser : RuleParser
  {
    public static const int ParsedAstOfs = 0; //ссылка на разобранное правило
    public static const int BestAstOfs   = 1; //ссылка на лучшее текущее правило
    public static const int AstSize      = 2;

    private int                   FirstPostfixRule;
    private ExtentionRuleParser[] PrefixRules;
    private ExtentionRuleParser[] PostfixRules;

    public ExtensibleRuleParser(ExtensibleRuleDescriptor descriptor, int bindingPower, CompositeGrammar grammar)
    {
      var rules = grammar.GetExtentionRules(descriptor);
      var postfixRules = rules[0];
      PrefixRules      = rules[1];
      PostfixRules     = rules[2];
      FirstPostfixRule = 0;
      for (; FirstPostfixRule < postfixRules.Length && bindingPower >= postfixRules[FirstPostfixRule].BindingPower; ++FirstPostfixRule);
    }

    public override void Init()
    {
    }

    public sealed override int Parse(int curEndPos, string text, int astPos, ref int[] ast)
    {
      unchecked
      {
        int newEndPos;
        int bestEndPos;
        int i;
        char c; // временная переменная для отсечения правил по первой букве

        if (astPos >= 0)
          goto start_parsing;
        else
          goto error_recovery;

start_parsing:
        //AST расширяемого правила проинлайнин в AST того правила которое его вызывает
        ast[astPos + ParsedAstOfs] = -1;
        ast[astPos + BestAstOfs]   = -1;

        if (curEndPos >= text.Length) // конец текста
          return -1;

        goto prefix_loop;

error_recovery:
        astPos = ~astPos;

        int bestAst = ast[astPos + BestAstOfs];
        if (bestAst < 0)
          return -1; // ни одно правило не съело ни одного токена // TODO: Сделать восстановление

        i = ast[bestAst] - PrefixRules[0].RuleId; // восстанавливаем состояние из Id правила которое было разобрано дальше всех.
        if (i < PrefixRules.Length)
        { // префикное правило
          RuleParser prefixRule = PrefixRules[i];
          curEndPos = prefixRule.Parse(curEndPos, text, ~astPos, ref ast);
        }
        else
        { // постфиксное правило
          i -= PrefixRules.Length;
          RuleParser postfixRule = PostfixRules[i];
          curEndPos = curEndPos + ast[ast[astPos + ParsedAstOfs] + 1]; // к стартовой позиции в тексте добавляем размер уже разобранного AST
          curEndPos = postfixRule.Parse(curEndPos, text, ~astPos, ref ast);
        }
        assert(curEndPos >= 0);
        goto postfix_loop;

prefix_loop:
        i = 0;
        c = text[curEndPos];
        bestEndPos = -1;
        for (; i < PrefixRules.Length; ++i)
        {
          var prefixRule = PrefixRules[i];
          if (prefixRule.LowerBound <= c && c <= prefixRule.UpperBound)
          {
            newEndPos = prefixRule.Parse(curEndPos, text, astPos, ref ast);
            if (newEndPos > 0)
              bestEndPos = newEndPos;
          }
        }

        curEndPos = bestEndPos;

        if (curEndPos < 0)// не смогли разобрать префикс
          return -1;

postfix_loop:
        bestEndPos = curEndPos;
        while (curEndPos < text.Length) // постфиксное правило которое не съело ни одного символа игнорируется
                                        // при достижении конца текста есть нечего
        {
          i = FirstPostfixRule;
          c = text[curEndPos];
          for (; i < PostfixRules.Length; ++i)
          {
            var postfixRule = PostfixRules[i];
            if (postfixRule.LowerBound <= c && c <= postfixRule.UpperBound)
            {
              newEndPos = postfixRule.Parse(curEndPos, text, astPos, ref ast);
              if (newEndPos > 0)
                bestEndPos = newEndPos;
            }
          }

          if (bestEndPos == curEndPos)
            break; // если нам не удалось продвинуться то заканчиваем разбор

          curEndPos = bestEndPos;
        }

        return curEndPos;
      }
    }
  }
}
