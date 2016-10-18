//------------------------------------------------------------------------------
// <copyright file="EditorClassifierClassificationDefinition.cs" company="Company">
//     Copyright (c) Company.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

namespace Nitra.VisualStudio.Highlighting
{
  /// <summary>
  /// Classification type definition export for EditorClassifier
  /// </summary>
  internal static class NitraEditorClassifierClassificationDefinition
  {
    // This disables "The field is never used" compiler's warning. Justification: the field is used by MEF.
#pragma warning disable 169

    /// <summary>
    /// Defines the "EditorClassifier" classification type.
    /// </summary>
    [Export(typeof(ClassificationTypeDefinition))]
    [Name("EditorClassifier")]
    private static ClassificationTypeDefinition typeDefinition;

#pragma warning restore 169
  }
}
