//using System;
//using System.Collections.Generic;
//using System.Globalization;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;
//using System.Windows.Data;
//using System.Windows.Media.Imaging;


//namespace ImagoLib.Сonverter {
//    public class ByteArrayToImageConverter : IValueConverter {
//        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
//            if (value is byte[] byteArray) {
//                var bitmapImage = new BitmapImage();
//                using (var stream = new System.IO.MemoryStream(byteArray)) {
//                    bitmapImage.BeginInit();
//                    bitmapImage.StreamSource = stream;
//                    bitmapImage.EndInit();
//                }
//                return bitmapImage;
//            }
//            return null;
//        }

//        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
//            return null;
//        }
//    }
//}
