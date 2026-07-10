using static Beck.Model.Coerce;

namespace Beck.Model;

/// <summary>
/// <c>type: sequence</c> — participants across the top, lifelines down, messages in
/// authored order. A port of <c>src/model/sequence.ts</c>. Messages compile to
/// EdgeModel entries (one per row) so the shared animation layer can ride them.
/// </summary>
internal static class SequenceBuilder
{
    public static DiagramModel Build(IReadOnlyDictionary<string, object?> root)
    {
        DiagramMeta meta = Validate.BuildMeta(AsObject(root.GetValueOrDefault("meta"), "meta"), DiagramType.Sequence);

        var rawParts = AsArray(root.GetValueOrDefault("participants"), "participants");
        if (rawParts.Count == 0)
            throw new BeckYamlException("A sequence diagram needs at least one participant under `participants`");

        var nodes = new List<NodeModel>();
        var ids = new HashSet<string>();
        foreach (var rp in rawParts)
        {
            NodeModel n = Validate.BuildNode(AsObject(rp, "participant"));
            if (!ids.Add(n.Id)) throw new BeckYamlException($"Duplicate participant id \"{n.Id}\"");
            nodes.Add(n);
        }

        var accentOf = nodes.ToDictionary(n => n.Id, n => n.Accent);
        var edges = new List<EdgeModel>();
        var sections = new List<SectionMark>();
        foreach (var rm in AsArray(root.GetValueOrDefault("messages"), "messages"))
        {
            var m = AsObject(rm, "message");
            if (m.ContainsKey("section"))
            {
                sections.Add(new SectionMark(
                    Label: AsString(m["section"], "message section"),
                    At: edges.Count,
                    Accent: OptColor(m.GetValueOrDefault("accent")) ?? "var(--beck-neutral)"));
                continue;
            }
            string from = AsString(m.GetValueOrDefault("from"), "message.from");
            string to = AsString(m.GetValueOrDefault("to"), "message.to");
            if (!ids.Contains(from)) throw new BeckYamlException($"Message references unknown participant \"{from}\"");
            if (!ids.Contains(to)) throw new BeckYamlException($"Message references unknown participant \"{to}\"");
            bool reply = m.GetValueOrDefault("reply") is bool rb && rb;
            EdgeKind kind = OneOf(m.GetValueOrDefault("kind"), Tokens.EdgeKind, "message.kind",
                reply ? EdgeKind.Control : EdgeKind.Data);
            // Replies and async sends read as "lighter": dashed line, open arrowhead.
            bool dashed = reply || kind == EdgeKind.Async;
            // A message is tinted by the participant doing the work.
            string worker = reply ? from : to;
            string? authoredColor = OptColor(m.GetValueOrDefault("color"));
            edges.Add(new EdgeModel
            {
                Id = $"msg{edges.Count}",
                From = from,
                To = to,
                Label = OptString(m.GetValueOrDefault("label")),
                Style = OneOf(m.GetValueOrDefault("style"), Tokens.EdgeStyle, "message.style",
                    dashed ? EdgeStyle.Dashed : EdgeStyle.Solid),
                Curve = EdgeCurve.Straight,
                Kind = kind,
                Color = authoredColor ?? accentOf.GetValueOrDefault(worker)
                    ?? Defaults.EdgeKindDefaults[kind].Color,
                ColorAuthored = authoredColor != null,
                Arrow = ArrowEnds.End,
                MarkerEnd = dashed ? MarkerShape.ArrowOpen : MarkerShape.Arrow,
                Note = OptString(m.GetValueOrDefault("note")),
                Reply = reply,
                Activate = TriBool(m.GetValueOrDefault("activate"), "message.activate"),
            });
        }
        if (edges.Count == 0) throw new BeckYamlException("A sequence diagram needs at least one entry under `messages`");

        FlowModel flow = root.GetValueOrDefault("flow") != null
            ? Validate.BuildFlow(AsObject(root["flow"], "flow"), ids, new HashSet<string>())
            : DeriveSequenceFlow(edges, sections, meta.Loop);
        if (!meta.Loop) flow.Repeat = 0;

        return new DiagramModel { Meta = meta, Nodes = nodes, Groups = [], Edges = edges, Flow = flow, Sections = sections };
    }

    /// <summary>The authored message order IS the story: one packet per message, in order.</summary>
    private static FlowModel DeriveSequenceFlow(
        IReadOnlyList<EdgeModel> edges, IReadOnlyList<SectionMark> sections, bool loop)
    {
        var steps = new List<FlowStep>();
        for (int i = 0; i < edges.Count; i++)
        {
            EdgeModel e = edges[i];
            foreach (var s in sections) if (s.At == i) steps.Add(new PhaseStep { Label = s.Label });
            if (!string.IsNullOrEmpty(e.Note)) steps.Add(new NarrateStep { Text = e.Note });
            steps.Add(new PacketStep
            {
                From = e.From,
                To = e.To,
                Edge = e.Id,
                Color = e.Color,
                Knobs = new PacketKnobs { Ease = e.Reply ? PacketEase.Decelerate : null },
            });
        }
        // Trailing sections (after the last message) still get their phase beat.
        foreach (var s in sections) if (s.At >= edges.Count) steps.Add(new PhaseStep { Label = s.Label });
        if (loop)
        {
            steps.Add(new WaitStep { Seconds = 1.2 });
            steps.Add(new ResetStep());
        }
        return new FlowModel { Repeat = -1, RepeatDelay = 2, Steps = steps, Derived = true };
    }
}
