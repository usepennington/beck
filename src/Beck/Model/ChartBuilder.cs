using System.Globalization;
using static Beck.Model.Coerce;

namespace Beck.Model;

/// <summary>
/// <c>type: chart</c> — a small, static data chart (bar, line, pie/donut, or scatter). Charts share
/// the meta/theming shell with every other diagram but carry no nodes, edges, or flow: the builder
/// produces a <see cref="ChartModel"/> and an otherwise-empty <see cref="DiagramModel"/>, and the
/// <see cref="Svg.ChartPainter"/> draws it directly (no layout/route/animate pipeline).
///
/// <code>
/// type: chart
/// meta: { title: Revenue by region }
/// chart: bar               # bar | line | pie | donut | scatter (default bar)
/// palette: analogous       # analogous | monochromatic | complementary | sequential (default analogous)
/// legend: right            # right | top | bottom | none (default right)
/// legendValues: true       # annotate legend entries with their value (bar/pie/donut)
/// center: 134M             # pie/donut centre headline
/// centerLabel: total       # pie/donut centre sub-caption
/// series:                  # one entry per bar / slice / line / point-cluster
///   - { label: North America, value: 42 }          # bar / pie / donut: one magnitude
///   - { label: 2024, values: [30, 34, 38, 42] }    # line: a value per x-step
///   - { label: Cluster A, points: [[20, 72], [26, 80]] }  # scatter: [x, y] pairs
///   - { label: EMEA, value: 33, color: var(--beck-info) }  # any series may pin its own colour
/// </code>
///
/// <para>Every series colour is derived from <c>--beck-primary</c> by the chosen
/// <see cref="ChartPalette"/> (a pure <c>color-mix</c>/relative-colour expression), unless the series
/// pins an explicit <c>color:</c> — so the whole set re-tints with the host palette and flips
/// light↔dark on the same switch as the rest of Beck.</para>
/// </summary>
internal static class ChartBuilder
{
    public static DiagramModel Build(IReadOnlyDictionary<string, object?> root)
    {
        var meta = Validate.BuildMeta(AsObject(root.GetValueOrDefault("meta"), "meta"), DiagramType.Chart);
        // Charts ship static — the render funnels through the animation gates exactly as mindmaps do.
        meta.Animate = false;

        var kind = OneOf(root.GetValueOrDefault("chart"), Tokens.ChartKind, "chart", ChartKind.Bar);
        var palette = OneOf(root.GetValueOrDefault("palette"), Tokens.ChartPalette, "palette", ChartPalette.Analogous);
        var legend = OneOf(root.GetValueOrDefault("legend"), Tokens.LegendPlacement, "legend", LegendPlacement.Right);
        var legendValues = OptBool(root.GetValueOrDefault("legendValues"), "legendValues", false);

        var rawSeries = AsArray(root.GetValueOrDefault("series"), "series");
        if (rawSeries.Count == 0)
        {
            throw new BeckYamlException("A chart needs at least one entry under `series`");
        }

        var series = new List<ChartSeries>(rawSeries.Count);
        for (var i = 0; i < rawSeries.Count; i++)
        {
            var s = AsObject(rawSeries[i], $"series[{i}]");
            var label = OptString(s.GetValueOrDefault("label"))
                ?? $"Series {(i + 1).ToString(CultureInfo.InvariantCulture)}";

            IReadOnlyList<double> values = [];
            IReadOnlyList<ChartPoint> points = [];
            switch (kind)
            {
                case ChartKind.Scatter:
                    points = PointList(s.GetValueOrDefault("points"), $"series[{i}].points");
                    if (points.Count == 0)
                    {
                        throw new BeckYamlException($"Scatter `series[{i}]` needs a non-empty `points` list of [x, y] pairs");
                    }

                    break;
                case ChartKind.Line:
                    values = NumberList(s.GetValueOrDefault("values"), $"series[{i}].values");
                    if (values.Count == 0)
                    {
                        throw new BeckYamlException($"Line `series[{i}]` needs a non-empty `values` list");
                    }

                    break;
                default: // bar / pie / donut — a single magnitude, from `value` (or `values[0]`)
                    var single = OptNumber(s.GetValueOrDefault("value"), $"series[{i}].value");
                    if (single is null)
                    {
                        var vs = NumberList(s.GetValueOrDefault("values"), $"series[{i}].values");
                        single = vs.Count > 0 ? vs[0] : throw new BeckYamlException(
                            $"`series[{i}]` needs a `value` (a number)");
                    }

                    values = [single.Value];
                    break;
            }

            series.Add(new ChartSeries
            {
                Label = label,
                Color = OptColor(s.GetValueOrDefault("color")),
                Values = values,
                Points = points,
            });
        }

        var chart = new ChartModel
        {
            Kind = kind,
            Palette = palette,
            Legend = legend,
            LegendValues = legendValues,
            Series = series,
            Center = OptString(root.GetValueOrDefault("center")),
            CenterLabel = OptString(root.GetValueOrDefault("centerLabel")),
        };

        return new DiagramModel
        {
            Meta = meta,
            Nodes = [],
            Groups = [],
            Edges = [],
            Flow = new FlowModel { Repeat = 0, RepeatDelay = 0, Steps = [], Derived = false },
            Sections = [],
            Chart = chart,
        };
    }

    /// <summary>Parse a list of numbers (line series values).</summary>
    private static List<double> NumberList(object? v, string field)
    {
        var arr = AsArray(v, field);
        var nums = new List<double>(arr.Count);
        for (var i = 0; i < arr.Count; i++)
        {
            nums.Add(OptNumber(arr[i], $"{field}[{i}]")
                ?? throw new BeckYamlException($"`{field}[{i}]` must be a number"));
        }

        return nums;
    }

    /// <summary>Parse a list of <c>[x, y]</c> pairs (scatter points).</summary>
    private static List<ChartPoint> PointList(object? v, string field)
    {
        var arr = AsArray(v, field);
        var pts = new List<ChartPoint>(arr.Count);
        for (var i = 0; i < arr.Count; i++)
        {
            var pair = AsArray(arr[i], $"{field}[{i}]");
            if (pair.Count < 2)
            {
                throw new BeckYamlException($"`{field}[{i}]` must be an [x, y] pair");
            }

            var x = OptNumber(pair[0], $"{field}[{i}].x") ?? throw new BeckYamlException($"`{field}[{i}].x` must be a number");
            var y = OptNumber(pair[1], $"{field}[{i}].y") ?? throw new BeckYamlException($"`{field}[{i}].y` must be a number");
            pts.Add(new ChartPoint(x, y));
        }

        return pts;
    }
}
