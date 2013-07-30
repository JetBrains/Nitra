using System;
using System.ComponentModel;
using N2.Visualizer.Annotations;

namespace N2.Visualizer.ViewModels
{
  class FullPathVm : INotifyPropertyChanged
  {
    private bool      _isSelected;
    private TestState _testState;

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

    public TestState TestState
    {
      get { return _testState; }
      protected set
      {
        if (value == _testState) return;
        _testState = value;
        OnPropertyChanged("TestState");
        OnPropertyChanged("DispayImage");
      }
    }

    public string DispayImage
    {
      get
      {
        switch (TestState)
        {
          case TestState.Failure:      return @"Images/TreeIcons/failure.png";
          case TestState.Ignored:      return @"Images/TreeIcons/ignored.png";
          case TestState.Inconclusive: return @"Images/TreeIcons/inconclusive.png";
          case TestState.Skipped:      return @"Images/TreeIcons/skipped.png";
          case TestState.Success:      return @"Images/TreeIcons/success.png";
          default:
            throw new ArgumentOutOfRangeException();
        }
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
