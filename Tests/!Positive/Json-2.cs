// REFERENCE: Json.Grammar.dll

using N2;
using N2.Tests;

using System;
using System.IO;

namespace Sample.Json.Cs
{
  class Program
  {
    static void Main()
    {
      Test(@"{ : 1}");
      Test(@"{ a }");
      Test(@"{ a: }");
      Test(@"{ a:, }");
      Test(@"{ 'a':, a:1}");
      Test(@"{a::2,:}");
      //Test(@"[
      //        { : 1},
      //        { a },
      //        { a: }
      //      ]"); // падает волкер!
    }

    static void Test(string text)
    {
      var source = new SourceSnapshot(text);
      var parserHost = new ParserHost();
      var parseResult = JsonParser.Start(source, parserHost);

      var ast = JsonParserAstWalkers.Start(parseResult);
      Console.WriteLine("Pretty print: " + ast.ToString(ToStringOptions.DebugIndent | ToStringOptions.MissingNodes));
    }
  }
}

/*
BEGIN-OUTPUT
Pretty print: {
  #MISSING#: 1
}
Pretty print: {
  a: #MISSING#
}
Pretty print: {
  a: #MISSING#
}
Pretty print: {
  a: #MISSING#,
  #MISSING#
}
Pretty print: {
  'a': #MISSING#,
  a: 1
}
Pretty print: {
  a: 2,
  #MISSING#
}
END-OUTPUT
*/
