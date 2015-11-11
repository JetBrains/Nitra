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
    private readonly StringBuilder _buffer;
    private readonly StringWriter _writer; // for HtmlEncode
    private readonly SpanClass _missingNodeClass;
    private readonly SpanClass _debugClass;
    private readonly SpanClass _garbageClass;
    private int _currentIndent;
    private int _lastStartLine;
    private int _lastIndentEnd;
    private int _lastMissing;

    public HtmlPrettyPrintWriter(PrettyPrintOptions options, string missingNodeClass, string debugClass, string garbageClass)
      : base(options)
    {
      _buffer = new StringBuilder();
      _writer = new StringWriter(_buffer);
      _missingNodeClass = new SpanClass(Language.Instance, missingNodeClass, missingNodeClass, null);
      _debugClass = new SpanClass(Language.Instance, debugClass, debugClass, null);
      _garbageClass = new SpanClass(Language.Instance, garbageClass, garbageClass, null);
    }

    protected override void Garbage(IPrettyPrintSource source, NSpan skip)
    {
      var text = source.Text.Substring(skip.StartPos, skip.Length);
      WriteSpan(_garbageClass, text);
    }

    protected override void FormatToken(IPrettyPrintSource source, NSpan token, bool canBeEmpty, string ruleName, SpanClass spanClass)
    {
      TryPrintGarbage(source, token);
      if (token.IsEmpty)
      {
        if (!canBeEmpty && (Options & PrettyPrintOptions.MissingNodes) == PrettyPrintOptions.MissingNodes)
          WriteSpan(_missingNodeClass, ruleName);
      }
      else
      {
        var text = source.Text.Substring(token.StartPos, token.Length);
        WriteSpan(spanClass, text);
      }
    }

    protected override void FormatString(IPrettyPrintSource source, NSpan token, string text, SpanClass spanClass)
    {
      if (token.IsEmpty)
      {
        if ((Options & PrettyPrintOptions.MissingNodes) == PrettyPrintOptions.MissingNodes)
          WriteSpan(_missingNodeClass, text);
      }
      else
        WriteSpan(spanClass, text);
    }

    public override void MissingNode(RuleDescriptor ruleDescriptor)
    {
      if ((Options & PrettyPrintOptions.MissingNodes) == PrettyPrintOptions.MissingNodes)
        WriteSpan(_missingNodeClass, ruleDescriptor.Name);
      _lastMissing = _buffer.Length;
    }

    public override void AmbiguousNode(IAmbiguousParseTree ambiguousTree, SpanClass spanClass)
    {
      WriteSpan(_missingNodeClass, "<# ambiguous " + ambiguousTree.RuleDescriptor.Name + ", " + ambiguousTree.Ambiguities.Count + " options");
      NewLineAndIndent();
      var previousTokenPos = _previousTokenPos;
      foreach (var a in ambiguousTree.Ambiguities)
      {
        _previousTokenPos = previousTokenPos;
        a.PrettyPrint(this, 0, spanClass);
        NewLine();
      }
      Unindent();
      WriteSpan(_missingNodeClass, "#>");
      NewLine();
    }

    public override void AmbiguousNode<T>(IAmbiguousParseTree ambiguousTree, string ruleType, IPrettyPrintSource source, SpanClass spanClass, Action<PrettyPrintWriter, IPrettyPrintSource, T, SpanClass> printer)
    {
      WriteSpan(_missingNodeClass, "<# ambiguous " + ruleType + ", " + ambiguousTree.Ambiguities.Count + " options");
      NewLineAndIndent();
      var previousTokenPos = _previousTokenPos;
      foreach (object a in ambiguousTree.Ambiguities)
      {
        _previousTokenPos = previousTokenPos;
        printer(this, source, (T)a, spanClass);
        NewLine();
      }
      Unindent();
      WriteSpan(_missingNodeClass, "#>");
      NewLine();
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
      {
        if (_lastMissing == _buffer.Length)
        {
          IndentNewLine();
          _currentIndent++;
          Unindent();
        }
        else
          WriteSpan(_debugClass, " No new line before indentation decreasing.");
      }
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

    private void WriteSpan(SpanClass spanClass, string text)
    {
      if (spanClass != null)
      {
        _writer.Write("<span class=\"");
        WebUtility.HtmlEncode(spanClass.FullName.Replace('.', '-'), _writer);
        _writer.Write("\">");
      }
      WebUtility.HtmlEncode(text, _writer);
      if (spanClass != null)
      {
        _writer.Write("</span>");
      }
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
