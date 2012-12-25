using System;
using System.Diagnostics;

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
  [DebuggerStepThroughAttribute]
#endif
  public sealed class ExtensibleRuleParser : RuleParser
  {
    public static const int IdOfs      = 0;
    public static const int SizeOfs    = 1;
    public static const int StateOfs   = 2;
    public static const int AstOfs     = 3; //ссылка на разобранное правило
    public static const int BestAstOfs = 4; //ссылка на лучшее текущее правило

    public static const int PrefixOfs  = 2;

    private int                   FirstPostfixRule;
    private ExtentionRuleParser[] PrefixRules;
    private ExtentionRuleParser[] PostfixRules;

    public ExtensibleRuleParser(int RuleId, ExtensibleRuleDescriptor descriptor, int bindingPower, CompositeGrammar grammar)
      : base(RuleId, grammar)
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

    public sealed override int Parse(int curEndPos, string text, ref int resultPtr, ref Parser parser)
    {
      unchecked
      {
#if DEBUG || PARSER_DEBUG
        if (parser.ruleCalls[curEndPos] == null)
          parser.ruleCalls[curEndPos] = System.Collections.Generic.List();
        parser.ruleCalls[curEndPos].Add(parser.parserHost.GetRuleDescriptorById(RuleId));
#endif
        int newEndPos;
        int newResult;
        int bestEndPos;
        int bestResult;
        int i;
        int j;
        char c; // временная переменная для отсечения правил по первой букве

        if (resultPtr == -1)
          goto start_parsing;
        else
          goto error_recovery;

start_parsing:
        if (curEndPos >= text.Length) // конец текста
          return -1;

        resultPtr = parser.Allocate(5, RuleId);
        goto prefix_loop;

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

prefix_loop:
        i = 0;
        c = text[curEndPos];
        bestEndPos = -1;
        bestResult = -1;
        for (; i < PrefixRules.Length; ++i)
        {
          var prefixRule = PrefixRules[i];
          if (prefixRule.LowerBound <= c && c <= prefixRule.UpperBound)
          {
            newResult = -1;
            newEndPos = prefixRule.Parse(curEndPos, text, ref newResult, ref parser);
            if (newEndPos > 0)
            {
              if (bestResult < 0)
              {
                bestEndPos = newEndPos;
                bestResult = newResult;
              }
              else
                for (j = 3; true; ++j)
                {
                  var newSize  = parser.ast[newResult + j];
                  if (newSize < 0)
                    break;
                  if (parser.ast[bestResult + j] < newSize)
                  {
                    bestEndPos = newEndPos;
                    bestResult = newResult;
                    break;
                  }
                }
            }
          }
        }

        parser.ast[resultPtr + AstOfs] = bestResult;

        if (bestEndPos < 0)// не смогли разобрать префикс
          return -1;

//postfix_loop:
        curEndPos = bestEndPos;
        while (curEndPos < text.Length) // постфиксное правило которое не съело ни одного символа игнорируется
                                        // при достижении конца текста есть нечего
        {
          bestResult = -1;
          i = FirstPostfixRule;
          c = text[curEndPos];
          for (; i < PostfixRules.Length; ++i)
          {
            var postfixRule = PostfixRules[i];
            if (postfixRule.LowerBound <= c && c <= postfixRule.UpperBound)
            {
              newResult = parser.ast[resultPtr + AstOfs];
              newEndPos = postfixRule.Parse(curEndPos, text, ref newResult, ref parser);
              if (newEndPos > 0)
              {
                if (bestResult < 0)
                {
                  bestEndPos = newEndPos;
                  bestResult = newResult;
                }
                else
                  for (j = 3; true; ++j)
                  {
                    var newSize  = parser.ast[newResult + j];
                    if (newSize < 0)
                      break;
                    if (parser.ast[bestResult + j] < newSize)
                    {
                      bestEndPos = newEndPos;
                      bestResult = newResult;
                      break;
                    }
                  }
              }
            }
          }

          if (bestEndPos == curEndPos)
            break; // если нам не удалось продвинуться то заканчиваем разбор
          parser.ast[resultPtr + AstOfs] = bestResult;

          curEndPos = bestEndPos;
        }

        return curEndPos;
      }
    }
  }
}
