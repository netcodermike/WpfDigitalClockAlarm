using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using WpfDigitalClockAlarm.Models;
using WpfDigitalClockAlarm.Services;

namespace WpfDigitalClockAlarm.Converters
{
    public class TwoDigitConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int intVal)
            {
                return intVal.ToString("D2");
            }
            return value?.ToString() ?? "00";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (int.TryParse(value?.ToString(), out int result))
            {
                return result;
            }
            return 0;
        }
    }

    public class SoundTypeDescriptionConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is AlarmSoundType sound)
            {
                return sound switch
                {
                    AlarmSoundType.ClassicDigital => "Classic Digital (4-Pulse Beep)",
                    AlarmSoundType.HighAlert => "High-Frequency Alert",
                    AlarmSoundType.FastChirp => "Dual-Tone Rapid Chirp",
                    _ => sound.ToString()
                };
            }
            return value?.ToString() ?? string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class InverseBooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b)
            {
                return b ? Visibility.Collapsed : Visibility.Visible;
            }
            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility v)
            {
                return v != Visibility.Visible;
            }
            return false;
        }
    }

    public class PriorityToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is TaskPriority priority)
            {
                string hex = priority switch
                {
                    TaskPriority.Urgent => "#EF4444", // Red
                    TaskPriority.High => "#F97316",   // Orange
                    TaskPriority.Medium => "#F59E0B", // Amber
                    TaskPriority.Low => "#38BDF8",    // Sky blue
                    _ => "#94A3B8"
                };
                return (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;
            }
            return Brushes.SlateGray;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class BoolToTextDecorationConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is true)
            {
                return TextDecorations.Strikethrough;
            }
            return null!;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
