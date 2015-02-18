using System.ComponentModel.Composition;
using System.Windows.Media;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

#if RESHARPER_9
using JetBrains.TextControl.DocumentMarkup;
// ReSharper disable UnassignedField.Global

[assembly: RegisterHighlighter(
  id: XXNamespaceXX.XXSpanClassNaneXXClassificationDefinition.Name,
  EffectColor = "Red",
  EffectType = EffectType.TEXT,
  Layer = HighlighterLayer.SYNTAX,
  VSPriority = VSPriority.IDENTIFIERS)]

#endif // RESHARPER_9

namespace XXNamespaceXX
{
  [ClassificationType(ClassificationTypeNames = Name)]
  [Order(After = "Formal Language Priority", Before = "Natural Language Priority")]
  [Export(typeof(EditorFormatDefinition))]
  [Name(Name)]
  [DisplayName(Name)]
  [UserVisible(true)]
  internal class XXSpanClassNaneXXClassificationDefinition : ClassificationFormatDefinition
  {
    public const string Name = "XXDisplay nameXX";

    public XXSpanClassNaneXXClassificationDefinition()
    {
      DisplayName = Name;
      ForegroundColor = Colors.Red;
    }

#pragma warning disable 649
    [Export, Name(Name), BaseDefinition("formal language")]
    internal ClassificationTypeDefinition ClassificationTypeDefinition;
#pragma warning restore 649
  }
}