using Nitra;
using Nitra.ProjectSystem;
using Nitra.Tests;

using System;

namespace Sample.Json.Cs
{
  class Program
  {
    static void Main(string[] args)
    {
      if (args.Length > 0)
        text = System.IO.File.ReadAllText(args[0]);

      var session = new ParseSession(JsonParser.Start, compilerMessages: new ConsoleCompilerMessages());
      var result = session.Parse(text);

      if (result.IsSuccess)
      {
        var parseTree = result.CreateParseTree();
        Console.WriteLine("Pretty print: " + parseTree);
        Console.WriteLine();
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
