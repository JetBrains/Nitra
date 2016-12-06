using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace Nitra.VisualStudio.BraceMatching
{
  [Export(typeof(EditorFormatDefinition))]
  [ClassificationType(ClassificationTypeNames = Constants.DefenitionHighlighting)]
  [Name(Constants.DefenitionHighlighting)]
  [UserVisible(true)]
  [Order(Before = Priority.Default)]
  sealed class DefinitionFormat : ClassificationFormatDefinition
  {
    internal DefinitionFormat()
    {
      this.DisplayName = "Nitra Definition Highlighting";
      this.BackgroundColor = Color.FromRgb(255, 197, 205);
    }
  }

  static class DefinitionClassificationDefinition
  {
    [Export(typeof(ClassificationTypeDefinition))]
    [Name(Constants.DefenitionHighlighting)]
    internal static ClassificationTypeDefinition DefinitionType = null;
  }
}
