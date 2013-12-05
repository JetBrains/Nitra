using Nitra;
using Nitra.Tests;

using System;
using System.IO;

namespace Sample.Json.Cs
{
  class Program
  {
    static void Main(string[] args)
    {
      if (args.Length > 0)
        text = File.ReadAllText(args[0]);

      var parseResult = JsonParser.Start(new SourceSnapshot(text), new ParserHost());

      if (parseResult.IsSuccess)
      {
        var ast = JsonParserAst.Start.Create(parseResult);
        Console.WriteLine("Pretty print: " + ast);
        Console.WriteLine();
      }
      else
      {
        foreach(var error in parseResult.GetErrors())
        {
          var pos = error.Location.StartLineColumn;
          Console.WriteLine("{0}:{1}: {2}", pos.Line, pos.Column, error.Message);
        }
      }
    }

    static string text =
  @"{
      'glossary': {
          'title': 'example glossary',
      'GlossDiv': {
              'title': 'S',
        'GlossList': {
                  'GlossEntry': {
                      'ID': 'SGML',
            'SortAs': 'SGML',
            'GlossTerm': 'Standard Generalized Markup Language',
            'Acronym': 'SGML',
            'Abbrev': 'ISO 8879:1986',
            'GlossDef': {
                          'para': 'A meta-markup language, used to create markup languages such as DocBook.',
              'GlossSeeAlso': ['GML', 'XML']
                      },
            'GlossSee': 'markup'
                  }
              }
          }
      }
}";
  }
}
