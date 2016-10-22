//------------------------------------------------------------------------------
// <copyright file="EditorClassifierProvider.cs" company="Company">
//     Copyright (c) Company.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

namespace Nitra.VisualStudio.Highlighting
{
  /// <summary>
  /// Classifier provider. It adds the classifier to the set of classifiers.
  /// </summary>
  [Export(typeof(IClassifierProvider))]
  [ContentType("code")] // This classifier applies to all text files.
  internal class NitraEditorClassifierProvider : IClassifierProvider
  {
    // Disable "Field is never assigned to..." compiler's warning. Justification: the field is assigned by MEF.
#pragma warning disable 649

    /// <summary>
    /// Classification registry to be used for getting a reference
    /// to the custom classification type later.
    /// </summary>
    [Import] IClassificationTypeRegistryService _classificationRegistry;
    [Import] IClassificationFormatMapService    _classificationFormatMapService;

#pragma warning restore 649

    #region IClassifierProvider

    /// <summary>
    /// Gets a classifier for the given text buffer.
    /// </summary>
    /// <param name="buffer">The <see cref="ITextBuffer"/> to classify.</param>
    /// <returns>A classifier for the text buffer, or null if the provider cannot do so in its current state.</returns>
    public IClassifier GetClassifier(ITextBuffer buffer)
    {
      return buffer.Properties.GetOrCreateSingletonProperty(Constants.NitraEditorClassifierKey, () => new NitraEditorClassifier(_classificationRegistry, _classificationFormatMapService, buffer));
    }

    #endregion
  }
}
