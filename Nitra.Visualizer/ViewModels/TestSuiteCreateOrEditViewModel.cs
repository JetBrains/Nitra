using System;
using System.Collections.Immutable;
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
    public string Title { get; set; }
    public string RootFolder { get; }
    public string SuiteName { get; set; }
    public string SuitPath { get { return Path.Combine(Path.GetFullPath(RootFolder), SuiteName); } }
    public bool IsCreate { get; private set; }

    [Reactive]
    public string ParserLibsText { get; set; }
    [Reactive]
    public string[] ParserLibPaths { get; set; }

    [Reactive]
    public string LibsText { get; set; }
    [Reactive]
    public string[] Libs { get; set; }

    public ReactiveList<LanguageInfo> Languages { get; private set; }
    public ReactiveList<DynamicExtensionViewModel> DynamicExtensions { get; private set; }

    public LanguageInfo? SelectedLanguage { get; set; }
    private LanguageInfo? _oldLanguage;

    public TestSuiteCreateOrEditViewModel(SuiteVm baseSuite, NitraClient client, bool isCreate)
    {
      
      IsCreate = isCreate;
      Title = isCreate ? "New test suite" : "Edit test suite";
      ParserLibsText = "";
      LibsText = "";
      Languages = new ReactiveList<LanguageInfo>();
      DynamicExtensions = new ReactiveList<DynamicExtensionViewModel>();

      if (baseSuite != null) {
        RootFolder = baseSuite.Workspace.RootFolder;
        SuiteName = baseSuite.Name;
        Libs = baseSuite.Config.Libs;
      }

      this.WhenAnyValue(vm => vm.ParserLibsText)
          .Select(GetParserLibPaths)
          .Do(libs => ParserLibPaths = libs)
          .Subscribe(libs => UpdateParserLibs(client, libs));

      this.WhenAnyValue(vm => vm.LibsText)
          .Select(GetLibPaths)
          .Subscribe(libs => Libs = libs);
      
      this.WhenAnyValue(vm => vm.SelectedLanguage)
          .Subscribe(UpdateSelectedLanguage);
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

    private void UpdateParserLibs(NitraClient client, string[] libs)
    {
      var oldLanguages = Languages.ToHashSet();
      var oldDynamicExtensions = DynamicExtensions.ToDictionary(x => x.Name);
      var libsArray = libs.ToImmutableArray();

      client.Send(new ClientMessage.GetLibsMetadata(libsArray));
      var libsMetadata = client.Receive<ServerMessage.LibsMetadata>().metadatas;

      client.Send(new ClientMessage.GetLibsSyntaxModules(libsArray));
      var libsSyntaxModules = client.Receive<ServerMessage.LibsSyntaxModules>().modules;

      var data = libsMetadata.Zip(libsSyntaxModules, (metadata, syntax) => new {metadata, syntax});

      foreach (var a in data) {
        var languages = a.metadata.Languages;
        var syntaxModules = a.syntax.Modules;

        foreach (var language in languages)
          if (!oldLanguages.Remove(language))
            Languages.Add(language);

        foreach (var syntaxModule in syntaxModules)
          if (!oldDynamicExtensions.Remove(syntaxModule))
            DynamicExtensions.Add(new DynamicExtensionViewModel(syntaxModule));
      }

      foreach (var language in oldLanguages)
        Languages.Remove(language);

      foreach (var pair in oldDynamicExtensions)
        DynamicExtensions.Remove(pair.Value);
    }

    private string[] GetParserLibPaths(string assemblies)
    {
      return Utils.GetAssemblyPaths(assemblies)
                  .Select(ToFullSuitePath)
                  .ToArray();
    }

    private string[] GetLibPaths(string libs)
    {
      return Utils.GetAssemblyPaths(libs)
                  .Select(libPath => {
                    var fullAssemblyPath = ToFullSuitePath(libPath);
                    return File.Exists(fullAssemblyPath)
                           ? Utils.MakeRelativePath(SuitPath, true, fullAssemblyPath, false)
                           : libPath;
                  })
                  .Distinct()
                  .ToArray();
    }

    private string ToFullSuitePath(string path)
    {
      return Path.IsPathRooted(path) ? path : Path.Combine(SuitPath, path);
    }
  }
}
