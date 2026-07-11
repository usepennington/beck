using Beck.Authoring;
using Beck.Model;
using Xunit;
using AccentToken = Beck.Authoring.AccentToken;

namespace Beck.Tests;

/// <summary>
/// The authoring side of the <c>type: mindmap</c> schema contract
/// (<see cref="MindMapDiagramBuilder"/>): fluent-built YAML must parse via
/// <see cref="Validate.LoadDiagram"/> into the same model shapes the hand-written
/// YAML in <see cref="MindMapTests"/> exercises — nested topics emit as flow-style
/// YAML (still plain YAML, just single-line), so nesting depth must round-trip
/// exactly like the block-style schema examples do.
/// </summary>
public sealed class MindMapBuilderTests
{
    [Fact]
    public void RootAndThreeLevelsOfNesting_RoundTrip()
    {
        var yaml = new MindMapDiagramBuilder("Beck")
            .Root("Beck")
            .Topic("Rendering", t => t
                .Accent(AccentToken.Info)
                .Topic("Pipeline", p => p
                    .Items("Model", "Text", "Layout")
                    .Topic("Model", m => m.Body("YAML → DiagramModel"))))
            .Topic("Packages", t => t
                .Topic("Beck")
                .Topic("Beck.Skia"))
            .ToYaml();

        var model = Validate.LoadDiagram(yaml);

        Assert.Equal(new[] { "root", "root-0", "root-0-0", "root-0-0-0", "root-1", "root-1-0", "root-1-1" },
            model.Nodes.Select(n => n.Id).ToArray());

        Assert.Equal("Beck", model.Nodes.Single(n => n.Id == "root").Title);
        Assert.Equal("Rendering", model.Nodes.Single(n => n.Id == "root-0").Title);
        Assert.Equal("Pipeline", model.Nodes.Single(n => n.Id == "root-0-0").Title);
        Assert.Equal("Model", model.Nodes.Single(n => n.Id == "root-0-0-0").Title);

        // Ranks = depth.
        Assert.Equal(0d, model.Nodes.Single(n => n.Id == "root").Rank);
        Assert.Equal(1d, model.Nodes.Single(n => n.Id == "root-0").Rank);
        Assert.Equal(2d, model.Nodes.Single(n => n.Id == "root-0-0").Rank);
        Assert.Equal(3d, model.Nodes.Single(n => n.Id == "root-0-0-0").Rank);

        // Items/body pass through.
        Assert.Equal(new[] { "Model", "Text", "Layout" }, model.Nodes.Single(n => n.Id == "root-0-0").Items);
        Assert.Equal("YAML → DiagramModel", model.Nodes.Single(n => n.Id == "root-0-0-0").Body);

        // Accent set on "Rendering" flows down to its descendants.
        Assert.Equal("var(--beck-info)", model.Nodes.Single(n => n.Id == "root-0").Accent);
        Assert.Equal("var(--beck-info)", model.Nodes.Single(n => n.Id == "root-0-0").Accent);
        Assert.Equal("var(--beck-info)", model.Nodes.Single(n => n.Id == "root-0-0-0").Accent);

        // Edges: parent → child, S-curve, no arrow.
        Assert.Equal(6, model.Edges.Count);
        Assert.All(model.Edges, e =>
        {
            Assert.Equal(Beck.Model.EdgeCurve.S, e.Curve);
            Assert.Equal(Beck.Model.ArrowEnds.None, e.Arrow);
        });
    }

    [Fact]
    public void ExplicitAccentOverride_ResetsInheritanceForItsSubtree()
    {
        var yaml = new MindMapDiagramBuilder()
            .Root("C")
            .Topic("A", a => a.Topic("A1"))
            .Topic("B", b => b
                .Accent(AccentToken.Danger)
                .Topic("B1", b1 => b1.Accent("#00ff00").Topic("B1a"))
                .Topic("B2"))
            .ToYaml();

        var model = Validate.LoadDiagram(yaml);
        NodeModel N(string id) => model.Nodes.Single(n => n.Id == id);

        Assert.Equal("var(--beck-primary)", N("root").Accent);
        Assert.Equal("var(--beck-danger)", N("root-1").Accent);   // explicit override on B
        Assert.Equal("var(--beck-danger)", N("root-1-1").Accent); // B2 inherits B's override
        Assert.Equal("#00ff00", N("root-1-0").Accent);            // B1 explicit raw color
        Assert.Equal("#00ff00", N("root-1-0-0").Accent);          // B1a inherits B1's raw color
    }

    [Fact]
    public void ItemsBodyIdSubtitle_PassThrough()
    {
        var yaml = new MindMapDiagramBuilder()
            .Root("R")
            .Topic("Pipeline", t => t.Id("pipe").Subtitle("core stages").Items("Model", "Text"))
            .Topic("Notes", t => t.Body("A wrapped paragraph."))
            .ToYaml();

        var model = Validate.LoadDiagram(yaml);
        var pipeline = model.Nodes.Single(n => n.Id == "pipe");
        Assert.Equal("Pipeline", pipeline.Title);
        Assert.Equal("core stages", pipeline.Subtitle);
        Assert.Equal(new[] { "Model", "Text" }, pipeline.Items);

        Assert.Equal("A wrapped paragraph.", model.Nodes.Single(n => n.Id == "root-1").Body);
    }

    [Fact]
    public void MetaTitleAndStyle_Emit()
    {
        var yaml = new MindMapDiagramBuilder("My Mindmap")
            .Style("classic")
            .Root("Center")
            .Topic("A")
            .ToYaml();

        Assert.Contains("type: mindmap", yaml);
        Assert.Contains("title: My Mindmap", yaml);
        Assert.Contains("style: classic", yaml);

        var model = Validate.LoadDiagram(yaml);
        Assert.Equal("My Mindmap", model.Meta.Title);
    }

    [Fact]
    public void DeepNesting_FourLevels_IndentsAndParsesCorrectly()
    {
        var yaml = new MindMapDiagramBuilder()
            .Root("L0")
            .Topic("L1", l1 => l1
                .Topic("L2", l2 => l2
                    .Topic("L3", l3 => l3
                        .Topic("L4"))))
            .ToYaml();

        var model = Validate.LoadDiagram(yaml);
        Assert.Equal(new[] { "root", "root-0", "root-0-0", "root-0-0-0", "root-0-0-0-0" },
            model.Nodes.Select(n => n.Id).ToArray());
        Assert.Equal("L4", model.Nodes.Single(n => n.Id == "root-0-0-0-0").Title);
        Assert.Equal(4d, model.Nodes.Single(n => n.Id == "root-0-0-0-0").Rank);

        // Depth roles: root and rank 1 are always cards; a rank 2+ heading is a pill even with children.
        Assert.Equal(NodeShape.Pill, model.Nodes.Single(n => n.Id == "root-0-0-0-0").Shape); // L4 leaf
        Assert.Equal(NodeShape.Pill, model.Nodes.Single(n => n.Id == "root-0-0-0").Shape);   // L3 hub (rank 3)
        Assert.Equal(NodeShape.Card, model.Nodes.Single(n => n.Id == "root-0").Shape);       // L1 (rank 1)
    }

    [Fact]
    public void SpecialCharactersInTitle_QuoteAndEscapeThroughNesting()
    {
        var yaml = new MindMapDiagramBuilder()
            .Root("Beck: \"Mermaid, but sexy\"")
            .Topic("Say \"hi\": greetings", t => t
                .Topic("Nested: colon, comma \"quote\""))
            .ToYaml();

        var model = Validate.LoadDiagram(yaml);
        Assert.Equal("Beck: \"Mermaid, but sexy\"", model.Nodes.Single(n => n.Id == "root").Title);
        Assert.Equal("Say \"hi\": greetings", model.Nodes.Single(n => n.Id == "root-0").Title);
        Assert.Equal("Nested: colon, comma \"quote\"", model.Nodes.Single(n => n.Id == "root-0-0").Title);
    }

    [Fact]
    public void ScriptedFlow_Emits()
    {
        var yaml = new MindMapDiagramBuilder()
            .Root("R")
            .Topic("A")
            .Flow(f => f.Packet("root", "root-0", label: "go"))
            .ToYaml();

        Assert.Contains("flow:", yaml);
        Assert.Contains("packet:", yaml);
        Validate.LoadDiagram(yaml);
    }

    [Fact]
    public void MissingRoot_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => new MindMapDiagramBuilder().Topic("A").ToYaml());
    }

    [Fact]
    public void ToFence_WrapsInBeckCodeBlock()
    {
        var fence = new MindMapDiagramBuilder()
            .Root("R")
            .Topic("A")
            .ToFence();

        Assert.StartsWith("```beck\n", fence);
        Assert.EndsWith("```\n", fence);
    }
}
