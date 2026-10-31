using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace RWORM.GUI.Converters
{
    /// <summary>
    /// Converts a local image file path to a frozen WPF BitmapImage.
    /// Used for the country flag PNGs shipped in Resources/Flags.
    /// Results are cached by path: the Clients poll rebuilds rows every few
    /// seconds, and reloading a BitmapImage from disk each time would flicker.
    /// </summary>
    public class UrlToImageConverter : IValueConverter
    {
        private static readonly ConcurrentDictionary<string, BitmapImage?> Cache = new();

        public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not string path || string.IsNullOrWhiteSpace(path)) return null;
            return Cache.GetOrAdd(path, Load);
        }

        private static BitmapImage? Load(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption   = BitmapCacheOption.OnLoad;   // read fully, release the file lock
                bmp.CreateOptions = BitmapCreateOptions.None;
                bmp.UriSource     = new Uri(path, UriKind.Absolute);
                bmp.EndInit();
                bmp.Freeze();                                    // usable from any thread
                return bmp;
            }
            catch { return null; }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
