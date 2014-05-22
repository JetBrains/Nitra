using JetBrains.ProjectModel;

namespace JetBrains.Test
{
  [ProjectFileTypeDefinition(Name, Edition = "Dsl")]
  public class DslFileType : KnownProjectFileType
  {
    public new const string Name = "Dsl";
    public new static readonly DslFileType Instance = new DslFileType();

    public const string DslExtension = ".dsl";

    private DslFileType()
      : base(Name, "Dsl", new[] { DslExtension })
    {
    }

    protected DslFileType(string name)
      : base(name)
    {
    }

    protected DslFileType(string name, string presentableName)
      : base(name, presentableName)
    {
    }
  }
}