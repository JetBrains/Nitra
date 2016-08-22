using Microsoft.VisualBasic.FileIO;
using Microsoft.Win32;
using Nitra.ViewModels;
using System;
using System.IO;
using System.Windows;
using Nitra.Visualizer.ViewModels;
using ReactiveUI;

namespace Nitra.Visualizer
{
  internal partial class TestSuiteDialog : IViewFor<TestSuiteCreateOrEditViewModel>
  {
    private readonly SuiteVm _baseSuite;

    public TestSuiteDialog(SuiteVm baseSuite, TestSuiteCreateOrEditViewModel testSuiteCreateOrEditViewModel)
    {
      _baseSuite = baseSuite;

      DataContext = ViewModel = testSuiteCreateOrEditViewModel;

      InitializeComponent();
    }
    
    public string TestSuiteName
    {
      get { return ViewModel.SuiteName; }
    }

    private void _okButton_Click(object sender, RoutedEventArgs e)
    {
      var testSuiteName = TestSuiteName;

      if (string.IsNullOrWhiteSpace(testSuiteName))
      {
        MessageBox.Show(this, "Name of test suite can't be empty.", "Error!", MessageBoxButton.OK, MessageBoxImage.Error);
        _testSuiteName.Focus();
        return;
      }
      
      var path = ViewModel.SuitPath;

      if (Directory.Exists(path) && ViewModel.IsCreate)
      {
        MessageBox.Show(this, "The test suite '" + testSuiteName + "' already exists.", "Error!", MessageBoxButton.OK, MessageBoxImage.Error);
        _testSuiteName.Focus();
        return;
      }

      if (Utils.IsInvalidDirName(testSuiteName))
      {
        MessageBox.Show(this, "Name of test suite is invalid.", "Error!", MessageBoxButton.OK, MessageBoxImage.Error);
        _testSuiteName.Focus();
        return;
      }

      var assemblies = ViewModel.ParserLibs;

      if (assemblies.Count == 0)
      {
        MessageBox.Show(this, "No one valid library in library list.", "Error!", MessageBoxButton.OK, MessageBoxImage.Error);
        return;
      }

      var selectedLanguage = ViewModel.SelectedLanguage;

      if (selectedLanguage == null)
      {
        MessageBox.Show(this, "Langauge is not selected.", "Error!", MessageBoxButton.OK, MessageBoxImage.Error);
        _languageComboBox.Focus();
        return;
      }

      try
      {
        Directory.CreateDirectory(path);
        if (_baseSuite != null && _baseSuite.Name != testSuiteName && Directory.Exists(_baseSuite.FullPath))
        {
          FileSystem.CopyDirectory(_baseSuite.FullPath, path, UIOption.AllDialogs);
          Directory.Delete(_baseSuite.FullPath, recursive: true);
        }
      }
      catch (Exception ex)
      {
        MessageBox.Show(this, ex.GetType().Name + ":" + ex.Message, "Error!", MessageBoxButton.OK, MessageBoxImage.Error);
        return;
      }

      this.DialogResult = true;
      Close();
    }

    private void _addLibButton_Click(object sender, RoutedEventArgs e)
    {
      var dialog = new OpenFileDialog
      {
        DefaultExt = ".dll",
        InitialDirectory = ViewModel.SuitPath,
        Filter = "Parser library (.dll)|*.dll|Parser application (.exe)|*.exe",
        Title = "Load parser",
        Multiselect = true
      };

      if (dialog.ShowDialog(this) ?? false) {
          ViewModel.ParserLibs.AddRange(dialog.FileNames);
      }
    }

    object IViewFor.ViewModel
    {
      get { return ViewModel; }
      set { ViewModel = (TestSuiteCreateOrEditViewModel) value; }
    }

    public TestSuiteCreateOrEditViewModel ViewModel { get; set; }
  }
}
