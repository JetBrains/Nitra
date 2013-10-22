using System;
using System.Diagnostics;

#if N2RUNTIME
namespace Nitra.Strategies
#else
// ReSharper disable once CheckNamespace
namespace Nitra.DebugStrategies
#endif
{
  sealed class RecoveryPerformanceData
  {
    public Stopwatch Timer = new Stopwatch();
    public TimeSpan  TryParseTime;
    public int       Count;
    public TimeSpan  ContinueParseTime;
    public int       ContinueParseCount;
    public TimeSpan  TryParseSubrulesTime;
    public int       TryParseSubrulesCount;
    public int       TryParseCount;

    public RecoveryPerformanceData()
    {
      Init();
    }

    public void Init()
    {
      ContinueParseTime = TimeSpan.Zero;
      ContinueParseCount = 0;
      TryParseSubrulesTime = TimeSpan.Zero;
      TryParseSubrulesCount = 0;
      TryParseTime = TimeSpan.Zero;
      TryParseCount = 0;
      Timer.Reset();
      Count = 0;
    }
  }
}