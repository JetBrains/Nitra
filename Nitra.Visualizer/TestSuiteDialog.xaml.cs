using Microsoft.VisualBasic.FileIO;
using Microsoft.Win32;
using Nitra.ViewModels;
using System;
using System.IO;
using System.Reactive;
using System.Reactive.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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

      GetTextChanged(_parserLibs).Throttle(TimeSpan.FromSeconds(1.3), RxApp.MainThreadScheduler)
                                 .Subscribe(_ => ViewModel.ParserLibsText = _parserLibs.Text);

      GetTextChanged(_libs).Throttle(TimeSpan.FromSeconds(1.3), RxApp.MainThreadScheduler)
                           .Subscribe(_ => ViewModel.LibsText = _libs.Text);

      _parserLibs.LostFocus += (sender, args) => {
        ViewModel.ParserLibsText = _parserLibs.Text;
      };

      _libs.LostFocus += (sender, args) => {
        ViewModel.LibsText = _libs.Text;
      };

      _parserLibs.KeyUp += (sender, args) => {
        if (args.Key == Key.F5) {
          ViewModel.ParserLibsText = "";
          ViewModel.ParserLibsText = _parserLibs.Text;
        }
      };

      _libs.KeyUp += (sender, args) => {
        if (args.Key == Key.F5) {
          ViewModel.LibsText = "";
          ViewModel.LibsText = _libs.Text;
        }
      };
    }

    private IObservable<Unit> GetTextChanged(TextBox textBox)
    {
      return Observable.FromEventPattern<TextChangedEventHandler, TextChangedEventArgs>(h => textBox.TextChanged += h, h => textBox.TextChanged -= h)
                       .Select(a => Unit.Default);
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

      var assemblies = ViewModel.ParserLibPaths;

      if (assemblies.Length == 0)
      {
        MessageBox.Show(this, "No one valid library in library list.", "Error!", MessageBoxButton.OK, MessageBoxImage.Error);
        _parserLibs.Focus();
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
        Title = "Load parser"
      };

      if (dialog.ShowDialog(this) ?? false)
      {
        _parserLibs.Text += dialog.FileName + Environment.NewLine;
        _parserLibs.Focus();
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
