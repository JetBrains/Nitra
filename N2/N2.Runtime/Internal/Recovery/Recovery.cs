//#define DebugOutput
using N2.Internal;

using Nemerle.Collections;

using System;
using System.Collections.Generic;
using System.Linq;
using RecoveryStack = Nemerle.Core.list<N2.Internal.RecoveryStackFrame>.Cons;
using System.Diagnostics;

#if N2RUNTIME
namespace N2.Strategies
#else
// ReSharper disable once CheckNamespace
namespace N2.DebugStrategies
#endif
{
  public sealed class Recovery
  {
    public void Init()
    {
    }

    public void Strategy(int _startTextPos, Parser _parser)
    {
    }
  }
}
