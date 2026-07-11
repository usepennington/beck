using static Beck.Model.Coerce;

namespace Beck.Model;

/// <summary>
/// <c>type: flowchart</c> — a decision/process graph on the layered engine.
///
/// <code>
/// type: flowchart
/// meta: { title: ..., direction: TB, ... }   # shared meta (see Validate.BuildMeta)
/// steps:
///   - id: start                              # required, unique
///     text: Start                            # title; defaults to id
///     kind: start                            # process | decision | terminator | io | start | end (default: process)
///     subtitle: ...                          # optional
///     accent: primary|success|warn|danger|info|neutral
///     href: ...
///     target: ...
///     surface: ...
///     textColor: ...
///     width: 200
///     rank: 0
///     order: 0
///     icon: ...
/// links:
///   - from: start                            # required; also accepts the "[*]" pseudo-step
///     to: validate                           # required
///     label: yes                             # e.g. a decision branch label
///     style: solid|dashed
///     color: ...
///     note: ...
/// flow: ...                                  # optional authored flow; else derived from links
/// </code>
///
/// Kind → shape: <c>process</c> → Card, <c>decision</c> → Diamond, <c>terminator</c> → Pill,
/// <c>io</c> → Parallelogram, <c>start</c>/<c>end</c> → the Start/End pseudo-shape (also reachable
/// by referencing the literal <c>"[*]"</c> id from a link, exactly like <see cref="StateBuilder"/>'s
/// entry/exit pseudo-state). Steps referenced only by a link (never declared under <c>steps:</c>) are
/// auto-materialized as <c>process</c> cards. Default accent is <see cref="AccentToken.Neutral"/> for
/// every kind (kept uniform with <see cref="StateBuilder"/> rather than special-casing decisions).
///
/// <para><b><c>"[*]"</c> binding.</b> Unlike a state diagram (where the entry/exit dot has no
/// declarable counterpart), a flowchart can declare its own <c>kind: start</c> / <c>kind: end</c>
/// steps. So <c>"[*]"</c> binds to a declared step rather than always spawning an anonymous dot:
/// a <c>"[*]"</c> in a link's <c>from</c> resolves to the single declared <c>kind: start</c> step
/// (and in <c>to</c>, the single declared <c>kind: end</c>) when exactly one exists — the author
/// gets the node they named, not a stray dot beside it. With <em>zero</em> declared starts (ends)
/// the anonymous <c>#start</c> (<c>#end</c>) pseudo-node is materialized as before. With <em>two or
/// more</em>, <c>"[*]"</c> is ambiguous and throws: reference the specific step id instead.</para>
/// </summary>
internal static class FlowchartBuilder
{
    private const string Pseudo = "[*]";
    private const string StartId = "#start";
    private const string EndId = "#end";

    private static NodeShape ShapeFor(string kind) => kind switch
    {
        "process" => NodeShape.Card,
        "decision" => NodeShape.Diamond,
        "terminator" => NodeShape.Pill,
        "io" => NodeShape.Parallelogram,
        "start" or "end" => NodeShape.Start, // overwritten below to Start/End as appropriate
        _ => throw new BeckYamlException(
            $"`kind` must be one of: process, decision, terminator, io, start, end (got \"{kind}\")"),
    };

    private static NodeModel Step(IReadOnlyDictionary<string, object?> s)
    {
        var id = AsString(s.GetValueOrDefault("id"), "step.id");
        if (id is Pseudo or StartId or EndId)
        {
            throw new BeckYamlException($"\"{id}\" is reserved — reference the start/end pseudo-step from a link instead");
        }

        var kind = OneOfString(s.GetValueOrDefault("kind"), ["process", "decision", "terminator", "io", "start", "end"], $"step \"{id}\" kind", "process");
        var shape = kind switch
        {
            "start" => NodeShape.Start,
            "end" => NodeShape.End,
            _ => ShapeFor(kind),
        };

        return new NodeModel
        {
            Id = id,
            Title = OptString(s.GetValueOrDefault("text")) ?? id,
            Subtitle = OptString(s.GetValueOrDefault("subtitle")),
            Icon = OptString(s.GetValueOrDefault("icon")),
            Kind = NodeKind.Service,
            Variant = NodeVariant.Solid,
            Accent = Colors.AccentToCss(OptString(s.GetValueOrDefault("accent")), AccentToken.Neutral),
            Href = OptString(s.GetValueOrDefault("href")),
            Target = OptString(s.GetValueOrDefault("target")),
            Surface = OptString(s.GetValueOrDefault("surface")),
            TextColor = OptString(s.GetValueOrDefault("textColor")),
            Width = OptNumber(s.GetValueOrDefault("width"), $"step \"{id}\" width"),
            Rank = OptNumber(s.GetValueOrDefault("rank"), $"step \"{id}\" rank"),
            Order = OptNumber(s.GetValueOrDefault("order"), $"step \"{id}\" order"),
            Shape = shape,
            Items = [],
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
        Items = [],
        Fields = [],
        Methods = [],
    };

    public static DiagramModel Build(IReadOnlyDictionary<string, object?> root)
    {
        var meta = Validate.BuildMeta(AsObject(root.GetValueOrDefault("meta"), "meta"), DiagramType.Flowchart);

        // Declared steps collected first, pushed in first-reference order.
        var declared = new Dictionary<string, NodeModel>();
        foreach (var rs in AsArray(root.GetValueOrDefault("steps"), "steps"))
        {
            var n = Step(AsObject(rs, "step"));
            if (!declared.TryAdd(n.Id, n))
            {
                throw new BeckYamlException($"Duplicate step id \"{n.Id}\"");
            }
        }

        var nodes = new List<NodeModel>();
        var byId = new Dictionary<string, NodeModel>();
        void Add(NodeModel n) { byId[n.Id] = n; nodes.Add(n); }

        // "[*]" prefers a declared start/end step over the anonymous pseudo-node; keyed by context.
        var declaredStarts = declared.Values.Where(n => n.Shape == NodeShape.Start).ToList();
        var declaredEnds = declared.Values.Where(n => n.Shape == NodeShape.End).ToList();

        string Ensure(string id, string ctx)
        {
            if (id == Pseudo)
            {
                var candidates = ctx == "from" ? declaredStarts : declaredEnds;
                var kindName = ctx == "from" ? "start" : "end";
                if (candidates.Count == 1)
                {
                    // Exactly one declared start/end: bind "[*]" to it — no floating pseudo-dot.
                    var bound = candidates[0];
                    if (!byId.ContainsKey(bound.Id))
                    {
                        Add(bound);
                    }

                    return bound.Id;
                }
                if (candidates.Count >= 2)
                {
                    throw new BeckYamlException(
                        $"\"[*]\" is ambiguous — {candidates.Count} steps declare `kind: {kindName}` "
                        + $"({string.Join(", ", candidates.Select(c => $"\"{c.Id}\""))}); "
                        + $"reference a specific step id instead of \"[*]\"");
                }

                var pid = ctx == "from" ? StartId : EndId;
                if (!byId.ContainsKey(pid))
                {
                    Add(PseudoNode(pid));
                }

                return pid;
            }
            if (!byId.ContainsKey(id))
            {
                Add(declared.GetValueOrDefault(id) ?? Step(new Dictionary<string, object?> { ["id"] = id }));
            }

            return id;
        }

        var edges = new List<EdgeModel>();
        foreach (var rl in AsArray(root.GetValueOrDefault("links"), "links"))
        {
            var l = AsObject(rl, "link");
            var from = Ensure(AsString(l.GetValueOrDefault("from"), "link.from"), "from");
            var to = Ensure(AsString(l.GetValueOrDefault("to"), "link.to"), "to");
            edges.Add(new EdgeModel
            {
                Id = $"{from}->{to}#{edges.Count}",
                From = from,
                To = to,
                Label = OptString(l.GetValueOrDefault("label")),
                Style = OneOf(l.GetValueOrDefault("style"), Tokens.EdgeStyle, "link.style", EdgeStyle.Solid),
                Curve = EdgeCurve.StepRound,
                Kind = EdgeKind.Control,
                Color = OptColor(l.GetValueOrDefault("color")) ?? Defaults.EdgeColor,
                Arrow = ArrowEnds.End,
                Note = OptString(l.GetValueOrDefault("note")),
                Reply = false,
            });
        }
        // Declared steps never referenced by a link still render.
        foreach (var (id, n) in declared)
        {
            if (!byId.ContainsKey(id))
            {
                Add(n);
            }
        }

        if (nodes.Count == 0)
        {
            throw new BeckYamlException("A flowchart needs at least one entry under `steps` or `links`");
        }

        var flow = root.GetValueOrDefault("flow") != null
            ? Validate.BuildFlow(AsObject(root["flow"], "flow"), [..byId.Keys], [])
            : Defaults.DeriveFlow(nodes, edges);
        if (!meta.Loop)
        {
            flow.Repeat = 0;
        }

        return new DiagramModel { Meta = meta, Nodes = nodes, Groups = [], Edges = edges, Flow = flow, Sections = [] };
    }
}
