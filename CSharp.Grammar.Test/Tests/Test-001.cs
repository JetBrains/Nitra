using Nemerle;
using Nemerle.Collections;
using Nemerle.Text;
using Nemerle.Utility;

using System;
using System.Collections.Generic;
using System.Linq;

namespace CSharp.Grammar.Test
{
  using System.Linq;

  [A(aaa)]
  class Test_001<T, X> : A<T>.Ccc<int>.X<Y<Z.A<@int>>>, ITest<int>, string, int
    where T : class, new()
    where X : ITest<X>
  {
    public static int Foo(this string x, ref int r) { ;;; }

    string Prop1 { get; set; }

    int[] _field = {1, 2};

    const A x = y;
  }
}

enum E : byte
{
  // A, // FIXME: Bug in implementation of cycle
  A, B
}

class A
{
  public readonly int Field1 = 42;
}