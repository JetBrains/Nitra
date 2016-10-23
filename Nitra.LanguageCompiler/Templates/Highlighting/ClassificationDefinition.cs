using System.ComponentModel.Composition;
using System.Windows.Media;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

namespace XXNamespaceXX
{
  [ClassificationType(ClassificationTypeNames = Name)]
  [Order(After = "Formal Language Priority", Before = "Natural Language Priority")]
  [Export(typeof(EditorFormatDefinition))]
  [Name(Name)]
  [DisplayName("XXDisplay nameXX")]
  [UserVisible(true)]
  internal class XXSpanClassNameXXClassificationDefinition : ClassificationFormatDefinition
  {
    public const string Name = "XXSpanClassFullNameXX";

    public XXSpanClassNameXXClassificationDefinition()
    {
      DisplayName = "XXSpanClassFullNameXX";
      ForegroundColor = Colors.Red;
    }

#pragma warning disable 649
    [Export, Name(Name), BaseDefinition("formal language")]
    internal ClassificationTypeDefinition ClassificationTypeDefinition;
#pragma warning restore 649
  }
}