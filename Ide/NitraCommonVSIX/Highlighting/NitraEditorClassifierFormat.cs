//------------------------------------------------------------------------------
// <copyright file="EditorClassifierFormat.cs" company="Company">
//     Copyright (c) Company.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

using System.ComponentModel.Composition;
using System.Windows.Media;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

namespace Nitra.VisualStudio.Highlighting
{
  /// <summary>
  /// Defines an editor format for the EditorClassifier type that has a purple background
  /// and is underlined.
  /// </summary>
  [Export(typeof(EditorFormatDefinition))]
  [ClassificationType(ClassificationTypeNames = "EditorClassifier")]
  [Name("EditorClassifier")]
  [UserVisible(true)] // This should be visible to the end user
  [Order(Before = Priority.Default)] // Set the priority to be after the default classifiers
  internal sealed class NitraEditorClassifierFormat : ClassificationFormatDefinition
  {
    /// <summary>
    /// Initializes a new instance of the <see cref="NitraEditorClassifierFormat"/> class.
    /// </summary>
    public NitraEditorClassifierFormat()
    {
      this.DisplayName = "EditorClassifier"; // Human readable version of the name
      //this.BackgroundColor = Colors.BlueViolet;
      this.ForegroundColor = Colors.Cyan;
    }
  }
}
