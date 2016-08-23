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
    public string RootFolder { get; set; }
    public string SuiteName { get; set; }
    public string SuitPath { get { return Path.Combine(Path.GetFullPath(RootFolder), SuiteName); } }
    public bool IsCreate { get; private set; }
    
    public IReactiveList<string> ParserLibs { get; set; }
    public IReactiveList<string> References { get; set; }
    public IReactiveList<LanguageInfo> Languages { get; private set; }
    public IReactiveList<DynamicExtensionViewModel> DynamicExtensions { get; private set; }

    public LanguageInfo? SelectedLanguage { get; set; }
    private LanguageInfo? _oldLanguage;

    public TestSuiteCreateOrEditViewModel(NitraClient client)
    {
      Languages = new ReactiveList<LanguageInfo>();
      ParserLibs = new ReactiveList<string>();
      References = new ReactiveList<string>();
      DynamicExtensions = new ReactiveList<DynamicExtensionViewModel>();
      
      this.WhenAnyValue(vm => vm.ParserLibs)
          .Subscribe(libs => UpdateParserLibs(client, libs));
            
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

    private void UpdateParserLibs(NitraClient client, IReactiveList<string> libs)
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
  }
}
