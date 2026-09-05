namespace Chipmunk.Models;

/// <summary>
/// Validates the serialized #AARRGGBB representation without introducing a
/// dependency from the model layer to WPF's System.Windows.Media types.
/// </summary>
public static class ArgbColorHex
{
    public static bool IsValid(string? value)
    {
        if (value is null || value.Length != 9 || value[0] != '#')
        {
            return false;
        }

        for (var index = 1; index < value.Length; index++)
        {
            if (!Uri.IsHexDigit(value[index]))
            {
                return false;
            }
        }

        return true;
    }

    public static string Normalize(string? value, string fallback) =>
        IsValid(value) ? value!.ToUpperInvariant() : fallback;

    public static bool TryParse(string? value, out ArgbColorValue color)
    {
        if (!IsValid(value))
        {
            color = default;
            return false;
        }

        color = new ArgbColorValue(
            Convert.ToByte(value!.Substring(1, 2), 16),
            Convert.ToByte(value.Substring(3, 2), 16),
            Convert.ToByte(value.Substring(5, 2), 16),
            Convert.ToByte(value.Substring(7, 2), 16));
        return true;
    }

    /// <summary>
    /// Accepts the persisted #AARRGGBB form and the familiar #RRGGBB user-input
    /// shorthand. Six-digit input is converted to a fully opaque ARGB value.
    /// </summary>
    public static bool TryParseUserInput(string? value, out ArgbColorValue color)
    {
        if (TryParse(value, out color))
        {
            return true;
        }

        return value is { Length: 7 } && value[0] == '#'
            && TryParse($"#FF{value[1..]}", out color);
    }

    public static string Format(ArgbColorValue color) =>
        $"#{color.Alpha:X2}{color.Red:X2}{color.Green:X2}{color.Blue:X2}";
}

public readonly record struct ArgbColorValue(byte Alpha, byte Red, byte Green, byte Blue);
