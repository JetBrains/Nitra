using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Nitra.ViewModels
{
  public interface ITest
  {
    string Name { get; }
    string FullPath { get; }
    bool IsSelected { get; set; }
    TestState TestState { get; }
  }
}
