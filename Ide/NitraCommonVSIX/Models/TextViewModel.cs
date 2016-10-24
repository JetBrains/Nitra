using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Text.Editor;
using static Nitra.ClientServer.Messages.AsyncServerMessage;

namespace Nitra.VisualStudio.Models
{
  class TextViewModel : IEquatable<TextViewModel>, IDisposable
  {
    readonly FileModel              _fileModel;
    readonly IWpfTextView           _wpfTextView;
             MatchedBrackets        _matchedBrackets;

    public TextViewModel(IWpfTextView wpfTextView, FileModel file)
    {
      _wpfTextView = wpfTextView;
      _fileModel   = file;
    }

    public bool Equals(TextViewModel other)
    {
      return _wpfTextView.Equals(other._wpfTextView);
    }

    public override bool Equals(object obj)
    {
      var other = obj as TextViewModel;

      if (other != null)
        return Equals(other);

      return false;
    }

    public override int GetHashCode()
    {
      return _wpfTextView.GetHashCode();
    }

    public override string ToString()
    {
      return _wpfTextView.ToString();
    }

    public void Dispose()
    {
      _wpfTextView.Properties.RemoveProperty(Constants.TextViewModelKey);
    }
  }
}
