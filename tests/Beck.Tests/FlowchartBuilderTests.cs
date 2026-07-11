using Beck.Authoring;
using Beck.Model;
using Xunit;
using AccentToken = Beck.Authoring.AccentToken;
using Direction = Beck.Authoring.Direction;
using FitMode = Beck.Authoring.FitMode;

namespace Beck.Tests;

/// <summary>
/// The authoring side of the <c>type: flowchart</c> schema contract
/// (<see cref="FlowchartDiagramBuilder"/>): fluent-built YAML must parse via
/// <see cref="Validate.LoadDiagram"/> into the same model shapes/edge props the
/// hand-written YAML in <see cref="FlowchartTests"/> exercises.
/// </summary>
public sealed class FlowchartBuilderTests
{
    [Fact]
    public void AllStepKinds_RoundTripToExpectedShapes()
    {
        var yaml = new FlowchartDiagramBuilder("Kinds")
            .Process("p", "Process step")
            .Decision("d", "Decision step")
            .Terminator("t", "Terminator step")
            .Io("i", "IO step")
            .Start("begin")
            .End("finish")
            .Link("begin", "p")
            .Link("p", "d")
            .Link("d", "t", "yes")
            .Link("d", "i", "no")
            .Link("t", "finish")
            .Link("i", "finish")
            .ToYaml();

        var model = Validate.LoadDiagram(yaml);

        Assert.Equal(NodeShape.Card, model.Nodes.Single(n => n.Id == "p").Shape);
        Assert.Equal(NodeShape.Diamond, model.Nodes.Single(n => n.Id == "d").Shape);
        Assert.Equal(NodeShape.Pill, model.Nodes.Single(n => n.Id == "t").Shape);
        Assert.Equal(NodeShape.Parallelogram, model.Nodes.Single(n => n.Id == "i").Shape);
        Assert.Equal(NodeShape.Start, model.Nodes.Single(n => n.Id == "begin").Shape);
        Assert.Equal(NodeShape.End, model.Nodes.Single(n => n.Id == "finish").Shape);
    }

    [Fact]
    public void DecisionAndIoSteps_RoundTripToDiamondAndParallelogram()
    {
        var yaml = new FlowchartDiagramBuilder()
            .Decision("check", "Valid?")
            .Io("read", "Read input")
            .Link("read", "check")
            .ToYaml();

        var model = Validate.LoadDiagram(yaml);
        Assert.Equal(NodeShape.Diamond, model.Nodes.Single(n => n.Id == "check").Shape);
        Assert.Equal(NodeShape.Parallelogram, model.Nodes.Single(n => n.Id == "read").Shape);
    }

    [Fact]
    public void LinkLabel_PassesThroughToTheEdge()
    {
        var yaml = new FlowchartDiagramBuilder()
            .Decision("check", "Valid?")
            .Process("ok", "Continue")
            .Process("bad", "Reject")
            .Link("check", "ok", "yes")
            .Link("check", "bad", "no")
            .ToYaml();

        var model = Validate.LoadDiagram(yaml);
        Assert.Equal("yes", model.Edges.Single(e => e.To == "ok").Label);
        Assert.Equal("no", model.Edges.Single(e => e.To == "bad").Label);
    }

    [Fact]
    public void UndeclaredStepReferencedByLink_MaterializesAsProcessCard()
    {
        var yaml = new FlowchartDiagramBuilder()
            .Process("a", "A")
            .Link("a", "b")
            .ToYaml();

        var model = Validate.LoadDiagram(yaml);
        Assert.Equal(NodeShape.Card, model.Nodes.Single(n => n.Id == "b").Shape);
    }

    [Fact]
    public void PseudoStepReferencedFromLinks_MaterializesStartAndEnd()
    {
        var yaml = new FlowchartDiagramBuilder()
            .Process("work", "Do work")
            .Link(FlowchartDiagramBuilder.Pseudo, "work")
            .Link("work", FlowchartDiagramBuilder.Pseudo)
            .ToYaml();

        var model = Validate.LoadDiagram(yaml);
        Assert.Contains(model.Nodes, n => n.Id == "#start" && n.Shape == NodeShape.Start);
        Assert.Contains(model.Nodes, n => n.Id == "#end" && n.Shape == NodeShape.End);
        Assert.Equal(2, model.Edges.Count);
    }

    [Fact]
    public void StepBuilder_SetsSubtitleAccentAndPosition()
    {
        var yaml = new FlowchartDiagramBuilder()
            .Step("a", s => s
                .Text("Step A")
                .Kind(StepKind.Process)
                .Subtitle("a subtitle")
                .Accent(AccentToken.Success)
                .Width(180)
                .Rank(1)
                .Order(2))
            .Link("a", "b")
            .ToYaml();

        Assert.Contains("subtitle: a subtitle", yaml);
        Assert.Contains("accent:", yaml);
        Assert.Contains("width: 180", yaml);
        Assert.Contains("rank: 1", yaml);
        Assert.Contains("order: 2", yaml);

        var model = Validate.LoadDiagram(yaml);
        var node = model.Nodes.Single(n => n.Id == "a");
        Assert.Equal("Step A", node.Title);
        Assert.Equal("a subtitle", node.Subtitle);
    }

    [Fact]
    public void LinkBuilder_SetsStyleColorAndNote()
    {
        var yaml = new FlowchartDiagramBuilder()
            .Process("a")
            .Process("b")
            .Link("a", "b", configure: l => l
                .Label("go")
                .Style(Beck.Authoring.EdgeStyle.Dashed)
                .Color(AccentToken.Danger)
                .Note("narration text"))
            .ToYaml();

        var model = Validate.LoadDiagram(yaml);
        var edge = model.Edges.Single();
        Assert.Equal("go", edge.Label);
        Assert.Equal(Beck.Model.EdgeStyle.Dashed, edge.Style);
    }

    [Fact]
    public void MetaOptions_EmitAndRoundTrip()
    {
        var yaml = new FlowchartDiagramBuilder("My Flowchart")
            .Subtitle("a subtitle")
            .Direction(Direction.Lr)
            .Theme(Beck.Authoring.ThemeMode.Dark)
            .Animate(false)
            .Loop(false)
            .Fit(FitMode.Scroll)
            .Process("a")
            .Process("b")
            .Link("a", "b")
            .ToYaml();

        Assert.Contains("type: flowchart", yaml);
        Assert.Contains("title: My Flowchart", yaml);
        Assert.Contains("direction: LR", yaml);

        var model = Validate.LoadDiagram(yaml);
        Assert.Equal("My Flowchart", model.Meta.Title);
        Assert.Equal("a subtitle", model.Meta.Subtitle);
    }

    [Fact]
    public void ScriptedFlow_Emits()
    {
        var yaml = new FlowchartDiagramBuilder()
            .Process("a")
            .Process("b")
            .Link("a", "b")
            .Flow(f => f.Packet("a", "b", label: "go"))
            .ToYaml();

        Assert.Contains("flow:", yaml);
        Assert.Contains("packet:", yaml);

        // Flow parses cleanly against the materialized step ids.
        Validate.LoadDiagram(yaml);
    }

    [Fact]
    public void Empty_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => new FlowchartDiagramBuilder().ToYaml());
    }

    [Fact]
    public void ToFence_WrapsInBeckCodeBlock()
    {
        var fence = new FlowchartDiagramBuilder()
            .Process("a")
            .Process("b")
            .Link("a", "b")
            .ToFence();

        Assert.StartsWith("```beck\n", fence);
        Assert.EndsWith("```\n", fence);
    }
}
