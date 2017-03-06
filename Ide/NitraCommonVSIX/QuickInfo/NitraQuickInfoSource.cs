using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Formatting;
using Microsoft.VisualStudio.Text.Operations;

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

    public event Action Dismissed;

    ITextBuffer _textBuffer;
    ITextStructureNavigatorSelectorService _navigatorService;
    IWpfTextView _textView;
    PopupContainer _container;
    readonly DispatcherTimer _timer = new DispatcherTimer{ Interval=TimeSpan.FromMilliseconds(100), IsEnabled=false };
    IQuickInfoSession _session;
    D.Rectangle _activeAreaRect;
    D.Rectangle _hintRect;
    Window _subHuntWindow;
    Action<string> _onClick;
    //HwndSource _currentPoupupWndSource;

    public NitraQuickInfoSource(ITextBuffer textBuffer, ITextStructureNavigatorSelectorService navigatorService)
    {
      _textBuffer = textBuffer;
      _navigatorService = navigatorService;
    }

    public void AugmentQuickInfoSession(IQuickInfoSession session, IList<object> quickInfoContent, out ITrackingSpan applicableToSpan)
    {
      _timer.Stop();
      _activeAreaRect = default(D.Rectangle);
      _activeAreaRect = default(D.Rectangle);
      if (_subHuntWindow != null)
        _subHuntWindow.Close();
      _subHuntWindow = null;
      _container = null;

      Debug.WriteLine("AugmentQuickInfoSession");

      Hint.BackgroundResourceReference = EnvironmentColors.ToolTipBrushKey;
      Hint.ForegroundResourceReference = EnvironmentColors.ToolTipTextBrushKey;

      var snapshot = _textBuffer.CurrentSnapshot;
      var triggerPoint = session.GetTriggerPoint(snapshot);

      if (triggerPoint.HasValue)
      {
        _textView = (IWpfTextView)session.TextView;
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

        _textView.VisualElement.MouseWheel += MouseWheel;
        _activeAreaRect = rectOpt.Value.ToRectangle();
        _session = session;
        session.Dismissed += OnDismissed;

        var container = new PopupContainer();

        HintControl.AddClickHandler(container, OnClick);
        HintControl.AddMouseHoverHandler(container, OnMouseHover);

        container.LayoutUpdated += Container_LayoutUpdated;
        container.PopupOpened   += PopupOpened;
        _container = container;

        _container.Children.Add(new System.Windows.Controls.TextBlock() { Text="loading.." });

        quickInfoContent.Add(container);

        _timer.Tick += _timer_Tick;
        _timer.Start();
        //Externals.FillRect(_activeAreaRect);
        return;
      }
      applicableToSpan = null;
    }

    private void OnClick(object sender, RoutedEventArgs e)
    {
      if (_onClick == null)
        return;

      var hc = e.Source as HintControl;

      if (hc == null)
        return;

      if (hc.Handler != null)
        _onClick.Invoke(hc.Handler);
    }

    public void SetHintData(string data, Func<string, string> getHintContent, Func<string, Brush> mapBrush, Action<string> onClick)
    {
      Debug.WriteLine("SetHintData(" + data + ")");
      if (_container == null)
        return;

      _onClick = onClick;

      if (data == "<hint></hint>")
      {
        _textView.VisualElement.Dispatcher.BeginInvoke((Action)ResetHintData_inUIThread);
        return;
      }

      //"<hint>SubHint <hint value='SubSubHint 1'>active area 1</hint>! <hint value='SubSubHint 2'>active area 2</hint></hint>"
      Hint.SetCallbacks(getHintContent, mapBrush);
      var content = Hint.ParseToFrameworkElement(data);
      _textView.VisualElement.Dispatcher.BeginInvoke((Action<FrameworkElement>)SetHintData_inUIThread, content);
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
      if (_container == null)
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

      //if (_currentPoupupWndSource == null || _currentPoupupWndSource.Handle != hWndSource.Handle)
      //{
      //  _currentPoupupWndSource?.RemoveHook(HwndSourceHook);
      //  _currentPoupupWndSource = hWndSource;
      //  hWndSource.AddHook(HwndSourceHook);
      //}

      return WinApi.GetWindowRect(hWndOpt);
    }

    //IntPtr HwndSourceHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    //{
    //  const int WM_SYSCOMMAND = 0x0112;
    //  const int SC_MOVE = 0xF010;
    //  const int WM_MOVING = 0x0216;
    //
    //  switch (msg)
    //  {
    //    case WM_SYSCOMMAND:
    //      int command = wParam.ToInt32() & 0xfff0;
    //      if (command == SC_MOVE)
    //        handled = true;
    //      break;
    //    case WM_MOVING:
    //      handled = true;
    //      break;
    //  }
    //
    //  return IntPtr.Zero;
    //}

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

    void _timer_Tick(object sender, EventArgs e) { _textView.VisualElement.Dispatcher.BeginInvoke((Action)_timer_Tick_inUIThread); }

    void _timer_Tick_inUIThread()
    {
      if (IsMouseOverHintOrActiveArea)
        return;

      if (_container == null || _container.IsMouseOverAggregated)
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

      //if (_currentPoupupWndSource != null)
      //  _currentPoupupWndSource.RemoveHook(HwndSourceHook);
      //_currentPoupupWndSource = null;

      _container.LayoutUpdated           -= Container_LayoutUpdated;
      _container.PopupOpened             -= PopupOpened;
      _session.Dismissed                 -= OnDismissed;
      _textView.VisualElement.MouseWheel -= MouseWheel;

      _timer.Stop();
      _timer.Tick -= _timer_Tick;
      _container = null;
      Debug.WriteLine("Dismissed");
    }

    public Rect? GetViewSpanRect(ITrackingSpan viewSpan)
    {
      var wpfTextView = _textView;
      if (wpfTextView == null || wpfTextView.TextViewLines == null || wpfTextView.IsClosed)
        return null;

      return VsUtils.GetViewSpanRect(_textView, viewSpan.GetSpan(wpfTextView.TextSnapshot));
    }

    public void Dispose()
    {
    }
  }
}
