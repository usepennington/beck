using static Beck.Model.Coerce;

namespace Beck.Model;

/// <summary>
/// <c>type: state</c> — a state machine on the layered engine. States are pills;
/// transitions are edges; <c>"[*]"</c> is the UML entry/exit pseudo-state. A port
/// of <c>src/model/state.ts</c>.
/// </summary>
internal static class StateBuilder
{
    private const string Pseudo = "[*]";
    private const string StartId = "#start";
    private const string EndId = "#end";

    private static NodeModel StatePill(IReadOnlyDictionary<string, object?> s)
    {
        string id = AsString(s.GetValueOrDefault("id"), "state.id");
        if (id is Pseudo or StartId or EndId)
            throw new BeckYamlException($"\"{id}\" is reserved — reference the start/end pseudo-state from a transition instead");
        return new NodeModel
        {
            Id = id,
            Title = OptString(s.GetValueOrDefault("title")) ?? id,
            Subtitle = OptString(s.GetValueOrDefault("subtitle")),
            Kind = NodeKind.Service,
            Variant = NodeVariant.Solid,
            Accent = Colors.AccentToCss(OptString(s.GetValueOrDefault("accent")), AccentToken.Neutral),
            Href = OptString(s.GetValueOrDefault("href")),
            Target = OptString(s.GetValueOrDefault("target")),
            Surface = OptString(s.GetValueOrDefault("surface")),
            TextColor = OptString(s.GetValueOrDefault("textColor")),
            Width = OptNumber(s.GetValueOrDefault("width"), $"state \"{id}\" width"),
            Rank = OptNumber(s.GetValueOrDefault("rank"), $"state \"{id}\" rank"),
            Order = OptNumber(s.GetValueOrDefault("order"), $"state \"{id}\" order"),
            Shape = NodeShape.Pill,
            Fields = [],
            Methods = [],
        };
    }

    private static NodeModel PseudoNode(string id) => new()
    {
        Id = id,
        Title = "",
        Kind = NodeKind.Service,
        Variant = NodeVariant.Solid,
        Accent = "var(--beck-text)",
        Shape = id == StartId ? NodeShape.Start : NodeShape.End,
        Fields = [],
        Methods = [],
    };

    public static DiagramModel Build(IReadOnlyDictionary<string, object?> root)
    {
        DiagramMeta meta = Validate.BuildMeta(AsObject(root.GetValueOrDefault("meta"), "meta"), DiagramType.State);

        // Declared states collected first, pushed in first-reference order.
        var declared = new Dictionary<string, NodeModel>();
        foreach (var rs in AsArray(root.GetValueOrDefault("states"), "states"))
        {
            NodeModel n = StatePill(AsObject(rs, "state"));
            if (!declared.TryAdd(n.Id, n)) throw new BeckYamlException($"Duplicate state id \"{n.Id}\"");
        }

        var nodes = new List<NodeModel>();
        var byId = new Dictionary<string, NodeModel>();
        void Add(NodeModel n) { byId[n.Id] = n; nodes.Add(n); }

        string Ensure(string id, string ctx)
        {
            if (id == Pseudo)
            {
                string pid = ctx == "from" ? StartId : EndId;
                if (!byId.ContainsKey(pid)) Add(PseudoNode(pid));
                return pid;
            }
            if (!byId.ContainsKey(id))
                Add(declared.GetValueOrDefault(id) ?? StatePill(new Dictionary<string, object?> { ["id"] = id }));
            return id;
        }

        var edges = new List<EdgeModel>();
        foreach (var rt in AsArray(root.GetValueOrDefault("transitions"), "transitions"))
        {
            var t = AsObject(rt, "transition");
            string from = Ensure(AsString(t.GetValueOrDefault("from"), "transition.from"), "from");
            string to = Ensure(AsString(t.GetValueOrDefault("to"), "transition.to"), "to");
            edges.Add(new EdgeModel
            {
                Id = $"{from}->{to}#{edges.Count}",
                From = from,
                To = to,
                Label = OptString(t.GetValueOrDefault("label")),
                Style = OneOf(t.GetValueOrDefault("style"), Tokens.EdgeStyle, "transition.style", EdgeStyle.Solid),
                Curve = EdgeCurve.StepRound,
                Kind = EdgeKind.Control,
                Color = OptColor(t.GetValueOrDefault("color")) ?? Defaults.EdgeColor,
                Arrow = ArrowEnds.End,
                Note = OptString(t.GetValueOrDefault("note")),
                Reply = false,
            });
        }
        // Declared states never referenced by a transition still render.
        foreach (var (id, n) in declared) if (!byId.ContainsKey(id)) Add(n);
        if (nodes.Count == 0)
            throw new BeckYamlException("A state diagram needs at least one entry under `states` or `transitions`");

        FlowModel flow = root.GetValueOrDefault("flow") != null
            ? Validate.BuildFlow(AsObject(root["flow"], "flow"), new HashSet<string>(byId.Keys), new HashSet<string>())
            : Defaults.DeriveFlow(nodes, edges);
        if (!meta.Loop) flow.Repeat = 0;

        return new DiagramModel { Meta = meta, Nodes = nodes, Groups = [], Edges = edges, Flow = flow, Sections = [] };
    }
}
