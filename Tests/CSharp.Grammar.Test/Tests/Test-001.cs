/*
using Nemerle;
using Nemerle.Collections;
using Nemerle.Text;
using Nemerle.Utility;

using System;
using System.Collections.Generic;
using System.Linq;

class B { int? Field = z = a ? b = x : c = y ? d ? f : j : h; } //

namespace CSharp.Grammar.Test
{
  using System.Linq;

  [A(aaa)]
  class Test_001<T, X> : A<T>.Ccc<int>.X<Y<Z.A<@int>>>, ITest<int>, string, int
    where T : class, new()
    where X : ITest<X>
  {
    public static int Foo(this string x, ref int r)
    {
      ;
      ;;
      if (a == b)
        Foo(42);
      else
      {
        //var x = 42;
        switch (42)
        {
          case 2: break;
          case 3: Console.WriteLine(@"Test ""test""!"); break;
          case 42:
          default:
            return 42;
        }

        Foo(x + 8);
        Foo(x + 8);
      }

      for (; ; )
        Console.WriteLine("test");

      for (int i = 0; i < length; i++)
      {
        Console.WriteLine(i);
      }

      for (int i = 0, k = 42; i < length; i++, k += 2)
        Console.WriteLine(i + k);

      for (i = 0, k = 42; i < length; i++, k += 2)
        Console.WriteLine(i + k);

      foreach (var x in xs)
        Console.WriteLine(x);
    }
    public void F() { base.Foo<T>.Foo<T>(); }
    string Prop1 { get; set; }

    int[] _field = {1UL, 2u};

    const A x = y;

    Func<int, string> Foo() { return str => str + " test"; }
    Func<int, object> Foo2() { return x => new { X = x }; }
    Func<int, object> Bar() { return delegate(string str) { return new { }; }; }

    ~Test_001() { }
  }
}

enum E : byte
{
  // A, // FIXME: Bug in implementation of cycle
  A, B
}

unsafe class A
{
  public fixed char pathName[128];
  A() : this(42) { }
  A(int x) { }
  public readonly int Field1 = 42;

  public static int operator +(A c1, A c2)
  {
    //return c1 ? 42 : 12;
    return new A(Ns1.Type<int?>.Foo<A>(42, "aa"));
  }
}
*/
