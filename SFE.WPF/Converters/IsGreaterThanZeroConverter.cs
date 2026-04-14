using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace SFE.WPF.Converters
{
    public class IsGreaterThanZeroConverter : IValueConverter
    {
        public object Convert(object v, Type t, object p, CultureInfo c)
            => System.Convert.ToDecimal(v) > 0;

        public object ConvertBack(object v, Type t, object p, CultureInfo c)
            => throw new NotImplementedException();
    }
}
