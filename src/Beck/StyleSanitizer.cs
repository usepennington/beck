namespace Beck;

/// <summary>
/// Neutralises hostile substrings in a <em>custom</em> <see cref="BeckStyle"/> before its string
/// values reach the SVG <c>&lt;style&gt;</c> block. Custom styles may be fed from less-trusted config
/// (a token value or a font-family string lands verbatim inside <c>&lt;style&gt;</c>), so anything
/// that could close the style element or inject a rule — <c>&lt;/</c>, <c>&lt;!</c>, <c>{</c>,
/// <c>}</c>, <c>@import</c>, <c>url(</c> — is stripped. See <see cref="StyleTokens"/> for the
/// documented rule.
/// </summary>
/// <remarks>
/// Built-in styles (the instances in <see cref="BeckStyles.All"/>, matched by reference) are trusted
/// and bypass this entirely — Classic never pays any cost here and its output is untouched. The scan
/// is value-only: it never alters numeric geometry/mix fields, and it leaves <see cref="BeckStyle.Name"/>
/// alone (the name is validated <c>[a-z0-9-]+</c> at the seams and is never emitted into CSS).
/// </remarks>
internal static class StyleSanitizer
{
    // Substrings that could break out of the <style> block or inject a rule. `url(` is a documented
    // exfiltration vector once a token value is substituted into a url-consuming property
    // (background-image, filter: url(#x), …), so it is rejected in custom (less-trusted) styles.
    private static readonly string[] _forbidden = ["</", "<!", "@import", "url(", "{", "}"];

    /// <summary>Return <paramref name="style"/> unchanged if it is a trusted built-in; otherwise a
    /// copy with every CSS-bound string value stripped of forbidden substrings.</summary>
    public static BeckStyle Ensure(BeckStyle style)
    {
        foreach (var builtin in BeckStyles.All)
        {
            if (ReferenceEquals(builtin, style))
            {
                return style;
            }
        }

        return Sanitize(style);
    }

    private static BeckStyle Sanitize(BeckStyle s) => s with
    {
        LightTokens = CleanTokens(s.LightTokens),
        DarkTokens = CleanTokens(s.DarkTokens),
        Typography = s.Typography with
        {
            SansFamily = Clean(s.Typography.SansFamily),
            MonoFamily = Clean(s.Typography.MonoFamily),
        },
        Strokes = s.Strokes with
        {
            NodeDash = Clean(s.Strokes.NodeDash),
            GroupDash = Clean(s.Strokes.GroupDash),
            LifelineDash = Clean(s.Strokes.LifelineDash),
            EdgeDash = Clean(s.Strokes.EdgeDash),
            StreamDash = Clean(s.Strokes.StreamDash),
        },
        Motion = s.Motion with
        {
            PulseColor = CleanNullable(s.Motion.PulseColor),
        },
        Geometry = s.Geometry with
        {
            EdgeLabelHalo = Clean(s.Geometry.EdgeLabelHalo),
            NodeShadow = Clean(s.Geometry.NodeShadow),
            NodeShadowDark = Clean(s.Geometry.NodeShadowDark),
            SurfaceBackground = Clean(s.Geometry.SurfaceBackground),
        },
        Edges = s.Edges with
        {
            BaseLinecap = Clean(s.Edges.BaseLinecap),
            OverlayLinecap = Clean(s.Edges.OverlayLinecap),
            OverlayBloom = Clean(s.Edges.OverlayBloom),
            UnderlayColor = Clean(s.Edges.UnderlayColor),
            MarkerColor = CleanNullable(s.Edges.MarkerColor),
            MarkerOutline = CleanNullable(s.Edges.MarkerOutline),
            BaseColorPalette = CleanList(s.Edges.BaseColorPalette),
            OverlayPalette = CleanList(s.Edges.OverlayPalette),
        },
    };

    private static StyleTokens CleanTokens(StyleTokens t) =>
        new(t.Entries.Select(e => (Clean(e.Name), Clean(e.Value))).ToArray());

    private static IReadOnlyList<string> CleanList(IReadOnlyList<string> list) =>
        list.Count == 0 ? list : list.Select(Clean).ToArray();

    private static string? CleanNullable(string? value) => value is null ? null : Clean(value);

    private static string Clean(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }
        // Strip to a fixed point. A single Replace pass does not re-scan its own output, so a crafted
        // value can interleave a forbidden token such that removal rejoins the neighbours into a fresh
        // one (e.g. "<<//style>" → "</style>", "@im@importport" → "@import"). Loop until a full pass
        // finds nothing left to strip; each Replace strictly shrinks the string, so this terminates.
        var changed = true;
        while (changed)
        {
            changed = false;
            foreach (var bad in _forbidden)
            {
                if (value.Contains(bad, StringComparison.OrdinalIgnoreCase))
                {
                    value = value.Replace(bad, "", StringComparison.OrdinalIgnoreCase);
                    changed = true;
                }
            }
        }
        return value;
    }
}