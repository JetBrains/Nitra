using System;
using System.Diagnostics;

#if NITRA_RUNTIME
namespace Nitra.Strategies
#else
// ReSharper disable once CheckNamespace
namespace Nitra.DebugStrategies
#endif
{
  sealed class RecoveryPerformanceData
  {
    public Stopwatch Timer = new Stopwatch();
    public TimeSpan  EarleyParseTime;
    public TimeSpan  RecoverAllWaysTime;
    public TimeSpan  FindBestPathTime;
    public TimeSpan  FlattenSequenceTime;
    public int       ParseErrorCount;

    public TimeSpan  Previous;

    public RecoveryPerformanceData()
    {
      Init();
    }

    public void Init()
    {
      Timer.Reset();
      EarleyParseTime = TimeSpan.Zero;
      RecoverAllWaysTime = TimeSpan.Zero;
      FindBestPathTime = TimeSpan.Zero;
      FlattenSequenceTime = TimeSpan.Zero;
      ParseErrorCount = 0;
    }

    public TimeSpan NextTime()
    {
      var cur = Timer.Elapsed;
      var previous = Previous;
      Previous = cur;
      return cur - previous;
    }
  }
}