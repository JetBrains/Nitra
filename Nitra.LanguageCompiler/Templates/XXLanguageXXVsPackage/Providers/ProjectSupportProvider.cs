using Nitra.VisualStudio.Providers;

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XXNamespaceXX
{
  [Export(typeof(INitraProjectSupportProvider))]
  class NitraProjectSupportProvider : INitraProjectSupportProvider
  {
    public string Caption      => "Nitra C#";
    public string TypeFullName => "CSharp.CompilationUnit";
    public string Path
    {
      get
      {
        var plaginPath = GetPlaginPath();
        var path       = System.IO.Path.Combine(plaginPath, "CSharp.Grammar.dll");
        return path;
      }
    }
  }
}
