using System.Linq;

namespace Nitra.Visualizer.ViewModels
{
  class SyntaxModuleVm
  {
    public GrammarDescriptor GrammarDescriptor { get; private set; }

    public bool   IsChecked   { get; set; }
    public bool   HasTopRules { get { return this.GrammarDescriptor.Rules.Any(r => r is ExtensibleRuleDescriptor || r is SimpleRuleDescriptor); } }
    public string Name        { get { return this.GrammarDescriptor.Name; } }

    public SyntaxModuleVm(GrammarDescriptor grammarDescriptor)
    {
      this.GrammarDescriptor = grammarDescriptor;
    }

    public override bool Equals(object obj)
    {
      var other = obj as SyntaxModuleVm;
      if (other == null)
        return false;

      return this.GrammarDescriptor == other.GrammarDescriptor;
    }

    public override int GetHashCode()
    {
      return this.GrammarDescriptor.GetHashCode();
    }
  }
}
