using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace N2.Visualizer
{
  class SyntaxModuleVm
  {
    public GrammarDescriptor GrammarDescriptor { get; private set; }

    public bool IsChecked { get; set; }
    public string Name { get { return this.GrammarDescriptor.Name; } }

    public SyntaxModuleVm(GrammarDescriptor grammarDescriptor)
    {
      this.GrammarDescriptor = grammarDescriptor;
    }
  }
}
