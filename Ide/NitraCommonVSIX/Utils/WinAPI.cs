using System;
using System.Drawing;
using System.Runtime.InteropServices;

using Wpf = System.Windows;

namespace Nitra.VisualStudio
{
  static class WinApi
  {
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetCursorPos(out POINT point);
    public static POINT? GetCursorPos() => GetCursorPos(out var point) ? (POINT?)point : null;

    [DllImport("user32.dll")]
    public static extern IntPtr WindowFromPoint(POINT point);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    public static RECT? GetWindowRect(IntPtr hWnd)=> GetWindowRect(hWnd, out var rect) ? (RECT?)rect : null;

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
      public int X;
      public int Y;

      public POINT(int x, int y)
      {
        X = x;
        Y = y;
      }

      public POINT(Point     p) : this(     p.X,      p.Y) { }
      public POINT(Wpf.Point p) : this((int)p.X, (int)p.Y) { }

      public static implicit operator Point    (POINT p) => new Point    (p.X,      p.Y);
      public static implicit operator POINT    (Point p) => new POINT    (p.X,      p.Y);
      public static implicit operator Wpf.Point(POINT p) => new Wpf.Point(p.X,      p.Y);
      public static implicit operator POINT(Wpf.Point p) => new POINT    (p);

      public override string ToString()
      {
        return $"POINT {{ X={X} Y={Y} }}";
      }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
      public int Left;        // x position of upper-left corner
      public int Top;         // y position of upper-left corner
      public int Right;       // x position of lower-right corner
      public int Bottom;      // y position of lower-right corner

      public RECT(int left, int top, int right, int bottom)
      {
        Left   = left;
        Top    = top;
        Right  = right;
        Bottom = bottom;
      }

      public Size      Size    => new Size     (Right - Left, Bottom - Top);
      public Wpf.Size  WpfSize => new Wpf.Size (this.Size.Width, this.Size.Height);
      public Point     Pos     => new Point    (Left, Top);
      public Wpf.Point WpfPos  => new Wpf.Point(Left, Top);
      public Rectangle Rect    => new Rectangle(this.Pos, this.Size);
      public Wpf.Rect  WpfRect => new Wpf.Rect(this.WpfPos, this.WpfSize);

      public static implicit operator Rectangle(RECT p) => p.Rect;
      public static implicit operator RECT(Rectangle p) => new RECT(left:p.Left, top:p.Top, right:p.Right, bottom:p.Bottom);
      public static implicit operator Wpf.Rect(RECT p) => p.WpfRect;
      public static implicit operator RECT(Wpf.Rect p) => new RECT(left: (int)p.Left, top: (int)p.Top, right: (int)p.Right, bottom: (int)p.Bottom);

      public override string ToString()
      {
        return $"RECT {{Left={Left} Top={Top} Right={Right} Bottom={Bottom} Size={Size} }}";
      }
    }

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("gdi32.dll", EntryPoint = "CreateSolidBrush", SetLastError = true)]
    public static extern IntPtr CreateSolidBrush(int crColor);

    [DllImport("user32.dll")]
    static extern int FillRect(IntPtr hDC, [In] ref RECT rect, IntPtr hbr);

    public static void FillRect(RECT rect)
    {
      IntPtr desktop = GetDC(IntPtr.Zero);
      var blueBrush = CreateSolidBrush(0x0000FF);
      FillRect(desktop, ref rect, blueBrush);
      ReleaseDC(IntPtr.Zero, desktop);
    }
  }
}
