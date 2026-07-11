using Beck.Layout;
using Beck.Model;
using Beck.Svg;

namespace Beck.Text;

/// <summary>
/// Reproduces the browser's <c>getBoundingClientRect</c> for each node shape from
/// font metrics + the CSS box model — the C# replacement for <c>src/layout/measure.ts</c>
/// (which measures live DOM). Constants decoded from the utility classes in
/// <c>render/node.ts</c> and <c>embed/styles.css</c> and verified against the
/// browser to ≤1px. Sizes round like <c>measure.ts</c> (JS <c>Math.round</c>).
/// </summary>
/// <remarks>
/// Two subtleties the browser confirmed: the 1.5px CSS card border resolves to a
/// 1px used-width per side at DPR 1 (so 2px total), and card titles/subtitles
/// <em>wrap</em> (no <c>white-space:nowrap</c>), while pill titles, ghost labels,
/// and class members don't. Class members are measured with the mono role (the C#
/// renderer emits them in <c>--beck-font-mono</c>, unlike the JS engine which lets
/// them fall back to the system mono).
/// </remarks>
internal static class CardSizer
{
    /// <summary>The bullet prefix baked into each card <c>items:</c> row before measuring, so the
    /// measured advance matches the run <see cref="SvgRenderer"/> draws (measured == drawn).</summary>
    internal const string ItemBullet = "• ";

    // ---- mindmap depth roles (design handoff "Branch accents"): depth = size + shape ----
    /// <summary>Root card 210×68, rank-1 card 190×56, leaf pill height 30, and the leaf pill's horizontal
    /// text padding (16px each side). These bypass the auto card box model so depth reads as fixed size.</summary>
    internal const double MindMapRootW = 210, MindMapRootH = 68, MindMapRankW = 190, MindMapRankH = 56;
    internal const double MindMapRootChip = 34, MindMapRankChip = 30, MindMapLeafH = 30, MindMapLeafPadX = 32;

    /// <summary>The leaf pill's label typography (Inter 12 / 500) — measured here and drawn by the renderer
    /// so the pill hugs the same run the <c>textLength</c> guard pins.</summary>
    internal static readonly FontRoleSpec MindMapLeafLabel = new(false, 500, 12, 0, false);


    /// <summary>Measure a node's card to its rounded border-box size. The box-model constants come from
    /// <paramref name="geometry"/> and the per-role typography from <paramref name="roles"/> (both
    /// default to <see cref="BeckStyle.Classic"/>'s). Passing the style's <see cref="FontRoleTable"/>
    /// is what makes a remapped role (heavier/uppercased/other-family) size a matching box instead of a
    /// classic one the <c>textLength</c> guard would squeeze the real run into.</summary>
    public static Size Measure(NodeModel node, ITextMeasurer m, StyleGeometry? geometry = null, FontRoleTable? roles = null,
        string titlePrefix = "", string titleSuffix = "", IReadOnlyList<string>? flowStatuses = null, bool mindMap = false)
    {
        var g = geometry ?? BeckStyle.Classic.Geometry;
        var r = roles ?? BeckStyle.Classic.Typography.Roles;
        // The style's node-title decoration (terminal's [brackets]) applied to the measured title, so the
        // box is sized for the same run the renderer draws + word-wraps — the textLength guard stays matched.
        var title = Decorate(node.Title, titlePrefix, titleSuffix);
        // Mindmap nodes take fixed depth-role sizes (leaf pill / root / rank-1 card); a content card
        // (items/body) at any depth falls through to the auto box model. Authored width: always wins.
        if (mindMap && node.Width is null && MindMap(node, m, g, r, title) is { } depthSize)
        {
            return depthSize;
        }

        return node.Shape switch
        {
            NodeShape.Pill => Pill(node, m, g, r, title),
            NodeShape.Start or NodeShape.End => new Size(g.StartEndSize, g.StartEndSize),
            NodeShape.Class => Class(node, m, g, r, title),
            NodeShape.Diamond => Diamond(node, m, g, r, title),
            NodeShape.Parallelogram => Parallelogram(node, m, g, r, title),
            _ => IsGhost(node) ? Ghost(node, m, g, r, title) : Card(node, m, g, r, title, flowStatuses),
        };
    }

    /// <summary>Apply a style's title prefix/suffix (a no-op returning <paramref name="title"/> unchanged
    /// when neither is set — classic byte-identity). Mirrors <c>StyleTypography.DecorateTitle</c>, kept
    /// local so the measurement path doesn't need the whole typography record.</summary>
    private static string Decorate(string title, string pre, string suf) =>
        pre.Length == 0 && suf.Length == 0 ? title : pre + title + suf;

    private static bool IsGhost(NodeModel n) => n.Variant == NodeVariant.Ghost || n.Kind == NodeKind.Ghost;

    /// <summary>Depth-based fixed sizing for a mindmap node (handoff "Branch accents"): a leaf pill hugs its
    /// 12/500 label at height 30; the root card is 210×68 and a rank-1 card 190×56 (each floored so a long
    /// heading still fits). Returns null for a content card (items/body) — it uses the auto <see cref="Card"/>
    /// box. Ghost only changes rendering, not the box, so it is not special-cased here.</summary>
    private static Size? MindMap(NodeModel node, ITextMeasurer m, StyleGeometry g, FontRoleTable r, string title)
    {
        if (node.Shape == NodeShape.Pill)
        {
            var labelW = m.Measure(title, FontRole.PillTitle, MindMapLeafLabel).Width;
            return new Size(Round(Math.Ceiling(labelW) + MindMapLeafPadX), MindMapLeafH);
        }

        // A rank 2+ heading is a pill (above); a rank 2+ card always carries content → auto box (null).
        if (node.Shape != NodeShape.Card || node.Items.Count > 0 || node.Body != null)
        {
            return null;
        }

        var root = (node.Rank ?? 0) == 0;
        var hasIcon = Icons.ResolveIcon(node.Icon) != null;
        var iconBlock = hasIcon ? (root ? MindMapRootChip : MindMapRankChip) + g.IconGap : 0;
        var titleW = W(m, r, title, FontRole.CardTitle);
        var subW = node.Subtitle != null ? W(m, r, node.Subtitle, FontRole.CardSubtitle) : 0;
        var natural = Math.Ceiling(Math.Max(titleW, subW)) + iconBlock + g.CardPadX + g.MeasureBorder;
        return root
            ? new Size(Round(Math.Max(MindMapRootW, natural)), MindMapRootH)
            : new Size(Round(Math.Max(MindMapRankW, natural)), MindMapRankH);
    }

    /// <summary>Measured advance width of <paramref name="text"/> at <paramref name="role"/>, resolved
    /// through the active style's <paramref name="roles"/> table (classic when unremapped).</summary>
    private static double W(ITextMeasurer m, FontRoleTable roles, string text, FontRole role) =>
        m.Measure(text, role, roles.Of(role)).Width;

    private static Size Card(NodeModel node, ITextMeasurer m, StyleGeometry g, FontRoleTable r, string title,
        IReadOnlyList<string>? flowStatuses = null)
    {
        var hasIcon = Icons.ResolveIcon(node.Icon) != null;
        var iconBlock = hasIcon ? g.IconW + g.IconGap : 0;
        var width = CardWidth(node, m, iconBlock, g, r, title, flowStatuses);
        var avail = width - g.CardPadX - g.MeasureBorder - iconBlock;

        var textH = WrapLines(m, title, FontRole.CardTitle, avail, r) * g.CardTitleLine;
        if (node.Subtitle != null)
        {
            textH += g.TextGap + WrapLines(m, node.Subtitle, FontRole.CardSubtitle, avail, r) * g.CardSubLine;
        }

        // Bulleted items — single rows (never wrapped), stacked at the class-compartment pitch.
        if (node.Items.Count > 0)
        {
            textH += g.TextGap + node.Items.Count * g.ItemLine + (node.Items.Count - 1) * g.ItemGap;
        }

        // Wrapped paragraph body in the card-subtitle font, wrapping at the same avail as the title.
        if (node.Body != null)
        {
            textH += g.TextGap + WrapLines(m, node.Body, FontRole.CardSubtitle, avail, r) * g.BodyLine;
        }

        if (node.Status != null || flowStatuses is { Count: > 0 })
        {
            textH += g.TextGap + g.StatusMt + g.StatusChipH;
        }

        var content = Math.Max(hasIcon ? g.IconW : 0, textH);
        return new Size(Round(width), Round(content + g.CardPadY + g.MeasureBorder));
    }

    /// <summary>
    /// A diamond (flowchart decision). The text column must fit the <em>inscribed</em> rectangle: for a
    /// diamond whose bbox is w×h (half-diagonals w/2, h/2), the max-area centered axis-aligned rectangle
    /// has half-extents w/4, h/4 — i.e. a centered rect of exactly w/2 × h/2. So to seat a padded text
    /// block of width TW and height TH we need
    /// <code>w/2 ≥ TW + CardPadX + border   →  w = 2·(TW + CardPadX + border)</code>
    /// <code>h/2 ≥ TH + CardPadY + border   →  h = 2·(TH + CardPadY + border)</code>
    /// i.e. the diamond is twice the padded text block on each axis. Text wraps against <c>w/2</c> (the
    /// inscribed rect's width, exposed by <see cref="DiamondTextAvail"/>), so drawn wrap == measured wrap.
    /// </summary>
    private static Size Diamond(NodeModel node, ITextMeasurer m, StyleGeometry g, FontRoleTable r, string title)
    {
        var width = DiamondWidth(node, m, g, r, title);
        var avail = width / 2 - g.CardPadX - g.MeasureBorder;
        var textH = WrapLines(m, title, FontRole.CardTitle, avail, r) * g.CardTitleLine;
        if (node.Subtitle != null)
        {
            textH += g.TextGap + WrapLines(m, node.Subtitle, FontRole.CardSubtitle, avail, r) * g.CardSubLine;
        }

        var height = 2 * (textH + g.CardPadY + g.MeasureBorder);
        return new Size(Round(width), Round(height));
    }

    /// <summary>The diamond's bbox width: twice the padded single-line text block (so the inscribed rect
    /// holds the widest row without wrapping), floored at <see cref="StyleGeometry.CardMinW"/>; an authored
    /// <c>width:</c> is honoured (also floored at <c>CardMinW</c>).</summary>
    private static double DiamondWidth(NodeModel node, ITextMeasurer m, StyleGeometry g, FontRoleTable r, string title)
    {
        if (node.Width is { } authored)
        {
            return Math.Max(g.CardMinW, authored);
        }

        var titleW = W(m, r, title, FontRole.CardTitle);
        var subW = node.Subtitle != null ? W(m, r, node.Subtitle, FontRole.CardSubtitle) : 0;
        var block = Math.Max(titleW, subW);
        var natural = Math.Ceiling(2 * (block + g.CardPadX + g.MeasureBorder));
        return Math.Max(g.CardMinW, natural);
    }

    /// <summary>The horizontal space a diamond's centered text column wraps within — the inscribed rect's
    /// width (<c>w/2</c>) less padding. Matches <see cref="Diamond"/> so the renderer draws the same wrap.</summary>
    internal static double DiamondTextAvail(NodeModel node, ITextMeasurer m, StyleGeometry? geometry = null, FontRoleTable? roles = null,
        string titlePrefix = "", string titleSuffix = "")
    {
        var g = geometry ?? BeckStyle.Classic.Geometry;
        var r = roles ?? BeckStyle.Classic.Typography.Roles;
        return DiamondWidth(node, m, g, r, Decorate(node.Title, titlePrefix, titleSuffix)) / 2 - g.CardPadX - g.MeasureBorder;
    }

    /// <summary>
    /// A parallelogram (flowchart I/O). Sizes the text exactly like a (no-icon) card, then widens the
    /// bbox by the horizontal skew so the centered text column clears both slanted sides:
    /// <code>skew = min(12, h·0.4);  width = cardWidth + skew</code>
    /// The skew is taken off the rounded height, matching the renderer (which reads the rounded rect
    /// height back and recomputes the same skew). Text wraps against the card text column
    /// (<see cref="ParallelogramTextAvail"/>), unaffected by the skew, so drawn wrap == measured wrap.
    /// </summary>
    private static Size Parallelogram(NodeModel node, ITextMeasurer m, StyleGeometry g, FontRoleTable r, string title)
    {
        var cardW = CardWidth(node, m, 0, g, r, title);
        var avail = cardW - g.CardPadX - g.MeasureBorder;
        var textH = WrapLines(m, title, FontRole.CardTitle, avail, r) * g.CardTitleLine;
        if (node.Subtitle != null)
        {
            textH += g.TextGap + WrapLines(m, node.Subtitle, FontRole.CardSubtitle, avail, r) * g.CardSubLine;
        }

        var height = Round(textH + g.CardPadY + g.MeasureBorder);
        var skew = Beck.Svg.Artwork.ParallelogramSkew(height);
        return new Size(Round(cardW + skew), height);
    }

    /// <summary>The horizontal space a parallelogram's centered text column wraps within — the card text
    /// column (independent of the skew, which only widens the bbox). Matches <see cref="Parallelogram"/>.</summary>
    internal static double ParallelogramTextAvail(NodeModel node, ITextMeasurer m, StyleGeometry? geometry = null, FontRoleTable? roles = null,
        string titlePrefix = "", string titleSuffix = "")
    {
        var g = geometry ?? BeckStyle.Classic.Geometry;
        var r = roles ?? BeckStyle.Classic.Typography.Roles;
        return CardWidth(node, m, 0, g, r, Decorate(node.Title, titlePrefix, titleSuffix)) - g.CardPadX - g.MeasureBorder;
    }

    private static Size Pill(NodeModel node, ITextMeasurer m, StyleGeometry g, FontRoleTable r, string title)
    {
        var titleW = W(m, r, title, FontRole.PillTitle);
        var subW = node.Subtitle != null ? W(m, r, node.Subtitle, FontRole.PillSubtitle) : 0;
        var width = Math.Max(g.PillMinW, Math.Max(titleW, subW) + g.PillPadX + g.MeasureBorder);

        var h = g.CardTitleLine;
        if (node.Subtitle != null)
        {
            h += g.PillGap + g.PillSubLine;
        }

        return new Size(Round(width), Round(h + g.PillPadY + g.MeasureBorder));
    }

    private static Size Ghost(NodeModel node, ITextMeasurer m, StyleGeometry g, FontRoleTable r, string title)
    {
        var hasIcon = Icons.ResolveIcon(node.Icon) != null;
        var labelW = W(m, r, title, FontRole.GhostLabel);
        var rowW = (hasIcon ? g.GhostIcon + g.GhostIconGap : 0) + labelW;
        var statusW = node.Status != null ? W(m, r, node.Status, FontRole.StatusInline) : 0;
        var width = Math.Max(rowW, statusW) + g.GhostPadX + g.MeasureBorder;

        var rowH = Math.Max(hasIcon ? g.GhostIcon : 0, g.GhostLabelLine);
        var h = rowH;
        if (node.Status != null)
        {
            h += g.GhostGap + g.StatusInlineLine;
        }

        return new Size(Round(width), Round(h + g.GhostPadY + g.MeasureBorder));
    }

    private static Size Class(NodeModel node, ITextMeasurer m, StyleGeometry g, FontRoleTable r, string title)
    {
        var hasStereo = node.Stereotype != null;
        var titleW = W(m, r, title, FontRole.ClassTitle);
        var stereoW = hasStereo ? W(m, r, $"«{node.Stereotype}»", FontRole.ClassStereotype) : 0;
        var headW = Math.Max(stereoW, titleW);

        double widestMember = 0;
        foreach (var f in node.Fields)
        {
            widestMember = Math.Max(widestMember, W(m, r, f, FontRole.ClassMember));
        }

        foreach (var mm in node.Methods)
        {
            widestMember = Math.Max(widestMember, W(m, r, mm, FontRole.ClassMember));
        }

        var width = Math.Max(g.ClassMinW,
            Math.Max(headW + g.HeadPadX + g.MeasureBorder, widestMember + g.SectionPadX + g.MeasureBorder));

        var height = (hasStereo ? g.StereoLine : 0) + g.ClassTitleLine + g.HeadPadY + g.HeadBorderBottom;
        var sections = 0;
        height += SectionHeight(node.Fields.Count, ref sections, g);
        height += SectionHeight(node.Methods.Count, ref sections, g);
        if (sections > 1)
        {
            height += sections - 1; // 1px border between sections
        }

        return new Size(Round(width), Round(height + g.MeasureBorder));
    }

    private static double SectionHeight(int members, ref int sections, StyleGeometry g)
    {
        if (members == 0)
        {
            return 0;
        }

        sections++;
        return members * g.MemberLine + (members - 1) * g.MemberGap + g.SectionPadY;
    }

    /// <summary>Greedy word-wrap line count at the available width (browser soft-wrap at spaces).</summary>
    private static int WrapLines(ITextMeasurer m, string text, FontRole role, double avail, FontRoleTable roles) =>
        WrapText(m, text, role, avail, roles).Count;

    /// <summary>
    /// Greedy word-wrap into the individual lines the browser's soft-wrap would produce at
    /// <paramref name="avail"/>. The renderer wraps card titles/subtitles with this same helper so
    /// the drawn lines match the box <see cref="Card"/> sized for them (never overflow). Measured
    /// through <paramref name="roles"/> so a remapped title role wraps against its real width.
    /// </summary>
    internal static List<string> WrapText(ITextMeasurer m, string text, FontRole role, double avail, FontRoleTable? roles = null)
    {
        var r = roles ?? BeckStyle.Classic.Typography.Roles;
        var words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
        {
            return [text];
        }

        var lines = new List<string>();
        var current = "";
        foreach (var word in words)
        {
            var candidate = current.Length == 0 ? word : current + " " + word;
            if (current.Length == 0 || W(m, r, candidate, role) <= avail)
            {
                current = candidate;
            }
            else { lines.Add(current); current = word; }
        }
        lines.Add(current);
        return lines;
    }

    /// <summary>
    /// The card's border-box width. An authored <c>width:</c> is honoured (floored at
    /// <see cref="StyleGeometry.CardMinW"/>); otherwise the card auto-grows to hold its widest text
    /// row on one line, clamped to [<see cref="StyleGeometry.CardMinW"/>,
    /// <see cref="StyleGeometry.CardMaxW"/>]. Past the cap, text wraps.
    /// </summary>
    private static double CardWidth(NodeModel node, ITextMeasurer m, double iconBlock, StyleGeometry g, FontRoleTable r, string title,
        IReadOnlyList<string>? flowStatuses = null)
    {
        if (node.Width is { } authored)
        {
            return Math.Max(g.CardMinW, authored);
        }

        var chrome = g.CardPadX + g.MeasureBorder + iconBlock;
        var titleW = W(m, r, title, FontRole.CardTitle);
        var subW = node.Subtitle != null ? W(m, r, node.Subtitle, FontRole.CardSubtitle) : 0;
        // The card grows to hold the widest single-line item (bullet baked in) and the body's
        // single-line width, both clamped by CardMaxW below (past which they wrap/overflow).
        double itemW = 0;
        foreach (var it in node.Items)
        {
            itemW = Math.Max(itemW, W(m, r, ItemBullet + it, FontRole.CardSubtitle));
        }

        var bodyW = node.Body != null ? W(m, r, node.Body, FontRole.CardSubtitle) : 0;
        // A flow status/fail step swaps a pill into this card; the box pre-grows to the widest
        // pill (text + 16px chip padding) since compiled CSS can't reflow the way the live-DOM
        // engine did. Statuses never wrap, so past CardMaxW a pill still overflows (like the JS
        // engine's un-wrappable chip).
        double statusW = 0;
        if (flowStatuses != null)
        {
            foreach (var s in flowStatuses)
            {
                statusW = Math.Max(statusW, W(m, r, s, FontRole.Status) + 16);
            }
        }
        // Ceiling (not Round): the width must never shave the text column below the measured
        // single-line width, or the title would wrap inside a box sized to hold it on one line.
        var widest = Math.Max(Math.Max(Math.Max(titleW, subW), statusW), Math.Max(itemW, bodyW));
        var natural = Math.Ceiling(widest) + chrome;
        return Math.Clamp(natural, g.CardMinW, g.CardMaxW);
    }

    /// <summary>The horizontal space a card's text column wraps within — matches <see cref="Card"/>.</summary>
    internal static double CardTextAvail(NodeModel node, ITextMeasurer m, StyleGeometry? geometry = null, FontRoleTable? roles = null,
        string titlePrefix = "", string titleSuffix = "", IReadOnlyList<string>? flowStatuses = null)
    {
        var g = geometry ?? BeckStyle.Classic.Geometry;
        var r = roles ?? BeckStyle.Classic.Typography.Roles;
        var iconBlock = Icons.ResolveIcon(node.Icon) != null ? g.IconW + g.IconGap : 0;
        return CardWidth(node, m, iconBlock, g, r, Decorate(node.Title, titlePrefix, titleSuffix), flowStatuses) - g.CardPadX - g.MeasureBorder - iconBlock;
    }

    private static double Round(double n) => Js.Round(n);
}