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
    [ClassificationType(ClassificationTypeNames = Constants.CurrentSymbol)]
    [Name(Constants.CurrentSymbol)]
    [UserVisible(true)]
    [Order(Before = Priority.Default)]
    sealed class NitraCurrentSymbolClassifications : ClassificationFormatDefinition
    {
        internal NitraCurrentSymbolClassifications()
        {
            this.DisplayName = "Nitra Current Sybmol";
            this.BackgroundColor = Colors.LightGray;
        }
    }

    static class NitraCurrentSymbolClassificationDefinition
    {
        [Export(typeof(ClassificationTypeDefinition))]
        [Name(Constants.CurrentSymbol)]
        internal static ClassificationTypeDefinition NitraCurrentSymbolType = null;
    }
}
