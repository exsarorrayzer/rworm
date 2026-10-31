using System;
using System.Globalization;
using System.Windows.Data;

namespace RWORM.GUI.Converters
{
    [ValueConversion(typeof(bool), typeof(bool))]
    public class InverseBoolConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c)
            => value is bool b ? !b : false;
        public object ConvertBack(object value, Type t, object p, CultureInfo c)
            => value is bool b ? !b : false;
    }
}

