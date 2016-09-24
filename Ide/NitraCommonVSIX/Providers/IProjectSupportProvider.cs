using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nitra.VisualStudio.Providers
{
  public interface IProjectSupportProvider
  {
    string Caption { get; }
    string TypeFullName { get; }
    string Path { get; }
  }
}
