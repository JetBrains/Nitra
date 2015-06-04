using System.Collections.Generic;

namespace Nitra.ViewModels
{
  public interface ITestTreeContainerNode
  {
    bool IsSelected { get; set; }
    IEnumerable<ITest> Children { get; }
  }
}
