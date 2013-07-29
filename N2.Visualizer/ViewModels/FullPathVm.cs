using System.ComponentModel;
using N2.Visualizer.Annotations;

namespace N2.Visualizer.ViewModels
{
  class FullPathVm : INotifyPropertyChanged
  {
    private bool _isSelected;

    public FullPathVm(string fullPath)
    {
      FullPath = fullPath;
    }

    public string FullPath { get; private set; }

    public bool IsSelected
    {
      get { return _isSelected; }
      set
      {
        _isSelected = value;
        OnPropertyChanged("IsSelected");
      }
    }

    public event PropertyChangedEventHandler PropertyChanged;

    [NotifyPropertyChangedInvocator]
    protected virtual void OnPropertyChanged(string propertyName)
    {
      PropertyChangedEventHandler handler = PropertyChanged;
      if (handler != null) handler(this, new PropertyChangedEventArgs(propertyName));
    }
  }
}
