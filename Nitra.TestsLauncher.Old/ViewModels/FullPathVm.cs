using Nitra.Visualizer.Annotations;

using System;
using System.ComponentModel;
using System.Diagnostics;

namespace Nitra.ViewModels
{
  public abstract class FullPathVm : INotifyPropertyChanged, ITestTreeNode
  {
    [NotNull] public  string    FullPath { get; private set; }
    [NotNull] private bool      _isSelected;
    [NotNull] private TestState _testState;

    protected FullPathVm([NotNull] ITestTreeNode parent, [NotNull] string fullPath)
    {
      if (parent == null)
        throw new ArgumentNullException("parent");
      if (fullPath == null)
        throw new ArgumentNullException("fullPath");
      Parent = parent;
      FullPath = fullPath;
    }

    public abstract string Hint { get; }


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

    public virtual ITestTreeNode Parent { get; private set; }
  }
}
