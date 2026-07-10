using static Beck.Model.Coerce;

namespace Beck.Model;

/// <summary>
/// <c>type: class</c> — UML class diagrams on the layered engine. Classes are
/// multi-compartment cards; relations compile to edges with UML end markers.
/// <c>inherits</c>/<c>implements</c> are flipped to parent→child so parents rank
/// above children. A port of <c>src/model/classes.ts</c>.
/// </summary>
internal static class ClassBuilder
{
    private static readonly string[] RelationKinds =
        ["inherits", "implements", "association", "aggregation", "composition", "dependency"];

    private static NodeModel ClassNode(IReadOnlyDictionary<string, object?> c)
    {
        string id = AsString(c.GetValueOrDefault("id"), "class.id");
        return new NodeModel
        {
            // `name` is the natural word for a class; `title` also accepted.
            Id = id,
            Title = OptString(c.GetValueOrDefault("name")) ?? OptString(c.GetValueOrDefault("title")) ?? id,
            Subtitle = OptString(c.GetValueOrDefault("subtitle")),
            Kind = NodeKind.Service,
            Variant = NodeVariant.Solid,
            Accent = Colors.AccentToCss(OptString(c.GetValueOrDefault("accent")), AccentToken.Primary),
            Href = OptString(c.GetValueOrDefault("href")),
            Target = OptString(c.GetValueOrDefault("target")),
            Width = OptNumber(c.GetValueOrDefault("width"), $"class \"{id}\" width"),
            Rank = OptNumber(c.GetValueOrDefault("rank"), $"class \"{id}\" rank"),
            Order = OptNumber(c.GetValueOrDefault("order"), $"class \"{id}\" order"),
            Group = OptString(c.GetValueOrDefault("group")),
            Shape = NodeShape.Class,
            Stereotype = OptString(c.GetValueOrDefault("stereotype")),
            Fields = StringList(c.GetValueOrDefault("fields"), $"class \"{id}\" fields"),
            Methods = StringList(c.GetValueOrDefault("methods"), $"class \"{id}\" methods"),
        };
    }

    public static DiagramModel Build(IReadOnlyDictionary<string, object?> root)
    {
        DiagramMeta meta = Validate.BuildMeta(AsObject(root.GetValueOrDefault("meta"), "meta"), DiagramType.Class);

        var rawClasses = AsArray(root.GetValueOrDefault("classes"), "classes");
        if (rawClasses.Count == 0) throw new BeckYamlException("A class diagram needs at least one entry under `classes`");

        var nodes = new List<NodeModel>();
        var ids = new HashSet<string>();
        foreach (var rc in rawClasses)
        {
            NodeModel n = ClassNode(AsObject(rc, "class"));
            if (!ids.Add(n.Id)) throw new BeckYamlException($"Duplicate class id \"{n.Id}\"");
            nodes.Add(n);
        }

        List<GroupModel> groups = Validate.BuildGroups(AsArray(root.GetValueOrDefault("groups"), "groups"), nodes, ids);

        var edges = new List<EdgeModel>();
        foreach (var rr in AsArray(root.GetValueOrDefault("relations"), "relations"))
        {
            var r = AsObject(rr, "relation");
            string from = AsString(r.GetValueOrDefault("from"), "relation.from");
            string to = AsString(r.GetValueOrDefault("to"), "relation.to");
            if (!ids.Contains(from)) throw new BeckYamlException($"Relation references unknown class \"{from}\"");
            if (!ids.Contains(to)) throw new BeckYamlException($"Relation references unknown class \"{to}\"");
            string kind = OneOfString(r.GetValueOrDefault("kind"), RelationKinds, "relation.kind", "association");
            string? label = OptString(r.GetValueOrDefault("label"));
            string? fromCard = OptString(r.GetValueOrDefault("fromCard"));
            string? toCard = OptString(r.GetValueOrDefault("toCard"));
            string? color = OptColor(r.GetValueOrDefault("color"));
            int i = edges.Count;

            switch (kind)
            {
                case "inherits":
                case "implements":
                    // Flip so the parent ranks above the child; hollow triangle at the parent end.
                    edges.Add(new EdgeModel
                    {
                        Id = $"{to}->{from}#{i}",
                        From = to,
                        To = from,
                        Label = label,
                        Curve = EdgeCurve.StepRound,
                        Arrow = ArrowEnds.None,
                        Reply = false,
                        Style = kind == "implements" ? EdgeStyle.Dashed : EdgeStyle.Solid,
                        Kind = EdgeKind.Data,
                        Color = color ?? "var(--beck-neutral)",
                        MarkerStart = MarkerShape.Triangle,
                        FromLabel = toCard,
                        ToLabel = fromCard,
                    });
                    break;
                case "aggregation":
                case "composition":
                    edges.Add(new EdgeModel
                    {
                        Id = $"{from}->{to}#{i}",
                        From = from,
                        To = to,
                        Label = label,
                        Curve = EdgeCurve.StepRound,
                        Arrow = ArrowEnds.None,
                        Reply = false,
                        Style = EdgeStyle.Solid,
                        Kind = EdgeKind.Data,
                        Color = color ?? "var(--beck-neutral)",
                        MarkerStart = kind == "composition" ? MarkerShape.Diamond : MarkerShape.DiamondOpen,
                        FromLabel = fromCard,
                        ToLabel = toCard,
                    });
                    break;
                case "dependency":
                    edges.Add(new EdgeModel
                    {
                        Id = $"{from}->{to}#{i}",
                        From = from,
                        To = to,
                        Label = label,
                        Curve = EdgeCurve.StepRound,
                        Arrow = ArrowEnds.None,
                        Reply = false,
                        Style = EdgeStyle.Dashed,
                        Kind = EdgeKind.Dependency,
                        Color = color ?? "var(--beck-neutral)",
                        MarkerEnd = MarkerShape.ArrowOpen,
                        FromLabel = fromCard,
                        ToLabel = toCard,
                    });
                    break;
                default: // association
                    edges.Add(new EdgeModel
                    {
                        Id = $"{from}->{to}#{i}",
                        From = from,
                        To = to,
                        Label = label,
                        Curve = EdgeCurve.StepRound,
                        Reply = false,
                        Style = EdgeStyle.Solid,
                        Kind = EdgeKind.Data,
                        Color = color ?? "var(--beck-neutral)",
                        Arrow = r.GetValueOrDefault("arrow") is bool ab && !ab ? ArrowEnds.None : ArrowEnds.End,
                        FromLabel = fromCard,
                        ToLabel = toCard,
                    });
                    break;
            }
        }

        var groupIdSet = new HashSet<string>(groups.Select(g => g.Id));
        // Class diagrams are structural reference material — nothing is auto-derived.
        FlowModel flow;
        if (root.GetValueOrDefault("flow") != null)
        {
            flow = Validate.BuildFlow(AsObject(root["flow"], "flow"), ids, groupIdSet);
            if (!meta.Loop) flow.Repeat = 0;
        }
        else
        {
            flow = new FlowModel { Repeat = 0, RepeatDelay = 0, Steps = [], Derived = false };
            meta.Animate = false;
        }

        return new DiagramModel { Meta = meta, Nodes = nodes, Groups = groups, Edges = edges, Flow = flow, Sections = [] };
    }
}
