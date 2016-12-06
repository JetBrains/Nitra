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
  [ClassificationType(ClassificationTypeNames = Constants.ReferenceHighlighting)]
  [Name(Constants.ReferenceHighlighting)]
  [UserVisible(true)]
  [Order(After = Priority.Low)]
  sealed class ReferenceFormat : ClassificationFormatDefinition
  {
    internal ReferenceFormat()
    {
      this.DisplayName = "Nitra Reference Highlighting";
      this.BackgroundColor = Color.FromRgb(159, 216, 251);
    }
  }

  static class ReferenceClassificationDefinition
  {
    [Export(typeof(ClassificationTypeDefinition))]
    [Name(Constants.ReferenceHighlighting)]
    internal static ClassificationTypeDefinition ReferenceType = null;
  }
}
