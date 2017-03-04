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
    ITextBuffer _textBuffer;
    ITextStructureNavigatorSelectorService _navigatorService;
    IWpfTextView _textView;
    Hint _hint = new Hint { WrapWidth = 900.1 };
    PopupContainer _container;
    readonly DispatcherTimer _timer = new DispatcherTimer{ Interval=TimeSpan.FromMilliseconds(100), IsEnabled=false };
    IQuickInfoSession _session;
    D.Rectangle _activeAreaRect;
    D.Rectangle _hintRect;
    Window _subHuntWindow;
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

      _hint.BackgroundResourceReference = EnvironmentColors.ToolTipBrushKey;
      _hint.ForegroundResourceReference = EnvironmentColors.ToolTipTextBrushKey;

      var snapshot = _textBuffer.CurrentSnapshot;
      var triggerPoint = session.GetTriggerPoint(snapshot);

      if (triggerPoint.HasValue)
      {
        _textView = (IWpfTextView)session.TextView;
        var navigator    = _navigatorService.GetTextStructureNavigator(_textBuffer);
        var extent       = navigator.GetExtentOfWord(triggerPoint.Value);
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
        session.Dismissed += Dismissed;

        var container = new PopupContainer();

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

    public void SetHintData(string data, Func<string, string> getHintContent, Func<string, Brush> mapBrush)
    {
      Debug.WriteLine("SetHintData(" + data + ")");
      if (_container == null)
        return;

      if (data == "<hint></hint>")
      {
        _textView.VisualElement.Dispatcher.BeginInvoke((Action)ResetHintData_inUIThread);
        return;
      }

      //"<hint>SubHint <hint value='SubSubHint 1'>active area 1</hint>! <hint value='SubSubHint 2'>active area 2</hint></hint>"
      _hint.SetCallbacks(getHintContent, mapBrush);
      var content = _hint.ParseToFrameworkElement(data);
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
        var subHuntWindow = _hint.ShowSubHint(hc, hc.Hint, null);
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

    private void Dismissed(object sender, EventArgs e)
    {
      if (_subHuntWindow != null)
        _subHuntWindow.Close();

      //if (_currentPoupupWndSource != null)
      //  _currentPoupupWndSource.RemoveHook(HwndSourceHook);
      //_currentPoupupWndSource = null;

      _container.LayoutUpdated           -= Container_LayoutUpdated;
      _container.PopupOpened             -= PopupOpened;
      _session.Dismissed                 -= Dismissed;
      _textView.VisualElement.MouseWheel -= MouseWheel;

      _timer.Stop();
      _timer.Tick -= _timer_Tick;
      _container = null;
      Debug.WriteLine("Dismissed");
    }

    Rect? GetViewSpanRect(ITrackingSpan viewSpan)
    {
      var wpfTextView = _textView;
      if (wpfTextView == null || wpfTextView.TextViewLines == null || wpfTextView.IsClosed)
        return new Rect?();
      SnapshotSpan span = viewSpan.GetSpan(wpfTextView.TextSnapshot);
      var nullable = new Rect?();
      if (span.Length > 0)
      {
        double num1 = double.MaxValue;
        double num2 = double.MaxValue;
        double val1_1 = double.MinValue;
        double val1_2 = double.MinValue;
        foreach (TextBounds textBounds in wpfTextView.TextViewLines.GetNormalizedTextBounds(span))
        {
          num1 = Math.Min(num1, textBounds.Left);
          num2 = Math.Min(num2, textBounds.TextTop);
          val1_1 = Math.Max(val1_1, textBounds.Right);
          val1_2 = Math.Max(val1_2, textBounds.TextBottom + 1.0);
        }
        IWpfTextViewLine containingBufferPosition = wpfTextView.TextViewLines.GetTextViewLineContainingBufferPosition(span.Start);
        if (containingBufferPosition != null)
        {
          TextBounds extendedCharacterBounds = containingBufferPosition.GetExtendedCharacterBounds(span.Start);
          if (extendedCharacterBounds.Left < val1_1 && extendedCharacterBounds.Left >= wpfTextView.ViewportLeft && extendedCharacterBounds.Left < wpfTextView.ViewportRight)
            num1 = extendedCharacterBounds.Left;
        }
        ITextViewLine textViewLine = (ITextViewLine)wpfTextView.TextViewLines.GetTextViewLineContainingBufferPosition(span.End);
        if (textViewLine != null && textViewLine.Start == span.End)
          val1_2 = Math.Max(val1_2, textViewLine.TextBottom + 1.0);
        if (num1 < val1_1)
          nullable = new Rect(num1, num2, val1_1 - num1, val1_2 - num2);
      }
      else
      {
        ITextViewLine textViewLine = (ITextViewLine)wpfTextView.TextViewLines.GetTextViewLineContainingBufferPosition(span.Start);
        if (textViewLine != null)
        {
          TextBounds characterBounds = textViewLine.GetCharacterBounds(span.Start);
          nullable = new Rect?(new Rect(characterBounds.Left, characterBounds.TextTop, 0.0, characterBounds.TextHeight + 1.0));
        }
      }
      if (!nullable.HasValue || nullable.Value.IsEmpty)
        return new Rect?();
      Rect rect1 = new Rect(wpfTextView.ViewportLeft, wpfTextView.ViewportTop, wpfTextView.ViewportWidth, wpfTextView.ViewportHeight);
      Rect rect2 = nullable.Value;
      rect2.Intersect(rect1);
      return new Rect?(new Rect(this.GetScreenPointFromTextXY(rect2.Left, rect2.Top), this.GetScreenPointFromTextXY(rect2.Right, rect2.Bottom)));
    }

    private Point GetScreenPointFromTextXY(double x, double y)
    {
      var wpfTextView = (IWpfTextView)_textView;
      return wpfTextView.VisualElement.PointToScreen(new Point(x - wpfTextView.ViewportLeft, y - wpfTextView.ViewportTop));
    }

    public void Dispose()
    {
    }
  }
}
