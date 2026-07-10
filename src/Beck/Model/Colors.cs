using System.Globalization;

namespace Beck.Model;

/// <summary>
/// Color helpers ported from <c>src/util/color.ts</c>. <c>resolveColor</c> is NOT
/// ported — there is no DOM to probe; effects consume the <c>color-mix()</c>
/// output directly.
/// </summary>
internal static class Colors
{
    public static bool IsAccentToken(string value) => Tokens.Accent.TryParse(value, out _);

    /// <summary>
    /// Map an accent input to a CSS color value: a known token → <c>var(--beck-&lt;token&gt;)</c>;
    /// anything else (hex/rgb/named) passes through verbatim; null/empty falls back
    /// to the given token's var.
    /// </summary>
    public static string AccentToCss(string? accent, AccentToken fallback)
    {
        if (string.IsNullOrEmpty(accent)) return $"var(--beck-{Tokens.Accent.Wire(fallback)})";
        if (IsAccentToken(accent)) return $"var(--beck-{accent})";
        return accent;
    }

    /// <summary>A translucent version of a color, safe for rgb/var/hex inputs alike.</summary>
    public static string WithAlpha(string color, double percent) =>
        $"color-mix(in srgb, {color} {percent.ToString(CultureInfo.InvariantCulture)}%, transparent)";
}
