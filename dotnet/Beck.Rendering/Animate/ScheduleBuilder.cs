namespace Beck.Rendering.Animate;

/// <summary>A routed edge as the schedule sees it (id, endpoints, kind, path data).</summary>
internal sealed record FlowEdge(string Id, string From, string To, EdgeKind Kind, string D);

/// <summary>One travelling packet + its trail: an absolute-time window on a path.</summary>
internal sealed record PacketHop(
    double Start, double Duration, string D, double Length, bool Reversed,
    string Color, PacketShape Shape, double Size, bool Glow, Ease Ease, string? Label, bool Impact);

/// <summary>A node-card effect (pulse / highlight / fail) pinned to an absolute time.</summary>
internal enum CardFxKind { Pulse, Highlight, Fail }
internal sealed record CardFx(int Node, CardFxKind Kind, double Start, string Color);

/// <summary>An expanding "impact" ring at a packet's landing point (the <c>impact</c> knob).</summary>
internal sealed record ImpactFx(double Start, double X, double Y, string Color, double Radius);

/// <summary>A persistent edge overlay: <c>activate</c> (solid recolor) or <c>stream</c> (marching dashes).</summary>
internal enum EdgeFxKind { Activate, Stream }
internal sealed record EdgeFx(EdgeFxKind Kind, double Start, string D, string Color, double Length);

/// <summary>A node's <c>working</c> breathing ring over an interval (until <c>idle</c> or restore).</summary>
internal sealed record WorkFx(int Node, double Start, double End, string Color);

/// <summary>The compiled, absolute-time flow schedule.</summary>
internal sealed record Schedule(
    double Duration, double RepeatDelay, int Repeat, double RestoreAt,
    IReadOnlyList<PacketHop> Packets, IReadOnlyList<CardFx> Cards, IReadOnlyList<ImpactFx> Impacts,
    IReadOnlyList<EdgeFx> Edges, IReadOnlyList<WorkFx> Working);

/// <summary>
/// Re-implements <c>src/animate/timeline.ts</c> as a <em>simulation</em>: walks
/// <c>flow.steps</c> with the same position/duration semantics and emits an
/// absolute-time <see cref="Schedule"/>. Motion steps (packet/burst) produce
/// <see cref="PacketHop"/>s; node effects (pulse/highlight/fail incl. the
/// pulse-on-arrival that every hop fires on its target) produce <see cref="CardFx"/>;
/// the <c>impact</c> knob produces <see cref="ImpactFx"/>. Sub-timeline effects
/// extend the duration; zero-duration effects register a time but don't (§9).
/// </summary>
internal static class ScheduleBuilder
{
    // Effect sub-timeline lengths (seconds) — must match effects.ts so the schedule
    // duration lands where GSAP's tl.duration() would.
    private const double PulseDur = 0.6, HighlightDur = 0.7, FailDur = 1.0;

    public static Schedule Build(DiagramModel model, IReadOnlyList<FlowEdge> edges)
    {
        FlowModel flow = model.Flow;
        var packets = new List<PacketHop>();
        var cards = new List<CardFx>();
        var impacts = new List<ImpactFx>();
        var edgeFx = new List<EdgeFx>();
        var workEvents = new List<(int Node, double At, bool Start, string Color)>();
        double duration = 0;
        double restoreAt = -1;

        var accentOf = model.Nodes.ToDictionary(n => n.Id, n => n.Accent);
        var indexOf = new Dictionary<string, int>();
        for (int i = 0; i < model.Nodes.Count; i++) indexOf[model.Nodes[i].Id] = i;
        var membersOf = model.Groups.ToDictionary(g => g.Id, g => (IReadOnlyList<string>)g.Members);

        // A node-card effect that also extends the duration to cover its sub-timeline.
        void AddCard(string nodeId, CardFxKind kind, double at, string color, double dur)
        {
            if (!indexOf.TryGetValue(nodeId, out int i)) return;
            cards.Add(new CardFx(i, kind, at, color));
            duration = Math.Max(duration, at + dur);
        }

        // Pulse-on-arrival: a node target flashes; a group target flashes every member.
        void PulseTarget(string targetId, double at, string color)
        {
            if (indexOf.ContainsKey(targetId)) { AddCard(targetId, CardFxKind.Pulse, at, color, PulseDur); return; }
            if (membersOf.TryGetValue(targetId, out var members))
                foreach (var m in members) AddCard(m, CardFxKind.Pulse, at, color, PulseDur);
        }

        (string D, bool Reversed, EdgeKind Kind)? PathOf(string from, string to, string? edgeId)
        {
            if (edgeId != null)
            {
                var hit = edges.FirstOrDefault(e => e.Id == edgeId);
                if (hit != null) return (hit.D, false, hit.Kind);
            }
            var direct = edges.FirstOrDefault(e => e.From == from && e.To == to);
            if (direct != null) return (direct.D, false, direct.Kind);
            var rev = edges.FirstOrDefault(e => e.From == to && e.To == from);
            return rev != null ? (rev.D, true, rev.Kind) : null;
        }

        double EmitDot(IReadOnlyList<string> chain, PacketKnobs k, string color, string? label, double startAt, string? edgeId)
        {
            double at = startAt;
            for (int i = 0; i < chain.Count - 1; i++)
            {
                var found = PathOf(chain[i], chain[i + 1], chain.Count == 2 ? edgeId : null);
                if (found is not { } f) continue;
                string? hopLabel = i == chain.Count - 2 ? label : null;

                PacketKindStyle ks = Defaults.PacketKindStyle[f.Kind];
                PacketShape shape = k.Shape ?? PacketShape.Dot;
                double size = k.Size ?? Defaults.PacketShapeSize[shape] ?? ks.Size;
                double speed = k.Speed ?? ks.Speed;
                bool glow = k.Glow ?? ks.Glow;
                bool impact = k.Impact ?? false;
                Ease ease = Easing.ForPacket(k.Ease ?? ks.Ease);

                double len = PathLength.Of(f.D);
                double dur = Math.Max(0.3, len / speed);
                packets.Add(new PacketHop(at, dur, f.D, len, f.Reversed, color, shape, size, glow, ease, hopLabel, impact));
                at += dur;

                // Each hop pulses its destination as the dot lands (packetWithTrail),
                // and drops an impact ring there when the knob is set (packet.ts).
                PulseTarget(chain[i + 1], at, color);
                if (impact)
                {
                    (double x, double y) = EndPoint(f.D, f.Reversed);
                    impacts.Add(new ImpactFx(at, x, y, color, size));
                }
            }
            return at;
        }

        void Exec(FlowStep step, double? position)
        {
            switch (step)
            {
                case PacketStep p:
                {
                    double at = position ?? duration;
                    var chain = new List<string> { p.From };
                    if (p.Via != null) chain.AddRange(p.Via);
                    chain.Add(p.To);
                    double arrival = EmitDot(chain, p.Knobs, p.Color ?? "var(--beck-packet)", p.Label, at, p.Edge);
                    duration = Math.Max(duration, arrival);
                    break;
                }
                case BurstStep b:
                {
                    double bas = position ?? duration;
                    var targets = b.Targets.ToList();
                    for (int c = 0; c < b.Count; c++)
                    {
                        double at = bas + c * b.Stagger;
                        for (int t = 0; t < targets.Count; t++)
                        {
                            var chain = new List<string> { b.From };
                            if (b.Via != null) chain.AddRange(b.Via);
                            chain.Add(targets[t]);
                            double arrival = EmitDot(chain, b.Knobs, b.Color ?? "var(--beck-packet)",
                                c == 0 && t == 0 ? b.Label : null, at, null);
                            duration = Math.Max(duration, arrival);
                        }
                    }
                    break;
                }
                case PulseStep ps:
                    AddCard(ps.Node, CardFxKind.Pulse, position ?? duration, ps.Color ?? Accent(accentOf, ps.Node), PulseDur);
                    break;
                case HighlightStep hs:
                    AddCard(hs.Node, CardFxKind.Highlight, position ?? duration, hs.Color ?? Accent(accentOf, hs.Node), HighlightDur);
                    break;
                case FailStep fs:
                    AddCard(fs.Node, CardFxKind.Fail, position ?? duration, fs.Color ?? "var(--beck-danger)", FailDur);
                    break;
                case ActivateStep a:
                    if (PathOf(a.From, a.To, null) is { } af)
                        edgeFx.Add(new EdgeFx(EdgeFxKind.Activate, position ?? duration, af.D, a.Color ?? "var(--beck-primary)", PathLength.Of(af.D)));
                    break;
                case StreamStep st:
                    if (PathOf(st.From, st.To, null) is { } sf)
                        edgeFx.Add(new EdgeFx(EdgeFxKind.Stream, position ?? duration, sf.D, st.Color ?? "var(--beck-primary)", PathLength.Of(sf.D)));
                    break;
                case WorkingStep wk:
                    if (indexOf.TryGetValue(wk.Node, out int wi))
                        workEvents.Add((wi, position ?? duration, true, wk.Color ?? Accent(accentOf, wk.Node)));
                    break;
                case IdleStep il:
                    if (indexOf.TryGetValue(il.Node, out int ii))
                        workEvents.Add((ii, position ?? duration, false, ""));
                    break;
                case WaitStep w:
                    duration = Math.Max(duration, (position ?? duration) + w.Seconds);
                    break;
                case NarrateStep n:
                {
                    double at = position ?? duration;
                    double hold = n.Hold ?? ReadingTime(n.Text, model.Meta.Narration);
                    duration = Math.Max(duration, at + 0.12 + 0.3 + Math.Max(0, hold));
                    break;
                }
                case ResetStep:
                    restoreAt = position ?? duration;
                    break;
                case ParallelStep par:
                {
                    double bas = position ?? duration;
                    foreach (var child in par.Steps) Exec(child, bas);
                    break;
                }
                // status/working/idle/activate/stream/phase land in later M9 commits.
            }
        }

        foreach (var step in flow.Steps) Exec(step, null);

        int repeat = (int)flow.Repeat;
        if (restoreAt < 0 && repeat != 0) restoreAt = duration;
        double restore = restoreAt < 0 ? duration : restoreAt;

        // Pair each working start with the next idle on that node (else it breathes
        // until the restore point). A re-issued working before an idle is idempotent.
        var working = new List<WorkFx>();
        foreach (var byNode in workEvents.GroupBy(e => e.Node))
        {
            double? openAt = null; string openColor = "";
            foreach (var ev in byNode.OrderBy(e => e.At))
            {
                if (ev.Start) { openAt ??= ev.At; if (openAt == ev.At) openColor = ev.Color; }
                else if (openAt is { } s) { working.Add(new WorkFx(byNode.Key, s, ev.At, openColor)); openAt = null; }
            }
            if (openAt is { } os) working.Add(new WorkFx(byNode.Key, os, Math.Max(os, restore), openColor));
        }

        return new Schedule(duration, flow.RepeatDelay, repeat, restore, packets, cards, impacts, edgeFx, working);
    }

    private static string Accent(IReadOnlyDictionary<string, string> accentOf, string node) =>
        accentOf.TryGetValue(node, out string? a) ? a : "var(--beck-primary)";

    /// <summary>The landing point of a hop: the path's end (forward) or start (reversed).</summary>
    private static (double X, double Y) EndPoint(string d, bool reversed)
    {
        var nums = new List<double>();
        int i = 0;
        while (i < d.Length)
        {
            char c = d[i];
            if (c == '-' || c == '.' || char.IsDigit(c))
            {
                int j = i + 1;
                while (j < d.Length && (char.IsDigit(d[j]) || d[j] == '.' || d[j] == '-' && d[j - 1] == 'e')) j++;
                if (double.TryParse(d.AsSpan(i, j - i), System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out double v)) nums.Add(v);
                i = j;
            }
            else i++;
        }
        if (nums.Count < 2) return (0, 0);
        return reversed ? (nums[0], nums[1]) : (nums[^2], nums[^1]);
    }

    private static double ReadingTime(string text, NarrationOptions opts)
    {
        int words = text.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
        if (words == 0) words = 1;
        return Math.Max(opts.Min, opts.Pad + (double)words / opts.Wpm * 60);
    }
}
