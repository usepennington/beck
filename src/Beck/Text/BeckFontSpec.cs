namespace Beck.Text;

/// <summary>
/// Describes the fonts a diagram should be measured and rendered with. Lives in
/// the core assembly (not <c>Beck.Skia</c>) because <see cref="SvgRenderOptions.Font"/>
/// consumes it to rewrite the emitted <c>--beck-font</c> / <c>--beck-font-mono</c>
/// tokens and to optionally embed the files as <c>@font-face</c> data URIs. The
/// same spec is handed to <c>SkiaTextMeasurer</c> for exact shaping.
/// </summary>
public sealed class BeckFontSpec
{
    /// <summary>Primary (sans) family; emitted into <c>--beck-font</c>.</summary>
    public required string Family { get; init; }

    /// <summary>Monospace family for class members / sequence messages; emitted into <c>--beck-font-mono</c>.</summary>
    public string? MonoFamily { get; init; }

    /// <summary>Sans font files per weight (400/500/600/700). A single variable font may back several weights.</summary>
    public required IReadOnlyDictionary<int, string> Files { get; init; }

    /// <summary>Monospace font files per weight, when a mono family is used.</summary>
    public IReadOnlyDictionary<int, string>? MonoFiles { get; init; }
}