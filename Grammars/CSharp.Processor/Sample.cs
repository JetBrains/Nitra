using System;
using System.Linq;

namespace Sample
{
  public static class Program
  {
    public int Main(string[] arguments)
    {
      var items = new int[] { 1, 2, 3, 4, 5, 6, 7 }.Where(x => x % 2 == 0);
      foreach (var x in items) Console.WriteLine(x);
      return 0;
    }
  }
}
