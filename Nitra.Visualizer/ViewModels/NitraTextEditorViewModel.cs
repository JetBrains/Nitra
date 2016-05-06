using System;
using Nitra.ClientServer.Messages;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace Nitra.Visualizer.ViewModels
{
  public class NitraTextEditorViewModel : ReactiveObject
  {
    public MainWindowViewModel Host { get; set; }
    public IReactiveCommand<object> TryHighlightBraces { get; }
    public ReactiveList<PopupItemViewModel> PopupList { get; }
    public IReactiveCommand<object> SelectItem { get; }

    [Reactive] public int CaretOffset { get; set; }
    [Reactive] public int CaretLine { get; set; }
    [Reactive] public int CaretColumn { get; set; }

    [Reactive] public NSpan Selection { get; set; }
    [Reactive] public ScrollPosition ScrollPosition { get; set; }

    [Reactive] public bool PopupVisible { get; set; }
    [Reactive] public PopupItemViewModel SelectedPopupItem { get; set; }

    public NitraTextEditorViewModel(MainWindowViewModel host)
    {
      Host = host;

      PopupList = new ReactiveList<PopupItemViewModel>();
      
      TryHighlightBraces = ReactiveCommand.Create();
      TryHighlightBraces.Subscribe(_ => {
        //if (_matchedBracketsMarkers.Count > 0)
        //{
        //  foreach (var marker in _matchedBracketsMarkers)
        //    _textMarkerService.Remove(marker);
        //  _matchedBracketsMarkers.Clear();
        //}

        //var context = new MatchBracketsWalker.Context(caretPos);
        //_matchBracketsWalker.Walk(_parseResult, context);
        //_matchedBrackets = context.Brackets;

        //if (context.Brackets != null)
        //{
        //  foreach (var bracket in context.Brackets)
        //  {
        //    var marker1 = _textMarkerService.Create(bracket.OpenBracket.StartPos, bracket.OpenBracket.Length);
        //    marker1.BackgroundColor = Colors.LightGray;
        //    _matchedBracketsMarkers.Add(marker1);

        //    var marker2 = _textMarkerService.Create(bracket.CloseBracket.StartPos, bracket.CloseBracket.Length);
        //    marker2.BackgroundColor = Colors.LightGray;
        //    _matchedBracketsMarkers.Add(marker2);
        //  }
        //}
      });

      this.WhenAnyValue(vm => vm.CaretOffset)
          .InvokeCommand(TryHighlightBraces);
    }

    public void SelectText(Location location)
    {
      if (Host.CurrentSolution == null)
        return;

      var fileIdent = location.File;
      var span = location.Span;
      var file = Host.CurrentSolution.GetFile(fileIdent.FileId);

      if (file == null)
        return;

      if (file.Version != fileIdent.FileVersion)
        return;

      file.IsSelected = true;

      CaretOffset = span.StartPos;
      Selection = span;
      ScrollPosition = new ScrollPosition(CaretLine, CaretColumn);
    }
  }

  public struct ScrollPosition
  {
    public int Line;
    public int Column;

    public ScrollPosition(int line, int column)
    {
      Line = line;
      Column = column;
    }
  }
}