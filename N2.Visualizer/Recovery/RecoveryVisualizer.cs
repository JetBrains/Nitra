//#define DebugOutput
using N2.Internal;

using System;
using System.Collections.Generic;
using System.Diagnostics;

#if N2RUNTIME
namespace N2.Strategies
#else
// ReSharper disable once CheckNamespace
namespace N2.DebugStrategies
#endif
{
  using PrseData = Tuple<int, int, List<ParsedStateInfo>>;
  using ReortData = Action<RecoveryResult, List<RecoveryResult>, List<RecoveryResult>, List<RecoveryStackFrame>>;

  sealed class RecoveryVisualizer : Recovery
  {
    private readonly RecoveryPerformanceData _recoveryPerformanceData;

    public RecoveryPerformanceData RecoveryPerformanceData
    {
      get { return _recoveryPerformanceData; }
    }

    public RecoveryVisualizer(ReortData reportResult) : base(reportResult)
    {
      _recoveryPerformanceData = new RecoveryPerformanceData();
    }

    public RecoveryVisualizer(RecoveryVisualizer other) : base(other.ReportResult)
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
      _recoveryPerformanceData.Count++;

      var res = base.Strategy(parseResult);

      _recoveryPerformanceData.Timer.Stop();
      return res;
    }

    //protected override void TryParseSubrules(List<RecoveryStackFrame> newFrames, int startTextPos, ParseResult parseResult, RecoveryStackFrame frame, int curTextPos, string text, int subruleLevel, int state)
    //{
    //  if (_nestedLevel > 20) // ловим зацикленную рекурсию для целей отладки
    //    return;

    //  _recoveryPerformanceData.TryParseSubrulesCount++;
    //  var time = _recoveryPerformanceData.Timer.Elapsed;

    //  base.TryParseSubrules(newFrames, startTextPos, parseResult, frame, curTextPos, text, subruleLevel, state);

    //  if (_nestedLevel == 0)
    //    _recoveryPerformanceData.TryParseSubrulesTime += _recoveryPerformanceData.Timer.Elapsed - time;
    //}

    //protected override Recovery CreateSubRecovery()
    //{
    //  return new RecoveryVisualizer(this);
    //}
  }
}
