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
    /// <summary>Measure <paramref name="text"/> as rendered at <paramref name="role"/>.</summary>
    TextMetrics Measure(string text, FontRole role);
}
