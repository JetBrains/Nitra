using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using Nitra.Runtime.Reflection;

namespace Nitra.Visualizer.ViewModels
{
  public class ReflectionStructColorConverter : IValueConverter
  {
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
      var node = (ReflectionStruct)value;

      if (node.Info.IsMarker)
        return Brushes.DarkGray;

      if (node.Location.IsEmpty)
      {
        if (node.Info.CanParseEmptyString)
          return Brushes.Teal;

        return Brushes.Red;
      }

      return SystemColors.ControlTextBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
      throw new NotImplementedException();
    }
  }
}
