using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using JetBrains.DocumentModel;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.ExtensionsAPI;
using JetBrains.ReSharper.Psi.Files;
using JetBrains.ReSharper.Psi.Resolve;
using JetBrains.ReSharper.Psi.Search;
using JetBrains.ReSharper.Psi.Tree;
using JetBrains.Test;
using JetBrains.Util;

namespace JetBrains.Nitra.FindUsages
{
  internal class NitraTextOccurenceSearcher : IDomainSpecificSearcher
  {
    private readonly JetHashSet<string> myTexts;

    public NitraTextOccurenceSearcher(IEnumerable<string> texts)
    {
      myTexts = new JetHashSet<string>(texts.Where(value => !string.IsNullOrEmpty(value)));
    }

    public bool ProcessProjectItem<TResult>(IPsiSourceFile sourceFile, IFindResultConsumer<TResult> consumer)
    {
      return sourceFile.GetPsiFiles<DslLanguage>().Any(file => ProcessElement(file, consumer));
    }

    public bool ProcessElement<TResult>(ITreeNode element, IFindResultConsumer<TResult> consumer)
    {
      var processor = new TextOccurencesCollector<TResult>(myTexts, consumer);
      element.ProcessDescendants(processor);
      return false;
    }

    private sealed class TextOccurencesCollector<TResult> : IRecursiveElementProcessor
    {
      private readonly JetHashSet<string> myTexts;
      private readonly IFindResultConsumer<TResult> myConsumer;

      public TextOccurencesCollector(JetHashSet<string> texts, IFindResultConsumer<TResult> consumer)
      {
        myTexts = texts;
        myConsumer = consumer;
      }

      public bool ProcessingIsFinished { get { return false; } }

      public bool InteriorShouldBeProcessed(ITreeNode element)
      {
        return myTexts.Any(text => element.GetText().IndexOf(text, StringComparison.Ordinal) >= 0);
      }

      public void ProcessBeforeInterior(ITreeNode element)
      {
        var tokenNode = element as ITokenNode;
        if (tokenNode != null)
        {
          var tokenType = tokenNode.GetTokenType();
          if (!tokenType.IsKeyword)
            FetchTextOccurences(element, myConsumer);
        }
      }

      public void ProcessAfterInterior(ITreeNode element)
      {
      }

      private void FetchTextOccurences([NotNull] ITreeNode textToken, IFindResultConsumer<TResult> consumer)
      {
        var file = textToken.GetContainingFile();
        if (file != null)
        {
          var text = textToken.GetText();
          var textLength = text.Length;

          foreach (string name in myTexts)
          {
            var nameLength = name.Length;
            for (int start = 0; start < textLength; )
            {
              int pos = text.IndexOf(name, start, StringComparison.Ordinal);
              if (pos < 0)
                break;

              var range = textToken.GetDocumentRange();
              if (range.IsValid())
              {
                var textRange = new TextRange(range.TextRange.StartOffset + pos, range.TextRange.StartOffset + pos + nameLength);

                var nameDocumentRange = new DocumentRange(range.Document, textRange);
                var translatedRange = file.Translate(nameDocumentRange);
                if (!DeclarationExists(textToken, translatedRange) && !ReferenceExists(file, translatedRange))
                  consumer.Accept(new FindResultText(file.GetSourceFile(), nameDocumentRange));
              }

              start = pos + nameLength;
            }
          }
        }
      }

      private static bool ReferenceExists(IFile file, TreeTextRange range)
      {
        return file.FindReferencesAt(range).Any(reference => reference.CheckResolveResult() != ResolveErrorType.OK || !reference.Resolve().Result.IsEmpty);
      }

      private static bool DeclarationExists(ITreeNode textToken, TreeTextRange textRange)
      {
        var declaration = textToken.GetContainingNode<IDeclaration>(true);
        return declaration != null && declaration.GetNameRange().Contains(textRange);
      }
    }
  }
}
