using System;
using System.Diagnostics;
using System.Windows.Input;
using System.Windows;
using System.Windows.Threading;
using System.Linq;

namespace WpfHint2
{
	internal class HintSource : IDisposable
	{
		public event Action Activate;
		public HintWindow HintWindow { get; set; }
    bool _disposed;

    ///// text editor window handle
    //public IntPtr Owner { get; private set; }

    private InputManager _inputManager;
		private Dispatcher   _dispatcher;

		public void SubClass()
		{
      CheckDisposed();
      _inputManager = InputManager.Current;
			_dispatcher   = _inputManager.Dispatcher;

			_inputManager.PreProcessInput += OnPreProcessInputEventHandler;
			_inputManager.EnterMenuMode   += OnEnterMenuMode;

			Debug.WriteLine("SubClass()");
		}

		void Unsubscribe()
		{
      lock (this)
      {
        if (_disposed)
          return;

        _inputManager.PreProcessInput -= OnPreProcessInputEventHandler;
        _inputManager.EnterMenuMode -= OnEnterMenuMode;
        Dispose();
      }
		}

		public void UnSubClass()
		{
      CheckDisposed();

			if (_dispatcher != null)
				_dispatcher.BeginInvoke((Action)Unsubscribe);

      Debug.WriteLine("UnSubClass(): ");
		}

		#region Dispatcher handlers

		bool MouseHoverHintWindow()
		{
      CheckDisposed();
			var pos = Win32.GetCursorPos();

			Func<Window, bool> process = null; process = wnd => // local funtion :)
			{
				if (wnd.RestoreBounds.Contains(pos))
					return true;

				foreach (Window win in wnd.OwnedWindows)
					if (process(win))
						return true;

				return false;
			};

			Trace.Assert(HintWindow != null);
			return process(HintWindow);
		}

		void OnPreProcessInputEventHandler(object sender, PreProcessInputEventArgs e)
		{
      CheckDisposed();
			var name = e.StagingItem.Input.RoutedEvent.Name;

			switch (name)
			{
				case "PreviewMouseDown":
				case "PreviewMouseUp":
				case "MouseDown":
				case "MouseUp":
					if (!MouseHoverHintWindow())
						CollActivate();
					break;

				case "PreviewKeyDown":
				case "PreviewKeyUp":
				case "KeyDown":
				case "KeyUp":
				case "LostKeyboardFocus":
					CollActivate();
					break;

				case "PreviewInputReport":
				case "InputReport":
				case "QueryCursor":
				case "PreviewMouseMove":
				case "MouseMove":
					break;
				default:
					Debug.WriteLine(name);
					break;
			}
		}

		void CollActivate()
		{
      CheckDisposed();
			if (Activate != null)
				Activate();
		}

		void OnEnterMenuMode(object sender, EventArgs e)
		{
      CheckDisposed();

      CollActivate();
		}

    #endregion Dispatcher handlers

    #region Implementation of IDisposable

    void CheckDisposed()
    {
      if (_disposed)
      {
        Debug.Assert(!_disposed);
        throw new ObjectDisposedException(this.GetType().FullName);
      }
    }

    ~HintSource()
		{
      if (!_disposed)
			  Dispose();
		}

		public void Dispose()
		{
      if (_disposed)
        return;

      UnSubClass();

      _disposed = true;
      _dispatcher = null;
      _inputManager = null;

      GC.SuppressFinalize(this);
		}

		#endregion
	}
}
