//using System;
//using System.Collections.Generic;
//using System.ComponentModel.Composition;
//using System.Linq;
//using System.Windows.Media;
//using Microsoft.VisualStudio.Text.Classification;
//using Microsoft.VisualStudio.Utilities;
//
//namespace XXNamespaceXX
//{
//  [Export(typeof(EditorFormatDefinition))]
//  [ClassificationType(ClassificationTypeNames = Constants.ErrorClassificationTypeName)]
//  [Name(Constants.ErrorClassificationTypeName)]
//  [DisplayName("XXLanguageXX Error")]
//  [UserVisible(true)]
//  [Order(Before = Priority.Default)]
//  public sealed class ErrorClassificationDefinition : ClassificationFormatDefinition
//  {
//    public ErrorClassificationDefinition()
//    {
//      this.DisplayName = "XXLanguageXX Error";
//      this.BackgroundColor = Colors.Red;
//      this.ForegroundColor = Colors.White;
//    }
//  }
//}
