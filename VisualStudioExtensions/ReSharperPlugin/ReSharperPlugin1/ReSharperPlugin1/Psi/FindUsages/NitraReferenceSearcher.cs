using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.Caches;
using JetBrains.ReSharper.Psi.ExtensionsAPI;
using JetBrains.ReSharper.Psi.ExtensionsAPI.Finder;
using JetBrains.ReSharper.Psi.Files;
using JetBrains.ReSharper.Psi.Search;
using JetBrains.ReSharper.Psi.Tree;
using JetBrains.Test;
using JetBrains.Util;

namespace JetBrains.Nitra.FindUsages
{
  internal class NitraReferenceSearcher : IDomainSpecificSearcher
  {
    private readonly JetHashSet<string> myNames;
    private readonly JetHashSet<string> myWordsInText;
    private readonly JetHashSet<IDeclaredElement> myElements;
    private readonly bool myFindCandidates;
    private readonly bool mySearchForLateBound;
    private readonly IWordIndex myWordIndex;

    public NitraReferenceSearcher(NitraSearcherFactory factory, IEnumerable<IDeclaredElement> elements, bool findCandidates, bool searchForLateBound)
    {
      myFindCandidates = findCandidates;
      mySearchForLateBound = searchForLateBound;
      myElements = new JetHashSet<IDeclaredElement>(elements);

      myNames = new JetHashSet<string>();
      myWordsInText = new JetHashSet<string>();

      foreach (var element in myElements)
      {
        myNames.Add(element.ShortName);
        myWordsInText.UnionWith(factory.GetAllPossibleWordsInFile(element));
      }

      myWordIndex = myElements.First().GetPsiServices().WordIndex;
    }

    public bool ProcessProjectItem<TResult>(IPsiSourceFile sourceFile, IFindResultConsumer<TResult> consumer)
    {
      if (myWordsInText.Any(word => myWordIndex.CanContainWord(sourceFile, word)))
      {
        return sourceFile.GetPsiFiles<DslLanguage>().Any(file => ProcessElement(file, consumer));
      }

      return false;
    }

    public bool ProcessElement<TResult>(ITreeNode element, IFindResultConsumer<TResult> consumer)
    {
      Assertion.Assert(element != null, "The condition (element != null) is false.");

      var names = new JetHashSet<string>(myNames, StringComparer.OrdinalIgnoreCase);

      NamedThingsSearchSourceFileProcessor processor;

      if (mySearchForLateBound)
        processor = new LateBoundReferenceSourceFileProcessor<TResult>(element, consumer, myElements, myWordsInText, names);
      else
        processor = new ReferenceSearchSourceFileProcessor<TResult>(element, myFindCandidates, consumer, myElements, myWordsInText, names);

      return processor.Run() == FindExecution.Stop;
    }
  }
}
