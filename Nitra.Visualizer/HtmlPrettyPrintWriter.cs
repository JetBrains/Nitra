using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.IO;

namespace Nitra.Visualizer
{
  internal class HtmlPrettyPrintWriter : PrettyPrintWriter
  {
    private StringBuilder _buffer;
    private StringWriter _writer; // for HtmlEncode
    private string _missingNodeClass;
    private string _debugClass;
    private string _garbageClass;
    private int _currentIndent;
    private int _lastStartLine;
    private int _lastIndentEnd;

    public HtmlPrettyPrintWriter(PrettyPrintOptions options, string missingNodeClass, string debugClass, string garbageClass)
      : base(options)
    {
      _buffer = new StringBuilder();
      _writer = new StringWriter(_buffer);
      _missingNodeClass = missingNodeClass;
      _debugClass = debugClass;
      _garbageClass = garbageClass;
    }

    protected override void Garbage(IPrettyPrintSource source, NSpan skip)
    {
      var text = source.Text.Substring(skip.StartPos, skip.Length);
      WriteSpan(_garbageClass, text);
    }

    protected override void FormatToken(IPrettyPrintSource source, NSpan token)
    {
      TryPrintGarbage(source, token);
      var text = source.Text.Substring(token.StartPos, token.Length);
      WebUtility.HtmlEncode(text, _writer);
    }

    protected override void FormatString(IPrettyPrintSource source, NSpan token, string text)
    {
      if (token.IsEmpty)
      {
        if ((Options & PrettyPrintOptions.MissingNodes) == PrettyPrintOptions.MissingNodes)
          WriteSpan(_missingNodeClass, text);
      }
      else
        WebUtility.HtmlEncode(text, _writer);
    }

    public override void MissingNode(RuleDescriptor ruleDescriptor)
    {
      if ((Options & PrettyPrintOptions.MissingNodes) == PrettyPrintOptions.MissingNodes)
        WriteSpan(_missingNodeClass, ruleDescriptor.Name);
    }

    public override void AmbiguousNode(IAmbiguousAst ast)
    {
      WriteSpan(_missingNodeClass, "ambiguous " + ast.RuleDescriptor.Name);
    }

    public override void NewLine()
    {
      IndentNewLine();
    }

    public override void NewLineAndIndent()
    {
      _currentIndent++;
      IndentNewLine();
    }

    public override void Whitespace()
    {
      _buffer.Append(' ');
    }

    public override void Indent()
    {
      _currentIndent++;
      if ((Options & PrettyPrintOptions.DebugIndent) == PrettyPrintOptions.DebugIndent && _lastStartLine != _buffer.Length)
        WriteSpan(_debugClass, "The indentation increasing not from the beginning of line.");
      IndentCurrentLine();
    }

    public override void Unindent()
    {
      _currentIndent--;
      if (_lastIndentEnd == _buffer.Length)
      {
        _buffer.Length = _lastStartLine;
        IndentCurrentLine();
      }
      else if ((Options & PrettyPrintOptions.DebugIndent) == PrettyPrintOptions.DebugIndent)
        WriteSpan(_debugClass, "No new line before indentation decreasing.");
      else
        IndentNewLine();
    }

    private void IndentNewLine()
    {
      _buffer.AppendLine();
      _lastStartLine = _buffer.Length;
      IndentCurrentLine();
    }

    private void IndentCurrentLine()
    {
      // TODO: Make indent customizable.
      _buffer.Append(' ', _currentIndent * 2);
      _lastIndentEnd = _buffer.Length;
    }

    private void WriteSpan(string cssClass, string text)
    {
      _writer.Write("<span class=\"");
      WebUtility.HtmlEncode(cssClass, _writer);
      _writer.Write("\">");
      WebUtility.HtmlEncode(text, _writer);
      _writer.Write("</span>");
    }

    public void WriteTo(TextWriter writer)
    {
      writer.Write("<pre>");
      writer.Write(_buffer);
      writer.Write("</pre>");
    }

    public override string ToString()
    {
      using (var result = new StringWriter())
      {
        WriteTo(result);
        return result.ToString();
      }
    }
  }
}
