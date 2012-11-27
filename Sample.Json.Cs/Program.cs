using N2;

using System;
using System.Collections.Generic;
using System.Linq;

namespace Sample.Json.Cs
{
  class Program
  {
    static void Main()
    {
      var source = new SourceSnapshot(text);
      var parserHost = new ParserHost();
      var parseResult = parserHost.DoParsing(source, JsonParser.GrammarImpl.StartRuleDescriptor);
      if (parseResult.IsSuccess)
      {
        var ast = parseResult.CreateAst<JsonParser.Start.Ast>();
        Console.WriteLine("Pretty print: " + ast);
        Console.WriteLine();
      }
      else
      {
        var errors = parseResult.CollectErrors();
        var pos    = source.PositionToLineColumn(errors.Position);
        Console.WriteLine("Parse error at ({0}, {1}), rules: {2})", pos.Line, pos.Column, string.Join(", ", errors.Messages));
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
