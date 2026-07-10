namespace Beck.Text;

/// <summary>
/// Selects which embedded per-glyph metrics table the default <see cref="EmbeddedMetricsMeasurer"/>
/// sizes a diagram against. A <see cref="StyleTypography.MetricsFont"/> picks the table matching the
/// style's sans family so layout stays correct with zero font dependencies; the host page still
/// supplies the actual webfonts, and the <c>textLength</c> guard absorbs any residual mismatch.
/// Mono roles always resolve against the shared IBM Plex Mono coverage baked into every table.
/// </summary>
/// <remarks>
/// This is a data-only key (a plain value-type enum) so it composes cleanly into the
/// <see cref="BeckStyle"/> record's structural equality and <c>with</c>-derivation. An explicitly
/// supplied <see cref="SvgRenderOptions.Measurer"/> (e.g. <c>Beck.Skia.SkiaTextMeasurer</c>) always
/// overrides table selection — the key only steers the built-in fallback measurer.
/// </remarks>
public enum MetricsFont
{
    /// <summary>Inter (sans) + IBM Plex Mono — the classic default.</summary>
    Inter,

    /// <summary>Source Serif 4 (sans slot) + IBM Plex Mono — the editorial serif table.</summary>
    SourceSerif,

    /// <summary>Archivo + IBM Plex Mono — the brutalist / metro grotesque table (covers weight 800).</summary>
    Archivo,

    /// <summary>Shantell Sans + IBM Plex Mono — the sketch hand-drawn table.</summary>
    ShantellSans,
}
