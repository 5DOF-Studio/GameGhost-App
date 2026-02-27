using System.Globalization;
using System.IO;
using Microsoft.Maui.Controls;
using WitnessDesktop.Models;

namespace WitnessDesktop.Utilities;

public class IsNotNullConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value != null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class InvertedBoolConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return !boolValue;
        }
        if (value is byte[] bytes)
        {
            return bytes == null || bytes.Length == 0;
        }
        return value == null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return !boolValue;
        }
        return false;
    }
}

public class ByteArrayToImageSourceConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is byte[] bytes && bytes.Length > 0)
        {
            return ImageSource.FromStream(() => new MemoryStream(bytes));
        }
        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class AudioBarHeightConverter : IValueConverter
{
    // Converts a normalized volume [0..1] into a bar height (double) with per-bar variation.
    // ConverterParameter: bar index (int)
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var v = value switch
        {
            float f => f,
            double d => (float)d,
            _ => 0f
        };

        v = Math.Clamp(v, 0f, 1f);

        var index = 0;
        if (parameter is string s && int.TryParse(s, out var parsed))
            index = parsed;
        else if (parameter is int i)
            index = i;

        const double minHeight = 6.0;
        const double maxHeight = 26.0;

        // Deterministic "spread" so bars aren't all the same height for a single volume value.
        var variation = 0.65 + 0.35 * Math.Abs(Math.Sin((index + 1) * 1.37));

        return minHeight + (maxHeight - minHeight) * v * variation;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts DeliveryState to short display string for chat metadata.
/// Returns empty string for None.
/// </summary>
public class DeliveryStateDisplayConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not DeliveryState state || state == DeliveryState.None)
            return string.Empty;
        return state switch
        {
            DeliveryState.Pending => "…",
            DeliveryState.Sent => "✓",
            DeliveryState.Failed => "✗",
            _ => string.Empty
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}
