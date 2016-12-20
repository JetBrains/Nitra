using System;
using System.Windows;
using System.Windows.Interop;
using System.Diagnostics;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Controls;

namespace WpfHint2
{
  public class Hint
  {
    private HintWindow _hintWindow;
    private HintSource _hintSource;
    private string _text;
    private double _wrapWidth = 1200.0;
    private Func<string, string> _getHintContent;

    public event Action<Hint, string> Click;
    public event Action<Hint> Closed;

    public Func<string, Brush> MapBrush { get; private set; }


    /// <summary>
    /// Deafult = 400.0
    /// </summary>      
    public double WrapWidth
    {
      get { return _wrapWidth; }
      set
      {
        _wrapWidth = value;

        if (_hintWindow != null)
          _hintWindow.WrapWidth = value;
      }
    }

    /// <summary>
    /// Placment rect, relative to the screen
    /// (in screen coord)
    /// </summary>
    public Rect PlacementRect { get; set; }

    /// <summary>
    /// Hint text, could be change any time
    /// </summary>
    public string Text
    {
      get { return _text; }
      set
      {
        _text = value;

        if (_hintWindow == null)
          return;

        foreach (Window window in _hintWindow.OwnedWindows)
          window.Close();

        _hintWindow.Text = _text;
      }
    }

    public bool IsOpen { get { return _hintWindow != null; } }

    public object BackgroundResourceReference { get; set; }
    public object ForegroundResourceReference { get; set; }

    public void Close()
    {
      if (_hintWindow != null)
        _hintWindow.Close();

      Debug.WriteLine("Close()");
    }

    public void Show(IntPtr owner, Rect placementRect, Func<string, string> getHintContent, string text, Func<string, Brush> mapBrush)
    {
      PlacementRect = placementRect;
      Text = text;
      _getHintContent = getHintContent;
      MapBrush = mapBrush;

      try
      {
        Show(owner);
      }
      catch
      {
      }
      finally
      {
      }
   }

    private void Show(IntPtr owner)
    {
      if (InputManager.Current.IsInMenuMode)
        return;

      if (_hintWindow != null)
        throw new NotSupportedException("Hint already shown");

      // subclass
      _hintSource = new HintSource();
      _hintSource.Activate += Close;
      _hintSource.SubClass(owner);

      // create hint window
      var ht = HintRoot.Create(PlacementRect, _hintSource);
      _hintWindow = new HintWindow(this, ht) { Text = _text };

      if (BackgroundResourceReference != null)
        _hintWindow.border.SetResourceReference(Border.BackgroundProperty, BackgroundResourceReference);

      if (ForegroundResourceReference != null)
        _hintWindow._textBlock.SetResourceReference(TextBlock.ForegroundProperty, ForegroundResourceReference);

      _hintSource.HintWindow = _hintWindow;
      //new WindowInteropHelper(_hintWindow) { Owner = _hintSource.Owner };
      _hintWindow.Closed += HintWindowClosed;
      _hintWindow.MaxHeight = 1200.0;//System.Windows.Forms.Screen.FromRectangle(PlacementRect).WorkingArea.
      _wrapWidth = 1200.0;

      _hintWindow.WrapWidth = _wrapWidth;
      _hintWindow.Show();
    }

    void HintWindowClosed(object sender, EventArgs e)
    {
      _hintSource.UnSubClass();
      _hintSource = null;
      _hintWindow = null;
      if (Closed != null) Closed(this);
    }

    internal string RaiseGetHintContent(string key)
    {
      try
      {
        if (_getHintContent != null)
          return _getHintContent(key);
      }
      catch (Exception ex)
      {
        Trace.WriteLine(ex);
        return "<hint><font color=\"Red\"><b>Exception throw when do hint text lookup:</b></font><lb/>" + ex.Message + "</hint>";
      }

      return key;
    }

    internal void RaiseClick(string handler)
    {
      if (Click != null)
        Click(this, handler);
    }
  }
}
