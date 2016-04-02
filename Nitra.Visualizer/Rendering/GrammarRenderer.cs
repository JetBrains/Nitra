using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Nitra.Visualizer.Rendering
{
  class GrammarRenderer
  {
    //readonly CompositeGrammar _compositeGrammar;
    //readonly Dictionary<SequenceInfo, string> _sequenceInfoToLabelMap = new Dictionary<SequenceInfo,string>();

    //public GrammarRenderer(CompositeGrammar compositeGrammar)
    //{
    //  _compositeGrammar = compositeGrammar;
    //}

    public void Render()
    {
      //foreach (var rule in _compositeGrammar.Simples)
      //{
      //  Debug.WriteLine(rule.Descriptor.Name);
      //  var reflection = rule.Reflection(rule.RuleId);

      //  // Pattern matchin emuletion :)
      //  var root = reflection as SequenceInfo.Root;
      //  if (root != null)
      //  {
      //    Render(root);
      //    continue;
      //  }

      //  var list = reflection as SequenceInfo.ListItem;
      //  if (list != null)
      //  {
      //    Render(list);
      //    continue;
      //  }

      //  var listWithSeparatorRule = reflection as SequenceInfo.ListWithSeparatorItem;
      //  if (listWithSeparatorRule != null)
      //  {
      //    Render(listWithSeparatorRule);
      //    continue;
      //  }

      //  var listWithSeparatorSeparator = reflection as SequenceInfo.ListWithSeparatorSeparator;
      //  if (listWithSeparatorSeparator != null)
      //  {
      //    Render(listWithSeparatorSeparator);
      //    continue;
      //  }

      //  var option = reflection as SequenceInfo.Option;
      //  if (option != null)
      //  {
      //    Render(option);
      //    continue;
      //  }
      //}
    }

    //public void Render(SequenceInfo.Root info)
    //{
    //  var name = info.RuleName;
    //  var xx = info.Subrules[0];

    //}

    //public void Render(SequenceInfo.ListItem info)
    //{
    //  var name = info.RuleName;
    //}

    //public void Render(SequenceInfo.ListWithSeparatorItem info)
    //{
    //  var name = info.RuleName;
    //}

    //public void Render(SequenceInfo.ListWithSeparatorSeparator info)
    //{
    //  var name = info.RuleName;
    //}

    //public void Render(SequenceInfo.Option info)
    //{
    //  var name = info.RuleName;
    //}
  }
}
