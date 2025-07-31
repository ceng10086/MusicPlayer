using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MusicPlayer.Converters
{
    public class PlaylistWidthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isCollapsed)
            {
                // Width is 80 pixels when collapsed (showing only toggle button), 250 pixels when expanded
                return isCollapsed ? new GridLength(80, GridUnitType.Pixel) : new GridLength(250, GridUnitType.Pixel);
            }
            return new GridLength(250, GridUnitType.Pixel);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
