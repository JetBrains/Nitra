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
  class Test_001 : A<B>.Ccc<int>.X<Y<Z.A<@int>>>, ITest<int>
  {
    public static int Foo(this string x, ref int r) { ;;; }
  }
}

enum E : byte
{
  // A, // FIXME: Bug in implementation of cycle
  A, B
}
