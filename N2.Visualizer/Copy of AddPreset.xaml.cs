using System;
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
using System.Windows.Shapes;

namespace N2.Visualizer
{
  /// <summary>
  /// Interaction logic for AddPreset.xaml
  /// </summary>
  public partial class AddPreset : Window
  {
    public AddPreset(string syntaxModuleName, string code)
    {
      InitializeComponent();
      _presetName.Text = MakeDefaultName(syntaxModuleName, code);
    }

    private string MakeDefaultName(string syntaxModuleName, string code)
    {
      var sb = new StringBuilder(syntaxModuleName).Append(": ").Append(code).Replace('|', ' ').Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ');
      var len = 0;
      while (sb.Length != len)
      {
        len = sb.Length;
        sb.Replace("  ", " ");
      }

      if (sb.Length > 50)
      {
        sb.Length = 50;
        sb.Append("...");
      }

      return sb.ToString();
    }

    public string PresetName { get; private set; }

    private void _okButton_Click(object sender, RoutedEventArgs e)
    {
      if (_presetName.Text.Contains('|'))
      {
        MessageBox.Show(this, "The symbol '|' unacceptable in preset name.", "Nitra Visualizer",
          MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.Cancel);
        return;
      }

      PresetName = _presetName.Text;
      this.DialogResult = true;
      Close();
    }
  }
}
