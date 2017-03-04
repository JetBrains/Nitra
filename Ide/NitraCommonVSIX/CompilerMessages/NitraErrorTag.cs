using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Tagging;

using Nitra.ClientServer.Messages;

namespace Nitra.VisualStudio.CompilerMessages
{
  public class NitraErrorTag : IErrorTag
  {
    public CompilerMessage Msg { get; }

    public NitraErrorTag(CompilerMessage msg)
    {
      Msg = msg;
    }

    public object ToolTipContent => Msg.Text;

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
