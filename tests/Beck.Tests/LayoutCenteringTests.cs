using Beck.Rendering;
using Xunit;

namespace Beck.Tests;

/// <summary>
/// Coordinate assignment must be symmetric: a node shared by several neighbors sits at
/// their midpoint regardless of whether the fan is above or below it. Guards the
/// pool-adjacent-violators placement in <see cref="LayeredLayout"/> — the earlier
/// two-pass sweep anchored colliding blocks at the first node's desire, leaving a
/// shared sink flush under its first source.
/// </summary>
public sealed class LayoutCenteringTests
{
    private static IReadOnlyDictionary<string, Rect> Lay(string yaml)
    {
        DiagramModel model = Validate.LoadDiagram(yaml);
        var sizes = model.Nodes.ToDictionary(n => n.Id, _ => new Size(120, 60));
        return LayeredLayout.Compute(model, sizes).Nodes;
    }

    private static double Cx(Rect r) => r.X + r.W / 2;

    [Fact]
    public void TwoSources_OneSink_SinkCentered()
    {
        var n = Lay("""
            type: architecture
            meta: { direction: TB }
            nodes:
              - { id: a }
              - { id: b }
              - { id: c }
            edges:
              - { from: a, to: c }
              - { from: b, to: c }
            """);
        double mid = (Cx(n["a"]) + Cx(n["b"])) / 2;
        Assert.True(Math.Abs(Cx(n["c"]) - mid) < 1,
            $"c center {Cx(n["c"])} vs midpoint {mid} (a={Cx(n["a"])}, b={Cx(n["b"])})");
    }

    [Fact]
    public void OneSource_TwoSinks_SourceCentered()
    {
        var n = Lay("""
            type: architecture
            meta: { direction: TB }
            nodes:
              - { id: a }
              - { id: b }
              - { id: c }
            edges:
              - { from: a, to: c }
              - { from: a, to: b }
            """);
        double mid = (Cx(n["b"]) + Cx(n["c"])) / 2;
        Assert.True(Math.Abs(Cx(n["a"]) - mid) < 1,
            $"a center {Cx(n["a"])} vs midpoint {mid} (b={Cx(n["b"])}, c={Cx(n["c"])})");
    }
}
