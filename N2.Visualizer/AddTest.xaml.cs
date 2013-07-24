using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using N2.Visualizer.Properties;

namespace N2.Visualizer
{
  /// <summary>
  /// Interaction logic for Test.xaml
  /// </summary>
  public partial class AddTest : Window
  {
    Settings _settings;
    string _code;
    string _gold;

    public AddTest(string code, string gold)
    {
      _settings = Settings.Default;
      _code = code;
      _gold = gold;

      InitializeComponent();
      _testName.Text = MakeDefaultName();
    }

    private string MakeDefaultName()
    {
      var path = Path.Combine(_settings.TestsLocationRoot, _settings.LastGrammarName, _settings.LastRuleName);

      if (!Directory.Exists(path))
        Directory.CreateDirectory(path);

      var found = Directory.EnumerateFiles(path, "*.test").FirstOrDefault(file => File.ReadAllText(file).Equals(_code, StringComparison.Ordinal));
      
      if (found == null)
        return "test-" + (Directory.EnumerateFiles(path, "*.test").Count() + 1).ToString("0000") + ".test";

      return Path.GetFileName(found);
    }

    public string TestName { get; private set; }

    private void _okButton_Click(object sender, RoutedEventArgs e)
    {
      var path = Path.Combine(_settings.TestsLocationRoot, _settings.LastGrammarName, _settings.LastRuleName);
      var filePath = Path.Combine(path, _testName.Text);

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
