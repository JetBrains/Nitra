using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows;
using Nitra.ViewModels;
using Nitra.Visualizer.Properties;

namespace Nitra.Visualizer
{
  internal class TestSuiteCreateOrEditModel : DependencyObject
  {
    private readonly Settings _settings;

    public TestSuiteCreateOrEditModel(Settings settings, bool isCreate)
    {
      _settings = settings;

      IsCreate = isCreate;
      Title = isCreate ? "New test suite" : "Edit test suite";
      RootFolder = Path.GetDirectoryName(_settings.CurrentSolution);
      Languages = new ObservableCollection<Language>();
      DynamicExtensions = new ObservableCollection<DynamicExtensionModel>();
    }

    public bool IsCreate
    {
      get;
      private set;
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

    public string LibReferences
    {
      get { return (string)GetValue(AssembliesProperty); }
      set { SetValue(AssembliesProperty, value); }
    }

    public static readonly DependencyProperty LibReferencesProperty =
        DependencyProperty.Register("Assemblies", typeof(string), typeof(TestSuiteCreateOrEditModel), new FrameworkPropertyMetadata("", OnLibReferencesChanged));

    private static void OnLibReferencesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
      var model = (TestSuiteCreateOrEditModel)d;
      var normalized = new List<string>();
      foreach (var libPath in Utils.GetAssemblyPaths((string)e.NewValue))
      {
        var fullAssemblyPath = Path.GetFullPath(Path.IsPathRooted(libPath) ? libPath : Path.Combine(model.RootFolder, libPath));
        var relativePath = Utils.MakeRelativePath(model.RootFolder, true, fullAssemblyPath, false);
        normalized.Add(relativePath);
      }
      model.NormalizedLibReferences = normalized.ToArray();
    }

    public string[] NormalizedLibReferences { get; set; }

    public string NormalizedLibReferencesText
    {
      get
      {
        var text = new StringBuilder();
        foreach (var assembly in NormalizedAssemblies)
          text.AppendLine(Utils.MakeRelativePath(@from: RootFolder, isFromDir: true, @to: assembly.Location, @isToDir: false));
        return text.ToString();
      }
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
        DependencyProperty.Register("NormalizedAssemblies", typeof(Assembly[]), typeof(TestSuiteCreateOrEditModel), new FrameworkPropertyMetadata(TestSuiteVm.NoAssembiles, OnNormalizedAssembliesChanged));

    private static void OnNormalizedAssembliesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
      var model                = (TestSuiteCreateOrEditModel)d;
      var oldLanguages         = model.Languages.ToHashSet();
      var oldDynamicExtensions = model.DynamicExtensions.ToDictionary(x => x.Descriptor);
      foreach (var assembly in (Assembly[])e.NewValue)
      {
        foreach (var language in Language.GetLanguages(assembly))
        {
          if (!oldLanguages.Remove(language))
            model.Languages.Add(language);
        }

        foreach (var descriptor in GrammarDescriptor.GetDescriptors(assembly))
        {
          if (!oldDynamicExtensions.Remove(descriptor))
            model.DynamicExtensions.Add(new DynamicExtensionModel(descriptor));
        }
      }
      foreach (var language in oldLanguages)
        model.Languages.Remove(language);
      foreach (var pair in oldDynamicExtensions)
        model.DynamicExtensions.Remove(pair.Value);
    }

    public Language SelectedLanguage
    {
      get { return (Language)GetValue(SelectedLanguageProperty); }
      set { SetValue(SelectedLanguageProperty, value); }
    }

    public static readonly DependencyProperty SelectedLanguageProperty =
        DependencyProperty.Register("SelectedLanguage", typeof(Language), typeof(TestSuiteCreateOrEditModel), new FrameworkPropertyMetadata(null, OnSelectedLanguageChanged));

    private static void OnSelectedLanguageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
      var model = (TestSuiteCreateOrEditModel)d;
      var newLanguage = (Language)e.NewValue;
      if (newLanguage == null)
        return;

      var oldLanguage = (Language)e.OldValue;
      var suiteName = model.SuiteName;
      if (string.IsNullOrEmpty(suiteName) || (oldLanguage != null && suiteName == oldLanguage.Name))
        model.SuiteName = newLanguage.Name;

      foreach (var dynamicExtension in model.DynamicExtensions)
        dynamicExtension.IsEnabled = !newLanguage.CompositeGrammar.Grammars.Contains(dynamicExtension.Descriptor);
    }

    public ObservableCollection<Language> Languages
    {
      get;
      private set;
    }

    public ObservableCollection<DynamicExtensionModel> DynamicExtensions
    {
      get;
      private set;
    }
  }

  internal sealed class DynamicExtensionModel : DependencyObject
  {
    private readonly GrammarDescriptor _descriptor;

    public DynamicExtensionModel(GrammarDescriptor descriptor)
    {
      _descriptor = descriptor;
    }

    public GrammarDescriptor Descriptor
    {
      get { return _descriptor; }
    }

    public string Name
    {
      get { return _descriptor.FullName; }
    }

    public bool IsChecked
    {
      get { return (bool)GetValue(IsCheckedProperty); }
      set { SetValue(IsCheckedProperty, value); }
    }

    public static readonly DependencyProperty IsCheckedProperty =
        DependencyProperty.Register("IsChecked", typeof(bool), typeof(DynamicExtensionModel), new FrameworkPropertyMetadata(false));

    public bool IsEnabled
    {
      get { return (bool)GetValue(IsEnabledProperty); }
      set { SetValue(IsEnabledProperty, value); }
    }

    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.Register("IsEnabled", typeof(bool), typeof(DynamicExtensionModel), new FrameworkPropertyMetadata(true, OnIsEnabledChanged));

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
      var model = (DynamicExtensionModel)d;
      if (!(bool)e.NewValue)
        model.IsChecked = false;
    }
  }
}
