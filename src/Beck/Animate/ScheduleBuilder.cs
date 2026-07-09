namespace Beck.Rendering.Animate;

/// <summary>A routed edge as the schedule sees it (id, endpoints, kind, path data).</summary>
internal sealed record FlowEdge(string Id, string From, string To, EdgeKind Kind, string D);

/// <summary>One travelling packet + its trail: an absolute-time window on a path.</summary>
internal sealed record PacketHop(
    double Start, double Duration, string D, double Length, bool Reversed,
    string Color, PacketShape Shape, double Size, bool Glow, Ease Ease, string? Label, bool Impact, string? EdgeId);

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

/// <summary>One narration beat: the caption swaps in at <see cref="At"/> (pre-fade base).</summary>
internal sealed record NarrateFx(double At, string? Color);

/// <summary>A status-pill swap: node <see cref="Node"/> shows state <see cref="State"/> at <see cref="At"/>.</summary>
internal sealed record StatusFx(int Node, int State, double At);

/// <summary>
/// The distinct status pill states each node shows — its authored resting state
/// (index 0) plus every <c>(text,color)</c> a flow <c>status</c>/<c>fail</c> step
/// gives it. Computed identically by the renderer (to pre-build the pill groups)
/// and the schedule (to reference them by index), so it must stay a pure function.
/// </summary>
internal static class StatusStates
{
    public static Dictionary<string, List<(string Text, string Color)>> Build(DiagramModel model)
    {
        var accent = model.Nodes.ToDictionary(n => n.Id, n => n.Accent);
        var ids = model.Nodes.Select(n => n.Id).ToHashSet();
        var map = new Dictionary<string, List<(string, string)>>();
        foreach (var n in model.Nodes)
            if (n.Status is { } s) map[n.Id] = new List<(string, string)> { (s, n.Accent) };

        void Add(string node, string text, string color)
        {
            if (!ids.Contains(node)) return;
            // A node the flow gives a status but with no authored one gets an empty
            // resting state 0 (no pill shown at rest), then the flow states on top.
            if (!map.TryGetValue(node, out var list)) map[node] = list = new List<(string, string)> { ("", "") };
            if (!list.Any(x => x.Item1 == text && x.Item2 == color)) list.Add((text, color));
        }
        void Walk(IEnumerable<FlowStep> steps)
        {
            foreach (var st in steps)
                switch (st)
                {
                    case StatusStep s: Add(s.Node, s.Text, s.Color ?? Ac(accent, s.Node)); break;
                    case FailStep f when f.Text is { } t: Add(f.Node, t, f.Color ?? "var(--beck-danger)"); break;
                    case ParallelStep p: Walk(p.Steps); break;
                }
        }
        Walk(model.Flow.Steps);
        return map;
    }

    public static int IndexOf(List<(string Text, string Color)> states, string text, string color)
    {
        for (int i = 0; i < states.Count; i++) if (states[i].Text == text && states[i].Color == color) return i;
        return -1;
    }

    private static string Ac(Dictionary<string, string> a, string n) => a.TryGetValue(n, out var v) ? v : "var(--beck-primary)";
}

/// <summary>The compiled, absolute-time flow schedule.</summary>
internal sealed record Schedule(
    double Duration, double RepeatDelay, int Repeat, double RestoreAt,
    IReadOnlyList<PacketHop> Packets, IReadOnlyList<CardFx> Cards, IReadOnlyList<ImpactFx> Impacts,
    IReadOnlyList<EdgeFx> Edges, IReadOnlyList<WorkFx> Working, IReadOnlyList<NarrateFx> Narrations,
    IReadOnlyList<double> Phases, IReadOnlyList<StatusFx> Statuses);

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
    public static Schedule Build(DiagramModel model, IReadOnlyList<FlowEdge> edges, StyleMotion motion)
    {
        // Effect sub-timeline lengths (seconds) — from the style so the schedule duration and the
        // compiler's keyframe windows stay in lockstep (they read the same fields).
        double PulseDur = motion.PulseDur, HighlightDur = motion.HighlightDur, FailDur = motion.FailDur;
        FlowModel flow = model.Flow;
        var packets = new List<PacketHop>();
        var cards = new List<CardFx>();
        var impacts = new List<ImpactFx>();
        var edgeFx = new List<EdgeFx>();
        var workEvents = new List<(int Node, double At, bool Start, string Color)>();
        var narrations = new List<NarrateFx>();
        var phases = new List<double>();
        var statuses = new List<StatusFx>();
        double duration = 0;
        double restoreAt = -1;

        var accentOf = model.Nodes.ToDictionary(n => n.Id, n => n.Accent);
        var statusMap = StatusStates.Build(model);
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

        // Resolve a status (text,color) to its pill-state index and register the swap.
        void AddStatus(string node, string text, string color, double at)
        {
            if (!statusMap.TryGetValue(node, out var states) || !indexOf.TryGetValue(node, out int i)) return;
            int s = StatusStates.IndexOf(states, text, color);
            if (s >= 0) statuses.Add(new StatusFx(i, s, at));
        }

        // Pulse-on-arrival: a node target flashes; a group target flashes every member.
        void PulseTarget(string targetId, double at, string color)
        {
            if (indexOf.ContainsKey(targetId)) { AddCard(targetId, CardFxKind.Pulse, at, color, PulseDur); return; }
            if (membersOf.TryGetValue(targetId, out var members))
                foreach (var m in members) AddCard(m, CardFxKind.Pulse, at, color, PulseDur);
        }

        // Precomputed once so chain hops (and burst expansion) don't each re-scan every edge:
        // first-match-wins per key, mirroring the FirstOrDefault precedence this replaces.
        var edgeById = new Dictionary<string, FlowEdge>();
        var edgeByFromTo = new Dictionary<(string From, string To), FlowEdge>();
        foreach (var e in edges)
        {
            edgeById.TryAdd(e.Id, e);
            edgeByFromTo.TryAdd((e.From, e.To), e);
        }

        (FlowEdge Edge, bool Reversed)? PathOf(string from, string to, string? edgeId)
        {
            if (edgeId != null && edgeById.TryGetValue(edgeId, out var hit)) return (hit, false);
            if (edgeByFromTo.TryGetValue((from, to), out var direct)) return (direct, false);
            if (edgeByFromTo.TryGetValue((to, from), out var rev)) return (rev, true);
            return null;
        }

        double EmitDot(IReadOnlyList<string> chain, PacketKnobs k, string color, string? label, double startAt, string? edgeId)
        {
            double at = startAt;
            for (int i = 0; i < chain.Count - 1; i++)
            {
                var found = PathOf(chain[i], chain[i + 1], chain.Count == 2 ? edgeId : null);
                if (found is not { } fr) continue;
                FlowEdge fe = fr.Edge;
                string? hopLabel = i == chain.Count - 2 ? label : null;

                PacketKindStyle ks = Defaults.PacketKindStyle[fe.Kind];
                PacketShape shape = k.Shape ?? StyleGlyph(motion.PacketGlyph) ?? PacketShape.Dot;
                double size = k.Size ?? Defaults.PacketShapeSize[shape] ?? ks.Size;
                double speed = k.Speed ?? ks.Speed;
                bool glow = k.Glow ?? ks.Glow;
                bool impact = k.Impact ?? false;
                Ease ease = Easing.ForPacket(k.Ease ?? ks.Ease);

                double len = PathLength.Of(fe.D);
                // 0.6s floor — doubled with the halved PacketKindStyle speeds so degenerate short
                // hops slow down in step with the rest of the choreography.
                double dur = Math.Max(0.6, len / speed);
                packets.Add(new PacketHop(at, dur, fe.D, len, fr.Reversed, color, shape, size, glow, ease, hopLabel, impact, fe.Id));
                at += dur;

                // Each hop pulses its destination as the dot lands (packetWithTrail),
                // and drops an impact ring there when the knob is set (packet.ts).
                PulseTarget(chain[i + 1], at, color);
                if (impact)
                {
                    (double x, double y) = EndPoint(fe.D, fr.Reversed);
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
                case StatusStep sts:
                    AddStatus(sts.Node, sts.Text, sts.Color ?? Accent(accentOf, sts.Node), position ?? duration);
                    break;
                case FailStep fs:
                {
                    double at = position ?? duration;
                    string fcol = fs.Color ?? "var(--beck-danger)";
                    AddCard(fs.Node, CardFxKind.Fail, at, fcol, FailDur);
                    if (fs.Text is { } ft) AddStatus(fs.Node, ft, fcol, at);
                    break;
                }
                case ActivateStep a:
                    if (PathOf(a.From, a.To, null) is { } af)
                        edgeFx.Add(new EdgeFx(EdgeFxKind.Activate, position ?? duration, af.Edge.D, a.Color ?? "var(--beck-primary)", PathLength.Of(af.Edge.D)));
                    break;
                case StreamStep st:
                    if (PathOf(st.From, st.To, null) is { } sf)
                        edgeFx.Add(new EdgeFx(EdgeFxKind.Stream, position ?? duration, sf.Edge.D, st.Color ?? "var(--beck-primary)", PathLength.Of(sf.Edge.D)));
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
                    narrations.Add(new NarrateFx(at, n.Color));
                    duration = Math.Max(duration, at + 0.12 + 0.3 + Math.Max(0, hold));
                    break;
                }
                case PhaseStep:
                    phases.Add(position ?? duration);
                    break;
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

        return new Schedule(duration, flow.RepeatDelay, repeat, restore, packets, cards, impacts, edgeFx, working, narrations, phases, statuses);
    }

    private static string Accent(IReadOnlyDictionary<string, string> accentOf, string node) =>
        accentOf.TryGetValue(node, out string? a) ? a : "var(--beck-primary)";

    /// <summary>Maps the public, style-scoped <see cref="Beck.PacketGlyph"/> to the internal
    /// per-hop <see cref="PacketShape"/> the schedule/compiler actually render.</summary>
    private static PacketShape? StyleGlyph(Beck.PacketGlyph? glyph) => glyph switch
    {
        Beck.PacketGlyph.Dot => PacketShape.Dot,
        Beck.PacketGlyph.Ring => PacketShape.Ring,
        Beck.PacketGlyph.Square => PacketShape.Square,
        Beck.PacketGlyph.Train => PacketShape.Train,
        _ => null,
    };

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
