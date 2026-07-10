using Beck.Rendering;
using Beck.Rendering.Route;
using Xunit;

namespace Beck.Tests;

/// <summary>
/// Against-flow edges divert to the gutter face only when their direct route is
/// actually blocked. A child rank-pinned behind its parents (blog | interfaces | doc)
/// has a clear corridor, so its flipped implements edges must enter the parents'
/// near faces instead of looping over the canvas through the parent column.
/// </summary>
public sealed class BackEdgeRoutingTests
{
    [Fact]
    public void RankPinnedChild_BehindParents_RoutesDirectly()
    {
        string yaml = """
            type: class
            meta: { direction: LR }
            classes:
              - { id: p1, name: IOne, stereotype: interface, rank: 1 }
              - { id: p2, name: ITwo, stereotype: interface, rank: 1 }
              - { id: p3, name: IThree, stereotype: interface, rank: 1 }
              - { id: left, name: LeftChild, rank: 0 }
              - { id: right, name: RightChild, rank: 2 }
            relations:
              - { from: left, to: p1, kind: implements }
              - { from: left, to: p2, kind: implements }
              - { from: left, to: p3, kind: implements }
              - { from: right, to: p1, kind: implements }
              - { from: right, to: p2, kind: implements }
              - { from: right, to: p3, kind: implements }
            """;
        DiagramModel model = Validate.LoadDiagram(yaml);
        var sizes = model.Nodes.ToDictionary(n => n.Id, _ => new Size(140, 60));
        LayoutResult layout = LayeredLayout.Compute(model, sizes);
        var routes = EdgePainter.RouteEdges(model, layout);

        // Implements edges are flipped parent->child, so the left child's edges run
        // against the LR flow. Direct routing = the path starts on the parent's left
        // face (x == parent rect left) and never rises above the topmost node.
        double topY = layout.Nodes.Values.Min(r => r.Y);
        foreach (var route in routes.Where(r => r.Edge.To == "left"))
        {
            Rect parent = layout.Nodes[route.Edge.From];
            Assert.Equal(parent.X, route.Points[0].X, 3);
            Assert.All(route.Points, p => Assert.True(p.Y >= topY, $"{route.Edge.Id} detours above the canvas (y={p.Y})"));
        }
    }
}
