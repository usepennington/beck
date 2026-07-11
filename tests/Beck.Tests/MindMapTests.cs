using Beck.Layout;
using Beck.Model;
using Beck.Text;
using Xunit;
using Xunit.Abstractions;

namespace Beck.Tests;

/// <summary>
/// Gates <c>type: mindmap</c>: the <see cref="MindMapBuilder"/> tree flattening (ids, ranks, orders,
/// accent cycling + inheritance, shape mapping, edge defaults) and the <see cref="MindMapLayout"/>
/// two-sided butterfly (root centred between disjoint left/right halves, no overlaps, no negative
/// coordinates), plus a render-determinism smoke check.
/// </summary>
public sealed class MindMapTests
{
    private readonly ITestOutputHelper _out;

    public MindMapTests(ITestOutputHelper output) => _out = output;

    private static (DiagramModel Model, LayoutResult Layout) Run(string yaml)
    {
        var model = Validate.LoadDiagram(yaml);
        var sizes = model.Nodes.ToDictionary(n => n.Id, n => CardSizer.Measure(n, InterMetricsMeasurer.Instance, mindMap: true));
        return (model, MindMapLayout.Compute(model, sizes));
    }

    // ---------------------------------------------------------------- model

    [Fact]
    public void TreeFlattening_AssignsIdsRanksAndOrders()
    {
        const string yaml = """
            type: mindmap
            root: Center
            topics:
              - title: A
                children:
                  - title: A1
                  - title: A2
              - title: B
            """;
        var model = Validate.LoadDiagram(yaml);

        // Path-based ids, deterministic and collision-free.
        Assert.Equal(new[] { "root", "root-0", "root-0-0", "root-0-1", "root-1" },
            model.Nodes.Select(n => n.Id).ToArray());

        // Rank = depth (root 0), Order = stable traversal index.
        Assert.Equal(0d, model.Nodes.Single(n => n.Id == "root").Rank);
        Assert.Equal(1d, model.Nodes.Single(n => n.Id == "root-0").Rank);
        Assert.Equal(2d, model.Nodes.Single(n => n.Id == "root-0-0").Rank);
        Assert.Equal(Enumerable.Range(0, 5).Select(i => (double)i).ToArray(),
            model.Nodes.Select(n => n.Order!.Value).ToArray());
    }

    [Fact]
    public void RootStringShorthand_IsTheTitle()
    {
        var model = Validate.LoadDiagram("type: mindmap\nroot: Beck\ntopics: []\n");
        var root = model.Nodes.Single();
        Assert.Equal("Beck", root.Title);
        Assert.Equal("root", root.Id);
        Assert.Equal(NodeShape.Card, root.Shape); // root is always a card
    }

    [Fact]
    public void AuthoredId_IsHonoured()
    {
        var model = Validate.LoadDiagram("type: mindmap\nroot: { id: hub, title: Hub }\ntopics: [{ id: r, title: R }]\n");
        Assert.Contains(model.Nodes, n => n.Id == "hub");
        Assert.Contains(model.Nodes, n => n.Id == "r");
    }

    [Fact]
    public void AccentCycling_InheritanceAndExplicitOverride()
    {
        const string yaml = """
            type: mindmap
            root: Center
            topics:
              - title: A
                children:
                  - title: A1
              - title: B
                accent: danger
                children:
                  - title: B1
            """;
        var model = Validate.LoadDiagram(yaml);
        NodeModel N(string id) => model.Nodes.Single(n => n.Id == id);

        Assert.Equal("var(--beck-primary)", N("root").Accent);   // root → primary
        Assert.Equal("var(--beck-info)", N("root-0").Accent);    // branch 0 → cycle[0] = info
        Assert.Equal("var(--beck-info)", N("root-0-0").Accent);  // inherits A
        Assert.Equal("var(--beck-danger)", N("root-1").Accent);  // explicit override
        Assert.Equal("var(--beck-danger)", N("root-1-0").Accent); // override flows to child
    }

    [Fact]
    public void AccentCycle_WrapsPastFiveBranches()
    {
        var topics = string.Join("\n", Enumerable.Range(0, 7).Select(i => $"  - title: T{i}"));
        var model = Validate.LoadDiagram($"type: mindmap\nroot: C\ntopics:\n{topics}\n");
        // cycle = [info, primary, success, warn, danger] (neutral reserved for ghosts); branch 5 wraps to info.
        Assert.Equal("var(--beck-info)", model.Nodes.Single(n => n.Id == "root-0").Accent);    // branch 0 → info
        Assert.Equal("var(--beck-primary)", model.Nodes.Single(n => n.Id == "root-1").Accent); // branch 1 → primary
        Assert.Equal("var(--beck-danger)", model.Nodes.Single(n => n.Id == "root-4").Accent);  // branch 4 → danger
        Assert.Equal("var(--beck-info)", model.Nodes.Single(n => n.Id == "root-5").Accent);    // wraps → info
        Assert.Equal("var(--beck-primary)", model.Nodes.Single(n => n.Id == "root-6").Accent); // → primary
    }

    [Fact]
    public void ItemsAndBody_PassThrough()
    {
        const string yaml = """
            type: mindmap
            root: C
            topics:
              - title: Pipeline
                items: [Model, Text, Layout]
              - title: Determinism
                body: Same YAML, same SVG.
            """;
        var model = Validate.LoadDiagram(yaml);
        Assert.Equal(new[] { "Model", "Text", "Layout" }, model.Nodes.Single(n => n.Id == "root-0").Items);
        Assert.Equal("Same YAML, same SVG.", model.Nodes.Single(n => n.Id == "root-1").Body);
    }

    [Fact]
    public void ShapeMapping_LeafPillHubAndContentCard()
    {
        const string yaml = """
            type: mindmap
            root: R
            topics:
              - title: Leaf
              - title: Hub
                children:
                  - title: Deep
              - title: Content
                items: [x, y]
            """;
        var model = Validate.LoadDiagram(yaml);
        Assert.Equal(NodeShape.Card, model.Nodes.Single(n => n.Id == "root").Shape);       // root
        Assert.Equal(NodeShape.Card, model.Nodes.Single(n => n.Id == "root-0").Shape);     // rank-1 is always a card
        Assert.Equal(NodeShape.Card, model.Nodes.Single(n => n.Id == "root-1").Shape);     // rank-1 hub
        Assert.Equal(NodeShape.Pill, model.Nodes.Single(n => n.Id == "root-1-0").Shape);   // rank-2 heading-only leaf → pill
        Assert.Equal(NodeShape.Card, model.Nodes.Single(n => n.Id == "root-2").Shape);     // rank-1 content
    }

    [Fact]
    public void Edges_AreParentToChild_SCurve_NoArrow_ColouredByChild()
    {
        var model = Validate.LoadDiagram("type: mindmap\nroot: C\ntopics: [{ title: A, accent: warn }]\n");
        var e = model.Edges.Single();
        Assert.Equal("root", e.From);
        Assert.Equal("root-0", e.To);
        Assert.Equal(EdgeCurve.S, e.Curve);
        Assert.Equal(ArrowEnds.None, e.Arrow);
        Assert.Equal(EdgeStyle.Solid, e.Style);
        // A muted branch thread: the child's accent blended 55% into the edge token.
        Assert.Equal("color-mix(in srgb, var(--beck-warn) 55%, var(--beck-edge))", e.Color);
    }

    [Fact]
    public void MissingTitle_DefaultsToId()
    {
        var model = Validate.LoadDiagram("type: mindmap\nroot: { id: hub }\ntopics: []\n");
        Assert.Equal("hub", model.Nodes.Single().Title);
    }

    [Fact]
    public void DuplicateId_Throws()
    {
        var ex = Assert.Throws<BeckYamlException>(() =>
            Validate.LoadDiagram("type: mindmap\nroot: { id: dup }\ntopics: [{ id: dup, title: X }]\n"));
        Assert.Contains("Duplicate", ex.Message);
    }

    [Fact]
    public void MissingRoot_Throws()
    {
        var ex = Assert.Throws<BeckYamlException>(() => Validate.LoadDiagram("type: mindmap\ntopics: []\n"));
        Assert.Contains("root", ex.Message);
    }

    // ---------------------------------------------------------------- butterfly layout

    private const string FourBranch = """
        type: mindmap
        root: Root
        topics:
          - title: North
            children: [{ title: N1 }, { title: N2 }]
          - title: East
            children: [{ title: E1 }, { title: E2 }]
          - title: South
            children: [{ title: S1 }, { title: S2 }]
          - title: West
            children: [{ title: W1 }, { title: W2 }]
        """;

    [Fact]
    public void Butterfly_RootCentredBetweenDisjointHalves_NoOverlaps_NoNegatives()
    {
        var (_, layout) = Run(FourBranch);
        var root = layout.Nodes["root"];
        var rootCx = root.X + root.W / 2;

        var others = layout.Nodes.Where(kv => kv.Key != "root").ToList();
        var leftNodes = others.Where(kv => kv.Value.X + kv.Value.W / 2 < rootCx).Select(kv => kv.Value).ToList();
        var rightNodes = others.Where(kv => kv.Value.X + kv.Value.W / 2 >= rootCx).Select(kv => kv.Value).ToList();

        Assert.NotEmpty(leftNodes);
        Assert.NotEmpty(rightNodes);

        // Every left-half rect is entirely left of the root's left edge; every right-half rect is
        // entirely right of the root's right edge — the halves are disjoint and the root sits between.
        foreach (var r in leftNodes)
        {
            Assert.True(r.X + r.W <= root.X + 0.01, $"left node right edge {r.X + r.W} must be ≤ root left {root.X}");
        }

        foreach (var r in rightNodes)
        {
            Assert.True(r.X >= root.X + root.W - 0.01, $"right node left edge {r.X} must be ≥ root right {root.X + root.W}");
        }

        // No pairwise overlap.
        var all = layout.Nodes.Values.ToList();
        for (var i = 0; i < all.Count; i++)
        {
            for (var j = i + 1; j < all.Count; j++)
            {
                Assert.False(Overlap(all[i], all[j]), $"rects {i} and {j} overlap");
            }
        }

        // No negative coordinates.
        foreach (var r in all)
        {
            Assert.True(r.X >= 0 && r.Y >= 0, $"rect ({r.X},{r.Y}) is off-canvas");
        }

        // Total canvas fits every rect plus padding.
        Assert.True(layout.Width >= all.Max(r => r.X + r.W));
        Assert.True(layout.Height >= all.Max(r => r.Y + r.H));
    }

    [Fact]
    public void Butterfly_SingleBranch_GoesRight()
    {
        var (_, layout) = Run("type: mindmap\nroot: R\ntopics: [{ title: Only }]\n");
        var root = layout.Nodes["root"];
        var branch = layout.Nodes["root-0"];
        Assert.True(branch.X >= root.X + root.W - 0.01, "a single branch lands on the right side");
    }

    [Fact]
    public void Butterfly_ReportsExampleLayoutNumbers()
    {
        var (model, layout) = Run(FourBranch);
        var root = layout.Nodes["root"];
        var rootCx = root.X + root.W / 2;
        _out.WriteLine($"canvas = {layout.Width} x {layout.Height}");
        _out.WriteLine($"root rect = ({root.X:0.##},{root.Y:0.##}) {root.W:0.##}x{root.H:0.##}  centreX = {rootCx:0.##}");
        foreach (var n in model.Nodes)
        {
            var r = layout.Nodes[n.Id];
            var side = n.Id == "root" ? "root" : (r.X + r.W / 2 < rootCx ? "L" : "R");
            _out.WriteLine($"  {n.Id,-10} {side,-4} ({r.X,7:0.##},{r.Y,7:0.##}) {r.W:0.##}x{r.H:0.##}");
        }
    }

    // ---------------------------------------------------------------- render smoke

    [Fact]
    public void Render_IsDeterministic_AndEmitsSCurves()
    {
        const string yaml = """
            type: mindmap
            meta: { title: Beck }
            root:
              title: Beck
            topics:
              - title: Rendering
                accent: info
                children:
                  - title: Pipeline
                    items: [Model, Text, Layout]
                  - title: Determinism
                    body: Same YAML, same SVG.
              - title: Packages
                children:
                  - title: Beck
                  - title: Beck.Skia
            """;
        var first = BeckSvg.Render(yaml);
        var second = BeckSvg.Render(yaml);
        Assert.Equal(first, second);
        Assert.StartsWith("<svg", first);
        // S-curve edges emit cubic Bézier ("C") path commands.
        Assert.Contains(" C ", first);

        // No off-canvas edge routes: no path `d` carries a negative coordinate (the signature of a
        // routing regression).
        foreach (System.Text.RegularExpressions.Match m in
                 System.Text.RegularExpressions.Regex.Matches(first, "\\sd=\"([^\"]*)\""))
        {
            Assert.DoesNotContain("-", m.Groups[1].Value);
        }
    }

    // ---------------------------------------------------------------- helpers

    private static bool Overlap(Rect a, Rect b)
    {
        const double eps = 0.01;
        return a.X < b.X + b.W - eps && b.X < a.X + a.W - eps
            && a.Y < b.Y + b.H - eps && b.Y < a.Y + a.H - eps;
    }
}
