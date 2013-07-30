using System;
using System.IO;
using System.Linq;
using System.Windows;
using N2.Visualizer.Properties;

namespace N2.Visualizer
{
  /// <summary>
  /// Interaction logic for Test.xaml
  /// </summary>
  public partial class AddTest
  {
    readonly string _testSuitPath;
    readonly string _code;
    readonly string _gold;

    public AddTest(string testSuitPath, string code, string gold)
    {
      _testSuitPath = testSuitPath;
      _code = code;
      _gold = gold;

      InitializeComponent();
      _testName.Text = MakeDefaultName();
    }

    private string MakeDefaultName()
    {
      var path = _testSuitPath;

      if (!Directory.Exists(path))
        Directory.CreateDirectory(path);

      var found = Directory.EnumerateFiles(path, "*.test").FirstOrDefault(file => File.ReadAllText(file).Equals(_code, StringComparison.Ordinal));
      
      if (found == null)
        return "test-" + (Directory.EnumerateFiles(path, "*.test").Count() + 1).ToString("0000");

      return Path.GetFileNameWithoutExtension(found);
    }

    public string TestName { get; private set; }

    private void _okButton_Click(object sender, RoutedEventArgs e)
    {
      var path = _testSuitPath;
      var filePath = Path.Combine(path, _testName.Text) + ".test";

      try 
	    {	        
		    File.WriteAllText(filePath, _code);
	    }
	    catch (Exception ex)
	    {
        MessageBox.Show(this, ex.Message, "Nitra Visualizer",
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
