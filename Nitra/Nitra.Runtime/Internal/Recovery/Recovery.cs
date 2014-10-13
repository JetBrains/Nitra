//#region Пролог
//#define DebugOutput
//#define DebugThreading

using System.Text;
using Nemerle.Collections;
using Nitra.Internal.Recovery;
using Nitra.Runtime.Errors;
using Nitra.Runtime.Reflection;

using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Xml.Linq;

using NB = Nemerle.Builtins;
using SCG = System.Collections.Generic;


//using ParsedSequenceAndSubrule2 = Nemerle.Builtins.Tuple</*Inserted tokens*/int, Nitra.Internal.Recovery.ParsedSequence, Nitra.Internal.Recovery.ParsedSubrule>;

#if NITRA_RUNTIME
namespace Nitra.Strategies
#else
using Nemerle.Core;
// ReSharper disable once CheckNamespace

namespace Nitra.DebugStrategies
#endif
{
  using SubrulesTokenChanges = Dictionary<ParsedSubrule, TokenChanges>;
  using ParsedSequenceAndSubrules = Nemerle.Core.list<SubruleTokenChanges>;
  using FlattenSequences = List<Nemerle.Core.list<SubruleTokenChanges>>;
  using ParsedList = Nemerle.Core.list<ParsedSequenceAndSubrule>;
  using Nitra.Runtime;

  //#endregion

  public static class RecoveryDebug
  {
    public static string CurrentTestName;
  }

  public class Recovery
  {
    public const int NumberOfTokensForSpeculativeDeleting = 4;
    public const int Fail = int.MaxValue;
    private readonly Dictionary<ParsedSequenceAndSubrule, bool> _deletedToken = new Dictionary<ParsedSequenceAndSubrule, bool>();
    private ParseResult _parseResult;
    private RecoveryParser _recoveryParser;
#if DebugThreading
    private static int _counter = 0;
    private int _id;
    public int ThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId;

    public Recovery()
    {
      _id = System.Threading.Interlocked.Increment(ref _counter);
      Debug.WriteLine("Recovery() " + _id + " ThreadId=" + System.Threading.Thread.CurrentThread.ManagedThreadId);
    }
#endif

    public virtual int Strategy(ParseResult parseResult)
    {
#if DebugThreading
      if (ThreadId != System.Threading.Thread.CurrentThread.ManagedThreadId)
        Debug.Assert(false);
      Debug.WriteLine(">>>> Strategy " + _id + " ThreadId=" + System.Threading.Thread.CurrentThread.ManagedThreadId);
#endif
      //Debug.Assert(parseResult.RecoveryStacks.Count > 0);
      _parseResult = parseResult;

#if DebugOutput
      Debug.IndentSize = 1;
      var timer = Stopwatch.StartNew();
      Debug.WriteLine(RecoveryDebug.CurrentTestName + " -----------------------------------------------------------");
#endif
      _deletedToken.Clear();
      var rp = new RecoveryParser(parseResult);
      _recoveryParser = rp;
      rp.RecoveryFromAllErrors();

      var errorCollector = new ErrorCollectorWalker();
      errorCollector.Walk(parseResult);

      UpdateEarleyParseTime();
      return parseResult.Text.Length;
    }

    protected virtual void UpdateEarleyParseTime() { }
    protected virtual void UpdateRecoverAllWaysTime() { }
    protected virtual void UpdateFindBestPathTime() { }
    protected virtual void UpdateFlattenSequenceTime() { }
    protected virtual void UpdateParseErrorCount() { }
  }

  #region Utility methods

  public static class ParsePathsVisializer
  {
    #region HtmlTemplate
    private const string HtmlTemplate = @"
<html>
<head>
    <title>Pretty Print</title>
    <meta http-equiv='Content-Type' content='text/html;charset=utf-8'/>
    <style type='text/css'>
pre
{
  color: black;
  font-weight: normal;
  font-size: 12pt;
  font-family: Consolas, Courier New, Monospace;
}

.default
{
  color: black;
  background: white;
}

.garbage
{
  color: rgb(243, 212, 166);
  background: rgb(170, 134, 80);
}

.deleted
{
  background: lightpink;
}

.parsed
{
  color: Green;
  background: LightGreen;
}

.prefix
{
  color: Indigo;
  background: Plum;
}

.postfix
{
  color: blue;
  background: lightgray;
}

.skipedState
{
  color: darkgray;
  background: lightgray;
  -webkit-print-color-adjust:exact;
}
.currentRulePrefix
{
  color: darkgoldenrod;
  background: lightgoldenrodyellow;
}
</style>
</head>
<body>
<pre>
<content/>
</pre>
</body>
</html>
";
    #endregion

    static readonly XAttribute _garbageClass = new XAttribute("class", "garbage");
    static readonly XAttribute _deletedClass = new XAttribute("class", "deleted");
    static readonly XAttribute _skipedStateClass = new XAttribute("class", "skipedState");
    static readonly XAttribute _default = new XAttribute("class", "default");

    public static void PrintPaths(ParseResult parseResult, Dictionary<ParsedSequenceAndSubrule, bool> deletedToken, FlattenSequences paths)
    {
      var results = new List<XNode> { new XText(RecoveryDebug.CurrentTestName + "\r\n" + parseResult.DebugText + "\r\n\r\n") };

      foreach (var path in paths)
        PrintPath(results, parseResult.Text, deletedToken, path);

      var template = XElement.Parse(HtmlTemplate);
      var content = template.Descendants("content").First();
      Debug.Assert(content.Parent != null);
      content.Parent.ReplaceAll(results);
      var filePath = Path.ChangeExtension(Path.GetTempFileName(), ".html");
      template.Save(filePath);
      Process.Start(filePath);
    }

    public static void PrintPath(List<XNode> results, string text, Dictionary<ParsedSequenceAndSubrule, bool> deletedToken, ParsedSequenceAndSubrules path)
    {
      var isPrevInsertion = false;

      foreach (var node in path.Reverse())
      {
        var tokenChanges = node.TokenChanges;
        var seq = node.Seq;
        var subrule = node.Subrule;
        
        bool isGarbage;
        if (deletedToken.TryGetValue(new ParsedSequenceAndSubrule(seq, subrule), out isGarbage))// TODO: Возможно здесь надо проверять значение tokenChanges.Deleted
        {
          isPrevInsertion = false;
          var title = new XAttribute("title", "Deleted token;  Subrule: " + subrule + ";  Sequence: " + seq + ";");
          results.Add(new XElement("span", isGarbage ? _garbageClass : _deletedClass,
            title, text.Substring(subrule.Begin, subrule.End - subrule.Begin)));
        }
        else if (tokenChanges.HasChanges)
        {
          var desc = seq.ParsingSequence.States[subrule.State].Description;
          if (!subrule.IsEmpty)
          { }
          var title = new XAttribute("title", "Inserted tokens: " + tokenChanges + ";  Subrule: " + subrule + ";  Sequence: " + seq + ";");
          results.Add(new XElement("span", _skipedStateClass, title, isPrevInsertion ? " " + desc : desc));
          isPrevInsertion = true;
        }
        else
        {
          var desc = seq.ParsingSequence.States[subrule.State].Description;
          var title = new XAttribute("title", "Description: " + desc + ";  Subrule: " + subrule + ";  Sequence: " + seq + ";");
          results.Add(new XElement("span", title, text.Substring(subrule.Begin, subrule.End - subrule.Begin)));

          isPrevInsertion = false;
        }
      }

      results.Add(new XText("\r\n"));
    }
  }

  #endregion
}
