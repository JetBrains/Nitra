using Nitra.ClientServer.Messages;

namespace Nitra.Visualizer.ViewModels
{
  public class ItemAstNodeViewModel : AstNodeViewModel
  {
    public int Index { get; private set; }

    public ItemAstNodeViewModel(AstContext context, ObjectDescriptor objectDescriptor, int index) : base(context, objectDescriptor)
    {
      Index = index;
    }

    public string Pefix
    {
      get
      {
        if (Index >= 0)
          return "[" + Index + "] ";

        return "";
      }
    }

    public override bool IsRoot { get { return Index == -1; } }

    public override string ToString()
    {
      return _objectDescriptor.ToString();
    }
  }
}
