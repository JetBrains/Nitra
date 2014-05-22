//#define DebugOutput
using Nitra;
using Nitra.Internal.Recovery;

using System;
using System.Collections.Generic;
using System.Diagnostics;

#if NITRA_RUNTIME
namespace Nitra.Strategies
#else
// ReSharper disable once CheckNamespace
namespace Nitra.DebugStrategies
#endif
{
  sealed class RecoveryVisualizer : Recovery
  {
    private readonly RecoveryPerformanceData _recoveryPerformanceData;

    public RecoveryPerformanceData RecoveryPerformanceData
    {
      get { return _recoveryPerformanceData; }
    }

    public RecoveryVisualizer() : base()
    {
      _recoveryPerformanceData = new RecoveryPerformanceData();
    }

    public RecoveryVisualizer(RecoveryVisualizer other) : base()
    {
      _recoveryPerformanceData = other._recoveryPerformanceData;
    }
    
    public void Init()
    {
      _recoveryPerformanceData.Init();
    }

    public override int Strategy(ParseResult parseResult)
    {
      _recoveryPerformanceData.Timer.Start();

      var res = base.Strategy(parseResult);

      _recoveryPerformanceData.Timer.Stop();
      return res;
    }

    protected override void UpdateEarleyParseTime()     { _recoveryPerformanceData.EarleyParseTime    = _recoveryPerformanceData.NextTime(); }
    protected override void UpdateRecoverAllWaysTime()  { _recoveryPerformanceData.RecoverAllWaysTime = _recoveryPerformanceData.NextTime(); }
    protected override void UpdateFindBestPathTime()    { _recoveryPerformanceData.FindBestPathTime   = _recoveryPerformanceData.NextTime(); }
    protected override void UpdateFlattenSequenceTime() { _recoveryPerformanceData.EarleyParseTime    = _recoveryPerformanceData.NextTime(); }
    protected override void UpdateParseErrorCount()     { _recoveryPerformanceData.ParseErrorCount++; }
  }
}
