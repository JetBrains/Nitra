using Nitra.VisualStudio.Providers;

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XXNamespaceXX
{
  [Export(typeof(ILanguageProvider))]
  class LanguageProvider : ILanguageProvider
  {
    public string Name => "NitraCSharp";
    public string Path => "CSharp.Grammar.dll";
  }
}
