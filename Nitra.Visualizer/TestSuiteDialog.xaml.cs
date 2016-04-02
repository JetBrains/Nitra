﻿using Microsoft.VisualBasic.FileIO;
using Microsoft.Win32;

using Nitra.Visualizer.Properties;
using Nitra.ViewModels;

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Nitra.Visualizer.Serialization;

namespace Nitra.Visualizer
{
  internal partial class TestSuiteDialog
  {
    const string _assembiesToolTip   = "Enter paths to assemblies (one assembly per line)";

    private readonly TestSuiteCreateOrEditModel _model;
    private readonly DispatcherTimer _assembliesChangedTimer;
    private readonly DispatcherTimer _libsChangedTimer;
    private readonly SuiteVm _baseSuite;

    public TestSuiteDialog(bool isCreate, SuiteVm baseSuite, Settings settings)
    {
      _baseSuite = baseSuite;
      _assembliesChangedTimer          = new DispatcherTimer();
      _assembliesChangedTimer.Interval = TimeSpan.FromSeconds(1.3);
      _assembliesChangedTimer.Tick    += _assembliesEdit_timer_Tick;

      _libsChangedTimer          = new DispatcherTimer();
      _libsChangedTimer.Interval = TimeSpan.FromSeconds(1.3);
      _libsChangedTimer.Tick    += _libsEdit_timer_Tick;

      DataContext = _model = new TestSuiteCreateOrEditModel(settings, isCreate);

      InitializeComponent();

      if (baseSuite != null)
      {
        _model.RootFolder           = baseSuite.Workspace.RootFolder;
        _model.SuiteName            = baseSuite.Name;
        Trace.Assert(false, "Not implemented");
        // TODO: Make convertion
        //_model.NormalizedAssemblies = baseSuite.Language.DynamicExtensions;
        //_model.NormalizedLibs       = baseSuite.Language.Libs;

        //if (baseSuite.Language != Nitra.Language.Instance)
        //  _model.SelectedLanguage = baseSuite.Language;
      }

      _assemblies.Text = _model.NormalizedAssembliesText;
      _assemblies.TextChanged += _assemblies_TextChanged;

      _libs.Text = _model.NormalizedLibsText;
      _libs.TextChanged += _libs_TextChanged;
    }

    private void _libs_TextChanged(object sender, TextChangedEventArgs e)
    {
      _libsChangedTimer.Stop();
      _libsChangedTimer.Start();
    }

    private void _libsEdit_timer_Tick(object sender, EventArgs e)
    {
      _libsChangedTimer.Stop();
      _model.Libs = _libs.Text;
    }

    public string TestSuiteName
    {
      get { return _model.SuiteName; }
    }

    private void _assemblies_TextChanged(object sender, TextChangedEventArgs e)
    {
      _assembliesChangedTimer.Stop();
      _assembliesChangedTimer.Start();
    }

    void _assembliesEdit_timer_Tick(object sender, EventArgs e)
    {
      _assembliesChangedTimer.Stop();
      _model.Assemblies = _assemblies.Text;
    }

    private void _assemblies_LostFocus(object sender, RoutedEventArgs e)
    {
      _assembliesChangedTimer.Stop();
      _model.Assemblies = _assemblies.Text;
      _assemblies.Text = _model.NormalizedAssembliesText;
    }

    private void _assemblies_KeyUp(object sender, KeyEventArgs e)
    {
      if (e.Key == Key.F5)
      {
        _model.Assemblies = _assemblies.Text;
        _assemblies.Text = _model.NormalizedAssembliesText;
      }
    }

    private void _libs_KeyUp(object sender, KeyEventArgs e)
    {
      if (e.Key == Key.F5)
      {
        _model.Libs = _libs.Text;
        _libs.Text = _model.NormalizedLibsText;
      }
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

      var root = Path.GetFullPath(_model.RootFolder);
      var path = _model.SuitPath;

      if (Directory.Exists(path) && _model.IsCreate)
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

      var assemblies = _model.NormalizedAssemblies;

      if (assemblies.Length == 0)
      {
        MessageBox.Show(this, "No one valid library in library list.", "Error!", MessageBoxButton.OK, MessageBoxImage.Error);
        _assemblies.Focus();
        return;
      }

      var selectedLanguage = _model.SelectedLanguage;

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

        //var dynamicExtensions = _model.DynamicExtensions.Where(x => x.IsEnabled && x.IsChecked).Select(x => x.Descriptor);
        //var lang = new Language();
        //lang.Name = selectedLanguage;
        //var xml               = Utils.MakeXml(root, selectedLanguage, dynamicExtensions, _model.NormalizedLibs);
        //var configPath        = Path.Combine(path, SuiteVm.ConfigFileName);
        //File.WriteAllText(configPath, xml);
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
        InitialDirectory = _model.SuitPath,
        Filter = "Parser library (.dll)|*.dll|Parser application (.exe)|*.exe",
        Title = "Load parser"
      };

      if (dialog.ShowDialog(this) ?? false)
      {
        _assemblies.Text += Environment.NewLine + dialog.FileName;
        _assemblies.Focus();
      }
    }

    private void _libs_LostFocus(object sender, RoutedEventArgs e)
    {
      _libsChangedTimer.Stop();
      _model.Libs = _libs.Text;
      _libs.Text = _model.NormalizedLibsText;
    }
  }
}
