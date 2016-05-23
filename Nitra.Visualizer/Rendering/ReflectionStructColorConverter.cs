using Nitra.ClientServer.Messages;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Nitra.Visualizer
{
  public class ParseTreeReflectionStructColorConverter : IValueConverter
  {
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
      var node = (ParseTreeReflectionStruct)value;

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

      return SystemColors.ControlTextBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
      throw new NotImplementedException();
    }
  }
}
