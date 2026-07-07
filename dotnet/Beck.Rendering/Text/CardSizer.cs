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
    private const double Border = 2;          // 1.5px CSS → 1px used-width per side at DPR 1

    // main card (render/node.ts CLS.cardMain + .beck-icon + CLS.text/title/subtitle/status)
    private const double CardPadX = 32, CardPadY = 28, CardMinW = 180;
    private const double IconW = 34, IconGap = 12;
    private const double TitleLine = 1.3 * 14;       // 18.2
    private const double SubLine = 1.35 * 12;        // 16.2
    private const double TextGap = 3, StatusMt = 2;
    private const double StatusChipH = 3 * 2 + 1.2 * 10.4; // py-[3px] + leading-[1.2]·0.65rem = 18.48

    // pill (CLS.pill / pillTitle / pillSubtitle)
    private const double PillPadX = 40, PillPadY = 20, PillMinW = 96, PillGap = 1;
    private const double PillSubLine = 1.3 * 10.88;  // 14.144

    // ghost (CLS.cardGhost / ghostRow / ghostLabel / statusInline)
    private const double GhostPadX = 28, GhostPadY = 16, GhostIcon = 16, GhostIconGap = 7, GhostGap = 3;
    private const double GhostLabelLine = 1.4 * 11.52;   // 16.128 (inherits root lh 1.4)
    private const double StatusInlineLine = 1.4 * 9.92;  // 13.888

    // class (CLS.classCard / classHead / classStereo / classTitle / classSection / classMember)
    private const double ClassMinW = 170;
    private const double HeadPadX = 32, HeadPadY = 16, HeadBorderBottom = 1;
    private const double StereoLine = 1.3 * 10.4;    // 13.52
    private const double ClassTitleLine = 1.4 * 14;  // 19.6
    private const double SectionPadX = 28, SectionPadY = 14, MemberGap = 2;
    private const double MemberLine = 1.45 * 11.52;  // 16.704

    /// <summary>Measure a node's card to its rounded border-box size.</summary>
    public static Size Measure(NodeModel node, ITextMeasurer m) => node.Shape switch
    {
        NodeShape.Pill => Pill(node, m),
        NodeShape.Start or NodeShape.End => new Size(16, 16),
        NodeShape.Class => Class(node, m),
        _ => IsGhost(node) ? Ghost(node, m) : Card(node, m),
    };

    private static bool IsGhost(NodeModel n) => n.Variant == NodeVariant.Ghost || n.Kind == NodeKind.Ghost;

    private static Size Card(NodeModel node, ITextMeasurer m)
    {
        double width = Math.Max(CardMinW, node.Width ?? CardMinW);
        bool hasIcon = Icons.ResolveIcon(node.Icon) != null;
        double iconBlock = hasIcon ? IconW + IconGap : 0;
        double avail = width - CardPadX - Border - iconBlock;

        double textH = WrapLines(m, node.Title, FontRole.CardTitle, avail) * TitleLine;
        if (node.Subtitle != null)
            textH += TextGap + WrapLines(m, node.Subtitle, FontRole.CardSubtitle, avail) * SubLine;
        if (node.Status != null)
            textH += TextGap + StatusMt + StatusChipH;

        double content = Math.Max(hasIcon ? IconW : 0, textH);
        return new Size(Round(width), Round(content + CardPadY + Border));
    }

    private static Size Pill(NodeModel node, ITextMeasurer m)
    {
        double titleW = m.Measure(node.Title, FontRole.PillTitle).Width;
        double subW = node.Subtitle != null ? m.Measure(node.Subtitle, FontRole.PillSubtitle).Width : 0;
        double width = Math.Max(PillMinW, Math.Max(titleW, subW) + PillPadX + Border);

        double h = TitleLine;
        if (node.Subtitle != null) h += PillGap + PillSubLine;
        if (node.Status != null) h += PillGap + StatusMt + StatusChipH;
        return new Size(Round(width), Round(h + PillPadY + Border));
    }

    private static Size Ghost(NodeModel node, ITextMeasurer m)
    {
        bool hasIcon = Icons.ResolveIcon(node.Icon) != null;
        double labelW = m.Measure(node.Title, FontRole.GhostLabel).Width;
        double rowW = (hasIcon ? GhostIcon + GhostIconGap : 0) + labelW;
        double statusW = node.Status != null ? m.Measure(node.Status, FontRole.StatusInline).Width : 0;
        double width = Math.Max(rowW, statusW) + GhostPadX + Border;

        double rowH = Math.Max(hasIcon ? GhostIcon : 0, GhostLabelLine);
        double h = rowH;
        if (node.Status != null) h += GhostGap + StatusInlineLine;
        return new Size(Round(width), Round(h + GhostPadY + Border));
    }

    private static Size Class(NodeModel node, ITextMeasurer m)
    {
        bool hasStereo = node.Stereotype != null;
        double titleW = m.Measure(node.Title, FontRole.ClassTitle).Width;
        double stereoW = hasStereo ? m.Measure($"«{node.Stereotype}»", FontRole.ClassStereotype).Width : 0;
        double headW = Math.Max(stereoW, titleW);

        double widestMember = 0;
        foreach (string f in node.Fields) widestMember = Math.Max(widestMember, m.Measure(f, FontRole.ClassMember).Width);
        foreach (string mm in node.Methods) widestMember = Math.Max(widestMember, m.Measure(mm, FontRole.ClassMember).Width);

        double width = Math.Max(ClassMinW,
            Math.Max(headW + HeadPadX + Border, widestMember + SectionPadX + Border));

        double height = (hasStereo ? StereoLine : 0) + ClassTitleLine + HeadPadY + HeadBorderBottom;
        int sections = 0;
        height += SectionHeight(node.Fields.Count, ref sections);
        height += SectionHeight(node.Methods.Count, ref sections);
        if (sections > 1) height += sections - 1; // 1px border between sections
        return new Size(Round(width), Round(height + Border));
    }

    private static double SectionHeight(int members, ref int sections)
    {
        if (members == 0) return 0;
        sections++;
        return members * MemberLine + (members - 1) * MemberGap + SectionPadY;
    }

    /// <summary>Greedy word-wrap line count at the available width (browser soft-wrap at spaces).</summary>
    private static int WrapLines(ITextMeasurer m, string text, FontRole role, double avail)
    {
        string[] words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0) return 1;
        int lines = 1;
        string current = "";
        foreach (string word in words)
        {
            string candidate = current.Length == 0 ? word : current + " " + word;
            if (current.Length == 0 || m.Measure(candidate, role).Width <= avail)
                current = candidate;
            else { lines++; current = word; }
        }
        return lines;
    }

    private static double Round(double n) => Js.Round(n);
}
