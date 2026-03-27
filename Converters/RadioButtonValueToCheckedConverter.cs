using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Globalization;

namespace ZebraRFIDApp.Converters
{
    public class RadioButtonValueToCheckedConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Devuelve true si el valor actual coincide con el parámetro (modo seleccionado)
            return value?.ToString() == parameter?.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Si el RadioButton está marcado, devolvemos el parámetro (el modo seleccionado)
            if (value is bool isChecked && isChecked)
                return parameter?.ToString();

            // Si no está marcado, devolvemos Binding.DoNothing en vez de null
            return Binding.DoNothing;
        }
    }
}
