using System;
using System.Collections.Generic;
using System.Windows.Data;
using System.Globalization;

namespace RedisClient
{
    
    public class DelegateValueConverter : IValueConverter
    {
        private Func<object, Type, object, CultureInfo, object> _convertFunc;
        private Func<object, Type, object, CultureInfo, object> _convertBackFunc;

        public DelegateValueConverter(Func<object, Type, object, CultureInfo, object> convertFunc, Func<object, Type, object, CultureInfo, object> convertBackFunc)
            : this(convertFunc)
        {
            this._convertBackFunc = convertBackFunc;
        }

        public DelegateValueConverter(Func<object, Type, object, CultureInfo, object> convertFunc)
        {
            this._convertFunc = convertFunc;
        }

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (this._convertFunc != null)
                return this._convertFunc.Invoke(value, targetType, parameter, culture);
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (this._convertBackFunc == null)
                return value;
            return this._convertBackFunc.Invoke(value, targetType, parameter, culture);
        }
    }

}
