using Beck.Styles;

namespace Beck;

/// <summary>
/// The registry of built-in <see cref="BeckStyle"/>s, keyed by their YAML <c>meta.style</c> token,
/// with a static shortcut per style for C# callers (<c>BeckStyles.Glow</c> reads better than
/// <c>BeckStyles.ByName["glow"]</c> and survives a rename).
/// </summary>
/// <remarks>
/// A <c>meta.style</c> token (and a custom style's <see cref="BeckStyle.Name"/>) must match
/// <c>[a-z0-9-]+</c> — see <see cref="IsValidName"/>. The model validates the token against that rule
/// and warns (then ignores) on a malformed one; <c>BeckSvg</c> resolves a well-formed token through
/// this table first, then the per-render <see cref="SvgRenderOptions.Styles"/> custom registry.
/// </remarks>
public static class BeckStyles
{
    /// <summary>The <c>classic</c> default style; alias of <see cref="BeckStyle.Classic"/>.</summary>
    public static BeckStyle Classic => BeckStyle.Classic;

    /// <summary>The <c>minimal</c> built-in style (<see cref="Styles.MinimalStyle"/>).</summary>
    public static BeckStyle Minimal => MinimalStyle.Instance;

    /// <summary>The <c>terminal</c> built-in style (<see cref="Styles.TerminalStyle"/>).</summary>
    public static BeckStyle Terminal => TerminalStyle.Instance;

    /// <summary>The <c>blueprint</c> built-in style (<see cref="Styles.BlueprintStyle"/>).</summary>
    public static BeckStyle Blueprint => BlueprintStyle.Instance;

    /// <summary>The <c>glow</c> built-in style (<see cref="Styles.GlowStyle"/>).</summary>
    public static BeckStyle Glow => GlowStyle.Instance;

    /// <summary>The <c>brutalist</c> built-in style (<see cref="Styles.BrutalistStyle"/>).</summary>
    public static BeckStyle Brutalist => BrutalistStyle.Instance;

    /// <summary>The <c>sketch</c> built-in style (<see cref="Styles.SketchStyle"/>).</summary>
    public static BeckStyle Sketch => SketchStyle.Instance;

    /// <summary>The <c>extrude</c> built-in style (<see cref="Styles.ExtrudeStyle"/>).</summary>
    public static BeckStyle Extrude => ExtrudeStyle.Instance;

    /// <summary>The <c>circuit</c> built-in style (<see cref="Styles.CircuitStyle"/>).</summary>
    public static BeckStyle Circuit => CircuitStyle.Instance;

    /// <summary>All built-in styles, in declaration order.</summary>
    public static IReadOnlyList<BeckStyle> All { get; } = [Classic, Minimal, Terminal, Blueprint, Glow, Brutalist, Sketch, Extrude, Circuit,
    ];

    /// <summary>Built-in styles keyed by <see cref="BeckStyle.Name"/> (ordinal, case-sensitive).</summary>
    public static IReadOnlyDictionary<string, BeckStyle> ByName { get; } =
        All.ToDictionary(s => s.Name, StringComparer.Ordinal);

    /// <summary>
    /// True when <paramref name="name"/> is a legal style token: non-empty and only lowercase
    /// ASCII letters, digits, and hyphens (<c>[a-z0-9-]+</c>). The single source of the naming rule.
    /// </summary>
    public static bool IsValidName(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return false;
        }

        foreach (var c in name)
        {
            if (c is not (>= 'a' and <= 'z' or >= '0' and <= '9' or '-'))
            {
                return false;
            }
        }

        return true;
    }
}