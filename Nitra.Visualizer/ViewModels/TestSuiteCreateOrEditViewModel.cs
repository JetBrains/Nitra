using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using Nitra.ClientServer.Client;
using Nitra.ClientServer.Messages;
using Nitra.ViewModels;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using File = System.IO.File;

namespace Nitra.Visualizer.ViewModels
{
  public class TestSuiteCreateOrEditViewModel : ReactiveObject
  {
    [Reactive]
    public string Title { get; set; }
    [Reactive]
    public string RootFolder { get; set; }
    [Reactive]
    public string SuiteName { get; set; }
    [Reactive]
    public string SuitPath { get; set; }
    public bool IsCreate { get; private set; }
    
    public IReactiveList<ParserLibViewModel> ParserLibs { get; set; }
    public IReactiveList<ProjectSupportViewModel> ProjectSupports { get; set; }
    public IReactiveList<string> References { get; set; }
    public IReactiveList<LanguageInfo> Languages { get; private set; }
    public IReactiveList<DynamicExtensionViewModel> DynamicExtensions { get; private set; }

    public LanguageInfo? SelectedLanguage { get; set; }

    private LanguageInfo? _oldLanguage;

    public TestSuiteCreateOrEditViewModel(NitraClient client)
    {
      Languages = new ReactiveList<LanguageInfo>();
      ParserLibs = new ReactiveList<ParserLibViewModel>();
      ProjectSupports = new ReactiveList<ProjectSupportViewModel>();
      References = new ReactiveList<string>();
      DynamicExtensions = new ReactiveList<DynamicExtensionViewModel>();
      
      ParserLibs.Changed
                .Subscribe(_ => UpdateParserLibs(client));

      Languages.Changed.Subscribe(_ => RemoveDuplicates(Languages, li => ToFullSuitePath(li.Path)));

      ProjectSupports.Changed
                     .Subscribe(_ => {
                       if (!ProjectSupports.Any(ps => ps.IsSelected) && ProjectSupports.Count > 0)
                         ProjectSupports[0].IsSelected = true;
                     });

      this.WhenAnyValue(vm => vm.SelectedLanguage)
          .Subscribe(UpdateSelectedLanguage);

      this.WhenAnyValue(vm => vm.RootFolder, vm => vm.SuiteName)
        .Where(t => t.Item1 != null && t.Item2 != null)
        .Select(t => Path.Combine(Path.GetFullPath(t.Item1), t.Item2))
        .Subscribe(path => SuitPath = path);
    }

    private void UpdateSelectedLanguage(LanguageInfo? newLanguage)
    {
      if (newLanguage == null)
        return;

      var languageInfo = newLanguage.Value;
      var suiteName = SuiteName;

      if (string.IsNullOrEmpty(suiteName) || (_oldLanguage != null && suiteName == _oldLanguage.Value.Name))
        SuiteName = languageInfo.Name;

      foreach (var dynamicExtension in DynamicExtensions)
        dynamicExtension.IsEnabled = languageInfo.DynamicExtensions.Any(ei => ei.Name == dynamicExtension.Name);

      _oldLanguage = newLanguage;
    }

    private void UpdateParserLibs(NitraClient client)
    {
      try {
        RemoveDuplicates(ParserLibs, pl => ToFullSuitePath(pl.Path));

        var libsArray = ParserLibs.Select(vm => ToFullSuitePath(vm.Path)).ToImmutableArray();

        client.Send(new ClientMessage.GetLibsMetadata(libsArray));
        var libsMetadata = client.Receive<ServerMessage.LibsMetadata>().metadatas;

        client.Send(new ClientMessage.GetLibsSyntaxModules(libsArray));
        var libsSyntaxModules = client.Receive<ServerMessage.LibsSyntaxModules>().modules;

        client.Send(new ClientMessage.GetLibsProjectSupports(libsArray));
        var libsProjectSupports = client.Receive<ServerMessage.LibsProjectSupports>().libs;
      
        Languages.Clear();
        DynamicExtensions.Clear();
        ProjectSupports.Clear();

        for (int i = 0; i < libsMetadata.Length; i++) {
          var languages = libsMetadata[i].Languages;
          var syntaxModules = libsSyntaxModules[i].Modules;
          var projectSupports = libsProjectSupports[i].ProjectSupports;

          foreach (var language in languages)
            Languages.Add(language);

          foreach (var syntaxModule in syntaxModules)
            DynamicExtensions.Add(new DynamicExtensionViewModel(syntaxModule));

          foreach (var projectSupport in projectSupports) {
            ProjectSupports.Add(new ProjectSupportViewModel(projectSupport.Caption, projectSupport.Path, projectSupport));
          }
        }
      } catch (Exception e) {
        Debug.WriteLine("Failed to update parser lib metadata" + Environment.NewLine + e);
      }
    }

    private void RemoveDuplicates<T, TKey>(IReactiveList<T> list, Func<T, TKey> keySelector)
    {
      var distinct = list.Distinct(keySelector).ToList();
      if (distinct.Count != list.Count) {
        list.Clear();
        list.AddRange(distinct);
      }
    }

    private string ToFullSuitePath(string path)
    {
      return Path.IsPathRooted(path) ? path : new Uri(Path.Combine(SuitPath, path)).LocalPath;
    }
  }

  public class ProjectSupportViewModel : ReactiveObject
  {
    public bool IsSelected { get; set; }
    public string Caption { get; set; }
    public string Path { get; set; }
    public ProjectSupport Source { get; set; }

    public ProjectSupportViewModel(string caption, string path, ProjectSupport source)
    {
      Caption = caption;
      Path = path;
      Source = source;
    }
  }

  public class ParserLibViewModel : ReactiveObject
  {
    [Reactive]
    public string Path { get; set; }

    public ParserLibViewModel(string path)
    {
      Path = path;
    }
  }
}
