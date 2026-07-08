using Beck.Rendering.Svg;
using static Beck.Rendering.Coerce;

namespace Beck.Rendering;

/// <summary>
/// Validate and normalize the raw (parsed-YAML) tree into a full
/// <see cref="DiagramModel"/> — a port of <c>src/model/validate.ts</c>. The prime
/// invariant carries over: the model fills every default, so downstream stages
/// never see null where the TS type is non-optional.
/// </summary>
internal static class Validate
{
    // ---- builders shared across diagram types ----

    /// <summary>Parse <c>meta.narrate</c>: a bool toggles the bar; a mapping tunes the pace.</summary>
    private static NarrationOptions BuildNarration(object? v)
    {
        NarrationOptions d = Defaults.DefaultNarration;
        if (v is null) return d;
        if (v is bool b) return d with { Enabled = b };
        var o = AsObject(v, "meta.narrate");
        return new NarrationOptions(
            Enabled: OptBool(o.GetValueOrDefault("enabled"), "meta.narrate.enabled", true),
            Wpm: Math.Max(30, OptNumber(o.GetValueOrDefault("wpm"), "meta.narrate.wpm") ?? d.Wpm),
            Min: Math.Max(0, OptNumber(o.GetValueOrDefault("min"), "meta.narrate.min") ?? d.Min),
            Pad: Math.Max(0, OptNumber(o.GetValueOrDefault("pad"), "meta.narrate.pad") ?? d.Pad));
    }

    public static DiagramMeta BuildMeta(IReadOnlyDictionary<string, object?> m, DiagramType type)
    {
        var sp = AsObject(m.GetValueOrDefault("spacing"), "meta.spacing");
        Spacing spacingDefault = Defaults.SpacingByType[type];
        return new DiagramMeta
        {
            Type = type,
            Title = OptString(m.GetValueOrDefault("title")),
            Subtitle = OptString(m.GetValueOrDefault("subtitle")),
            Direction = OneOf(m.GetValueOrDefault("direction"), Tokens.Direction, "meta.direction", Direction.TB),
            Theme = OneOf(m.GetValueOrDefault("theme"), Tokens.Theme, "meta.theme", ThemeMode.Auto),
            Animate = OptBool(m.GetValueOrDefault("animate"), "meta.animate", true),
            Loop = OptBool(m.GetValueOrDefault("loop"), "meta.loop", true),
            Fit = OneOf(m.GetValueOrDefault("fit"), Tokens.Fit, "meta.fit", FitMode.Shrink),
            Spacing = new Spacing(
                Rank: OptNumber(sp.GetValueOrDefault("rank"), "meta.spacing.rank") ?? spacingDefault.Rank,
                Node: OptNumber(sp.GetValueOrDefault("node"), "meta.spacing.node") ?? spacingDefault.Node,
                CornerRadius: OptNumber(sp.GetValueOrDefault("cornerRadius"), "meta.spacing.cornerRadius")
                    ?? spacingDefault.CornerRadius),
            Narration = BuildNarration(m.GetValueOrDefault("narrate")),
        };
    }

    public static NodeModel BuildNode(IReadOnlyDictionary<string, object?> n)
    {
        string id = AsString(n.GetValueOrDefault("id"), "node.id");
        NodeKind kind = OneOf(n.GetValueOrDefault("kind"), Tokens.NodeKind, $"node \"{id}\" kind", NodeKind.Service);
        KindDefault kd = Defaults.KindDefaults[kind];
        // An explicit but unknown icon key falls back to the kind default; inline
        // <svg> and known keys pass through.
        string? rawIcon = OptString(n.GetValueOrDefault("icon"));
        return new NodeModel
        {
            Id = id,
            Title = OptString(n.GetValueOrDefault("title")) ?? id,
            Subtitle = OptString(n.GetValueOrDefault("subtitle")),
            Icon = rawIcon != null && Icons.IsKnownIcon(rawIcon) ? rawIcon : kd.Icon,
            Kind = kind,
            Variant = OneOf(n.GetValueOrDefault("variant"), Tokens.NodeVariant, $"node \"{id}\" variant", kd.Variant),
            Status = OptString(n.GetValueOrDefault("status")),
            Accent = Colors.AccentToCss(OptString(n.GetValueOrDefault("accent")), kd.Accent),
            Href = OptString(n.GetValueOrDefault("href")),
            Target = OptString(n.GetValueOrDefault("target")),
            Surface = OptString(n.GetValueOrDefault("surface")),
            TextColor = OptString(n.GetValueOrDefault("textColor")),
            Width = OptNumber(n.GetValueOrDefault("width"), $"node \"{id}\" width"),
            Rank = OptNumber(n.GetValueOrDefault("rank"), $"node \"{id}\" rank"),
            Order = OptNumber(n.GetValueOrDefault("order"), $"node \"{id}\" order"),
            Group = OptString(n.GetValueOrDefault("group")),
            Shape = NodeShape.Card,
            Fields = [],
            Methods = [],
        };
    }

    public static List<GroupModel> BuildGroups(
        IReadOnlyList<object?> rawGroups, IReadOnlyList<NodeModel> nodes, HashSet<string> nodeIds)
    {
        var groups = new Dictionary<string, GroupModel>();
        var order = new List<string>();

        GroupModel Ensure(string id, string? label = null, string? accent = null)
        {
            if (!groups.TryGetValue(id, out GroupModel? g))
            {
                g = new GroupModel
                {
                    Id = id,
                    Label = label ?? id,
                    Members = new List<string>(),
                    Accent = Colors.AccentToCss(accent, AccentToken.Neutral),
                };
                groups[id] = g;
                order.Add(id);
            }
            else if (!string.IsNullOrEmpty(label))
            {
                g.Label = label;
            }
            return g;
        }

        // Pass 1: register every explicit group + any inline node.group.
        foreach (var rg in rawGroups)
        {
            var g = AsObject(rg, "group");
            Ensure(AsString(g.GetValueOrDefault("id"), "group.id"),
                OptString(g.GetValueOrDefault("label")), OptString(g.GetValueOrDefault("accent")));
        }
        foreach (var n in nodes) if (n.Group != null) Ensure(n.Group);

        var groupIds = new HashSet<string>(groups.Keys);
        bool IsMember(string mid) => nodeIds.Contains(mid) || groupIds.Contains(mid);

        // Pass 2: members may be node ids OR group ids (nesting).
        foreach (var rg in rawGroups)
        {
            var g = AsObject(rg, "group");
            string id = AsString(g.GetValueOrDefault("id"), "group.id");
            GroupModel grp = groups[id];
            foreach (var m in AsArray(g.GetValueOrDefault("members"), $"group \"{id}\" members"))
            {
                string mid = AsString(m, $"group \"{id}\" member");
                if (!IsMember(mid)) throw new BeckYamlException($"Group \"{id}\" references unknown node or group \"{mid}\"");
                if (mid == id) throw new BeckYamlException($"Group \"{id}\" cannot contain itself");
                if (!grp.Members.Contains(mid)) grp.Members.Add(mid);
            }
        }

        // Inline node.group membership.
        foreach (var n in nodes)
        {
            if (n.Group == null) continue;
            GroupModel grp = groups[n.Group];
            if (!grp.Members.Contains(n.Id)) grp.Members.Add(n.Id);
        }

        // Each node/group belongs to at most one parent (membership is a tree).
        var parentOf = new Dictionary<string, string>();
        foreach (var id in order)
        {
            foreach (var m in groups[id].Members)
            {
                if (parentOf.TryGetValue(m, out string? prev))
                    throw new BeckYamlException($"\"{m}\" is in two groups (\"{prev}\" and \"{id}\")");
                parentOf[m] = id;
            }
        }

        // No cycles: a group cannot be its own ancestor.
        foreach (var id in order)
        {
            string? cur = parentOf.GetValueOrDefault(id);
            int guard = 0;
            while (cur != null)
            {
                if (cur == id) throw new BeckYamlException($"Group \"{id}\" is nested inside itself");
                cur = parentOf.GetValueOrDefault(cur);
                if (++guard > order.Count + 1) break;
            }
        }

        return order.Select(id => groups[id]).ToList();
    }

    /// <summary>Arrowheads: accept the legacy bool (true→end, false→none) or an end token.</summary>
    private static ArrowEnds ArrowEndsOf(object? v)
    {
        if (v is null || (v is bool bt && bt)) return ArrowEnds.End;
        if (v is bool bf && !bf) return ArrowEnds.None;
        return OneOf(v, Tokens.ArrowEnds, "edge.arrow", ArrowEnds.End);
    }

    private static List<EdgeModel> BuildEdges(IReadOnlyList<object?> rawEdges, HashSet<string> validTargets)
    {
        var edges = new List<EdgeModel>(rawEdges.Count);
        for (int i = 0; i < rawEdges.Count; i++)
        {
            var e = AsObject(rawEdges[i], "edge");
            string from = AsString(e.GetValueOrDefault("from"), "edge.from");
            string to = AsString(e.GetValueOrDefault("to"), "edge.to");
            if (!validTargets.Contains(from)) throw new BeckYamlException($"Edge references unknown source \"{from}\"");
            if (!validTargets.Contains(to)) throw new BeckYamlException($"Edge references unknown target \"{to}\"");
            EdgeKind kind = OneOf(e.GetValueOrDefault("kind"), Tokens.EdgeKind, "edge.kind", EdgeKind.Data);
            EdgeKindDefault kd = Defaults.EdgeKindDefaults[kind];
            object? fromSideRaw = e.GetValueOrDefault("fromSide");
            object? toSideRaw = e.GetValueOrDefault("toSide");
            edges.Add(new EdgeModel
            {
                Id = $"{from}->{to}#{i}",
                From = from,
                To = to,
                Label = OptString(e.GetValueOrDefault("label")),
                Style = OneOf(e.GetValueOrDefault("style"), Tokens.EdgeStyle, "edge.style", kd.Style),
                Curve = OneOf(e.GetValueOrDefault("curve"), Tokens.EdgeCurve, "edge.curve", EdgeCurve.StepRound),
                Kind = kind,
                Color = OptColor(e.GetValueOrDefault("color")) ?? kd.Color,
                Arrow = ArrowEndsOf(e.GetValueOrDefault("arrow")),
                Note = OptString(e.GetValueOrDefault("note")),
                FromSide = fromSideRaw != null ? OneOf(fromSideRaw, Tokens.Side, "edge.fromSide", Side.Bottom) : null,
                ToSide = toSideRaw != null ? OneOf(toSideRaw, Tokens.Side, "edge.toSide", Side.Top) : null,
                Reply = false,
            });
        }
        return edges;
    }

    /// <summary>Shared packet/burst motion knobs — each null when unset so the animator can fall back.</summary>
    private static PacketKnobs PacketKnobsOf(IReadOnlyDictionary<string, object?> p) => new()
    {
        Shape = p.GetValueOrDefault("shape") == null ? null
            : OneOf(p.GetValueOrDefault("shape"), Tokens.PacketShape, "packet.shape", PacketShape.Dot),
        Size = OptNumber(p.GetValueOrDefault("size"), "packet.size"),
        Speed = OptNumber(p.GetValueOrDefault("speed"), "packet.speed"),
        Glow = p.GetValueOrDefault("glow") == null ? null : OptBool(p.GetValueOrDefault("glow"), "packet.glow", true),
        Impact = p.GetValueOrDefault("impact") == null ? null : OptBool(p.GetValueOrDefault("impact"), "packet.impact", false),
        Ease = p.GetValueOrDefault("ease") == null ? null
            : OneOf(p.GetValueOrDefault("ease"), Tokens.PacketEase, "packet.ease", PacketEase.Linear),
    };

    private static FlowStep ParseStep(
        IReadOnlyDictionary<string, object?> s, HashSet<string> nodeIds, HashSet<string> groupIds)
    {
        string Node(string id, string ctx)
        {
            if (!nodeIds.Contains(id)) throw new BeckYamlException($"Flow {ctx} references unknown node \"{id}\"");
            return id;
        }
        string Endpoint(string id, string ctx)
        {
            if (!nodeIds.Contains(id) && !groupIds.Contains(id))
                throw new BeckYamlException($"Flow {ctx} references unknown node or group \"{id}\"");
            return id;
        }

        if (s.ContainsKey("packet"))
        {
            var p = AsObject(s["packet"], "flow packet");
            var via = AsArray(p.GetValueOrDefault("via"), "packet.via")
                .Select(v => Endpoint(AsString(v, "packet.via"), "packet via")).ToList();
            return new PacketStep
            {
                From = Endpoint(AsString(p.GetValueOrDefault("from"), "packet.from"), "packet"),
                To = Endpoint(AsString(p.GetValueOrDefault("to"), "packet.to"), "packet"),
                Via = via.Count > 0 ? via : null,
                Edge = OptString(p.GetValueOrDefault("edge")),
                Color = OptColor(p.GetValueOrDefault("color")),
                Label = OptString(p.GetValueOrDefault("label")),
                Knobs = PacketKnobsOf(p),
            };
        }
        if (s.ContainsKey("burst"))
        {
            var p = AsObject(s["burst"], "flow burst");
            object? toRaw = p.GetValueOrDefault("to");
            string? toSingle = null;
            List<string>? toList = null;
            if (toRaw is IReadOnlyList<object?> arr)
                toList = arr.Select(v => Endpoint(AsString(v, "burst.to"), "burst to")).ToList();
            else
                toSingle = Endpoint(AsString(toRaw, "burst.to"), "burst");
            var via = AsArray(p.GetValueOrDefault("via"), "burst.via")
                .Select(v => Endpoint(AsString(v, "burst.via"), "burst via")).ToList();
            int count = (int)Math.Max(1, Math.Min(24, Js.Round(OptNumber(p.GetValueOrDefault("count"), "burst.count") ?? 3)));
            double stagger = Math.Max(0, OptNumber(p.GetValueOrDefault("stagger"), "burst.stagger") ?? 0.12);
            return new BurstStep
            {
                From = Endpoint(AsString(p.GetValueOrDefault("from"), "burst.from"), "burst"),
                To = toSingle,
                ToList = toList,
                Via = via.Count > 0 ? via : null,
                Count = count,
                Stagger = stagger,
                Color = OptColor(p.GetValueOrDefault("color")),
                Label = OptString(p.GetValueOrDefault("label")),
                Knobs = PacketKnobsOf(p),
            };
        }
        if (s.ContainsKey("status"))
        {
            var p = AsObject(s["status"], "flow status");
            return new StatusStep
            {
                Node = Node(AsString(p.GetValueOrDefault("node"), "status.node"), "status"),
                Text = AsString(p.GetValueOrDefault("text"), "status.text"),
                Color = OptColor(p.GetValueOrDefault("color")),
            };
        }
        if (s.ContainsKey("highlight"))
        {
            var p = AsObject(s["highlight"], "flow highlight");
            return new HighlightStep { Node = Node(AsString(p.GetValueOrDefault("node"), "highlight.node"), "highlight"), Color = OptColor(p.GetValueOrDefault("color")) };
        }
        if (s.ContainsKey("pulse"))
        {
            var p = AsObject(s["pulse"], "flow pulse");
            return new PulseStep { Node = Node(AsString(p.GetValueOrDefault("node"), "pulse.node"), "pulse"), Color = OptColor(p.GetValueOrDefault("color")) };
        }
        if (s.ContainsKey("activate"))
        {
            var p = AsObject(s["activate"], "flow activate");
            return new ActivateStep
            {
                From = Endpoint(AsString(p.GetValueOrDefault("from"), "activate.from"), "activate"),
                To = Endpoint(AsString(p.GetValueOrDefault("to"), "activate.to"), "activate"),
                Color = OptColor(p.GetValueOrDefault("color")),
            };
        }
        if (s.ContainsKey("stream"))
        {
            var p = AsObject(s["stream"], "flow stream");
            return new StreamStep
            {
                From = Endpoint(AsString(p.GetValueOrDefault("from"), "stream.from"), "stream"),
                To = Endpoint(AsString(p.GetValueOrDefault("to"), "stream.to"), "stream"),
                Color = OptColor(p.GetValueOrDefault("color")),
            };
        }
        if (s.ContainsKey("working"))
        {
            var p = AsObject(s["working"], "flow working");
            return new WorkingStep { Node = Node(AsString(p.GetValueOrDefault("node"), "working.node"), "working"), Color = OptColor(p.GetValueOrDefault("color")) };
        }
        if (s.ContainsKey("idle"))
        {
            var p = AsObject(s["idle"], "flow idle");
            return new IdleStep { Node = Node(AsString(p.GetValueOrDefault("node"), "idle.node"), "idle") };
        }
        if (s.ContainsKey("fail"))
        {
            var p = AsObject(s["fail"], "flow fail");
            return new FailStep
            {
                Node = Node(AsString(p.GetValueOrDefault("node"), "fail.node"), "fail"),
                Text = OptString(p.GetValueOrDefault("text")),
                Color = OptColor(p.GetValueOrDefault("color")),
            };
        }
        if (s.ContainsKey("narrate"))
        {
            object? nar = s["narrate"];
            if (nar is string or double or bool)
                return new NarrateStep { Text = AsString(nar, "narrate") };
            var p = AsObject(nar, "flow narrate");
            return new NarrateStep
            {
                Text = AsString(p.GetValueOrDefault("text"), "narrate.text"),
                Hold = OptNumber(p.GetValueOrDefault("hold"), "narrate.hold"),
                Color = OptColor(p.GetValueOrDefault("color")),
            };
        }
        if (s.ContainsKey("phase"))
            return new PhaseStep { Label = AsString(s["phase"], "flow phase") };
        if (s.ContainsKey("wait"))
            return new WaitStep { Seconds = OptNumber(s["wait"], "flow wait") ?? 0.5 };
        if (s.ContainsKey("reset"))
            return new ResetStep();
        if (s.ContainsKey("parallel"))
        {
            var steps = AsArray(s["parallel"], "flow parallel")
                .Select(p => ParseStep(AsObject(p, "parallel step"), nodeIds, groupIds)).ToList();
            return new ParallelStep { Steps = steps };
        }
        throw new BeckYamlException(
            "A flow step must have one of: packet, burst, status, highlight, pulse, activate, stream, working, idle, fail, narrate, phase, wait, reset, parallel");
    }

    public static FlowModel BuildFlow(
        IReadOnlyDictionary<string, object?> f, HashSet<string> nodeIds, HashSet<string> groupIds)
    {
        var steps = AsArray(f.GetValueOrDefault("steps"), "flow.steps")
            .Select(s => ParseStep(AsObject(s, "flow step"), nodeIds, groupIds)).ToList();
        return new FlowModel
        {
            Repeat = OptNumber(f.GetValueOrDefault("repeat"), "flow.repeat") ?? -1,
            RepeatDelay = OptNumber(f.GetValueOrDefault("repeatDelay"), "flow.repeatDelay") ?? 1.5,
            Steps = steps,
            Derived = false,
        };
    }

    // ---- the architecture builder (the original Beck diagram type) ----

    private static DiagramModel BuildArchitectureModel(IReadOnlyDictionary<string, object?> root)
    {
        DiagramMeta meta = BuildMeta(AsObject(root.GetValueOrDefault("meta"), "meta"), DiagramType.Architecture);

        var rawNodes = AsArray(root.GetValueOrDefault("nodes"), "nodes");
        if (rawNodes.Count == 0) throw new BeckYamlException("A diagram needs at least one node under `nodes`");

        var nodes = new List<NodeModel>();
        var nodeIds = new HashSet<string>();
        foreach (var rn in rawNodes)
        {
            NodeModel n = BuildNode(AsObject(rn, "node"));
            if (!nodeIds.Add(n.Id)) throw new BeckYamlException($"Duplicate node id \"{n.Id}\"");
            nodes.Add(n);
        }

        List<GroupModel> groups = BuildGroups(AsArray(root.GetValueOrDefault("groups"), "groups"), nodes, nodeIds);

        var validTargets = new HashSet<string>(nodeIds);
        foreach (var g in groups) validTargets.Add(g.Id);
        List<EdgeModel> edges = BuildEdges(AsArray(root.GetValueOrDefault("edges"), "edges"), validTargets);

        var groupIdSet = new HashSet<string>(groups.Select(g => g.Id));
        FlowModel flow = root.GetValueOrDefault("flow") != null
            ? BuildFlow(AsObject(root["flow"], "flow"), nodeIds, groupIdSet)
            : Defaults.DeriveFlow(nodes, edges);

        if (!meta.Loop) flow.Repeat = 0;

        return new DiagramModel { Meta = meta, Nodes = nodes, Groups = groups, Edges = edges, Flow = flow, Sections = [] };
    }

    private static bool _warnedUntyped;

    /// <summary>Validate and normalize a raw (parsed-YAML) object into a full DiagramModel.</summary>
    public static DiagramModel BuildModel(object? raw)
    {
        var root = AsObject(raw, "document");

        DiagramType type;
        if (root.GetValueOrDefault("type") == null)
        {
            type = DiagramType.Architecture;
            if (!_warnedUntyped)
            {
                _warnedUntyped = true;
                BeckDiagnostics.Warn(
                    "Beck: document has no root `type:` — rendering as `type: architecture`. " +
                    "Untyped documents are deprecated; declare `type: architecture` (or sequence / state / class).");
            }
        }
        else
        {
            type = OneOf(root["type"], Tokens.DiagramType, "type", DiagramType.Architecture);
        }

        return type switch
        {
            DiagramType.Sequence => SequenceBuilder.Build(root),
            DiagramType.State => StateBuilder.Build(root),
            DiagramType.Class => ClassBuilder.Build(root),
            _ => BuildArchitectureModel(root),
        };
    }

    /// <summary>Parse + validate a YAML diagram source into a normalized DiagramModel.</summary>
    public static DiagramModel LoadDiagram(string src) => BuildModel(YamlLoader.ParseYaml(src));
}
