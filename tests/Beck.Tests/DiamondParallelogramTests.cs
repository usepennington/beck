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

    // ---- flowchart decision: edges spread across distinct diamond vertices ----

    [Fact]
    public void Flowchart_DecisionEdges_UseDistinctVertices_NoSharedTrunk()
    {
        // A decision with two same-rank-below targets plus a return edge back into it. Point-anchoring
        // once landed all three on the bottom vertex, sharing a trunk that read as one line forking.
        // The flowchart reassignment must give them distinct vertices (yes → one side, no → the other,
        // return → the free vertex), so no two edges leave/enter the diamond on a shared collinear run.
        const string yaml = """
            type: flowchart
            meta: { direction: TB }
            steps:
              - { id: check, text: Valid?, kind: decision }
              - { id: process, text: Process }
              - { id: retry, text: Fix input }
            links:
              - { from: check, to: process, label: "yes" }
              - { from: check, to: retry, label: "no" }
              - { from: retry, to: check }
            """;

        var (_, layout, edges) = LineQuality.Route(yaml);
        var dia = layout.Nodes["check"];
        var cx = dia.X + dia.W / 2;
        var cy = dia.Y + dia.H / 2;

        // Each edge's anchor on the diamond (start if it leaves check, end if it enters).
        var anchors = edges
            .Where(e => e.Edge.From == "check" || e.Edge.To == "check")
            .Select(e => e.Edge.From == "check" ? e.Points[0] : e.Points[^1])
            .ToList();
        Assert.Equal(3, anchors.Count);

        // Three free vertices, three edges → three distinct anchor points (no shared vertex/trunk).
        var distinct = anchors.Select(p => (Math.Round(p.X, 1), Math.Round(p.Y, 1))).Distinct().Count();
        Assert.Equal(3, distinct);

        // Every anchor sits exactly on one of the four bbox face midpoints (a diamond vertex), and
        // the two out-branches land on opposite side vertices (Left/Right), not the same bottom point.
        bool IsVertex(Point p) =>
            (Near(p.X, cx) && (Near(p.Y, dia.Y) || Near(p.Y, dia.Y + dia.H)))
            || (Near(p.Y, cy) && (Near(p.X, dia.X) || Near(p.X, dia.X + dia.W)));
        Assert.All(anchors, p => Assert.True(IsVertex(p), $"anchor ({p.X:0.#},{p.Y:0.#}) is not a diamond vertex"));

        var yes = edges.Single(e => e.Edge.To == "process").Points[0];
        var no = edges.Single(e => e.Edge.To == "retry").Points[0];
        Assert.True(Near(yes.Y, cy) && Near(no.Y, cy), "both branches should leave a side (Left/Right) vertex");
        Assert.False(Near(yes.X, no.X), "yes- and no-branch must leave DIFFERENT side vertices, not one trunk");
    }

    private static bool Near(double a, double b) => Math.Abs(a - b) < 0.5;

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
