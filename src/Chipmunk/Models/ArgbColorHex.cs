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
}
