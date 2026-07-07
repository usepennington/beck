namespace Beck.Rendering.Animate;

/// <summary>A routed edge as the schedule sees it (id, endpoints, kind, path data).</summary>
internal sealed record FlowEdge(string Id, string From, string To, EdgeKind Kind, string D);

/// <summary>One travelling packet + its trail: an absolute-time window on a path.</summary>
internal sealed record PacketHop(
    double Start, double Duration, string D, double Length, bool Reversed,
    string Color, PacketShape Shape, double Size, bool Glow, Ease Ease, string? Label, bool Impact);

/// <summary>The compiled, absolute-time flow schedule.</summary>
internal sealed record Schedule(double Duration, double RepeatDelay, int Repeat, double RestoreAt, IReadOnlyList<PacketHop> Packets);

/// <summary>
/// Re-implements <c>src/animate/timeline.ts</c> as a <em>simulation</em>: walks
/// <c>flow.steps</c> with the same position/duration semantics and emits an
/// absolute-time <see cref="Schedule"/>. Motion steps (packet/burst) produce
/// <see cref="PacketHop"/>s; wait/narrate extend duration; reset marks the restore
/// point. (Non-motion effects register their time but their CSS is M9.)
/// </summary>
internal static class ScheduleBuilder
{
    public static Schedule Build(DiagramModel model, IReadOnlyList<FlowEdge> edges)
    {
        FlowModel flow = model.Flow;
        var packets = new List<PacketHop>();
        double duration = 0;
        double restoreAt = -1;

        var accentOf = model.Nodes.ToDictionary(n => n.Id, n => n.Accent);

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
                Ease ease = Easing.ForPacket(k.Ease ?? ks.Ease);

                double len = PathLength.Of(f.D);
                double dur = Math.Max(0.3, len / speed);
                packets.Add(new PacketHop(at, dur, f.D, len, f.Reversed, color, shape, size, glow, ease, hopLabel, k.Impact ?? false));
                at += dur;
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
                case WaitStep w:
                {
                    double at = position ?? duration;
                    duration = Math.Max(duration, at + w.Seconds);
                    break;
                }
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
                // phase + non-motion effects register a time but don't extend duration (their CSS is M9).
            }
        }

        foreach (var step in flow.Steps) Exec(step, null);

        int repeat = (int)flow.Repeat;
        // Clean loop restart: if looping and the flow didn't end in reset, restore at the end.
        if (restoreAt < 0 && repeat != 0) restoreAt = duration;

        return new Schedule(duration, flow.RepeatDelay, repeat, restoreAt < 0 ? duration : restoreAt, packets);
    }

    private static double ReadingTime(string text, NarrationOptions opts)
    {
        int words = text.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
        if (words == 0) words = 1;
        return Math.Max(opts.Min, opts.Pad + (double)words / opts.Wpm * 60);
    }
}
