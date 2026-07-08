namespace Beck.Rendering;

// A port of src/model/defaults.ts — every table verbatim, plus topoOrder + deriveFlow.

internal sealed record KindDefault(AccentToken Accent, string Icon, NodeVariant Variant);

internal sealed record EdgeKindDefault(EdgeStyle Style, string Color);

internal sealed record PacketKindStyle(double Size, double Speed, bool Glow, PacketEase Ease);

internal static class Defaults
{
    /// <summary>Baseline layout spacing — the architecture/sequence default.</summary>
    public static readonly Spacing DefaultSpacing = new(Rank: 96, Node: 32, CornerRadius: 16);

    /// <summary>Default spacing per diagram type (state/class get a roomier gap).</summary>
    public static readonly IReadOnlyDictionary<DiagramType, Spacing> SpacingByType =
        new Dictionary<DiagramType, Spacing>
        {
            [DiagramType.Architecture] = DefaultSpacing,
            [DiagramType.Sequence] = DefaultSpacing,
            [DiagramType.State] = new(Rank: 130, Node: 72, CornerRadius: 16),
            [DiagramType.Class] = new(Rank: 130, Node: 72, CornerRadius: 16),
        };

    /// <summary>Narration is available by default; wpm/min/pad set the reading-time pace.</summary>
    public static readonly NarrationOptions DefaultNarration =
        new(Enabled: true, Wpm: 170, Min: 1.4, Pad: 0.5);

    /// <summary>True when a flow contains any caption content (a <c>narrate</c> step, at any depth).</summary>
    public static bool FlowHasNarration(IReadOnlyList<FlowStep> steps) =>
        steps.Any(s => s is NarrateStep || (s is ParallelStep p && FlowHasNarration(p.Steps)));

    /// <summary>Per-kind visual defaults: accent token, default icon key, and visual weight.</summary>
    public static readonly IReadOnlyDictionary<NodeKind, KindDefault> KindDefaults =
        new Dictionary<NodeKind, KindDefault>
        {
            [NodeKind.Service] = new(AccentToken.Primary, "service", NodeVariant.Solid),
            [NodeKind.Db] = new(AccentToken.Info, "db", NodeVariant.Solid),
            [NodeKind.Queue] = new(AccentToken.Warn, "queue", NodeVariant.Solid),
            [NodeKind.Cache] = new(AccentToken.Warn, "cache", NodeVariant.Solid),
            [NodeKind.Gateway] = new(AccentToken.Primary, "gateway", NodeVariant.Solid),
            [NodeKind.External] = new(AccentToken.Neutral, "external", NodeVariant.Solid),
            [NodeKind.User] = new(AccentToken.Success, "user", NodeVariant.Solid),
            [NodeKind.Ghost] = new(AccentToken.Neutral, "service", NodeVariant.Ghost),
        };

    /// <summary>Per-edge-kind defaults: line style and stroke color token.</summary>
    public static readonly IReadOnlyDictionary<EdgeKind, EdgeKindDefault> EdgeKindDefaults =
        new Dictionary<EdgeKind, EdgeKindDefault>
        {
            [EdgeKind.Data] = new(EdgeStyle.Solid, "var(--beck-edge)"),
            [EdgeKind.Control] = new(EdgeStyle.Solid, "var(--beck-edge)"),
            [EdgeKind.Async] = new(EdgeStyle.Dashed, "var(--beck-edge)"),
            [EdgeKind.Dependency] = new(EdgeStyle.Dashed, "var(--beck-neutral)"),
        };

    /// <summary>Default radius per shape; <c>dot</c> → null keeps the edge-kind size.</summary>
    public static readonly IReadOnlyDictionary<PacketShape, double?> PacketShapeSize =
        new Dictionary<PacketShape, double?>
        {
            [PacketShape.Dot] = null,
            [PacketShape.Circle] = 12,
            [PacketShape.Ring] = 12,
            [PacketShape.Square] = 12,
            // Train's "size" is the capsule's half-height; the capsule elongates along the path from it.
            [PacketShape.Train] = 6,
        };

    /// <summary>Per-edge-kind packet motion: radius, speed, glow, and ease token.</summary>
    public static readonly IReadOnlyDictionary<EdgeKind, PacketKindStyle> PacketKindStyle =
        new Dictionary<EdgeKind, PacketKindStyle>
        {
            [EdgeKind.Data] = new(Size: 6, Speed: 420, Glow: true, Ease: PacketEase.Linear),
            [EdgeKind.Control] = new(Size: 5, Speed: 640, Glow: true, Ease: PacketEase.Accelerate),
            [EdgeKind.Async] = new(Size: 7.5, Speed: 300, Glow: true, Ease: PacketEase.Smooth),
            [EdgeKind.Dependency] = new(Size: 4, Speed: 380, Glow: false, Ease: PacketEase.Linear),
        };

    /// <summary>Kahn topological sort of node ids; falls back to declared order on a cycle.</summary>
    public static List<string> TopoOrder(IReadOnlyList<NodeModel> nodes, IReadOnlyList<EdgeModel> edges)
    {
        var indegree = new Dictionary<string, int>();
        var outAdj = new Dictionary<string, List<string>>();
        foreach (var n in nodes)
        {
            indegree[n.Id] = 0;
            outAdj[n.Id] = new List<string>();
        }
        foreach (var e in edges)
        {
            if (!indegree.ContainsKey(e.From) || !indegree.ContainsKey(e.To) || e.From == e.To) continue;
            indegree[e.To] += 1;
            outAdj[e.From].Add(e.To);
        }
        var queue = new Queue<string>(nodes.Where(n => indegree[n.Id] == 0).Select(n => n.Id));
        var order = new List<string>();
        var seen = new HashSet<string>();
        while (queue.Count > 0)
        {
            string id = queue.Dequeue();
            if (!seen.Add(id)) continue;
            order.Add(id);
            foreach (var next in outAdj.GetValueOrDefault(id) ?? new List<string>())
            {
                int dec = (indegree.TryGetValue(next, out int iv) ? iv : 1) - 1;
                indegree[next] = dec;
                if (dec <= 0) queue.Enqueue(next);
            }
        }
        foreach (var n in nodes) if (!seen.Contains(n.Id)) order.Add(n.Id);
        return order;
    }

    /// <summary>
    /// Auto-derive a flow when none is authored: a packet traverses each edge in
    /// topological order (roots → leaves), then the diagram resets and loops.
    /// </summary>
    public static FlowModel DeriveFlow(IReadOnlyList<NodeModel> nodes, IReadOnlyList<EdgeModel> edges)
    {
        List<string> order = TopoOrder(nodes, edges);
        var pos = new Dictionary<string, int>();
        for (int i = 0; i < order.Count; i++) pos[order[i]] = i;
        int P(string id) => pos.GetValueOrDefault(id, 0);

        // Stable sort by (from-pos, to-pos) — mirrors JS's stable Array.sort.
        var sorted = edges.OrderBy(e => P(e.From)).ThenBy(e => P(e.To)).ToList();

        var steps = new List<FlowStep>();
        var labeled = new HashSet<string>();
        foreach (var e in sorted)
        {
            if (labeled.Add(e.From)) steps.Add(new PhaseStep { Label = e.From });
            if (!string.IsNullOrEmpty(e.Note)) steps.Add(new NarrateStep { Text = e.Note });
            steps.Add(new PacketStep { From = e.From, To = e.To, Knobs = new PacketKnobs() });
        }
        if (steps.Count > 0)
        {
            steps.Add(new WaitStep { Seconds = 1 });
            steps.Add(new ResetStep());
        }
        return new FlowModel { Repeat = -1, RepeatDelay = 1.2, Steps = steps, Derived = true };
    }
}
