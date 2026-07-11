using Beck.Layout;
using Beck.Model;
using Beck.Svg;
using Beck.Text;
using Xunit;

namespace Beck.Tests;

/// <summary>
/// Gates the <c>items:</c> (bulleted list) and <c>body:</c> (wrapped paragraph) terms of
/// <see cref="CardSizer"/>'s card box-model math. Each item is one un-wrapped row prefixed with
/// <see cref="CardSizer.ItemBullet"/> (measured == drawn); the body wraps at the card's text-column
/// width exactly as the renderer draws it. A card without either measures byte-identically to before
/// (the blocks are inert when absent). Companion to <see cref="CardFlowStatusSizingTests"/> and the
/// browser-parity <see cref="CardSizeParityTests"/>.
/// </summary>
public sealed class CardItemsBodySizingTests
{
    private static readonly ITextMeasurer _m = InterMetricsMeasurer.Instance;
    private static readonly StyleGeometry _geo = BeckStyle.Classic.Geometry;
    private static readonly FontRoleTable _roles = BeckStyle.Classic.Typography.Roles;

    private static double W(string t, FontRole role) => _m.Measure(t, role, _roles.Of(role)).Width;

    private static double Round(double n) => Math.Floor(n + 0.5); // JS Math.round, matching Js.Round.

    private static NodeModel Node(string title, string[]? items = null, string? body = null, string? subtitle = null)
    {
        var seq = items is { Length: > 0 }
            ? ", items: [" + string.Join(", ", items.Select(i => $"\"{i}\"")) + "]"
            : "";
        var bod = body != null ? $", body: \"{body}\"" : "";
        var sub = subtitle != null ? $", subtitle: \"{subtitle}\"" : "";
        return Validate.LoadDiagram(
            $"type: architecture\nnodes: [{{ id: a, title: \"{title}\"{sub}{seq}{bod} }}]\nedges: []\n"
        ).Nodes[0];
    }

    /// <summary>An independent oracle for <see cref="CardSizer.Card"/>'s box model, including the new
    /// items/body terms. Reduces to the pre-existing formula when both are absent.</summary>
    private static Size Expected(NodeModel node)
    {
        var g = _geo;
        var hasIcon = Icons.ResolveIcon(node.Icon) != null;
        var iconBlock = hasIcon ? g.IconW + g.IconGap : 0;
        var chrome = g.CardPadX + g.MeasureBorder + iconBlock;

        var titleW = W(node.Title, FontRole.CardTitle);
        var subW = node.Subtitle != null ? W(node.Subtitle, FontRole.CardSubtitle) : 0;
        double itemW = 0;
        foreach (var it in node.Items)
        {
            itemW = Math.Max(itemW, W(CardSizer.ItemBullet + it, FontRole.CardSubtitle));
        }

        var bodyW = node.Body != null ? W(node.Body, FontRole.CardSubtitle) : 0;
        var widest = Math.Max(Math.Max(Math.Max(titleW, subW), 0), Math.Max(itemW, bodyW));
        var width = Math.Clamp(Math.Ceiling(widest) + chrome, g.CardMinW, g.CardMaxW);
        var avail = width - g.CardPadX - g.MeasureBorder - iconBlock;

        var textH = CardSizer.WrapText(_m, node.Title, FontRole.CardTitle, avail, _roles).Count * g.CardTitleLine;
        if (node.Subtitle != null)
        {
            textH += g.TextGap + CardSizer.WrapText(_m, node.Subtitle, FontRole.CardSubtitle, avail, _roles).Count * g.CardSubLine;
        }

        if (node.Items.Count > 0)
        {
            textH += g.TextGap + node.Items.Count * g.ItemLine + (node.Items.Count - 1) * g.ItemGap;
        }

        if (node.Body != null)
        {
            textH += g.TextGap + CardSizer.WrapText(_m, node.Body, FontRole.CardSubtitle, avail, _roles).Count * g.BodyLine;
        }

        var content = Math.Max(hasIcon ? g.IconW : 0, textH);
        return new Size(Round(width), Round(content + g.CardPadY + g.MeasureBorder));
    }

    [Fact]
    public void TitlePlusItems_MatchesBoxModel()
    {
        var node = Node("Gateway Service", items: ["Validate token", "Rate limit", "Route request"]);
        Assert.Equal(3, node.Items.Count);
        Assert.Equal(Expected(node), CardSizer.Measure(node, _m, _geo));
    }

    [Fact]
    public void ItemsBlockHeight_IsTextGapPlusRowPitch()
    {
        // Pin the items term precisely: TextGap + n·ItemLine + (n-1)·ItemGap, added to the
        // title+subtitle stack. Computed in unrounded content space (the box rounds only once).
        var g = _geo;
        var withItems = Node("Gateway", subtitle: "edge tier", items: ["A longer bullet line", "Second", "Third"]);
        var avail = CardSizer.CardTextAvail(withItems, _m, g);

        var baseTextH = CardSizer.WrapText(_m, "Gateway", FontRole.CardTitle, avail, _roles).Count * g.CardTitleLine
            + g.TextGap + CardSizer.WrapText(_m, "edge tier", FontRole.CardSubtitle, avail, _roles).Count * g.CardSubLine;
        var itemsH = g.TextGap + 3 * g.ItemLine + 2 * g.ItemGap;
        var content = Math.Max(g.IconW, baseTextH + itemsH);

        Assert.True(baseTextH + itemsH > g.IconW, "precondition: card is text-dominated, not icon-dominated");
        Assert.Equal(Round(content + g.CardPadY + g.MeasureBorder), CardSizer.Measure(withItems, _m, g).H);
    }

    [Fact]
    public void TitlePlusBody_WrapsAtCardMaxW()
    {
        // A body far wider than CardMaxW must clamp the card to CardMaxW and wrap to several lines.
        const string Body = "This is a deliberately long body paragraph that must wrap across several lines inside the card once the width clamps at the maximum.";
        var node = Node("Notes", body: Body);
        var size = CardSizer.Measure(node, _m, _geo);

        Assert.Equal(_geo.CardMaxW, size.W); // clamped
        var avail = CardSizer.CardTextAvail(node, _m, _geo);
        Assert.True(CardSizer.WrapText(_m, Body, FontRole.CardSubtitle, avail, _roles).Count > 1, "body must wrap");
        Assert.Equal(Expected(node), size);
    }

    [Fact]
    public void TitleItemsAndBody_Combined_MatchesBoxModel()
    {
        var node = Node("Order Pipeline",
            subtitle: "async",
            items: ["Receive", "Enrich", "Persist"],
            body: "Each stage acknowledges before the next begins, so a failure rewinds cleanly.");
        Assert.Equal(Expected(node), CardSizer.Measure(node, _m, _geo));
    }

    [Fact]
    public void NoItemsNoBody_MeasuresExactlyAsBefore()
    {
        // The new paths are inert when absent: a plain card (and a title+subtitle+status card) reduce
        // to the pre-existing formula (the Expected oracle collapses to the old terms).
        var plain = Node("API");
        Assert.Empty(plain.Items);
        Assert.Null(plain.Body);
        Assert.Equal(Expected(plain), CardSizer.Measure(plain, _m, _geo));

        var withSub = Node("API", subtitle: "gateway");
        Assert.Equal(Expected(withSub), CardSizer.Measure(withSub, _m, _geo));
    }

    [Fact]
    public void Render_EmitsGuardedItemAndBodyText()
    {
        const string Body = "A short body line under the bullets.";
        var yaml = "type: architecture\n"
                   + "nodes: [{ id: a, title: Topic, items: [\"Alpha point\", \"Beta point\"], body: \"" + Body + "\" }]\n"
                   + "edges: []\n";
        var svg = BeckSvg.Render(yaml); // default InterMetricsMeasurer → approximate → textLength guards

        Assert.Contains(CardSizer.ItemBullet + "Alpha point", svg);
        Assert.Contains(CardSizer.ItemBullet + "Beta point", svg);
        Assert.Contains("A short body line", svg);
        Assert.Contains("textLength=", svg);
    }
}
