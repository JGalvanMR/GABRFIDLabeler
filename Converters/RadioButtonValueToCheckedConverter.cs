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

            // Comparamos el valor del ViewModel con el parámetro del RadioButton
            return value.ToString() == parameter.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Si el RadioButton se marca (value es true), devolvemos el parámetro al ViewModel
            if (value is bool isChecked && isChecked)
                return parameter?.ToString();

            return string.Empty;
        }
    }
}

