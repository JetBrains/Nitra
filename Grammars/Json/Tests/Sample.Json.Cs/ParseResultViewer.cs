using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using N2;

namespace Sample.Json.Cs
{
  public partial class ParseResultViewer : Form
  {
    List<object> Rules;
    int[]       Ast;
    int[]       Mem;
    int         Res;
    ParseResult ParseResult;

    public ParseResultViewer(ParseResult parseResult)
    {
      Rules       = parseResult.ParserHost.Grammars;
      Ast         = parseResult.RawAst;
      Mem         = parseResult.RawMemoize;
      Res         = parseResult.RawResult;
      ParseResult = parseResult;
      InitializeComponent();
      _code.Text = parseResult.Source.Text;
      //_code.SelectionStart
    }



    void ShowInfo(int pos)
    {
      var rules = new List<object>();
      try
      {
        _lbRules.Items.Clear();

        if (pos > Mem.Length)
          return;

        int i = Mem[pos];

        while (pos > 0 && i == 0)
          i = Mem[--pos];

        for (; i > 0; i = Ast[i + 1])
          rules.Add(Rules[Ast[i]]);

        _lbRules.Items.AddRange(rules.ToArray());
      }
      finally
      {
        _lbRules.EndUpdate();
      }
    }

    private void _code_MouseDown(object sender, MouseEventArgs e)
    {
      this.Text = _code.SelectionStart.ToString();
      ShowInfo(_code.SelectionStart);
    }

    private void _code_KeyUp(object sender, KeyEventArgs e)
    {
      this.Text = _code.SelectionStart.ToString();
      ShowInfo(_code.SelectionStart);
    }
  }
}
