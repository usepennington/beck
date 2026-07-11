using Beck.Model;
using Xunit;

namespace Beck.Tests;

/// <summary>
/// Model-shape unit tests for <c>type: flowchart</c> (<see cref="FlowchartBuilder"/>) plus a
/// byte-identity determinism check over the two new corpus files, mirroring the invariant that
/// same YAML + same options always renders byte-identical SVG.
/// </summary>
public sealed class FlowchartTests
{
    private static readonly string _corpusDir = Path.Combine(AppContext.BaseDirectory, "Corpus");

    [Theory]
    [InlineData("process", "Card")]
    [InlineData("decision", "Diamond")]
    [InlineData("terminator", "Pill")]
    [InlineData("io", "Parallelogram")]
    [InlineData("start", "Start")]
    [InlineData("end", "End")]
    public void KindMapsToExpectedShape(string kind, string expectedShape)
    {
        var yaml = "type: flowchart\nsteps:\n  - { id: a, kind: " + kind + " }\nlinks: []\n";
        var model = Validate.LoadDiagram(yaml);
        Assert.Equal(expectedShape, model.Nodes.Single(n => n.Id == "a").Shape.ToString());
    }

    [Fact]
    public void PseudoStepReferencedFromLinks_MaterializesStartAndEnd()
    {
        const string yaml = """
            type: flowchart
            steps:
              - { id: work, text: Do work }
            links:
              - { from: "[*]", to: work }
              - { from: work, to: "[*]" }
            """;
        var model = Validate.LoadDiagram(yaml);
        Assert.Contains(model.Nodes, n => n.Id == "#start" && n.Shape == NodeShape.Start);
        Assert.Contains(model.Nodes, n => n.Id == "#end" && n.Shape == NodeShape.End);
        Assert.Equal(2, model.Edges.Count);
    }

    [Fact]
    public void PseudoStep_BindsToDeclaredStartAndEnd_WhenExactlyOne()
    {
        // A declared `kind: start` / `kind: end` step wired via "[*]" binds to that step rather than
        // spawning a floating #start/#end dot beside it.
        const string yaml = """
            type: flowchart
            steps:
              - { id: begin, kind: start }
              - { id: process, text: Process order }
              - { id: finish, kind: end }
            links:
              - { from: "[*]", to: process }
              - { from: process, to: "[*]" }
            """;
        var model = Validate.LoadDiagram(yaml);
        Assert.DoesNotContain(model.Nodes, n => n.Id is "#start" or "#end");
        Assert.Equal(3, model.Nodes.Count);
        Assert.Equal("begin", model.Edges[0].From);
        Assert.Equal("finish", model.Edges[1].To);
        Assert.Contains(model.Nodes, n => n.Id == "begin" && n.Shape == NodeShape.Start);
        Assert.Contains(model.Nodes, n => n.Id == "finish" && n.Shape == NodeShape.End);
    }

    [Fact]
    public void PseudoStep_FallsBackToPseudoNode_WhenNoDeclaredStartEnd()
    {
        // No declared start/end of the relevant kind → the anonymous pseudo-node is materialized.
        const string yaml = """
            type: flowchart
            steps:
              - { id: work, text: Do work }
            links:
              - { from: "[*]", to: work }
              - { from: work, to: "[*]" }
            """;
        var model = Validate.LoadDiagram(yaml);
        Assert.Contains(model.Nodes, n => n.Id == "#start" && n.Shape == NodeShape.Start);
        Assert.Contains(model.Nodes, n => n.Id == "#end" && n.Shape == NodeShape.End);
    }

    [Fact]
    public void PseudoStep_Throws_WhenMultipleDeclaredStarts()
    {
        const string yaml = """
            type: flowchart
            steps:
              - { id: begin1, kind: start }
              - { id: begin2, kind: start }
              - { id: work }
            links:
              - { from: "[*]", to: work }
            """;
        var ex = Assert.Throws<BeckYamlException>(() => Validate.LoadDiagram(yaml));
        Assert.Contains("ambiguous", ex.Message);
        Assert.Contains("start", ex.Message);
    }

    [Fact]
    public void LinkLabelPassesThroughToTheEdge()
    {
        const string yaml = """
            type: flowchart
            steps:
              - { id: a }
              - { id: b }
              - { id: c }
            links:
              - { from: a, to: b, label: "yes" }
              - { from: a, to: c, label: "no" }
            """;
        var model = Validate.LoadDiagram(yaml);
        Assert.Equal("yes", model.Edges.Single(e => e.To == "b").Label);
        Assert.Equal("no", model.Edges.Single(e => e.To == "c").Label);
    }

    [Fact]
    public void UnknownKind_Throws()
    {
        const string yaml = """
            type: flowchart
            steps:
              - { id: a, kind: bogus }
            links: []
            """;
        var ex = Assert.Throws<BeckYamlException>(() => Validate.LoadDiagram(yaml));
        Assert.Contains("kind", ex.Message);
    }

    [Theory]
    [InlineData("flowchart-simple")]
    [InlineData("flowchart-branchy")]
    public void Render_IsDeterministic_AcrossTwoRuns(string name)
    {
        var yaml = File.ReadAllText(Path.Combine(_corpusDir, name + ".yaml"));
        var first = BeckSvg.Render(yaml);
        var second = BeckSvg.Render(yaml);
        Assert.Equal(first, second);
    }
}
