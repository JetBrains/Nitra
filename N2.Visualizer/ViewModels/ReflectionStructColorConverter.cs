using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using N2.Runtime.Reflection;

namespace N2.Visualizer.ViewModels
{
  public class ReflectionStructColorConverter : IValueConverter
  {
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
      var node = (ReflectionStruct)value;

      if (node.Location.IsEmpty)
        return Brushes.Teal;

      return SystemColors.ControlTextBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
      throw new NotImplementedException();
    }
  }
}
