using JetBrains.DocumentModel;
using JetBrains.ReSharper.Feature.Services.CodeCompletion;
using JetBrains.ReSharper.Feature.Services.CodeCompletion.Infrastructure;
using JetBrains.ReSharper.Feature.Services.CodeCompletion.Infrastructure.LookupItems;
using JetBrains.ReSharper.Psi;
using Nitra.Declarations;

namespace XXNamespaceXX
{
  [Language(typeof(XXLanguageXXLanguage))]
  public class NitraItemsProvider : ItemsProviderOfSpecificContext<NitraCodeCompletionContext>
  {
    protected override bool IsAvailable(NitraCodeCompletionContext context)
    {
      return true;
    }

    protected override TextLookupRanges GetDefaultRanges(NitraCodeCompletionContext context)
    {
      return context.CompletedElementRange;
    }

    public override bool IsDynamic
    {
      get { return true; }
    }

    protected override bool AddLookupItems(NitraCodeCompletionContext context, GroupedItemsCollector collector)
    {
      var documentRange = context.CompletedElementRange.ReplaceRange;
      var replaceRangeMarker = documentRange.CreateRangeMarker(context.BasicContext.Document);

      foreach (var complationItem in context.ComplationItems)
      {
        string content = null;
        string description = null;

        var symbol = complationItem as DeclarationSymbol;
        if (symbol != null && symbol.IsNameValid)
        {
          content = symbol.Name;
          description = symbol.Kind;
        }

        var literal = complationItem as string;
        if (literal != null)
        {
          content = literal;
          description = "keyword or literal";
        }

        if (!string.IsNullOrEmpty(content) && content[0] != '#')
        {
          var item = new SimpleTextLookupItem(content, replaceRangeMarker);
          item.InitializeRanges(context.CompletedElementRange, context.BasicContext);
          var rr = item.Ranges;
          collector.Add(item);
        }
      }
      return true;
    }

    private static string GetCompletionPrefix(NitraCodeCompletionContext context)
    {
      return context.BasicContext.Document.GetText(context.CompletedElementRange.ReplaceRange);
    }

    protected override void TransformItems(NitraCodeCompletionContext context, GroupedItemsCollector collector)
    {
    }

    public override CompletionMode SupportedCompletionMode
    {
      get { return CompletionMode.All; }
    }

    public override EvaluationMode SupportedEvaluationMode
    {
      get { return EvaluationMode.Light | EvaluationMode.OnlyDynamicRules; }
    }
  }
}