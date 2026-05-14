using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace GABRFIDLabeler.Converters
{
    public class RadioButtonValueToCheckedConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null)
                return false;
            return value.ToString().Equals(parameter.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isChecked && isChecked && parameter is string paramStr)
            {
                if (targetType.IsEnum)
                    return Enum.Parse(targetType, paramStr);
                return paramStr;
            }
            return BindableProperty.UnsetValue;
        }
    }
}