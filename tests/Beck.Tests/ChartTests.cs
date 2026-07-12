using System.Text.RegularExpressions;
using Beck;
using Beck.Model;
using Beck.Skia;
using Xunit;
using Author = Beck.Authoring;

namespace Beck.Tests;

/// <summary>
/// The <c>type: chart</c> contract: the model builder normalises each chart kind, the painter emits a
/// well-formed on-canvas SVG whose series colours are token-derived expressions (never resolved
/// literals), and the fluent <see cref="Author.ChartDiagramBuilder"/> round-trips through the parser.
/// </summary>
public sealed class ChartTests
{
    private static readonly string _examplesDir = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "../../../../../docs/Beck.Docs/wwwroot/examples"));

    private static string Render(string yaml)
    {
        using var measurer = new SkiaTextMeasurer(TestFonts.Spec());
        return BeckSvg.Render(yaml, new SvgRenderOptions { Measurer = measurer, Font = TestFonts.Spec() });
    }

    // ---- model ----

    [Fact]
    public void Bar_ParsesSeriesAndDefaults()
    {
        var model = Validate.LoadDiagram("""
            type: chart
            chart: bar
            series:
              - { label: A, value: 42 }
              - { label: B, value: 33 }
              - { label: C, value: 28 }
            """);

        Assert.Equal(DiagramType.Chart, model.Meta.Type);
        Assert.False(model.Meta.Animate); // charts ship static
        var chart = Assert.IsType<ChartModel>(model.Chart);
        Assert.Equal(ChartKind.Bar, chart.Kind);
        Assert.Equal(ChartPalette.Analogous, chart.Palette);   // default
        Assert.Equal(LegendPlacement.Right, chart.Legend);      // default
        Assert.Equal(3, chart.Series.Count);
        Assert.Equal(new[] { "A", "B", "C" }, chart.Series.Select(s => s.Label).ToArray());
        Assert.Equal(42, chart.Series[0].Values[0]);
        Assert.Empty(model.Nodes);
        Assert.Empty(model.Edges);
    }

    [Fact]
    public void Line_ReadsValueLists()
    {
        var model = Validate.LoadDiagram("""
            type: chart
            chart: line
            palette: complementary
            series:
              - { label: This year, values: [30, 38, 42] }
              - { label: Last year, values: [24, 27, 33] }
            """);

        var chart = model.Chart!;
        Assert.Equal(ChartKind.Line, chart.Kind);
        Assert.Equal(ChartPalette.Complementary, chart.Palette);
        Assert.Equal(new double[] { 30, 38, 42 }, chart.Series[0].Values.ToArray());
    }

    [Fact]
    public void Scatter_ReadsPointPairs()
    {
        var model = Validate.LoadDiagram("""
            type: chart
            chart: scatter
            series:
              - { label: A, points: [[20, 72], [26, 80]] }
            """);

        var chart = model.Chart!;
        Assert.Equal(ChartKind.Scatter, chart.Kind);
        var pts = chart.Series[0].Points;
        Assert.Equal(2, pts.Count);
        Assert.Equal(new ChartPoint(20, 72), pts[0]);
        Assert.Equal(new ChartPoint(26, 80), pts[1]);
    }

    [Fact]
    public void Donut_ReadsCenterAndLegendValues()
    {
        var model = Validate.LoadDiagram("""
            type: chart
            chart: donut
            legend: right
            legendValues: true
            center: 134M
            centerLabel: total
            series:
              - { label: A, value: 42, color: "#ff0000" }
            """);

        var chart = model.Chart!;
        Assert.Equal(ChartKind.Donut, chart.Kind);
        Assert.True(chart.LegendValues);
        Assert.Equal("134M", chart.Center);
        Assert.Equal("total", chart.CenterLabel);
        Assert.Equal("#ff0000", chart.Series[0].Color); // explicit override preserved
    }

    [Theory]
    [InlineData("chart: bar\nseries: []", "at least one")]
    [InlineData("chart: scatter\nseries:\n  - { label: A }", "points")]
    [InlineData("chart: line\nseries:\n  - { label: A }", "values")]
    [InlineData("chart: bar\nseries:\n  - { label: A }", "value")]
    public void MissingData_Throws(string tail, string needle)
    {
        var ex = Assert.Throws<BeckYamlException>(() => Validate.LoadDiagram("type: chart\n" + tail));
        Assert.Contains(needle, ex.Message);
    }

    // ---- render ----

    [Theory]
    [InlineData("bar")]
    [InlineData("line")]
    [InlineData("pie")]
    [InlineData("donut")]
    [InlineData("scatter")]
    public void EveryKind_RendersOnCanvas(string kind)
    {
        var data = kind is "line"
            ? "  - { label: A, values: [10, 20, 30] }\n  - { label: B, values: [5, 15, 25] }"
            : kind is "scatter"
                ? "  - { label: A, points: [[1, 2], [3, 4]] }\n  - { label: B, points: [[5, 1], [6, 3]] }"
                : "  - { label: A, value: 42 }\n  - { label: B, value: 33 }\n  - { label: C, value: 28 }";
        var svg = Render($"type: chart\nchart: {kind}\nseries:\n{data}");

        Assert.StartsWith("<svg", svg);
        Assert.Contains("viewBox=\"0 0 ", svg);        // canvas starts at the origin
        Assert.False(HasNegativeGeometry(svg), "chart has off-canvas (negative) coordinates");
    }

    [Fact]
    public void SeriesColours_AreTokenDerivedNotLiterals()
    {
        // Analogous is a relative-colour expression over the primary token; sequential mixes toward neutral.
        var analogous = Render("type: chart\nchart: bar\npalette: analogous\nseries:\n  - { label: A, value: 1 }\n  - { label: B, value: 2 }");
        Assert.Contains("oklch(from var(--beck-primary)", analogous);

        var sequential = Render("type: chart\nchart: bar\npalette: sequential\nseries:\n  - { label: A, value: 1 }\n  - { label: B, value: 2 }");
        Assert.Contains("var(--beck-neutral)", sequential);

        var mono = Render("type: chart\nchart: bar\npalette: monochromatic\nseries:\n  - { label: A, value: 1 }\n  - { label: B, value: 2 }");
        Assert.Contains("color-mix(in oklab, var(--beck-primary), var(--beck-surface)", mono);

        // No resolved hex leaks in for a token-only chart (the fallbacks live in the <style> token block).
        var body = analogous[analogous.IndexOf("beck-canvas", StringComparison.Ordinal)..];
        Assert.DoesNotContain("#", body);
    }

    [Fact]
    public void SingleSlicePie_RendersAsCircleNotDegenerateArc()
    {
        var svg = Render("type: chart\nchart: pie\nlegend: none\nseries:\n  - { label: A, value: 1 }");
        Assert.Contains("<circle", svg);
    }

    [Theory]
    [InlineData("none")]
    [InlineData("top")]
    [InlineData("right")]
    [InlineData("bottom")]
    public void EveryLegendPlacement_Renders(string placement)
    {
        var svg = Render($"type: chart\nchart: bar\nlegend: {placement}\nseries:\n  - {{ label: North America, value: 42 }}\n  - {{ label: EMEA, value: 33 }}");
        Assert.StartsWith("<svg", svg);
        Assert.False(HasNegativeGeometry(svg));
    }

    // ---- authoring round-trip ----

    [Fact]
    public void Builder_RoundTripsThroughParser()
    {
        var yaml = new Author.ChartDiagramBuilder(Author.ChartKind.Donut, "Requests by service")
            .Palette(Author.ChartPalette.Analogous)
            .Legend(Author.LegendPlacement.Right, values: true)
            .Center("134M", "total")
            .Series("Gateway", 42.0)
            .Series("Catalog", 33.0)
            .Series("Search", s => s.Value(19).Color(Author.AccentToken.Info))
            .ToYaml();

        var model = Validate.LoadDiagram(yaml);
        var chart = model.Chart!;
        Assert.Equal("Requests by service", model.Meta.Title);
        Assert.Equal(ChartKind.Donut, chart.Kind);
        Assert.Equal(LegendPlacement.Right, chart.Legend);
        Assert.True(chart.LegendValues);
        Assert.Equal("134M", chart.Center);
        Assert.Equal(3, chart.Series.Count);
        Assert.Equal(42, chart.Series[0].Values[0]);
        Assert.Equal("var(--beck-info)", chart.Series[2].Color);
    }

    [Fact]
    public void Builder_LineAndScatterShapes()
    {
        var line = Validate.LoadDiagram(new Author.ChartDiagramBuilder(Author.ChartKind.Line)
            .Series("A", 1.0, 2.0, 3.0)
            .ToYaml());
        Assert.Equal(new double[] { 1, 2, 3 }, line.Chart!.Series[0].Values.ToArray());

        var scatter = Validate.LoadDiagram(new Author.ChartDiagramBuilder(Author.ChartKind.Scatter)
            .Series("A", (1.0, 2.0), (3.0, 4.0))
            .ToYaml());
        Assert.Equal(new ChartPoint(3, 4), scatter.Chart!.Series[0].Points[1]);
    }

    // ---- playground examples ----

    [Theory]
    [InlineData("chart-bar")]
    [InlineData("chart-line")]
    [InlineData("chart-pie")]
    [InlineData("chart-donut")]
    [InlineData("chart-scatter")]
    public void PlaygroundExampleRenders(string name)
    {
        var svg = Render(File.ReadAllText(Path.Combine(_examplesDir, name + ".beck.yaml")));
        Assert.StartsWith("<svg", svg);
        Assert.Contains("viewBox=\"0 0 ", svg);
        Assert.False(HasNegativeGeometry(svg), $"{name} has off-canvas coordinates");
    }

    /// <summary>True if any drawing coordinate (attribute or path/points data) is negative — the
    /// signature of an off-canvas render bug. Colour expressions like <c>calc(h - 52)</c> live in
    /// <c>style</c>/<c>fill</c>, not in the geometry attributes scanned here.</summary>
    private static bool HasNegativeGeometry(string svg)
    {
        foreach (Match mm in Regex.Matches(svg, @"\b(?:x|y|cx|cy|x1|y1|x2|y2|width|height|rx|r)=""(-?[0-9.]+)"""))
        {
            if (mm.Groups[1].Value.StartsWith('-'))
            {
                return true;
            }
        }

        foreach (Match mm in Regex.Matches(svg, @"(?:\bd|points)=""([^""]*)"""))
        {
            if (Regex.IsMatch(mm.Groups[1].Value, @"-[0-9]"))
            {
                return true;
            }
        }

        return false;
    }
}
