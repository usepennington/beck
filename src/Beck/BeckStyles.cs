namespace Beck;

/// <summary>
/// The registry of built-in <see cref="BeckStyle"/>s, keyed by their YAML <c>meta.style</c> token.
/// Phase 2 ships only <see cref="BeckStyle.Classic"/>; Phase 3/4 append the designed styles.
/// </summary>
/// <remarks>
/// A <c>meta.style</c> token (and a custom style's <see cref="BeckStyle.Name"/>) must match
/// <c>[a-z0-9-]+</c> — see <see cref="IsValidName"/>. The model validates the token against that rule
/// and warns (then ignores) on a malformed one; <c>BeckSvg</c> resolves a well-formed token through
/// this table first, then the per-render <see cref="Rendering.SvgRenderOptions.Styles"/> custom registry.
/// </remarks>
public static class BeckStyles
{
    /// <summary>All built-in styles, in declaration order.</summary>
    public static IReadOnlyList<BeckStyle> All { get; } = new[] { BeckStyle.Classic, MinimalStyle.Instance, TerminalStyle.Instance, BlueprintStyle.Instance, GlowStyle.Instance, BrutalistStyle.Instance, SketchStyle.Instance, ExtrudeStyle.Instance, CircuitStyle.Instance };

    /// <summary>Built-in styles keyed by <see cref="BeckStyle.Name"/> (ordinal, case-sensitive).</summary>
    public static IReadOnlyDictionary<string, BeckStyle> ByName { get; } =
        All.ToDictionary(s => s.Name, StringComparer.Ordinal);

    /// <summary>
    /// True when <paramref name="name"/> is a legal style token: non-empty and only lowercase
    /// ASCII letters, digits, and hyphens (<c>[a-z0-9-]+</c>). The single source of the naming rule.
    /// </summary>
    public static bool IsValidName(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        foreach (char c in name)
            if (c is not (>= 'a' and <= 'z' or >= '0' and <= '9' or '-')) return false;
        return true;
    }
}
