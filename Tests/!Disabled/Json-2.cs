// REFERENCE: Json.Grammar.dll

using Nitra;
using Nitra.Tests;

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
            { b: },
            { b:, },
            { 'd':, e:2 },
            {f::3,:},
            {g# :4},
            {h# : },
            {k : 5 6}
          ]");
      Test(@"{a : 1 44}");
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
  <Identifier> : 1
}
Pretty print: {
  a <:> <Value>
}
Pretty print: {
  a : <Value>
}
Pretty print: {
  a : <Value>,
  <Property>
}
Pretty print: {
  'a' : <Value>,
  a : 1
}
Pretty print: {
  a : 2,
  <Identifier> : <Value>
}
Pretty print: [{
  <Identifier> : 1
}, {
  a <:> <Value>
}, {
  b : <Value>
}, {
  b : <Value>,
  <Property>
}, {
  'd' : <Value>,
  e : 2
}, {
  f : 3,
  <Identifier> : <Value>
}, {
  g : 4
}, {
  h : <Value>
}, {
  k : 5<,>
  <Identifier> <:> 6
}]
Pretty print: {
  a : 1<,>
  <Identifier> <:> 44
}
END-OUTPUT
*/
