using Beck.Model;

namespace Beck.Layout;

internal sealed record MessageRow(int Index, double Y, bool Self);

internal sealed record SectionBand(string Label, string Accent)
{
    public double X { get; set; }
    public double Y { get; init; }
    public double W { get; set; }
    public double H { get; init; }
}

internal sealed record ActivationBar(
    string Participant, int Level, double Y1, double Y2, string Accent, string StartEdge, string EndEdge);

internal sealed record SequenceLayoutResult(
    IReadOnlyDictionary<string, Rect> Nodes, IReadOnlyDictionary<string, Rect> Groups, double Width, double Height,
    IReadOnlyDictionary<string, double> Centers, IReadOnlyList<MessageRow> Rows,
    IReadOnlyList<SectionBand> Bands, IReadOnlyList<ActivationBar> Activations, double LifelineBottom)
{
    public LayoutResult AsLayout() => new(Nodes, Groups, Width, Height);
}

/// <summary>
/// Fixed-grid sequence layout — a port of <c>src/layout/sequence.ts</c>.
/// Participants are columns (declared order), messages are rows (authored order);
/// column pitch stretches to fit labels, activation bars come from request/reply
/// pairing, and <c>- section:</c> marks open bands.
/// </summary>
internal static class SequenceLayout
{
    private static readonly Size Fallback = new(160, 56);
    private const double CanvasPad = 16, HeadGap = 20, LabelRoom = 40, RowTail = 4, SelfH = 22;
    private const double BandTopPad = 6, BandBottomPad = 16, BandGap = 20, BandInset = 20, Tail = 40;
    public const double BarHalf = 5, LevelStep = 4, SelfLoop = 32;

    private static double LabelEst(string? label) => string.IsNullOrEmpty(label) ? 0 : label.Length * 6.8 + 40;

    public static SequenceLayoutResult Compute(DiagramModel model, IReadOnlyDictionary<string, Size> sizes)
    {
        var parts = model.Nodes;
        Size Size(string id) => sizes.GetValueOrDefault(id, Fallback);
        double gap = Math.Max(48, model.Meta.Spacing.Node);

        var col = new Dictionary<string, int>();
        for (int i = 0; i < parts.Count; i++) col[parts[i].Id] = i;

        var gapNeed = new double[Math.Max(0, parts.Count - 1)];
        for (int i = 0; i < gapNeed.Length; i++) gapNeed[i] = gap;
        foreach (var e in model.Edges)
        {
            int a = col[e.From], b = col[e.To];
            if (e.From == e.To)
            {
                double need = SelfLoop + 10 + LabelEst(e.Label);
                if (a < gapNeed.Length) gapNeed[a] = Math.Max(gapNeed[a], need);
                continue;
            }
            if (Math.Abs(a - b) == 1)
            {
                int g = Math.Min(a, b);
                gapNeed[g] = Math.Max(gapNeed[g], LabelEst(e.Label) + 18);
            }
        }

        var centers = new Dictionary<string, double>();
        double x = CanvasPad;
        for (int i = 0; i < parts.Count; i++)
        {
            double w = Size(parts[i].Id).W;
            if (i == 0) x += w / 2;
            else x += Size(parts[i - 1].Id).W / 2 + gapNeed[i - 1] + w / 2;
            centers[parts[i].Id] = x;
        }

        double maxCardH = Math.Max(parts.Count > 0 ? parts.Max(p => Size(p.Id).H) : 0, Fallback.H);
        var rows = new List<MessageRow>();
        var bands = new List<SectionBand>();
        double bottom = CanvasPad + maxCardH + HeadGap;
        (string Label, string Accent, double Top)? open = null;
        void CloseBand()
        {
            if (open is not { } o) return;
            bottom += BandBottomPad;
            bands.Add(new SectionBand(o.Label, o.Accent) { Y = o.Top, H = bottom - o.Top });
            open = null;
        }
        for (int i = 0; i < model.Edges.Count; i++)
        {
            foreach (var s in model.Sections)
                if (s.At == i)
                {
                    CloseBand();
                    open = (s.Label, s.Accent, bottom + BandGap);
                    bottom = open.Value.Top + BandTopPad;
                }
            bool self = model.Edges[i].From == model.Edges[i].To;
            double y = bottom + LabelRoom;
            rows.Add(new MessageRow(i, y, self));
            bottom = y + (self ? SelfH : RowTail);
        }
        foreach (var s in model.Sections)
            if (s.At >= model.Edges.Count)
            {
                CloseBand();
                open = (s.Label, s.Accent, bottom + BandGap);
                bottom = open.Value.Top + LabelRoom;
            }
        CloseBand();
        double lifelineBottom = bottom + Tail;

        var accentOf = parts.ToDictionary(p => p.Id, p => p.Accent);
        var activations = ComputeActivations(model.Edges, rows, accentOf);

        var nodes = new Dictionary<string, Rect>();
        foreach (var p in parts)
        {
            Size s = Size(p.Id);
            nodes[p.Id] = new Rect(centers[p.Id] - s.W / 2, CanvasPad, s.W, s.H);
        }

        double width = 0;
        foreach (var p in parts) { Rect r = nodes[p.Id]; width = Math.Max(width, r.X + r.W); }
        var last = parts[^1];
        foreach (var e in model.Edges)
            if (e.From == e.To && e.From == last.Id)
                width = Math.Max(width, centers[last.Id] + SelfLoop + 10 + LabelEst(e.Label));
        width = Math.Ceiling(width + CanvasPad);
        double height = Math.Ceiling(lifelineBottom + CanvasPad);

        foreach (var band in bands) { band.X = BandInset; band.W = width - BandInset * 2; }

        return new SequenceLayoutResult(nodes, new Dictionary<string, Rect>(), width, height,
            centers, rows, bands, activations, lifelineBottom);
    }

    private static List<ActivationBar> ComputeActivations(
        IReadOnlyList<EdgeModel> edges, List<MessageRow> rows, Dictionary<string, string> accentOf)
    {
        var claimed = new HashSet<int>();
        var lastTouch = new Dictionary<string, int>();
        for (int i = 0; i < edges.Count; i++) { lastTouch[edges[i].From] = i; lastTouch[edges[i].To] = i; }

        var openBars = new List<(string Participant, int Start, int End, int Level)>();
        for (int i = 0; i < edges.Count; i++)
        {
            EdgeModel e = edges[i];
            if (e.Reply || e.From == e.To) continue;
            if (e.Activate == false) continue;
            int end = -1;
            for (int j = i + 1; j < edges.Count; j++)
            {
                if (claimed.Contains(j)) continue;
                EdgeModel r = edges[j];
                if (r.Reply && r.From == e.To && r.To == e.From) { end = j; claimed.Add(j); break; }
            }
            if (end == -1)
            {
                if (e.Activate != true) continue;
                end = lastTouch.GetValueOrDefault(e.To, i);
            }
            int level = openBars.Count(b => b.Participant == e.To && b.Start <= i && i <= b.End);
            openBars.Add((e.To, i, end, level));
        }

        double RowY(int i) => i >= 0 && i < rows.Count ? rows[i].Y : 0;
        return openBars.Select(b => new ActivationBar(
            b.Participant, b.Level, RowY(b.Start) - 8, RowY(b.End) + 8,
            accentOf.GetValueOrDefault(b.Participant, "var(--beck-neutral)"),
            edges[b.Start].Id, edges[b.End].Id)).ToList();
    }

    /// <summary>Bar-edge x offset for a message anchor on a participant at row y (0 if no bar covers it).</summary>
    public static double ActivationOffset(IReadOnlyList<ActivationBar> bars, string participant, double y)
    {
        int depth = 0;
        foreach (var b in bars)
            if (b.Participant == participant && b.Y1 <= y && y <= b.Y2) depth = Math.Max(depth, b.Level + 1);
        return depth == 0 ? 0 : BarHalf + (depth - 1) * LevelStep;
    }
}
