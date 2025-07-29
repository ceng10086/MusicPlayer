using System;
using System.Globalization;
using System.Windows.Data;

namespace MusicPlayer.Converters
{
    public class SpectrumHeightConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double spectrumValue)
            {
                var maxHeight = parameter != null ? double.Parse(parameter.ToString()!) : 100.0;
                return Math.Max(2, spectrumValue * maxHeight);
            }
            return 2.0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
