using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.ObjectModel;

namespace Nitra.Visualizer
{
  public class Preset
  {
    ObservableCollection<Preset> _owner;

    public string Header { get { return "_" + Index + " " + Name; } }
    public int Index { get { return _owner.IndexOf(this) + 1; } }

    public string Name { get; private set; }
    public string AssemblyFilePath { get; private set; }
    public string SynatxModuleName { get; private set; }
    public string StartRuleName { get; private set; }
    public string Code { get; private set; }

    public Preset(ObservableCollection<Preset> owner, string name, string assemblyFilePath, string synatxModuleName, string startRuleName, string code)
    {
      _owner           = owner;
      Name             = name;
      AssemblyFilePath = assemblyFilePath;
      StartRuleName    = startRuleName;
      Code             = code;
      SynatxModuleName = synatxModuleName;
    }

    public Preset(ObservableCollection<Preset> owner, string data)
    {
      var values = data.Split(new[] { '|' }, 5);

      _owner           = owner;
      Name             = values[0];
      AssemblyFilePath = values[1];
      SynatxModuleName = values[2];
      StartRuleName    = values[3];
      Code             = values[4];
    }

    public string Save()
    {
      return Name + "|" + AssemblyFilePath + "|" + SynatxModuleName + "|" + StartRuleName + "|" + Code;
    }
  }
}
