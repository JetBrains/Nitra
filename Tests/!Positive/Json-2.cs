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
  ### MISSING ###: 1
}
END-OUTPUT
*/
