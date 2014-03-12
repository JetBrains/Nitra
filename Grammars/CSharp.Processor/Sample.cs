using System;
using System.Linq;

namespace Sample
{
  public static class Program
  {
    public int Main(string[] arguments)
    {
      object x;
      if (!false)
        x = new int[] { 1, 2, 3, 4, 5, 6, 7, 0 }.Where(x => x > 4);
      var items = new int[] { 1, 2, 3, 4, 5, 6, 7, 0 }.Where(x => x % 2 == 0);
      foreach (var x in items) Console.WriteLine(x);
      return 0;
    }
  }
}
