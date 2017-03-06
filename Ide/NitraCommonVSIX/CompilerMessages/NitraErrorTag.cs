using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Tagging;

using Nitra.ClientServer.Messages;
using Nitra.VisualStudio.QuickInfo;
using System;

namespace Nitra.VisualStudio.CompilerMessages
{
  public class NitraErrorTag : IErrorTag
  {
    public CompilerMessage Msg { get; }

    public NitraErrorTag(CompilerMessage msg)
    {
      Msg = msg;
    }

    public object ToolTipContent
    {
      get
      {
        var data = Msg.Text;
        if (data.StartsWith("<hint>", StringComparison.InvariantCulture))
        {
          var content = NitraQuickInfoSource.Hint.ParseToFrameworkElement(Msg.Text);
          return content;
        }

        return data;
      }
    }

    public string ErrorType
    {
      get
      {
        switch (Msg.Type)
        {
          case CompilerMessageType.FatalError:
            return PredefinedErrorTypeNames.OtherError;
          case CompilerMessageType.Error:
            return PredefinedErrorTypeNames.SyntaxError;
          case CompilerMessageType.Warning:
            return PredefinedErrorTypeNames.Warning;
          case CompilerMessageType.Hint:
            return PredefinedErrorTypeNames.Suggestion;
          default:
            return PredefinedErrorTypeNames.OtherError;
        }
      }
    }
  }
}
