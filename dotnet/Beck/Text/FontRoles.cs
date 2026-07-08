namespace Beck.Rendering.Text;

/// <summary>
/// The concrete typography for a <see cref="FontRole"/>: sans-vs-mono, weight,
/// pixel size (rem values resolved at a 16px root), letter-spacing (em), and
/// whether the text is uppercased before measuring. Decoded from the utility
/// classes in <c>src/render/node.ts</c> and the CSS in <c>src/embed/styles.css</c>.
/// Line-height is deliberately excluded — it's a box-model concern owned by the
/// card sizer, not a text-measurement one.
/// </summary>
public readonly record struct FontRoleSpec(
    bool Mono, int Weight, double SizePx, double LetterSpacingEm, bool Uppercase);

/// <summary>
/// Resolves a <see cref="FontRole"/> to its concrete typography. Public so that a custom
/// <see cref="ITextMeasurer"/> (e.g. a browser-canvas measurer under WebAssembly) can be
/// implemented outside this assembly — measuring a role requires its family/size/weight.
/// </summary>
public static class FontRoles
{
    /// <summary>The classic role table, wrapping <see cref="Of"/> — the single source of truth
    /// for both measurement (which calls <see cref="Of"/> directly) and rendering (which reads
    /// through a style's <see cref="FontRoleTable"/>). A custom style supplies its own table.</summary>
    public static FontRoleSpec Of(FontRole role) => role switch
    {
        FontRole.CardTitle       => new(false, 600, 14,    0,     false),
        FontRole.CardSubtitle    => new(false, 400, 12,    0,     false),
        FontRole.Status          => new(false, 500, 10.4,  0,     false),
        FontRole.GhostLabel      => new(false, 500, 11.52, 0,     false),
        FontRole.StatusInline    => new(false, 500, 9.92,  0,     false),
        FontRole.PillTitle       => new(false, 600, 14,    0,     false),
        FontRole.PillSubtitle    => new(false, 400, 10.88, 0,     false),
        FontRole.ClassStereotype => new(false, 400, 10.4,  0.03,  false),
        FontRole.ClassTitle      => new(false, 600, 14,    0,     false),
        FontRole.ClassMember     => new(true,  400, 11.52, 0,     false),
        FontRole.EdgeLabel       => new(false, 500, 11.2,  0,     false),
        FontRole.PacketLabel     => new(false, 600, 10.56, 0,     false),
        FontRole.GroupLabel      => new(false, 600, 11.2,  0.04,  true),
        FontRole.MsgText         => new(true,  500, 10.88, 0,     false),
        FontRole.BandLabel       => new(true,  700, 9.92,  0.14,  true),
        FontRole.DiagramTitle    => new(false, 700, 24,   -0.02,  false),
        FontRole.DiagramSubtitle => new(false, 400, 14.4,  0,     false),
        FontRole.Narration       => new(false, 400, 14.72, 0,     false),
        _ => throw new ArgumentOutOfRangeException(nameof(role)),
    };
}

/// <summary>
/// A style-scoped resolver from <see cref="FontRole"/> to <see cref="FontRoleSpec"/>. The classic
/// table delegates to <see cref="FontRoles.Of"/> (no data duplicated; the measurement path is
/// unchanged); a custom style constructs one from its own spec map. Keeping the role table behind
/// this indirection lets rendering read style-scoped typography without touching
/// <see cref="ITextMeasurer"/> — the card sizer resolves roles around measurement calls.
/// </summary>
public sealed class FontRoleTable
{
    private readonly Func<FontRole, FontRoleSpec> _resolve;

    /// <summary>Wrap an arbitrary resolver (the classic table passes <see cref="FontRoles.Of"/>).</summary>
    public FontRoleTable(Func<FontRole, FontRoleSpec> resolve) => _resolve = resolve;

    /// <summary>Build a table from an explicit spec map (custom styles).</summary>
    public FontRoleTable(IReadOnlyDictionary<FontRole, FontRoleSpec> specs) =>
        _resolve = role => specs.TryGetValue(role, out var s) ? s : throw new ArgumentOutOfRangeException(nameof(role));

    /// <summary>Resolve a role's concrete typography.</summary>
    public FontRoleSpec Of(FontRole role) => _resolve(role);
}
