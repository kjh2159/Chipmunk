namespace Chipmunk.Models;

/// <summary>
/// Converts between RGB and HSV without coupling reusable color logic to WPF.
/// Hue is expressed in degrees; saturation and value are in the 0..1 range.
/// </summary>
public static class HsvColorConverter
{
    public static RgbColorValue ToRgb(double hue, double saturation, double value)
    {
        hue = double.IsFinite(hue) ? ((hue % 360) + 360) % 360 : 0;
        saturation = ClampUnit(saturation);
        value = ClampUnit(value);

        var chroma = value * saturation;
        var hueSection = hue / 60;
        var intermediate = chroma * (1 - Math.Abs(hueSection % 2 - 1));
        var (red, green, blue) = hueSection switch
        {
            < 1 => (chroma, intermediate, 0d),
            < 2 => (intermediate, chroma, 0d),
            < 3 => (0d, chroma, intermediate),
            < 4 => (0d, intermediate, chroma),
            < 5 => (intermediate, 0d, chroma),
            _ => (chroma, 0d, intermediate)
        };
        var match = value - chroma;

        return new RgbColorValue(
            ToByte(red + match),
            ToByte(green + match),
            ToByte(blue + match));
    }

    public static HsvColorValue ToHsv(byte red, byte green, byte blue)
    {
        var r = red / 255d;
        var g = green / 255d;
        var b = blue / 255d;
        var maximum = Math.Max(r, Math.Max(g, b));
        var minimum = Math.Min(r, Math.Min(g, b));
        var delta = maximum - minimum;

        double hue;
        if (delta == 0)
        {
            hue = 0;
        }
        else if (maximum == r)
        {
            hue = 60 * (((g - b) / delta) % 6);
        }
        else if (maximum == g)
        {
            hue = 60 * ((b - r) / delta + 2);
        }
        else
        {
            hue = 60 * ((r - g) / delta + 4);
        }

        if (hue < 0)
        {
            hue += 360;
        }

        var saturation = maximum == 0 ? 0 : delta / maximum;
        return new HsvColorValue(hue, saturation, maximum);
    }

    private static double ClampUnit(double value) =>
        double.IsFinite(value) ? Math.Clamp(value, 0, 1) : 0;

    private static byte ToByte(double value) =>
        (byte)Math.Round(Math.Clamp(value, 0, 1) * 255, MidpointRounding.AwayFromZero);
}

public readonly record struct RgbColorValue(byte Red, byte Green, byte Blue);
public readonly record struct HsvColorValue(double Hue, double Saturation, double Value);
