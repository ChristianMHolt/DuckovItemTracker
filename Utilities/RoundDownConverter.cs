using System;
using System.Globalization;
using System.Windows.Data;

namespace ItemTracker.Utilities;

public class RoundDownConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is IConvertible convertible)
        {
            try
            {
                var number = System.Convert.ToDouble(convertible, culture);
                return Math.Floor(number);
            }
            catch
            {
                return Binding.DoNothing;
            }
        }

        return Binding.DoNothing;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
