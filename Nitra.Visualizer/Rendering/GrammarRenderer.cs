using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Nitra.Runtime.Reflection;

namespace Nitra.Visualizer.Rendering
{
  class GrammarRenderer
  {
    private readonly CompositeGrammar _compositeGrammar;
    private readonly Dictionary<SequenceInfo, string> _sequenceInfoToLabelMap = new Dictionary<SequenceInfo,string>();

    public GrammarRenderer(CompositeGrammar compositeGrammar)
    {
      _compositeGrammar = compositeGrammar;
    }

    public void Render()
    {
      foreach (var rule in _compositeGrammar.Simples)
      {
        Debug.WriteLine(rule.Descriptor.Name);
        var reflection = rule.Reflection(rule.RuleId);

        // Pattern matchin emuletion :)
        var ast = reflection as SequenceInfo.Ast;
        if (ast != null)
        {
          Render(ast);
          continue;
        }

        var list = reflection as SequenceInfo.List;
        if (list != null)
        {
          Render(list);
          continue;
        }

        var listWithSeparatorRule = reflection as SequenceInfo.ListWithSeparatorRule;
        if (listWithSeparatorRule != null)
        {
          Render(listWithSeparatorRule);
          continue;
        }

        var listWithSeparatorSeparator = reflection as SequenceInfo.ListWithSeparatorSeparator;
        if (listWithSeparatorSeparator != null)
        {
          Render(listWithSeparatorSeparator);
          continue;
        }

        var option = reflection as SequenceInfo.Option;
        if (option != null)
        {
          Render(option);
          continue;
        }
      }
    }

    public void Render(SequenceInfo.Ast info)
    {
      var name = info.RuleName;
      var xx = info.Subrules[0];

    }

    public void Render(SequenceInfo.List info)
    {
      var name = info.RuleName;
    }

    public void Render(SequenceInfo.ListWithSeparatorRule info)
    {
      var name = info.RuleName;
    }

    public void Render(SequenceInfo.ListWithSeparatorSeparator info)
    {
      var name = info.RuleName;
    }

    public void Render(SequenceInfo.Option info)
    {
      var name = info.RuleName;
    }
  }
}
