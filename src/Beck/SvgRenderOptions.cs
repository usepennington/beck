using Beck.Text;

namespace Beck;

/// <summary>How the diagram resolves its light/dark theme. Overrides <c>meta.theme</c>.</summary>
public enum ThemeMode
{
    /// <summary>
    /// Follow the host: a dark-marker ancestor (default <c>[data-theme='dark']</c>), else
    /// <c>prefers-color-scheme</c>. The selectors are configurable via
    /// <see cref="SvgRenderOptions.ThemeHooks"/>.
    /// </summary>
    Auto,
    /// <summary>Pin the light token set (no media query emitted).</summary>
    Light,
    /// <summary>Pin the dark token set (no media query emitted).</summary>
    Dark,
}

/// <summary>
/// The host CSS hooks a <see cref="ThemeMode.Auto"/> diagram uses to follow the page's dark mode.
/// The default matches sites that stamp <c>data-theme="light"|"dark"</c> on an ancestor (falling
/// back to the OS preference when neither is set); <see cref="Class"/> matches Tailwind-style
/// sites that toggle a <c>.dark</c> class instead. A record — compare presets by value.
/// </summary>
public sealed record ThemeHooks
{
    /// <summary>The default: <c>data-theme</c> attribute markers plus the OS-preference fallback.</summary>
    public static ThemeHooks DataTheme { get; } = new();

    /// <summary>
    /// Tailwind-style class markers: <c>.dark</c> on an ancestor means dark, its absence means
    /// light. No <see cref="SystemFallback"/> — such sites resolve the OS preference themselves
    /// in a bootstrap script, so the class is always authoritative.
    /// </summary>
    public static ThemeHooks Class { get; } = new() { Dark = ".dark", Light = ".light", SystemFallback = false };

    /// <summary>Selector matching an ancestor that marks the page dark.</summary>
    public string Dark { get; init; } = "[data-theme='dark']";

    /// <summary>
    /// Selector matching an ancestor that explicitly marks the page light — it guards the
    /// <see cref="SystemFallback"/> media query (<c>:root:not(Light)</c>) so an explicit light
    /// choice beats a dark OS preference. Unused when <see cref="SystemFallback"/> is off.
    /// </summary>
    public string Light { get; init; } = "[data-theme='light']";

    /// <summary>
    /// Also follow <c>prefers-color-scheme: dark</c> when no explicit marker is present. Turn off
    /// when the host always stamps the resolved theme (most class-based dark modes), where the
    /// absence of <see cref="Dark"/> already means light.
    /// </summary>
    public bool SystemFallback { get; init; } = true;
}

/// <summary>How (or whether) the flow choreography is compiled into the SVG.</summary>
public enum AnimationMode
{
    /// <summary>Animations run on load, looping per the flow (default).</summary>
    Full,
    /// <summary>Skip the schedule; emit the fully-revealed static frame.</summary>
    Static,
    /// <summary>Drive the whole choreography from scroll position via <c>animation-timeline: view()</c>.</summary>
    Scrub,
}

/// <summary>When the per-text <c>textLength</c> guard is emitted.</summary>
public enum TextLengthGuard
{
    /// <summary>On every measured text run (default) — compresses glyphs a few percent on a font mismatch.</summary>
    All,
    /// <summary>Only when the fallback metrics measurer was used (skip it under exact Skia measurement).</summary>
    FallbackOnly,
    /// <summary>Never emit the guard.</summary>
    Off,
}

/// <summary>
/// Options controlling a <see cref="BeckSvg.Render(string, SvgRenderOptions?)"/> call. A record, so
/// a host can derive from a base configuration with a <c>with</c> expression — the properties are
/// init-only.
/// </summary>
public sealed record SvgRenderOptions
{
    /// <summary>Text measurer; defaults to the embedded Inter metrics table.</summary>
    public ITextMeasurer Measurer { get; init; } = InterMetricsMeasurer.Instance;

    /// <summary>
    /// Fonts to render (and optionally embed) into the SVG. Rewrites the
    /// <c>--beck-font</c> / <c>--beck-font-mono</c> tokens so the artifact asks
    /// for the measured font. Skia users pass the same spec to their measurer.
    /// </summary>
    public BeckFontSpec? Font { get; init; }

    /// <summary>Overrides <c>meta.theme</c> (<c>auto</c>/<c>light</c>/<c>dark</c>).</summary>
    public ThemeMode? Theme { get; init; }

    /// <summary>
    /// The host CSS hooks an <see cref="ThemeMode.Auto"/> diagram keys its dark tokens off.
    /// Defaults to <see cref="ThemeHooks.DataTheme"/>; use <see cref="ThemeHooks.Class"/> for
    /// Tailwind-style <c>.dark</c>-class sites.
    /// </summary>
    public ThemeHooks ThemeHooks { get; init; } = ThemeHooks.DataTheme;

    /// <summary>Animation strategy; defaults to <see cref="AnimationMode.Full"/>.</summary>
    public AnimationMode Animation { get; init; } = AnimationMode.Full;

    /// <summary>Controls emission of the per-text <c>textLength</c> guard; defaults to <see cref="TextLengthGuard.All"/>.</summary>
    public TextLengthGuard TextLengthGuard { get; init; } = TextLengthGuard.All;

    /// <summary>Embed the font files as <c>@font-face</c> <c>data:</c> URIs so the SVG is correct standalone.</summary>
    [Obsolete("Font embedding is not yet implemented and has no effect. This option is retained (and still " +
              "hashed) for output determinism and may be removed in a future release.")]
    public bool EmbedFonts { get; init; }

    /// <summary>Overrides the derived content-hash id suffix. Testing hook (goal G6); leave null in production.</summary>
    public string? IdSuffix { get; init; }

    /// <summary>
    /// The site-wide default <see cref="BeckStyle"/>. Lowest precedence: a diagram's own
    /// <c>meta.style</c> (resolved by name) overrides it, and it in turn overrides
    /// <see cref="BeckStyle.Classic"/> — deliberately the opposite of <see cref="Theme"/>, where the
    /// option wins over the document. Null keeps the default (<see cref="BeckStyle.Classic"/>).
    /// </summary>
    public BeckStyle? Style { get; init; }

    /// <summary>
    /// A registry of custom styles that a diagram's <c>meta.style</c> token can name (looked up after
    /// the built-in <see cref="BeckStyles.ByName"/> table). Keys are the YAML tokens; values are the
    /// styles to resolve to. An unknown token warns and falls back to <see cref="Style"/> (or Classic).
    /// </summary>
    public IReadOnlyDictionary<string, BeckStyle>? Styles { get; init; }
}