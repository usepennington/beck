using Beck.Rendering.Svg;

namespace Beck.Rendering.Text;

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
    /// <summary>Measure a node's card to its rounded border-box size. The box-model constants
    /// come from <paramref name="geometry"/> (defaults to <see cref="BeckStyle.Classic"/>'s).</summary>
    public static Size Measure(NodeModel node, ITextMeasurer m, StyleGeometry? geometry = null)
    {
        StyleGeometry g = geometry ?? BeckStyle.Classic.Geometry;
        return node.Shape switch
        {
            NodeShape.Pill => Pill(node, m, g),
            NodeShape.Start or NodeShape.End => new Size(g.StartEndSize, g.StartEndSize),
            NodeShape.Class => Class(node, m, g),
            _ => IsGhost(node) ? Ghost(node, m, g) : Card(node, m, g),
        };
    }

    private static bool IsGhost(NodeModel n) => n.Variant == NodeVariant.Ghost || n.Kind == NodeKind.Ghost;

    private static Size Card(NodeModel node, ITextMeasurer m, StyleGeometry g)
    {
        bool hasIcon = Icons.ResolveIcon(node.Icon) != null;
        double iconBlock = hasIcon ? g.IconW + g.IconGap : 0;
        double width = CardWidth(node, m, iconBlock, g);
        double avail = width - g.CardPadX - g.MeasureBorder - iconBlock;

        double textH = WrapLines(m, node.Title, FontRole.CardTitle, avail) * g.CardTitleLine;
        if (node.Subtitle != null)
            textH += g.TextGap + WrapLines(m, node.Subtitle, FontRole.CardSubtitle, avail) * g.CardSubLine;
        if (node.Status != null)
            textH += g.TextGap + g.StatusMt + g.StatusChipH;

        double content = Math.Max(hasIcon ? g.IconW : 0, textH);
        return new Size(Round(width), Round(content + g.CardPadY + g.MeasureBorder));
    }

    private static Size Pill(NodeModel node, ITextMeasurer m, StyleGeometry g)
    {
        double titleW = m.Measure(node.Title, FontRole.PillTitle).Width;
        double subW = node.Subtitle != null ? m.Measure(node.Subtitle, FontRole.PillSubtitle).Width : 0;
        double width = Math.Max(g.PillMinW, Math.Max(titleW, subW) + g.PillPadX + g.MeasureBorder);

        double h = g.CardTitleLine;
        if (node.Subtitle != null) h += g.PillGap + g.PillSubLine;
        if (node.Status != null) h += g.PillGap + g.StatusMt + g.StatusChipH;
        return new Size(Round(width), Round(h + g.PillPadY + g.MeasureBorder));
    }

    private static Size Ghost(NodeModel node, ITextMeasurer m, StyleGeometry g)
    {
        bool hasIcon = Icons.ResolveIcon(node.Icon) != null;
        double labelW = m.Measure(node.Title, FontRole.GhostLabel).Width;
        double rowW = (hasIcon ? g.GhostIcon + g.GhostIconGap : 0) + labelW;
        double statusW = node.Status != null ? m.Measure(node.Status, FontRole.StatusInline).Width : 0;
        double width = Math.Max(rowW, statusW) + g.GhostPadX + g.MeasureBorder;

        double rowH = Math.Max(hasIcon ? g.GhostIcon : 0, g.GhostLabelLine);
        double h = rowH;
        if (node.Status != null) h += g.GhostGap + g.StatusInlineLine;
        return new Size(Round(width), Round(h + g.GhostPadY + g.MeasureBorder));
    }

    private static Size Class(NodeModel node, ITextMeasurer m, StyleGeometry g)
    {
        bool hasStereo = node.Stereotype != null;
        double titleW = m.Measure(node.Title, FontRole.ClassTitle).Width;
        double stereoW = hasStereo ? m.Measure($"«{node.Stereotype}»", FontRole.ClassStereotype).Width : 0;
        double headW = Math.Max(stereoW, titleW);

        double widestMember = 0;
        foreach (string f in node.Fields) widestMember = Math.Max(widestMember, m.Measure(f, FontRole.ClassMember).Width);
        foreach (string mm in node.Methods) widestMember = Math.Max(widestMember, m.Measure(mm, FontRole.ClassMember).Width);

        double width = Math.Max(g.ClassMinW,
            Math.Max(headW + g.HeadPadX + g.MeasureBorder, widestMember + g.SectionPadX + g.MeasureBorder));

        double height = (hasStereo ? g.StereoLine : 0) + g.ClassTitleLine + g.HeadPadY + g.HeadBorderBottom;
        int sections = 0;
        height += SectionHeight(node.Fields.Count, ref sections, g);
        height += SectionHeight(node.Methods.Count, ref sections, g);
        if (sections > 1) height += sections - 1; // 1px border between sections
        return new Size(Round(width), Round(height + g.MeasureBorder));
    }

    private static double SectionHeight(int members, ref int sections, StyleGeometry g)
    {
        if (members == 0) return 0;
        sections++;
        return members * g.MemberLine + (members - 1) * g.MemberGap + g.SectionPadY;
    }

    /// <summary>Greedy word-wrap line count at the available width (browser soft-wrap at spaces).</summary>
    private static int WrapLines(ITextMeasurer m, string text, FontRole role, double avail) =>
        WrapText(m, text, role, avail).Count;

    /// <summary>
    /// Greedy word-wrap into the individual lines the browser's soft-wrap would produce at
    /// <paramref name="avail"/>. The renderer wraps card titles/subtitles with this same helper so
    /// the drawn lines match the box <see cref="Card"/> sized for them (never overflow).
    /// </summary>
    internal static List<string> WrapText(ITextMeasurer m, string text, FontRole role, double avail)
    {
        string[] words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0) return new List<string> { text };
        var lines = new List<string>();
        string current = "";
        foreach (string word in words)
        {
            string candidate = current.Length == 0 ? word : current + " " + word;
            if (current.Length == 0 || m.Measure(candidate, role).Width <= avail)
                current = candidate;
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
    private static double CardWidth(NodeModel node, ITextMeasurer m, double iconBlock, StyleGeometry g)
    {
        if (node.Width is double authored) return Math.Max(g.CardMinW, authored);
        double chrome = g.CardPadX + g.MeasureBorder + iconBlock;
        double titleW = m.Measure(node.Title, FontRole.CardTitle).Width;
        double subW = node.Subtitle != null ? m.Measure(node.Subtitle, FontRole.CardSubtitle).Width : 0;
        // Ceiling (not Round): the width must never shave the text column below the measured
        // single-line width, or the title would wrap inside a box sized to hold it on one line.
        double natural = Math.Ceiling(Math.Max(titleW, subW)) + chrome;
        return Math.Clamp(natural, g.CardMinW, g.CardMaxW);
    }

    /// <summary>The horizontal space a card's text column wraps within — matches <see cref="Card"/>.</summary>
    internal static double CardTextAvail(NodeModel node, ITextMeasurer m, StyleGeometry? geometry = null)
    {
        StyleGeometry g = geometry ?? BeckStyle.Classic.Geometry;
        double iconBlock = Icons.ResolveIcon(node.Icon) != null ? g.IconW + g.IconGap : 0;
        return CardWidth(node, m, iconBlock, g) - g.CardPadX - g.MeasureBorder - iconBlock;
    }

    private static double Round(double n) => Js.Round(n);
}
