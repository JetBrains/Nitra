using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.ExtensionsAPI.Caches2;
using JetBrains.ReSharper.Psi.Impl;
using JetBrains.ReSharper.Psi.Modules;
using JetBrains.ReSharper.Psi.Parsing;
using JetBrains.ReSharper.Psi.Tree;
using JetBrains.ReSharper.Psi.Util;
using JetBrains.Text;

namespace JetBrains.Test
{
  [Language(typeof(DslLanguage))]
  public class DslLanguageService : LanguageService
  {
    private readonly CommonIdentifierIntern _commonIdentifierIntern;

    public DslLanguageService(
      PsiLanguageType lexLanguageType, IConstantValueService constantValueService, CommonIdentifierIntern commonIdentifierIntern)
      : base(lexLanguageType, constantValueService)
    {
      _commonIdentifierIntern = commonIdentifierIntern;
    }

    public override bool IsCaseSensitive
    {
      get { return false; }
    }

    public override ILanguageCacheProvider CacheProvider
    {
      get { return null; }
    }

    public override bool SupportTypeMemberCache
    {
      get { return false; }
    }

    public override ITypePresenter TypePresenter
    {
      get { return DefaultTypePresenter.Instance; }
    }

    public override ILexerFactory GetPrimaryLexerFactory()
    {
      return new FakeLexerFactory();
    }

    private class FakeLexerFactory : ILexerFactory
    {
      public ILexer CreateLexer(IBuffer buffer)
      {
        return new FakeLexer(buffer);
      }
    }

    class FakeLexer : ILexer
    {
      private readonly IBuffer _buffer;

      public FakeLexer(IBuffer buffer)
      {
        _buffer = buffer;
      }

      public void Start()
      {
      }

      public void Advance()
      {
      }

      public object CurrentPosition { get; set; }
      public TokenNodeType TokenType { get { return null; } }
      public int TokenStart { get { return 0; } }
      public int TokenEnd { get { return 0; } }
      public IBuffer Buffer { get { return _buffer; } }
    }

    public override ILexer CreateFilteringLexer(ILexer lexer)
    {
      return null;
    }

    public override IParser CreateParser(
      ILexer lexer, IPsiModule module, IPsiSourceFile sourceFile)
    {
      return new Parser(lexer, sourceFile, _commonIdentifierIntern);
    }

    private class Parser : IParser
    {
      private readonly IPsiSourceFile _sourceFile;
      private readonly CommonIdentifierIntern _commonIdentifierIntern;

      public Parser(ILexer lexer, IPsiSourceFile sourceFile, CommonIdentifierIntern commonIdentifierIntern)
      {
        _sourceFile = sourceFile;
        _commonIdentifierIntern = commonIdentifierIntern;
      }

      public IFile ParseFile()
      {
        return new NitraFile(_sourceFile, _commonIdentifierIntern);
      }
    }
  }
}
