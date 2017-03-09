using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Formatting;
using Microsoft.VisualStudio.Text.Operations;
using Nitra.ClientServer.Messages;
using Nitra.VisualStudio.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

using WpfHint2;
using WpfHint2.UIBuilding;

using D = System.Drawing;

namespace Nitra.VisualStudio.QuickInfo
{
  class NitraQuickInfoSource : IQuickInfoSource
  {
    public static readonly Hint Hint = new Hint { WrapWidth = 900.1 };
    public static NitraQuickInfoSource Current;

    public event Action Dismissed;

    ITextBuffer _textBuffer;
    ITextStructureNavigatorSelectorService _navigatorService;
    IWpfTextView _wpfTextView;
    PopupContainer _container;
    readonly DispatcherTimer _timer = new DispatcherTimer{ Interval=TimeSpan.FromMilliseconds(100), IsEnabled=false };
    IQuickInfoSession _session;
    D.Rectangle _activeAreaRect;
    D.Rectangle _hintRect;
    Window _subHuntWindow;
    FileModel _fileModel;
    TextViewModel _textViewModel;
    int _subhintOpen;

    public NitraQuickInfoSource(ITextBuffer textBuffer, ITextStructureNavigatorSelectorService navigatorService)
    {
      _textBuffer = textBuffer;
      _navigatorService = navigatorService;
    }

    public void AugmentQuickInfoSession(IQuickInfoSession session, IList<object> quickInfoContent, out ITrackingSpan applicableToSpan)
    {
      Current = this;
      _timer.Stop();
      _activeAreaRect = default(D.Rectangle);
      _activeAreaRect = default(D.Rectangle);
      if (_subHuntWindow != null)
        _subHuntWindow.Close();
      _subHuntWindow = null;
      _container = null;

      Debug.WriteLine("AugmentQuickInfoSession");

      _wpfTextView = (IWpfTextView)session.TextView;
      _fileModel = VsUtils.TryGetFileModel(_textBuffer);
      Debug.Assert(_fileModel != null);
      _textViewModel = VsUtils.GetOrCreateTextViewModel(_wpfTextView, _fileModel);
      Debug.Assert(_textViewModel != null);
      Hint.SetCallbacks(SubHintText, SpanClassToBrush);
      Hint.Click += Hint_Click;

      Hint.BackgroundResourceReference = EnvironmentColors.ToolTipBrushKey;
      Hint.ForegroundResourceReference = EnvironmentColors.ToolTipTextBrushKey;

      var snapshot = _textBuffer.CurrentSnapshot;
      var triggerPoint = session.GetTriggerPoint(snapshot);

      if (triggerPoint.HasValue)
      {
        _wpfTextView = (IWpfTextView)session.TextView;
        var extent       = GetTextExtent(triggerPoint.Value);
        var text         = extent.Span.GetText();
        if (!text.All(c => char.IsLetterOrDigit(c) || c == '_'))
        {
          applicableToSpan = null;
          return;
        }
        var trackingSpan = snapshot.CreateTrackingSpan(extent.Span, SpanTrackingMode.EdgeInclusive);
        applicableToSpan = trackingSpan;
        var rectOpt      = GetViewSpanRect(trackingSpan);
        if (!rectOpt.HasValue)
          return;

        _wpfTextView.VisualElement.MouseWheel += MouseWheel;
        _activeAreaRect = rectOpt.Value.ToRectangle();
        _session = session;
        session.Dismissed += OnDismissed;

        var container = new PopupContainer();

        Subscribe(container);

        container.LayoutUpdated += Container_LayoutUpdated;
        container.PopupOpened   += PopupOpened;
        _container = container;

        _container.Children.Add(new System.Windows.Controls.TextBlock() { Text="loading.." });

        quickInfoContent.Add(container);

        _timer.Tick += _timer_Tick;
        //Externals.FillRect(_activeAreaRect);
        return;
      }
      applicableToSpan = null;
    }

    void Hint_Click(Hint hint, string handler)
    {
      OnHintRefClic(handler);
    }

    public FrameworkElement ParseToFrameworkElement(string value)
    {
      var content = Hint.ParseToFrameworkElement(value);
      Current.Subscribe(content);
      return content;
    }

    void Subscribe(FrameworkElement element)
    {
      HintControl.AddClickHandler(element, OnHintRefClic);
      HintControl.AddMouseHoverHandler(element, OnMouseHover);
    }

    void OnHintRefClic(object sender, RoutedEventArgs e)
    {
      var hc = e.Source as HintControl;

      if (hc == null)
        return;

      if (hc.Handler != null)
        OnHintRefClic(hc.Handler);
    }

    public void SetHintData(string data)
    {
      Debug.WriteLine("SetHintData(" + data + ")");
      if (_container == null)
        return;

      if (data == "<hint></hint>")
      {
        _wpfTextView.VisualElement.Dispatcher.BeginInvoke((Action)ResetHintData_inUIThread);
        return;
      }

      var content = Hint.ParseToFrameworkElement(data);
      _wpfTextView.VisualElement.Dispatcher.BeginInvoke((Action<FrameworkElement>)SetHintData_inUIThread, content);
    }

    void ResetHintData_inUIThread()
    {
      Debug.WriteLine("ResetHintData_inUIThread");

      _container.Children.Clear();
      _container.Height = 0;
      _container.Width = 0;
      _container.MinHeight = 0;
      _container.MinWidth = 0;
      _container.UpdateLayout();
    }

    void SetHintData_inUIThread(FrameworkElement data)
    {
      if (_container == null)
        return;

      Debug.WriteLine("SetHintData_inUIThread");

      _container.Children.Clear();
      _container.Children.Add(data);
      _container.UpdateLayout();
    }

    public TextExtent GetTextExtent(SnapshotPoint triggerPoint)
    {
      var navigator = _navigatorService.GetTextStructureNavigator(_textBuffer);
      var extent = navigator.GetExtentOfWord(triggerPoint);
      return extent;
    }

    void Container_LayoutUpdated(object sender, EventArgs e)
    {
      var hintRectOpt = GetHintRect();
      _hintRect = hintRectOpt.HasValue ? (D.Rectangle)hintRectOpt.Value : default(D.Rectangle);
      
      Debug.WriteLine($"Container_LayoutUpdated Rect='{hintRectOpt}'  _container?.PopupOpt={_container?.PopupOpt}");
    }


    void PopupOpened(object sender, EventArgs e)
    {
      Debug.WriteLine($"PopupOpened Rect='{GetHintRect()}'");
    }

    void OnMouseHover(object sender, RoutedEventArgs e)
    {
      var hc = e.Source as HintControl;
      if (hc == null)
        return;

      if (hc.Hint != null)
      {
        _timer.Stop();
        _subhintOpen++;
        Debug.WriteLine($"_subhintOpen = {_subhintOpen}  OnMouseHover");
        _container.IsMouseOverAggregated = true;
        var subHuntWindow = Hint.ShowSubHint(hc, hc.Hint, null);
        subHuntWindow.Closed += SubHuntWindow_Closed;
        _subHuntWindow = subHuntWindow;
      }
    }

    private void SubHuntWindow_Closed(object sender, EventArgs e)
    {
      var subHuntWindow = (Window)sender;
      subHuntWindow.Closed -= SubHuntWindow_Closed;
      if (subHuntWindow == _subHuntWindow)
        _subHuntWindow = null;
      if (_container == null)
        return;
      _subhintOpen--;
      Debug.WriteLine($"_subhintOpen = {_subhintOpen}  SubHuntWindow_Closed");
      _container.IsMouseOverAggregated = false;
      _timer.Start();
    }

    private void MouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
    {
      if (_session != null)
        _session.Dismiss();
    }

    WinApi.RECT? GetHintRect()
    {
      if (_container == null || !_container.IsVisible)
        return null;

      Point pointToScreen = _container.PointToScreen(new Point(0, 0));
      IntPtr hWndOpt = WinApi.WindowFromPoint(pointToScreen);
      if (hWndOpt == IntPtr.Zero)
        return null;

      var hWndSource = HwndSource.FromHwnd(hWndOpt);

      if (hWndSource == null)
        return null;

      if (!ReferenceEquals(_container.RootElementOpt, hWndSource.RootVisual))
        return null;

      return WinApi.GetWindowRect(hWndOpt);
    }

    bool IsMouseOverHintOrActiveArea
    {
      get
      {
        if (_hintRect.IsEmpty)
          return true;

        var cursorPosOpt = WinApi.GetCursorPos();
        Debug.Assert(cursorPosOpt.HasValue);
        if (!cursorPosOpt.HasValue)
          return false;

        D.Point cursorPos = cursorPosOpt.Value;

        if (_hintRect.Contains(cursorPos) || _activeAreaRect.Contains(cursorPos))
          return true;

        return false;
      }
    }

    internal void Dismiss()
    {
      _session?.Dismiss();
    }

    void _timer_Tick(object sender, EventArgs e) { _wpfTextView.VisualElement.Dispatcher.BeginInvoke((Action)_timer_Tick_inUIThread); }

    void _timer_Tick_inUIThread()
    {
      if (IsMouseOverHintOrActiveArea)
        return;

      if (_container == null || _subhintOpen > 0)
        return;

      _timer.Stop();
      Debug.WriteLine("Dismiss current session by _timer_Tick");
      _session.Dismiss();
    }

    private void OnDismissed(object sender, EventArgs e)
    {
      Dismissed?.Invoke();

      if (_subHuntWindow != null)
        _subHuntWindow.Close();

      _container.LayoutUpdated           -= Container_LayoutUpdated;
      _container.PopupOpened             -= PopupOpened;
      _session.Dismissed                 -= OnDismissed;
      _wpfTextView.VisualElement.MouseWheel -= MouseWheel;

      _timer.Stop();
      _timer.Tick -= _timer_Tick;
      Current = null;
      _container = null;
      Debug.WriteLine("Dismissed");
    }

    Brush SpanClassToBrush(string spanClass)
    {
      return _fileModel.SpanClassToBrush(spanClass, _wpfTextView);
    }

    string SubHintText(string symbolIdText)
    {
      var symbolId = int.Parse(symbolIdText);
      var fileModel = _fileModel;
      var client = fileModel.Server.Client;
      client.Send(new ClientMessage.GetSubHint(GetCurrntProjectId(), symbolId));
      var msg = client.Receive<ServerMessage.SubHint>();
      return msg.text;
    }

    ProjectId GetCurrntProjectId()
    {
      var project = _fileModel.Hierarchy.GetProject();
      return new ProjectId(_fileModel.Server.Client.StringManager.GetId(project.FullName));
    }

    void OnHintRefClic(string handler)
    {
      string tag = null;
      string data = null;

      var colonIndex = handler.IndexOf(':');
      if (colonIndex <= 0)
        tag = handler;
      else
      {
        tag = handler.Substring(0, colonIndex);
        data = handler.Substring(colonIndex + 1, handler.Length - colonIndex - 1);
      }

      switch (tag)
      {
        case "goto":
          {
            var rx = new System.Text.RegularExpressions.Regex(@"(?<path>.*)\[(?<pos>\d*),\s*(?<len>\d*)\]");
            var res = rx.Match(data);
            if (res.Success)
            {
              var path = res.Groups["path"].Value;
              var pos = int.Parse(res.Groups["pos"].Value);
              var len = int.Parse(res.Groups["len"].Value);
              if (path.StartsWith("file://", StringComparison.InvariantCultureIgnoreCase))
                return;
              VsUtils.NavigateTo(_fileModel.Server.ServiceProvider, path, pos);
              _session?.Dismiss();
            }
          }
          break;
        case "goto line":
          {
            var rx = new System.Text.RegularExpressions.Regex(@"(?<path>.*)\((?<line>\d*),\s*(?<col>\d*)\)");
            var res = rx.Match(data);
            if (res.Success)
            {
              var path = res.Groups["path"].Value;
              var line = int.Parse(res.Groups["line"].Value);
              var col  = int.Parse(res.Groups["col"].Value);
              if (path.StartsWith("file://", StringComparison.InvariantCultureIgnoreCase))
                return;
              VsUtils.NavigateTo(_fileModel.Server.ServiceProvider, path, line, col);
              _session?.Dismiss();
            }
          }
          break;
        default:
          break;
      }
    }

    public Rect? GetViewSpanRect(ITrackingSpan viewSpan)
    {
      var wpfTextView = _wpfTextView;
      if (wpfTextView == null || wpfTextView.TextViewLines == null || wpfTextView.IsClosed)
        return null;

      return VsUtils.GetViewSpanRect(_wpfTextView, viewSpan.GetSpan(wpfTextView.TextSnapshot));
    }

    public void Dispose()
    {
    }
  }
}
