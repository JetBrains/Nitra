using JetBrains.Application.Settings;
using JetBrains.DocumentModel;
using JetBrains.ReSharper.Feature.Services.Daemon;
using JetBrains.ReSharper.Psi;
using JetBrains.Util;

using XXNamespaceXX.ProjectSystem;
using Nitra;
using Nitra.ProjectSystem;

using System;
using System.Collections.Generic;
using XXNamespaceXX;

[assembly: RegisterStaticHighlightingsGroup(NitraError.NitraErrorGroup, "XXLanguageXX Errors", false)]

namespace XXNamespaceXX
{
  [Language(typeof(XXLanguageXXLanguage))]
  public class NitraDaemonBehavior : ILanguageSpecificDaemonBehavior
  {
    public ErrorStripeRequest InitialErrorStripe(IPsiSourceFile sourceFile)
    {
      var isNitraFile = ReSharperSolution.XXLanguageXXSolution.IsNitraFile(sourceFile);
      return isNitraFile ? ErrorStripeRequest.STRIPE_AND_ERRORS : ErrorStripeRequest.NONE;
    }

    public bool CanShowErrorBox
    {
      get { return true; }
    }

    public bool RunInSolutionAnalysis
    {
      get { return true; }
    }

    public bool RunInFindCodeIssues
    {
      get { return true; }
    }
  }

  [DaemonStage]
  public class CompilerMessagesDaemon : IDaemonStage
  {
    public IEnumerable<IDaemonStageProcess> CreateProcess(IDaemonProcess process, IContextBoundSettingsStore settings, DaemonProcessKind processKind)
    {
      return new[] { new CompilerMessagesStageProcess(process, settings) };
    }

    public ErrorStripeRequest NeedsErrorStripe(IPsiSourceFile sourceFile, IContextBoundSettingsStore settingsStore)
    {
      var isNitraFile = ReSharperSolution.XXLanguageXXSolution.IsNitraFile(sourceFile);
      return isNitraFile ? ErrorStripeRequest.STRIPE_AND_ERRORS : ErrorStripeRequest.NONE;
    }
  }

  public sealed class CompilerMessagesStageProcess : IDaemonStageProcess
  {
    private readonly IDaemonProcess             _daemonProcess;
    private readonly IContextBoundSettingsStore _settings;

    public CompilerMessagesStageProcess(IDaemonProcess daemonProcess, IContextBoundSettingsStore settings)
    {
      _daemonProcess   = daemonProcess;
      _settings        = settings;
    }

    public void Execute(Action<DaemonStageResult> committer)
    {
      var nitraFile = ReSharperSolution.XXLanguageXXSolution.GetNitraFile(_daemonProcess.SourceFile);
      if (nitraFile == null)
        return;
      var messages  = nitraFile.GetCompilerMessages();

      var highlightingInfos = new List<HighlightingInfo>(messages.Length);
      var consumer = new DefaultHighlightingConsumer(this, _settings);

      foreach (var message in messages)
      {
        if (_daemonProcess.InterruptFlag)
          return;

        var highlighting = new NitraError(message);
        consumer.ConsumeHighlighting(new HighlightingInfo(highlighting.DocumentRange, highlighting));
        //highlightingInfos.Add(new HighlightingInfo(highlighting.DocumentRange, highlighting));
      }

      committer(new DaemonStageResult(consumer.Highlightings));
      //committer(new DaemonStageResult(highlightingInfos));
    }

    public IDaemonProcess DaemonProcess
    {
      get { return _daemonProcess; }
    }
  }

  [StaticSeverityHighlighting(Severity.ERROR, NitraErrorGroup, OverlapResolve = OverlapResolveKind.ERROR)]
  public class NitraError : IHighlighting
  {
    public const string NitraErrorGroup = "XXLanguageXXErrors";

    public XXLanguageXXFile NitraFile { get; private set; }
    private readonly CompilerMessage _compilerMessage;
    public DocumentRange DocumentRange { get; private set; }

    public NitraError(CompilerMessage compilerMessage)
    {
      _compilerMessage = compilerMessage;
      
      var loc        = _compilerMessage.Location;
      var nitraFile = (XXLanguageXXFile)loc.Source.File;
      var span       = loc.Span;

      NitraFile = nitraFile;
      var doc = nitraFile.Document;
      // ReSharper don't show message for empty span
      if (span.IsEmpty)
      {
        if (span.StartPos > 0)
          span = new NSpan(span.StartPos - 1, span.EndPos);
        else if (span.EndPos < doc.GetTextLength())
          span = new NSpan(span.StartPos, span.EndPos + 1);
      }
      DocumentRange = new DocumentRange(doc, new TextRange(span.StartPos, span.EndPos));
    }

    public string ToolTip               { get { return _compilerMessage.Text; } }
    public string ErrorStripeToolTip    { get { return ToolTip; } }
    public int    NavigationOffsetPatch { get { return 0; } }

    public DocumentRange CalculateRange()
    {
      return DocumentRange;
    }

    public bool IsValid()
    {
      return true;
    }
  }
}
