using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Printing;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using static Microsoft.ML.Transforms.OneHotEncodingEstimator;

namespace TraiderAssistant.UI.Converters
{
    public class IndicatorValueToAngleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double indicatorValue)
            {
                // Преобразование значения индикатора в угол поворота (например, от -50 до 50 в диапазоне от -90 до 90 градусов)
                return (indicatorValue + 100) * 180 / 200; //формула линейной интерполяции
            }
            return 0.0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
