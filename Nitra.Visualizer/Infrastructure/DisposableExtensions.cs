using System;
using System.Collections.Generic;
using System.Reactive.Disposables;
using ReactiveUI;

namespace Nitra.Visualizer.Infrastructure
{
  public static class DisposableExtensions
  {
    public static void AddTo(this IDisposable d, ICollection<IDisposable> cd)
    {
      cd.Add(d);
    }

    public static void WhenActivated(this IActivatable instance, Action<CompositeDisposable> activate)
    {
      instance.WhenActivated(d => {
        var cd = new CompositeDisposable();
        activate(cd);
        d(cd);
      });
    }
  }
}