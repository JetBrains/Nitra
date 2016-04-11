using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using Nitra.Runtime.Reflection;

namespace Nitra.Visualizer
{
  public class ReflectionStructColorConverter : IValueConverter
  {
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
      var node = (ReflectionStruct)value;

      if (node.Info.IsMarker)
        return Brushes.DarkGray;

      if (node.Kind == ReflectionKind.Deleted)
        return Brushes.Red;

      if (node.Span.IsEmpty)
      {
        if (node.Info.CanParseEmptyString)
          return Brushes.Teal;

        return Brushes.Red;
      }

      if (node.Kind == ReflectionKind.Ambiguous)
        return Brushes.DarkOrange;

      if (node.Kind == ReflectionKind.Recovered)
        return Brushes.MediumVioletRed;

      return SystemColors.ControlTextBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
      throw new NotImplementedException();
    }
  }
}
