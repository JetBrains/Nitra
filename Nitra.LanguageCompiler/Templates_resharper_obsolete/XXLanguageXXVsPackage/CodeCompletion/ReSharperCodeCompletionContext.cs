using Nitra;

using System;
using System.Collections.Generic;
using JetBrains.ReSharper.Feature.Services.CodeCompletion.Infrastructure;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.Pointers;

namespace XXNamespaceXX
{
  public class NitraCodeCompletionContext : ISpecificCodeCompletionContext
  {
    private readonly CodeCompletionContext _context;
    public TextLookupRanges CompletedElementRange { get; private set; }
    public IEnumerable<object> ComplationItems { get; private set; }

    public NitraCodeCompletionContext(CodeCompletionContext context, IEnumerable<object> complationItems, TextLookupRanges completedElementRange)
    {
      CompletedElementRange = completedElementRange;
      ComplationItems = complationItems;
      _context = context;
    }

    public IElementInstancePointer<T> CreateElementPointer<T>(DeclaredElementInstance<T> instance) where T : class, IDeclaredElement
    {
      throw new NotImplementedException();
    }

    public CodeCompletionContext BasicContext
    {
      get { return _context; }
    }

    public string ContextId
    {
      get { return "42"; }
    }

    public PsiLanguageType Language
    {
      get { return XXLanguageXXLanguage.Instance; }
    }
  }
}