//#define XPP
//#line 2 //does not alter locations :)

using System;
using System.ComponentModel;
using System.Console;
using System.Linq;
using SCG = System.Collections.Generic;
using LIST = System.Collections.Generic.List<int>;
using SCG;

class Q { public int F() { return (XX)42; } }

namespace CSharpToNemerle.Test
{
    // test case for issue #93
    public class TestA
    {
        public static IEnumerable<T> B<T>(IEnumerable<T> source)
        {
            return source.Select(i => i);
        }

        public static IEnumerable<T> C<T>(IEnumerable<T> source)
        {
            return source.Select(delegate {return default(T);});
        }

    }
  /// docs for delegate

  [Description("this is delegate")]
  [return:Description("this is delegate result")]
  public delegate T X<T>(int a, T b) where T : class;

  /// <summary>
  /// docs
  /// for
  /// interface
  /// </summary>
  public interface IVarianceTest<out T> {
    /// method Bar
    T Bar();
  }

  /**
    docs for enum
   */
  public enum A {
    A1 = 10,
    A2,
    A3
  }

//#if X
  public static class AExtensions
  {
    public static void TestExtension(this A a)
    {
      Console.WriteLine(a);
    }
  }
//#endif

  public class Foo<T> where T : new()
  {
    public Foo() : base() {  }
    
    /// this is destructor
    ~Foo() {}

    public event EventHandler Bar;

    private EventHandler bla;

    //public event EventHandler Bla
    //{
    //  add { bla = (EventHandler) Delegate.Combine(bla, value); }
    //  remove { bla = (EventHandler) Delegate.Remove(bla, value); }
    //}

  }

}

