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
      Test(@"[
            { : 1},
            { a },
            { a: },
            { a:, },
            { 'a':, a:1},
            {a::2,:}
          ]");
    }

    static void Test(string text)
    {
      var source = new SourceSnapshot(text);
      var parserHost = new ParserHost();
      var parseResult = JsonParser.Start(source, parserHost);

      var ast = JsonParserAstWalkers.Start(parseResult);
      Console.WriteLine("Pretty print: " + ast.ToString(PrettyPrintOptions.DebugIndent | PrettyPrintOptions.MissingNodes));
    }
  }
}

/*
BEGIN-OUTPUT
Pretty print: {
  <Identifier>: 1
}
Pretty print: {
  a<:> <Value>
}
Pretty print: {
  a: <Value>
}
Pretty print: {
  a: <Value>,
  <Property>
}
Pretty print: {
  'a': <Value>,
  a: 1
}
Pretty print: {
  a: 2,
  <Identifier>: <Value>
}
Pretty print: [{
  <Identifier>: 1
}, {
  a<:> <Value>
}, {
  a: <Value>
}, {
  a: <Value>,
  <Property>
}, {
  'a': <Value>,
  a: 1
}, {
  a: 2,
  <Identifier>: <Value>
}]
END-OUTPUT
*/
