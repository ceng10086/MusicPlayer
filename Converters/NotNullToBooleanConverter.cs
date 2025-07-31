using System;
using System.Globalization;
using System.Windows.Data;

namespace MusicPlayer.Converters
{
    public class NotNullToBooleanConverter : IValueConverter
    {
        public static readonly NotNullToBooleanConverter Instance = new NotNullToBooleanConverter();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value != null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
