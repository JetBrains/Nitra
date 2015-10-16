using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using Nitra.Visualizer.Properties;

namespace Nitra.Visualizer
{
  internal class TestSuiteCreateOrEditModel : DependencyObject
  {
    private readonly Settings _settings;

    public TestSuiteCreateOrEditModel(Settings settings, bool isCreate)
    {
      _settings = settings;

      Title = isCreate ? "New test suite" : "Edit test suite";
      RootFolder = Path.GetDirectoryName(_settings.CurrentSolution);
      Languages = new ObservableCollection<Language>();
      DynamicExtensions = new ObservableCollection<GrammarDescriptor>();
    }

    public string Title
    {
      get { return (string)GetValue(TitleProperty); }
      set { SetValue(TitleProperty, value); }
    }

    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register("Title", typeof(string), typeof(TestSuiteCreateOrEditModel), new FrameworkPropertyMetadata(""));

    public string RootFolder
    {
      get { return (string)GetValue(RootFolderProperty); }
      set { SetValue(RootFolderProperty, value); }
    }

    public static readonly DependencyProperty RootFolderProperty =
        DependencyProperty.Register("RootFolder", typeof(string), typeof(TestSuiteCreateOrEditModel), new FrameworkPropertyMetadata(""));

    public string SuiteName
    {
      get { return (string)GetValue(SuiteNameProperty); }
      set { SetValue(SuiteNameProperty, value); }
    }

    public static readonly DependencyProperty SuiteNameProperty =
        DependencyProperty.Register("SuiteName", typeof(string), typeof(TestSuiteCreateOrEditModel), new FrameworkPropertyMetadata(""));

    public string Assemblies
    {
      get { return (string)GetValue(AssembliesProperty); }
      set { SetValue(AssembliesProperty, value); }
    }

    public static readonly DependencyProperty AssembliesProperty =
        DependencyProperty.Register("Assemblies", typeof(string), typeof(TestSuiteCreateOrEditModel), new FrameworkPropertyMetadata("", OnAssembliesChanged));

    private static void OnAssembliesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
      var model = (TestSuiteCreateOrEditModel)d;
      var normalizedAssemblies = new List<Assembly>();
      foreach (var assemblyPath in Utils.GetAssemblyPaths((string)e.NewValue))
      {
        var fullAssemblyPath = Path.IsPathRooted(assemblyPath) ? assemblyPath : Path.Combine(model.RootFolder, assemblyPath);
        var assembly = Utils.LoadAssembly(fullAssemblyPath, model._settings.Config);
        normalizedAssemblies.Add(assembly);
      }
      model.NormalizedAssemblies = normalizedAssemblies.ToArray();
    }

    public string NormalizedAssembliesText
    {
      get
      {
        var text = new StringBuilder();
        foreach (var assembly in NormalizedAssemblies)
          text.AppendLine(Utils.MakeRelativePath(@from: RootFolder, isFromDir: true, @to: assembly.Location, @isToDir: false));
        return text.ToString();
      }
    }

    public Assembly[] NormalizedAssemblies
    {
      get { return (Assembly[])GetValue(NormalizedAssembliesProperty); }
      set { SetValue(NormalizedAssembliesProperty, value); }
    }

    public static readonly DependencyProperty NormalizedAssembliesProperty =
        DependencyProperty.Register("NormalizedAssemblies", typeof(Assembly[]), typeof(TestSuiteCreateOrEditModel), new UIPropertyMetadata(new Assembly[0], OnNormalizedAssembliesChanged));

    private static void OnNormalizedAssembliesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
      var model = (TestSuiteCreateOrEditModel)d;
      var oldLanguages = new HashSet<Language>(model.Languages);
      foreach (var assembly in (Assembly[])e.NewValue)
      {
        foreach (var language in Language.GetLanguages(assembly))
        {
          if (!oldLanguages.Remove(language))
            model.Languages.Add(language);
        }
      }
      foreach (var language in oldLanguages)
        model.Languages.Remove(language);
    }

    public ObservableCollection<Language> Languages
    {
      get;
      private set;
    }

    public ObservableCollection<GrammarDescriptor> DynamicExtensions
    {
      get;
      private set;
    }
  }
}
