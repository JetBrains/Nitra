using JetBrains.ReSharper.Feature.Services.CodeCompletion.Impl;
using JetBrains.ReSharper.Feature.Services.CodeCompletion.Infrastructure;
using JetBrains.ReSharper.Psi;
using JetBrains.Util;

namespace XXNamespaceXX
{
  [IntellisensePart]
  internal class NitraCodeCompletionContextProvider : CodeCompletionContextProviderBase
  {
    // TODO: make language specific setting setting....

    public override bool IsApplicable(CodeCompletionContext context)
    {
      return context.File.Language.Is<XXLanguageXXLanguage>();
    }

    public override ISpecificCodeCompletionContext GetCompletionContext(CodeCompletionContext context)
    {
      var solution = ReSharperSolution.XXLanguageXXSolution;
      var nitraFile = solution.GetNitraFile(context.Document);
      if (nitraFile == null)
        return null;

      var ast = nitraFile.Ast;

      if (ast == null)
        return null;

      var pos = context.CaretTreeOffset.Offset;
      var parseResult = ast.File.ParseResult;
      NSpan replacementSpan;
      var result = NitraUtils.CompleteWord(pos, parseResult, ast, out replacementSpan);
      var textRange = new TextRange(replacementSpan.StartPos, replacementSpan.EndPos);
      return new NitraCodeCompletionContext(context, result, GetTextLookupRanges(context, textRange));
    }
  }
}