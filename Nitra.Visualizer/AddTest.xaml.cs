using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using Nitra.Visualizer.Properties;
using Nitra.ViewModels;

namespace Nitra.Visualizer
{
  /// <summary>
  /// Interaction logic for Test.xaml
  /// </summary>
  public partial class AddTest
  {
    readonly BaseVm _selectedNode;
    readonly string _code;
    readonly string _gold;
    string _testPath;

    public AddTest(BaseVm parent, string code, string gold)
    {
      _selectedNode = parent;
      _code = code;
      _gold = gold;

      InitializeComponent();
      _testPath = MakeDefaultTestPath();
      _testName.Text = Path.GetFileNameWithoutExtension(_testPath);
    }

    static string GetName(string parent, Func<string, IEnumerable<string>> getFileSystemObjects)
    {
      var rx = new Regex(@"^test-(\d+)");
      var maxNumber =
          getFileSystemObjects(parent)
            .Select(Path.GetFileName)
            .OrderByDescending(x => x)
            .Select(dir =>
            {
              var m = rx.Match(dir);
              if (m.Success)
                return (int?)int.Parse(m.Groups[1].Value);
              else
                return null;
            })
            .FirstOrDefault(x => x.HasValue);
      var nextNumber = maxNumber.HasValue ? maxNumber.Value + 1 : 0;
      return "test-" + nextNumber.ToString("0000");
    }

    string MakeDefaultTestPath()
    {
      string dir = null;
      string fileName = null;

      // We are on a test node, add a test next to it
      if (_selectedNode is FileVm)
      {
        dir = Path.GetDirectoryName(_selectedNode.FullPath);
        fileName = GetName(dir, Directory.GetFiles);
      }
      // We are on a project node, add a child test
      else if (_selectedNode is ProjectVm)
      {
        dir = _selectedNode.FullPath;
        fileName = GetName(dir, Directory.GetFiles);
      }
      else
      {
        var dirName = GetName(_selectedNode.FullPath, Directory.GetDirectories);
        // We are on a sulution node, add a project with a single test
        if (_selectedNode is SolutionVm)
        {
          dir = Path.Combine(_selectedNode.FullPath, dirName);
          fileName = dirName;
        }
        // We are on a suite node, add a solution with a project with a test
        else if (_selectedNode is SuiteVm)
        {
          dir = Path.Combine(_selectedNode.FullPath, dirName, dirName);
          fileName = dirName;
        }
      }

      return Path.Combine(dir, fileName + ".test");
    }

    public string TestName { get; private set; }

    private void _okButton_Click(object sender, RoutedEventArgs e)
    {
      var dir = Path.GetDirectoryName(_testPath);
      Directory.CreateDirectory(dir);
      var filePath = Path.Combine(dir, _testName.Text) + ".test";

      try
	    {
		    File.WriteAllText(filePath, _code);
	    }
	    catch (Exception ex)
	    {
        MessageBox.Show(this, ex.GetType().Name + ":" + ex.Message, Constants.AppName,
          MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.Cancel);
        return;
	    }

      var goldFilePath = Path.ChangeExtension(filePath, ".gold");
      File.WriteAllText(goldFilePath, _gold);
      TestName = _testName.Text;
      this.DialogResult = true;
      Close();
    }
  }
}
