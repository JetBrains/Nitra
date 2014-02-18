using JetBrains.Annotations;
using JetBrains.ReSharper.Psi;

namespace JetBrains.Test
{
  [LanguageDefinition(Name)]
  public class DslLanguage : KnownLanguage
  {
    private new const string Name = "Dsl";

    [UsedImplicitly]
    public static DslLanguage Instance;

    protected DslLanguage()
      : base(Name, Name)
    {
    }

    protected DslLanguage([NotNull] string name)
      : base(name, name)
    {
    }

    protected DslLanguage([NotNull] string name, [NotNull] string presentableName)
      : base(name, presentableName)
    {
    }
  }
}