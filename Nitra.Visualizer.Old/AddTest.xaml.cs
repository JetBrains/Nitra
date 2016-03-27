using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using Nitra.Visualizer.Properties;

namespace Nitra.Visualizer
{
  /// <summary>
  /// Interaction logic for Test.xaml
  /// </summary>
  public partial class AddTest
  {
    readonly string _testSuitePath;
    readonly string _code;
    readonly string _gold;

    public AddTest(string testSuitePath, string code, string gold)
    {
      _testSuitePath = testSuitePath;
      _code = code;
      _gold = gold;

      InitializeComponent();
      _testName.Text = MakeDefaultName();
    }

    private string MakeDefaultName()
    {
      var path = _testSuitePath;

      if (!Directory.Exists(path))
        Directory.CreateDirectory(path);

      return MakeTestName(path);
    }

    private static string MakeTestName(string path)
    {
      var rx = new Regex(@"test-(\d+)");
      var tests = new List<string>();
      tests.AddRange(Directory.GetDirectories(path).Select(Path.GetFileNameWithoutExtension));
      tests.AddRange(Directory.GetFiles(path, "*.test").Select(Path.GetFileNameWithoutExtension));
      tests.Sort();
      int num = 0;
      for (int i = tests.Count - 1; i >= 0; i--)
      {
        var testName = tests[i];
        var m = rx.Match(testName);
        if (m.Success)
        {
          num = int.Parse(m.Groups[1].Value) + 1;
          break;
        }
      }

      for (;; num++)
      {
        var fileName = "test-" + num.ToString("0000");
        if (!File.Exists(Path.Combine(path, fileName + ".test")))
          return fileName;
      }
    }

    public string TestName { get; private set; }

    private void _okButton_Click(object sender, RoutedEventArgs e)
    {
      var path = _testSuitePath;
      var filePath = Path.Combine(path, _testName.Text) + ".test";

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
