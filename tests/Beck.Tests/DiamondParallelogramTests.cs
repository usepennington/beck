using Beck.Layout;
using Beck.Model;
using Beck.Route;
using Beck.Svg;
using Beck.Tests.CleanLines;
using Beck.Text;
using Xunit;

namespace Beck.Tests;

/// <summary>
/// Gates the diamond/parallelogram node primitives (flowchart groundwork, rendered from architecture
/// YAML via <c>shape: diamond</c>/<c>parallelogram</c>): the <see cref="CardSizer"/> box-model math
/// (inscribed-rect for the diamond, card + skew for the parallelogram), the point-anchored router
/// contract (diamond anchors pinned to their vertices, never spread), and a render smoke check.
/// </summary>
public sealed class DiamondParallelogramTests
{
    private static readonly ITextMeasurer _m = InterMetricsMeasurer.Instance;
    private static readonly StyleGeometry _geo = BeckStyle.Classic.Geometry;
    private static readonly FontRoleTable _roles = BeckStyle.Classic.Typography.Roles;

    private static NodeModel Node(string shape, string title, string? subtitle = null) =>
        Validate.LoadDiagram(
            $"type: architecture\nnodes: [{{ id: a, title: \"{title}\", shape: {shape}"
            + $"{(subtitle != null ? $", subtitle: \"{subtitle}\"" : "")} }}]\nedges: []\n").Nodes[0];

    private static double TitleW(string s) => _m.Measure(s, FontRole.CardTitle, _roles.Of(FontRole.CardTitle)).Width;

    // ---- shape parsing ----

    [Fact]
    public void ShapeTokens_Parse_OnArchitectureNodes()
    {
        Assert.Equal(NodeShape.Diamond, Node("diamond", "D").Shape);
        Assert.Equal(NodeShape.Parallelogram, Node("parallelogram", "P").Shape);
        // Absent `shape:` still defaults to Card (existing documents unchanged).
        Assert.Equal(NodeShape.Card,
            Validate.LoadDiagram("type: architecture\nnodes: [{ id: a, title: A }]\nedges: []\n").Nodes[0].Shape);
    }

    // ---- diamond sizing: the inscribed-rect equation ----

    [Fact]
    public void Diamond_SizesAtTwiceThePaddedTextBlock()
    {
        var node = Node("diamond", "Decision");
        var size = CardSizer.Measure(node, _m, _geo);

        // width = 2·(widest single-line text + CardPadX + border), floored at CardMinW.
        var expectedW = Math.Max(_geo.CardMinW,
            Math.Ceiling(2 * (TitleW("Decision") + _geo.CardPadX + _geo.MeasureBorder)));
        Assert.Equal(Js.Round(expectedW), size.W);

        // height = 2·(wrapped text height + CardPadY + border).
        var avail = expectedW / 2 - _geo.CardPadX - _geo.MeasureBorder;
        var lines = CardSizer.WrapText(_m, "Decision", FontRole.CardTitle, avail, _roles).Count;
        var textH = lines * _geo.CardTitleLine;
        var expectedH = 2 * (textH + _geo.CardPadY + _geo.MeasureBorder);
        Assert.Equal(Js.Round(expectedH), size.H);
    }

    [Fact]
    public void Diamond_InscribedRect_HoldsTheTitleOnOneLine()
    {
        // The whole point of the 2× rule: a centered rect of w/2 (the avail the renderer wraps within)
        // is inscribed in the diamond and comfortably holds the widest single-line row.
        var node = Node("diamond", "Decision");
        var avail = CardSizer.DiamondTextAvail(node, _m, _geo, _roles);
        Assert.True(avail >= TitleW("Decision"), $"inscribed width {avail} must hold title {TitleW("Decision")}");
        Assert.Equal(new[] { "Decision" }, CardSizer.WrapText(_m, "Decision", FontRole.CardTitle, avail, _roles));
    }

    [Fact]
    public void Diamond_TextAvail_IsHalfTheWidthLessPadding()
    {
        var node = Node("diamond", "Decision", subtitle: "choose a branch");
        var size = CardSizer.Measure(node, _m, _geo);
        Assert.Equal(size.W / 2 - _geo.CardPadX - _geo.MeasureBorder,
            CardSizer.DiamondTextAvail(node, _m, _geo, _roles));
    }

    // ---- parallelogram sizing: card math + skew ----

    [Fact]
    public void Parallelogram_IsCardWidthPlusSkew()
    {
        var node = Node("parallelogram", "Read input");
        var size = CardSizer.Measure(node, _m, _geo);

        // Card auto-grow width (no icon block), clamped to [CardMinW, CardMaxW].
        var cardW = Math.Clamp(Math.Ceiling(TitleW("Read input")) + _geo.CardPadX + _geo.MeasureBorder,
            _geo.CardMinW, _geo.CardMaxW);
        var avail = cardW - _geo.CardPadX - _geo.MeasureBorder;
        var textH = CardSizer.WrapText(_m, "Read input", FontRole.CardTitle, avail, _roles).Count * _geo.CardTitleLine;
        var expectedH = Js.Round(textH + _geo.CardPadY + _geo.MeasureBorder);
        var skew = Artwork.ParallelogramSkew(expectedH);

        Assert.Equal(expectedH, size.H);
        Assert.Equal(Js.Round(cardW + skew), size.W);
        // The skew genuinely widened the box beyond the card text column.
        Assert.True(skew > 0);
        Assert.Equal(skew, Math.Min(12, size.H * 0.4));
        // The renderer's wrap width tracks the card text column (independent of the skew).
        Assert.Equal(avail, CardSizer.ParallelogramTextAvail(node, _m, _geo, _roles));
    }

    // ---- router: diamond anchors land exactly on the bbox face midpoints (vertices) ----

    [Fact]
    public void Diamond_FanIn_AnchorsLandExactlyOnFaceMidpoints_NotSpread()
    {
        const string yaml = """
            type: architecture
            meta: { direction: TB }
            nodes:
              - { id: a, title: Source A }
              - { id: b, title: Source B }
              - { id: c, title: Source C }
              - { id: d, title: Decision, shape: diamond }
            edges:
              - { from: a, to: d }
              - { from: b, to: d }
              - { from: c, to: d }
            """;

        var (_, layout, edges) = LineQuality.Route(yaml);
        var dia = layout.Nodes["d"];
        var topVertex = new Point(dia.X + dia.W / 2, dia.Y);

        // All three edges arrive on the diamond's TOP face; each must terminate EXACTLY on the top
        // vertex (the bbox top face midpoint), not fanned out to distinct anchor slots.
        var toDia = edges.Where(e => e.Edge.To == "d").ToList();
        Assert.Equal(3, toDia.Count);
        foreach (var e in toDia)
        {
            var end = e.Points[^1];
            Assert.True(Math.Abs(end.X - topVertex.X) < 0.01 && Math.Abs(end.Y - topVertex.Y) < 0.01,
                $"edge {e.Edge.Id} ends at ({end.X:0.###},{end.Y:0.###}), expected vertex ({topVertex.X:0.###},{topVertex.Y:0.###})");
        }
    }

    // ---- render smoke ----

    [Fact]
    public void Render_EmitsDiamondAndParallelogramPaths()
    {
        var diamondSvg = BeckSvg.Render(
            "type: architecture\nnodes: [{ id: a, title: Decision, shape: diamond }]\nedges: []\n");
        Assert.StartsWith("<svg", diamondSvg);
        Assert.Contains("beck-node beck-node--diamond", diamondSvg);
        // The plain diamond outline is a closed four-vertex path.
        Assert.Contains("<path class=\"beck-node beck-node--diamond\" d=\"M", diamondSvg);

        var paraSvg = BeckSvg.Render(
            "type: architecture\nnodes: [{ id: a, title: Read input, shape: parallelogram }]\nedges: []\n");
        Assert.StartsWith("<svg", paraSvg);
        Assert.Contains("beck-node beck-node--parallelogram", paraSvg);
    }
}
