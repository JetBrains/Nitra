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
    [ClassificationType(ClassificationTypeNames = Constants.BraceMatchingSecond)]
    [Name(Constants.BraceMatchingSecond)]
    [UserVisible(true)]
    [Order(Before = Priority.Default)]
    sealed class NitraBraceMatchingSecondFormat : ClassificationFormatDefinition
    {
        internal NitraBraceMatchingSecondFormat()
        {
            this.DisplayName = "Nitra BraceMatching 2";
            this.BackgroundColor = Colors.Gold;
        }
    }

    static class NitraBraceMatchingSecondClassificationDefinition
    {
        [Export(typeof(ClassificationTypeDefinition))]
        [Name(Constants.BraceMatchingSecond)]
        internal static ClassificationTypeDefinition NitraBraceMatchingSecondType = null;
    }
}
