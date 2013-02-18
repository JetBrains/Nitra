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
  public class Data
  {
    public Data(Tuple<string, string, int>[] obj, int pos)
    {
      Obj = obj;
      Pos = pos;
    }

    public readonly Tuple<string, string, int>[] Obj;
    public readonly int    Pos;

    //public bool IsLoop { get { return ToString().StartsWith("Loop:"); } }
    //public bool IsLoopWithSeparator { get { return ToString().StartsWith("Loop with separator:"); } }

    public override string ToString()
    {
      return string.Join("; ", Obj.Select(t => t.ToString()));
    }
  }

  public partial class ParseResultViewer : Form
  {
    int[]       Ast;
    int[]       Mem;
    int         Res;
    ParseResult ParseResult;

    public ParseResultViewer(ParseResult parseResult)
    {
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

        //while (pos > 0 && i == 0)
        //  i = Mem[--pos];

        for (; i > 0; i = Ast[i + 1])
          rules.Add(new Data(ParseResult.ParserHost.Reflection(Ast[i]), i));

        _lbRules.Items.AddRange(rules.ToArray());
      }
      finally
      {
        _lbRules.EndUpdate();
      }
    }

    void ShowLoopInfo(int pos)
    {
      var rules = new List<object>();
      try
      {
        _lbLoop.Items.Clear();




        _lbLoop.Items.AddRange(rules.ToArray());
      }
      finally
      {
        _lbLoop.EndUpdate();
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

    private void _lbRules_SelectedIndexChanged(object sender, EventArgs e)
    {
      if (_lbRules.SelectedItem == null)
        return;

      var data = (Data)_lbRules.SelectedItem;

      //if (data.IsLoop)
      {
        //var elemIndex = Ast[data.Pos + 2];
      }

      ShowLoopInfo(_code.SelectionStart);
    }
  }
}
