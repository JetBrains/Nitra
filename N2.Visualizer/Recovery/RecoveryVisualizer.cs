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

    public override int Strategy(Parser parser)
    {
      _recoveryPerformanceData.Timer.Start();
      _recoveryPerformanceData.Count++;

      var res = base.Strategy(parser);

      _recoveryPerformanceData.Timer.Stop();
      return res;
    }

    //protected override void TryParseSubrules(List<RecoveryStackFrame> newFrames, int startTextPos, Parser parser, RecoveryStackFrame frame, int curTextPos, string text, int subruleLevel, int state)
    //{
    //  if (_nestedLevel > 20) // ловим зацикленную рекурсию для целей отладки
    //    return;

    //  _recoveryPerformanceData.TryParseSubrulesCount++;
    //  var time = _recoveryPerformanceData.Timer.Elapsed;

    //  base.TryParseSubrules(newFrames, startTextPos, parser, frame, curTextPos, text, subruleLevel, state);

    //  if (_nestedLevel == 0)
    //    _recoveryPerformanceData.TryParseSubrulesTime += _recoveryPerformanceData.Timer.Elapsed - time;
    //}

    protected override int ContinueParse(int startTextPos, RecoveryStackFrame recoveryStack, Parser parser,
      bool trySkipStates)
    {
      var stratTime = _recoveryPerformanceData.Timer.Elapsed;
      _recoveryPerformanceData.ContinueParseCount++;

      var result = ContinueParseImpl(startTextPos, recoveryStack, parser, trySkipStates);

      _recoveryPerformanceData.ContinueParseTime += _recoveryPerformanceData.Timer.Elapsed - stratTime;
      return result;
    }

    protected override int TryParse(Parser parser, RecoveryStackFrame recoveryStack, int curTextPos, int state, out List<ParsedStateInfo> parsedStates)
    {
      _recoveryPerformanceData.TryParseCount++;
      var timer = _recoveryPerformanceData.Timer.Elapsed;

      var result = base.TryParse(parser, recoveryStack, curTextPos, state, out parsedStates);

      _recoveryPerformanceData.TryParseTime += _recoveryPerformanceData.Timer.Elapsed - timer;

      return result;
    }

    //protected override Recovery CreateSubRecovery()
    //{
    //  return new RecoveryVisualizer(this);
    //}
  }
}
