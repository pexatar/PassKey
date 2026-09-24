using Microsoft.UI;

namespace PassKey.Desktop.Helpers;

/// <summary>
/// Parses the "#RRGGBB" colour literals used by the category palettes.
/// </summary>
/// <remarks>
/// The same seven-line parser was copy-pasted into two note views. It is not view logic —
/// it is a string format — so it lives in one place where a fix reaches every caller.
/// </remarks>
public static class ColorHex
{
    /// <summary>Converts "#RRGGBB" (or "RRGGBB") into an opaque colour.</summary>
    public static Windows.UI.Color Parse(string hex)
    {
        hex = hex.TrimStart('#');
        return ColorHelper.FromArgb(255,
            byte.Parse(hex[..2], System.Globalization.NumberStyles.HexNumber),
            byte.Parse(hex[2..4], System.Globalization.NumberStyles.HexNumber),
            byte.Parse(hex[4..6], System.Globalization.NumberStyles.HexNumber));
    }
}
