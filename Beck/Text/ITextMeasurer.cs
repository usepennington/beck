namespace Beck.Rendering.Text;

/// <summary>
/// A typographic role in a Beck diagram. Each value pins a font family, pixel
/// size, weight (and, where relevant, letter-spacing / uppercasing) decoded from
/// the utility classes in <c>src/render/node.ts</c> and the CSS in
/// <c>src/embed/styles.css</c>. <c>rem</c> values resolve against a 16px root.
/// Measurers apply letter-spacing (width += ls·(n−1)) and uppercasing themselves,
/// so callers stay dumb.
/// </summary>
public enum FontRole
{
    CardTitle,        // Inter 14px / 600, line-height 1.3
    CardSubtitle,     // Inter 12px / 400, lh 1.35
    Status,           // Inter 10.4px (0.65rem) / 500, lh 1.2
    GhostLabel,       // Inter 11.52px (0.72rem) / 500
    StatusInline,     // Inter 9.92px (0.62rem) / 500
    PillTitle,        // Inter 14px / 600, lh 1.3
    PillSubtitle,     // Inter 10.88px (0.68rem) / 400, lh 1.3
    ClassStereotype,  // Inter 10.4px / 400, lh 1.3, letter-spacing 0.03em
    ClassTitle,       // Inter 14px / 600, lh 1.4
    ClassMember,      // IBM Plex Mono 11.52px / 400, lh 1.45
    EdgeLabel,        // Inter 11.2px (0.7rem) / 500
    PacketLabel,      // Inter 10.56px (0.66rem) / 600
    GroupLabel,       // Inter 11.2px (0.7rem) / 600, ls 0.04em, uppercase
    MsgText,          // IBM Plex Mono 10.88px / 500
    BandLabel,        // IBM Plex Mono 9.92px / 700, ls 0.14em, uppercase
    DiagramTitle,     // Inter 24px / 700, ls -0.02em
    DiagramSubtitle,  // Inter 14.4px (0.9rem) / 400
    Narration,        // Inter 14.72px (0.92rem) / 400, lh 1.45
}

/// <summary>Advance width and vertical extents of a laid-out text run, in px.</summary>
public readonly record struct TextMetrics(double Width, double Ascent, double Descent);

/// <summary>
/// Measures a single-line text run for a given typographic role. The default
/// implementation (<see cref="InterMetricsMeasurer"/>) uses an embedded metrics
/// table for Inter / IBM Plex Mono; <c>Beck.Skia</c> supplies an exact
/// SkiaSharp + HarfBuzz shaping measurer when the user provides font files.
/// </summary>
public interface ITextMeasurer
{
    /// <summary>Measure <paramref name="text"/> as rendered at <paramref name="role"/>, using the
    /// <em>classic</em> typography for that role (<see cref="FontRoles.Of"/>). Kept as the primitive
    /// every existing (and third-party) measurer implements.</summary>
    TextMetrics Measure(string text, FontRole role);

    /// <summary>
    /// Measure <paramref name="text"/> at <paramref name="role"/> using <paramref name="spec"/> as the
    /// concrete typography — the <em>active style's</em> resolved <see cref="FontRoleSpec"/> (from
    /// <see cref="FontRoleTable.Of"/>) rather than the classic <see cref="FontRoles.Of"/>. This is the
    /// seam that lets a style remap a role's weight/size/family/letter-spacing/case and have the
    /// <em>measured</em> box (and its <c>textLength</c> guard) match what actually renders, not a
    /// classic-sized box the guard would then squeeze glyphs into.
    /// <para>Source-compatible default: a measurer that does not override this ignores the override and
    /// measures the classic <paramref name="role"/>. The engine's own measurers
    /// (<see cref="EmbeddedMetricsMeasurer"/>, <c>Beck.Skia.SkiaTextMeasurer</c>) override it to honour
    /// <paramref name="spec"/>; the small residual delta under a non-overriding measurer is absorbed by
    /// the <c>textLength</c> guard exactly as an approximate measurer's is. For classic input
    /// (<paramref name="spec"/> == <c>FontRoles.Of(role)</c>) it is byte-identical to the two-arg call.</para>
    /// </summary>
    TextMetrics Measure(string text, FontRole role, FontRoleSpec spec) => Measure(text, role);

    /// <summary>
    /// True when this measurer <em>approximates</em> widths from an embedded metrics table rather
    /// than measuring the fonts the SVG actually renders with (the default,
    /// <see cref="InterMetricsMeasurer"/>, does). Drives <see cref="SvgRenderOptions.TextLengthGuard"/>'s
    /// <see cref="TextLengthGuard.FallbackOnly"/> mode: the per-text <c>textLength</c> guard is emitted
    /// only for approximate measurements. Defaults to <c>true</c> (conservative — a custom measurer
    /// that does not opt out still gets guards), so existing implementations need no change.
    /// An exact, font-file-backed measurer (e.g. <c>Beck.Skia.SkiaTextMeasurer</c>) overrides this to
    /// <c>false</c>.
    /// </summary>
    bool IsApproximate => true;
}
