using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Search;
using System.Windows.Input;
using System.Windows.Threading;

namespace Nitra.Visualizer
{
  internal class NitraSearchInputHandler : TextAreaInputHandler
  {
    private SearchPanel panel;

    public NitraSearchInputHandler(TextArea textArea) : base(textArea)
    {
      this.RegisterCommands(CommandBindings);
    }

    private void RegisterCommands(ICollection<CommandBinding> commandBindings)
    {
      commandBindings.Add(new CommandBinding(ApplicationCommands.Find, new ExecutedRoutedEventHandler(ExecuteFind)));
      commandBindings.Add(new CommandBinding(NitraSearchCommands.FindEx, new ExecutedRoutedEventHandler(ExecuteFindEx)));
      commandBindings.Add(new CommandBinding(NitraSearchCommands.FindExBackward, new ExecutedRoutedEventHandler(ExecuteFindExBackward)));
      commandBindings.Add(new CommandBinding(SearchCommands.FindNext, new ExecutedRoutedEventHandler(ExecuteFindNext)));
      commandBindings.Add(new CommandBinding(SearchCommands.FindPrevious, new ExecutedRoutedEventHandler(ExecuteFindPrevious)));
      commandBindings.Add(new CommandBinding(SearchCommands.CloseSearchPanel, new ExecutedRoutedEventHandler(ExecuteCloseSearchPanel)));
    }

    private void ExecuteFind(object sender, ExecutedRoutedEventArgs e)
    {
      string pattern = TextArea.Selection.GetText();
      ExecuteFindImpl(pattern, true, false);
    }

    private void ExecuteFindEx(object sender, ExecutedRoutedEventArgs e)
    {
      var pattern = DeterminePattern();
      if (!string.IsNullOrEmpty(pattern))
        ExecuteFindImpl(pattern, false, false);
    }

    private void ExecuteFindExBackward(object sender, ExecutedRoutedEventArgs e)
    {
      var pattern = DeterminePattern();
      if (!string.IsNullOrEmpty(pattern))
        ExecuteFindImpl(pattern, false, true);
    }

    private void ExecuteFindImpl(string pattern, bool setFocusToSearchBox, bool isBackward)
    {
      if (this.panel == null || this.panel.IsClosed)
      {
        this.panel = new SearchPanel();
        this.panel.Attach(TextArea);
      }
      var currentPos = TextArea.Caret.Position;
      panel.SearchPattern = pattern;
      if (setFocusToSearchBox)
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Input, (Action) delegate { this.panel.Reactivate(); });
      if (isBackward)
      {
        TextArea.Caret.Position = currentPos;
        this.panel.FindPrevious();
      }
    }

    private void ExecuteFindNext(object sender, ExecutedRoutedEventArgs e)
    {
      if (this.panel != null)
        this.panel.FindNext();
    }

    private void ExecuteFindPrevious(object sender, ExecutedRoutedEventArgs e)
    {
      if (this.panel != null)
        this.panel.FindPrevious();
    }

    private void ExecuteCloseSearchPanel(object sender, ExecutedRoutedEventArgs e)
    {
      if (this.panel != null)
        this.panel.Close();
      this.panel = null;
    }

    private string DeterminePattern()
    {
      if (TextArea.Selection.IsEmpty)
      {
        var line = TextArea.Document.Lines[TextArea.Caret.Line - 1];
        var text = TextArea.Document.GetText(line.Offset, line.Length);
        var startIndex = Math.Min(TextArea.Caret.Column - 1, text.Length - 1);
        var firstCh = text[startIndex];
        int patternStartIndex;
        if (char.IsWhiteSpace(firstCh))
        {
          if (startIndex > 1 && IsIdentifier(text[startIndex - 1]))
            return ExtractNotEmpty(text, startIndex - 1, out patternStartIndex);
          else
            return "";
        }
        else if (IsIdentifier(firstCh))
          return ExtractIdentifier(text, startIndex, out patternStartIndex);
        else
          return ExtractNotEmpty(text, startIndex, out patternStartIndex);
      }
      else
        return TextArea.Selection.GetText();
    }

    private static string ExtractNotEmpty(string text, int startIndex, out int patternStartIndex)
    {
      return ExtractString(text, startIndex, char.IsWhiteSpace, out patternStartIndex);
    }

    private static string ExtractIdentifier(string text, int startIndex, out int patternStartIndex)
    {
      return ExtractString(text, startIndex, ch => !IsIdentifier(ch), out patternStartIndex);
    }

    private static string ExtractString(string text, int startIndex, Func<char, bool> predicate, out int patternStartIndex)
    {
      int i = startIndex;
      for (; i > 0; i--)
      {
        var ch = text[i];
        if (predicate(ch))
        {
          i++;
          break;
        }
      }

      int j = startIndex;
      for (; j < text.Length; j++)
      {
        var ch = text[j];
        if (predicate(ch))
          break;
      }

      patternStartIndex = i;
      return text.Substring(i, j - i);
    }

    private static bool IsIdentifier(char ch)
    {
      return char.IsLetterOrDigit(ch) || ch == '_';
    }
  }

  internal class NitraSearchCommands
  {
    public static readonly RoutedCommand FindEx = new RoutedCommand("FindEx", typeof(SearchPanel), new InputGestureCollection { new KeyGesture(Key.F3, ModifierKeys.Control) });
    public static readonly RoutedCommand FindExBackward = new RoutedCommand("FindExBackward", typeof(SearchPanel), new InputGestureCollection { new KeyGesture(Key.F3, ModifierKeys.Control | ModifierKeys.Shift) });
  }
}
