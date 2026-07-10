using Beck.Layout;
using Beck.Model;
using Beck.Rendering;
using Beck.Rendering.Text;
using Beck.Text;
using Xunit;

namespace Beck.Tests;

/// <summary>
/// Gates the flow-status term of <see cref="CardSizer"/>'s box-model math: a card whose flow swaps
/// status pills through it is pre-sized for the row (height) and the widest pill (width + chip
/// padding), since compiled CSS can't grow the box the way the live-DOM engine could when a status
/// landed on a status-less node. Companion to <see cref="CardSizeParityTests"/> (the browser-parity
/// golden, which predates flow statuses).
/// </summary>
public sealed class CardFlowStatusSizingTests
{
    private static readonly ITextMeasurer M = InterMetricsMeasurer.Instance;
    private static readonly StyleGeometry Geo = BeckStyle.Classic.Geometry;

    private static NodeModel Node(string title, string? status = null) =>
        Validate.LoadDiagram(
            $"type: architecture\nnodes: [{{ id: a, title: \"{title}\"{(status != null ? $", status: \"{status}\"" : "")} }}]\nedges: []\n"
        ).Nodes[0];

    [Fact]
    public void WidePill_GrowsCardWidth_ToPillPlusChipPadding()
    {
        NodeModel node = Node("API");
        const string pill = "PROCESSING PAYMENT GATEWAY RETRY";
        Size plain = CardSizer.Measure(node, M, Geo);
        Size sized = CardSizer.Measure(node, M, Geo, flowStatuses: new[] { pill });

        Assert.True(sized.W > plain.W, $"wide pill must grow the card ({plain.W} → {sized.W})");
        // The exact box-model term: natural = ceil(widest of title/subtitle/pill+16) + chrome,
        // clamped to [CardMinW, CardMaxW] — with a short title the pill row wins. Chrome (pad +
        // border + icon block) is witnessed via CardTextAvail so the default kind icon is included.
        double pillW = M.Measure(pill, FontRole.Status, BeckStyle.Classic.Typography.Roles.Of(FontRole.Status)).Width + 16;
        double chrome = plain.W - CardSizer.CardTextAvail(node, M, Geo);
        Assert.Equal(Math.Clamp(Math.Ceiling(pillW) + chrome, Geo.CardMinW, Geo.CardMaxW), sized.W);
    }

    [Fact]
    public void FlowStatuses_ReserveTheSameRowAsAnAuthoredStatus()
    {
        // The reserved status row must be exactly the one an authored `status:` gets — a flow that
        // swaps a pill into a status-less card yields the same box as authoring that pill up front.
        Assert.Equal(
            CardSizer.Measure(Node("API", status: "ok"), M, Geo),
            CardSizer.Measure(Node("API"), M, Geo, flowStatuses: new[] { "ok" }));
    }

    [Fact]
    public void AuthoredStatus_AlreadyReservesTheRow_FlowStatusesAddNothing()
    {
        // A node with an authored `status:` already carries the status row; a flow pill narrower
        // than the title must leave the box byte-identical.
        NodeModel node = Node("A Fairly Long Gateway Title", status: "Ready");
        Assert.Equal(
            CardSizer.Measure(node, M, Geo),
            CardSizer.Measure(node, M, Geo, flowStatuses: new[] { "ok" }));
    }

    [Fact]
    public void CardTextAvail_TracksTheFlowStatusWidth()
    {
        // The painter wraps text into CardTextAvail; it must move in lockstep with Measure's width
        // (same chrome), or drawn text would wrap differently than the box was sized for.
        NodeModel node = Node("API");
        const string pill = "PROCESSING PAYMENT GATEWAY RETRY";
        double plainChrome = CardSizer.Measure(node, M, Geo).W - CardSizer.CardTextAvail(node, M, Geo);
        double sizedChrome = CardSizer.Measure(node, M, Geo, flowStatuses: new[] { pill }).W
            - CardSizer.CardTextAvail(node, M, Geo, flowStatuses: new[] { pill });
        Assert.Equal(plainChrome, sizedChrome);
    }
}
