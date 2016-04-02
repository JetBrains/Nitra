//using JetBrains.Annotations;
//using JetBrains.ReSharper.Psi;
//using JetBrains.ReSharper.Psi.Asxx.Parsing;
//using JetBrains.ReSharper.Psi.ExtensionsAPI.Caches2;
//using JetBrains.ReSharper.Psi.ExtensionsAPI.Tree;
//using JetBrains.ReSharper.Psi.Impl;
//using JetBrains.ReSharper.Psi.Modules;
//using JetBrains.ReSharper.Psi.Tree;

//using Microsoft.VisualStudio.Text.Classification;
//using Microsoft.VisualStudio.Utilities;

//using System.ComponentModel.Composition;
//using System.Windows.Media;

//namespace XXNamespaceXX
//{
//  internal static partial class NitraFileExtensionsAndContentTypeDefinition
//  {
//    [Export]
//    [Name("XXLanguageXX")]
//    [BaseDefinition("text")]
//    public static ContentTypeDefinition XXLanguageXXContentTypeDefinition = null;

//    internal static string[] FileExtensions = { "XXFileExtensionsXX" };
//  }
//}

//namespace XXNamespaceXX
//{
//  using JetBrains.ProjectModel;
//  using JetBrains.ReSharper.Psi;
//  using JetBrains.ReSharper.Psi.Parsing;
//  using JetBrains.Text;
//  using JetBrains.UI.Icons;
  
//  [ProjectFileTypeDefinition(Name)]
//  public class XXLanguageXXFileType : KnownProjectFileType
//  {
//    public new const string Name = "XXLanguageXX";
//    public new static readonly XXLanguageXXFileType Instance = new XXLanguageXXFileType();

//    private XXLanguageXXFileType()
//      : base(Name, "XXLanguageXX", NitraFileExtensionsAndContentTypeDefinition.FileExtensions)
//    {
//    }

//    protected XXLanguageXXFileType(string name)
//      : base(name)
//    {
//    }

//    protected XXLanguageXXFileType(string name, string presentableName)
//      : base(name, presentableName)
//    {
//    }
//  }

//  [Language(typeof(XXLanguageXXLanguage))]
//  internal class XXLanguageXXLanguageService : LanguageService
//  {
//    public XXLanguageXXLanguageService(
//      XXLanguageXXLanguage language, IConstantValueService constantValueService)
//      : base(language, constantValueService) { }

//    public class NitraLexerFactory : ILexerFactory
//    {
//      public static readonly NitraLexerFactory Instance = new NitraLexerFactory();

//      public ILexer CreateLexer(IBuffer buffer)
//      {
//        return (ILexer)new NitraLexer(buffer);
//      }
//    }

//    class NitraToken : BindedToBufferLeafElement, ITokenNode
//    {
//      public NitraToken(NodeType nodeType, [NotNull] IBuffer buffer, TreeOffset startOffset, TreeOffset endOffset) : base(nodeType, buffer, startOffset, endOffset) { }

//      public override PsiLanguageType Language
//      {
//        get { return XXLanguageXXLanguage.Instance; }
//      }

//      public TokenNodeType GetTokenType()
//      {
//        return NitraFakeTokenNodeType.Instance;
//      }
//    }

//    private class NitraFakeTokenNodeType : TokenNodeType
//    {
//      public static readonly NitraFakeTokenNodeType Instance = new NitraFakeTokenNodeType();

//      public NitraFakeTokenNodeType() : base("XXLanguageXXFakeToken", 42) { }

//      public override LeafElementBase Create(IBuffer buffer, TreeOffset startOffset, TreeOffset endOffset)
//      {
//        return new NitraToken(NitraFakeTokenNodeType.Instance, buffer, startOffset, endOffset);
//      }

//      public override bool IsWhitespace { get { return false; } }
//      public override bool IsComment { get { return false; } }
//      public override bool IsStringLiteral { get { return false; } }
//      public override bool IsConstantLiteral { get { return false; } }
//      public override bool IsIdentifier { get { return false; } }
//      public override bool IsKeyword { get { return false; } }
//      public override string TokenRepresentation
//      {
//        get { return "<-XXLanguageXX->"; }
//      }
//    }

//    class NitraLexer : ILexer
//    {
//      private readonly IBuffer _buffer;
//      NitraToken _nextToken;

//      public NitraLexer(IBuffer buffer)
//      {
//        _buffer = buffer;
//        _nextToken = new NitraToken(NitraFakeTokenNodeType.Instance, _buffer, TreeOffset.Zero, new TreeOffset(_buffer.Length));
//      }

//      public void Start()
//      {
//      }

//      public void Advance()
//      {
//        _nextToken = null;
//      }

//      public object CurrentPosition
//      {
//        get { return null; }
//        set {  }
//      }

//      public TokenNodeType TokenType
//      {
//        get { return _nextToken == null ? null : _nextToken.GetTokenType(); }
//      }

//      public int TokenStart
//      {
//        get { return _nextToken == null ? -1 : _nextToken.GetTreeStartOffset().Offset; }
//      }

//      public int TokenEnd
//      {
//        get { return _nextToken == null ? -1 : _nextToken.GetTreeEndOffset().Offset; }
//      }

//      public IBuffer Buffer
//      {
//        get { return _buffer; }
//      }
//    }


//    public override ILexerFactory GetPrimaryLexerFactory()
//    {
//      return NitraLexerFactory.Instance;
//    }

//    public override ILexer CreateFilteringLexer(ILexer lexer)
//    {
//      return new SimpleFilteringLexer(lexer, SimpleFilteringLexer.IS_WHITESPACE);
//    }

//    class NitraFileNodeType : NodeType
//    {
//      public static NitraFileNodeType Instance = new NitraFileNodeType();
//      public NitraFileNodeType() : base("XXLanguageXXFile", 42)
//      {
//      }
//    }

//    private class NitraFile : FileElementBase
//    {
//      public override NodeType NodeType
//      {
//        get { return NitraFileNodeType.Instance; }
//      }

//      public override PsiLanguageType Language
//      {
//        get { return XXLanguageXXLanguage.Instance; }
//      }
//    }

//    class NitraParser : IParser
//    {
//      public IFile ParseFile()
//      {
//        return new NitraFile();
//      }
//    }

//    public override IParser CreateParser(
//      ILexer lexer, IPsiModule module, IPsiSourceFile sourceFile)
//    {
//      return new NitraParser();
//    }

//    public override ILanguageCacheProvider CacheProvider
//    {
//      get { return null; }
//    }

//    public override ITypePresenter TypePresenter
//    {
//      get { return DefaultTypePresenter.Instance; }
//    }

//    public override bool IsCaseSensitive { get { return false; } }
//    public override bool SupportTypeMemberCache
//    {
//      get { return false; }
//    }
//  }
  
//  [ProjectFileType(typeof(XXLanguageXXFileType))]
//  public class XXLanguageXXProjectFileLanguageService : ProjectFileLanguageService
//  {
//    public XXLanguageXXProjectFileLanguageService(XXLanguageXXFileType projectFileType)
//      : base(projectFileType)
//    {
//    }

//    protected override PsiLanguageType PsiLanguageType
//    {
//      get { return XXLanguageXXLanguage.Instance; }
//    }

//    public override IconId Icon
//    {
//      get
//      {
//        //return LexPluginSymbolThemedIcons.PsiFile.Id;
//        return null;
//      }
//    }

//    public override ILexerFactory GetMixedLexerFactory(ISolution solution, IBuffer buffer, IPsiSourceFile sourceFile = null)
//    {
//      return null;
//    }
//  }

//  [LanguageDefinition(Name)]
//  public class XXLanguageXXLanguage : KnownLanguage
//  {
//    private new const string Name = "XXLanguageXX";

//    [UsedImplicitly]
//    public static XXLanguageXXLanguage Instance;

//    protected XXLanguageXXLanguage()
//      : base(Name, Name)
//    {
//    }

//    protected XXLanguageXXLanguage([NotNull] string name)
//      : base(name, name)
//    {
//    }

//    protected XXLanguageXXLanguage([NotNull] string name, [NotNull] string presentableName)
//      : base(name, presentableName)
//    {
//    }
//  }
//}