using System.Globalization;
using System.Text;

namespace Beck.Authoring;

/// <summary>
/// Builds a <c>type: chart</c> Beck diagram — a small, static data chart (bar, line, pie/donut, or
/// scatter). The chart kind is fixed at construction; add one <see cref="Series(string, double)"/> per
/// bar/slice, per line, or per point-cluster. Series colours are derived from <c>--beck-primary</c> by
/// the chosen <see cref="Palette"/> unless a series pins its own colour, so the whole chart re-tints
/// with the host palette and flips light↔dark like every other Beck diagram.
/// </summary>
/// <example>
/// <code>
/// var fence = new ChartDiagramBuilder(ChartKind.Donut, "Requests by service")
///     .Palette(ChartPalette.Analogous)
///     .Legend(LegendPlacement.Right, values: true)
///     .Center("134M", "total")
///     .Series("Gateway", 42)
///     .Series("Catalog", 33)
///     .Series("Checkout", 28)
///     .ToFence();
/// </code>
/// </example>
public sealed class ChartDiagramBuilder
{
    private readonly MetaOptions _meta = new();
    private readonly ChartKind _kind;
    private readonly List<string> _series = new();
    private ChartPalette? _palette;
    private LegendPlacement? _legend;
    private bool _legendValues;
    private string? _center;
    private string? _centerLabel;

    /// <summary>Create a chart of the given <paramref name="kind"/>.</summary>
    public ChartDiagramBuilder(ChartKind kind) => _kind = kind;

    /// <summary>Create a chart of the given <paramref name="kind"/> with a title.</summary>
    public ChartDiagramBuilder(ChartKind kind, string title) : this(kind) => _meta._title = title;

    /// <summary>Set the diagram title.</summary>
    public ChartDiagramBuilder Title(string title) { _meta._title = title; return this; }

    /// <summary>Set the diagram subtitle.</summary>
    public ChartDiagramBuilder Subtitle(string subtitle) { _meta._subtitle = subtitle; return this; }

    /// <summary>Set the visual style by its <c>meta.style</c> token (e.g. <c>"classic"</c>).</summary>
    public ChartDiagramBuilder Style(string name) { _meta._style = name; return this; }

    /// <summary>Set the visual style from a <see cref="BeckStyle"/> (emits its <see cref="BeckStyle.Name"/>).</summary>
    public ChartDiagramBuilder Style(BeckStyle style) { _meta._style = style.Name; return this; }

    /// <summary>Set the theme: <see cref="ThemeMode.Auto"/> (default), <see cref="ThemeMode.Light"/>, or <see cref="ThemeMode.Dark"/>.</summary>
    public ChartDiagramBuilder Theme(ThemeMode theme) { _meta._theme = theme; return this; }

    /// <summary>How the chart behaves when wider than its container.</summary>
    public ChartDiagramBuilder Fit(FitMode fit) { _meta._fit = fit; return this; }

    /// <summary>Choose how series colours beyond the first are derived from the primary token.</summary>
    public ChartDiagramBuilder Palette(ChartPalette palette) { _palette = palette; return this; }

    /// <summary>Place the legend (default <see cref="LegendPlacement.Right"/>). Set
    /// <paramref name="values"/> to annotate each entry with its value (a right-column legend of
    /// single-magnitude series only).</summary>
    public ChartDiagramBuilder Legend(LegendPlacement placement, bool values = false)
    {
        _legend = placement;
        _legendValues = values;
        return this;
    }

    /// <summary>Set a pie/donut centre headline and an optional sub-caption under it.</summary>
    public ChartDiagramBuilder Center(string headline, string? caption = null)
    {
        _center = headline;
        _centerLabel = caption;
        return this;
    }

    /// <summary>Add a single-magnitude series — one bar, or one pie/donut slice.</summary>
    public ChartDiagramBuilder Series(string label, double value) => Series(label, s => s.Value(value));

    /// <summary>Add a line series — a value per x-step.</summary>
    public ChartDiagramBuilder Series(string label, params double[] values) => Series(label, s => s.Values(values));

    /// <summary>Add a scatter series — a cluster of <c>(x, y)</c> points sharing one colour.</summary>
    public ChartDiagramBuilder Series(string label, params (double X, double Y)[] points) => Series(label, s => s.Points(points));

    /// <summary>Add a series and refine it via <see cref="ChartSeriesBuilder"/> — its data shape and an optional colour.</summary>
    public ChartDiagramBuilder Series(string label, Action<ChartSeriesBuilder> configure)
    {
        var s = new ChartSeriesBuilder();
        configure(s);
        _series.Add(s.ToFlow(label));
        return this;
    }

    /// <summary>Render the diagram as Beck YAML.</summary>
    /// <exception cref="InvalidOperationException">The chart has no series.</exception>
    public string ToYaml()
    {
        if (_series.Count == 0)
        {
            throw new InvalidOperationException("A chart needs at least one Series().");
        }

        var sb = new StringBuilder();
        sb.Append("type: chart\n");
        _meta.AppendYaml(sb);
        sb.Append("chart: ").Append(Tokens.Of(_kind)).Append('\n');
        if (_palette is { } p)
        {
            sb.Append("palette: ").Append(Tokens.Of(p)).Append('\n');
        }

        if (_legend is { } l)
        {
            sb.Append("legend: ").Append(Tokens.Of(l)).Append('\n');
        }

        if (_legendValues)
        {
            sb.Append("legendValues: true\n");
        }

        if (_center != null)
        {
            sb.Append("center: ").Append(YamlWriter.Scalar(_center)).Append('\n');
        }

        if (_centerLabel != null)
        {
            sb.Append("centerLabel: ").Append(YamlWriter.Scalar(_centerLabel)).Append('\n');
        }

        sb.Append("series:\n");
        foreach (var s in _series)
        {
            sb.Append("  - ").Append(s).Append('\n');
        }

        return sb.ToString();
    }

    /// <summary>Render as a fenced <c>```beck</c> Markdown block — drop it into any Markdown page and it renders to a static SVG.</summary>
    public string ToFence() => BeckMarkdown.Fence(ToYaml());

    /// <inheritdoc/>
    public override string ToString() => ToYaml();
}

/// <summary>
/// Refines one series inside a <c>Series(label, s => …)</c> callback: its data shape
/// (<see cref="Value"/> for bar/pie/donut, <see cref="Values"/> for line, <see cref="Points"/> for
/// scatter) and an optional colour override.
/// </summary>
public sealed class ChartSeriesBuilder
{
    private double? _single;
    private List<double>? _values;
    private List<(double X, double Y)>? _points;
    private string? _color;

    /// <summary>Set a single magnitude — a bar height or a pie/donut slice.</summary>
    public ChartSeriesBuilder Value(double value) { _single = value; return this; }

    /// <summary>Set the line series' values, one per x-step.</summary>
    public ChartSeriesBuilder Values(params double[] values) { _values = values.ToList(); return this; }

    /// <summary>Set the scatter series' <c>(x, y)</c> points.</summary>
    public ChartSeriesBuilder Points(params (double X, double Y)[] points) { _points = points.ToList(); return this; }

    /// <summary>Override this series' colour with a semantic token (follows the theme).</summary>
    public ChartSeriesBuilder Color(AccentToken token) { _color = Tokens.Of(token); return this; }

    /// <summary>Override this series' colour with a raw CSS colour.</summary>
    public ChartSeriesBuilder Color(string color) { _color = color; return this; }

    private static string Num(double v) => v.ToString(CultureInfo.InvariantCulture);

    internal string ToFlow(string label)
    {
        var pairs = new List<(string, string)> { ("label", YamlWriter.Scalar(label)) };
        if (_single is { } v)
        {
            pairs.Add(("value", Num(v)));
        }
        else if (_values is { } vs)
        {
            pairs.Add(("values", YamlWriter.FlowSeq(vs.Select(Num))));
        }
        else if (_points is { } ps)
        {
            pairs.Add(("points", YamlWriter.FlowSeq(ps.Select(p => $"[{Num(p.X)}, {Num(p.Y)}]"))));
        }

        if (_color != null)
        {
            pairs.Add(("color", YamlWriter.Scalar(_color)));
        }

        return YamlWriter.FlowMap(pairs);
    }
}
