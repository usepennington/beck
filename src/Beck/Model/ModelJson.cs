using System.Text;

namespace Beck.Model;

/// <summary>
/// Serializes a <see cref="DiagramModel"/> to canonical JSON — recursively
/// sorted keys, <c>undefined</c>-equivalent (null) optionals omitted, and JS
/// number/string formatting — so it byte-matches
/// <c>JSON.stringify(loadDiagram(yaml))</c> from the TS oracle (the M1 gate).
/// </summary>
internal static class ModelJson
{
    public static string Canonical(DiagramModel m)
    {
        var sb = new StringBuilder();
        Write(Root(m), sb);
        return sb.ToString();
    }

    // ---- tree builders (Dictionary/List/string/double/bool) ----

    private static Dictionary<string, object?> Root(DiagramModel m) => new()
    {
        ["meta"] = Meta(m.Meta),
        ["nodes"] = m.Nodes.Select(Node).ToList<object?>(),
        ["groups"] = m.Groups.Select(Group).ToList<object?>(),
        ["edges"] = m.Edges.Select(Edge).ToList<object?>(),
        ["flow"] = Flow(m.Flow),
        ["sections"] = m.Sections.Select(Section).ToList<object?>(),
    };

    private static Dictionary<string, object?> Meta(DiagramMeta m)
    {
        var o = new Dictionary<string, object?> { ["type"] = Tokens.DiagramType.Wire(m.Type) };
        if (m.Title != null)
        {
            o["title"] = m.Title;
        }

        if (m.Subtitle != null)
        {
            o["subtitle"] = m.Subtitle;
        }

        if (m.StyleName != null)
        {
            o["style"] = m.StyleName;
        }

        o["direction"] = Tokens.Direction.Wire(m.Direction);
        o["theme"] = Tokens.Theme.Wire(m.Theme);
        o["animate"] = m.Animate;
        o["loop"] = m.Loop;
        o["fit"] = Tokens.Fit.Wire(m.Fit);
        o["spacing"] = new Dictionary<string, object?>
        {
            ["rank"] = m.Spacing.Rank,
            ["node"] = m.Spacing.Node,
            ["cornerRadius"] = m.Spacing.CornerRadius,
        };
        o["narration"] = new Dictionary<string, object?>
        {
            ["enabled"] = m.Narration.Enabled,
            ["wpm"] = m.Narration.Wpm,
            ["min"] = m.Narration.Min,
            ["pad"] = m.Narration.Pad,
        };
        return o;
    }

    private static Dictionary<string, object?> Node(NodeModel n)
    {
        var o = new Dictionary<string, object?> { ["id"] = n.Id, ["title"] = n.Title };
        if (n.Subtitle != null)
        {
            o["subtitle"] = n.Subtitle;
        }

        // New card content blocks. Emitted only when present so every existing model-parity
        // golden (which predates these fields) stays byte-identical.
        if (n.Items.Count > 0)
        {
            o["items"] = n.Items.ToList<object?>();
        }

        if (n.Body != null)
        {
            o["body"] = n.Body;
        }

        if (n.Icon != null)
        {
            o["icon"] = n.Icon;
        }

        o["kind"] = Tokens.NodeKind.Wire(n.Kind);
        o["variant"] = Tokens.NodeVariant.Wire(n.Variant);
        if (n.Status != null)
        {
            o["status"] = n.Status;
        }

        o["accent"] = n.Accent;
        if (n.Href != null)
        {
            o["href"] = n.Href;
        }

        if (n.Target != null)
        {
            o["target"] = n.Target;
        }

        if (n.Surface != null)
        {
            o["surface"] = n.Surface;
        }

        if (n.TextColor != null)
        {
            o["textColor"] = n.TextColor;
        }

        if (n.Width != null)
        {
            o["width"] = n.Width.Value;
        }

        if (n.Rank != null)
        {
            o["rank"] = n.Rank.Value;
        }

        if (n.Order != null)
        {
            o["order"] = n.Order.Value;
        }

        if (n.Group != null)
        {
            o["group"] = n.Group;
        }

        o["shape"] = Tokens.NodeShape.Wire(n.Shape);
        if (n.Stereotype != null)
        {
            o["stereotype"] = n.Stereotype;
        }

        o["fields"] = n.Fields.ToList<object?>();
        o["methods"] = n.Methods.ToList<object?>();
        return o;
    }

    private static Dictionary<string, object?> Group(GroupModel g) => new()
    {
        ["id"] = g.Id,
        ["label"] = g.Label,
        ["members"] = g.Members.ToList<object?>(),
        ["accent"] = g.Accent,
    };

    private static Dictionary<string, object?> Edge(EdgeModel e)
    {
        var o = new Dictionary<string, object?> { ["id"] = e.Id, ["from"] = e.From, ["to"] = e.To };
        if (e.Label != null)
        {
            o["label"] = e.Label;
        }

        o["style"] = Tokens.EdgeStyle.Wire(e.Style);
        o["curve"] = Tokens.EdgeCurve.Wire(e.Curve);
        o["kind"] = Tokens.EdgeKind.Wire(e.Kind);
        o["color"] = e.Color;
        o["arrow"] = Tokens.ArrowEnds.Wire(e.Arrow);
        if (e.Note != null)
        {
            o["note"] = e.Note;
        }

        if (e.FromSide != null)
        {
            o["fromSide"] = Tokens.Side.Wire(e.FromSide.Value);
        }

        if (e.ToSide != null)
        {
            o["toSide"] = Tokens.Side.Wire(e.ToSide.Value);
        }

        if (e.MarkerStart != null)
        {
            o["markerStart"] = Tokens.MarkerShape.Wire(e.MarkerStart.Value);
        }

        if (e.MarkerEnd != null)
        {
            o["markerEnd"] = Tokens.MarkerShape.Wire(e.MarkerEnd.Value);
        }

        if (e.FromLabel != null)
        {
            o["fromLabel"] = e.FromLabel;
        }

        if (e.ToLabel != null)
        {
            o["toLabel"] = e.ToLabel;
        }

        o["reply"] = e.Reply;
        if (e.Activate != null)
        {
            o["activate"] = e.Activate.Value;
        }

        return o;
    }

    private static Dictionary<string, object?> Flow(FlowModel f) => new()
    {
        ["repeat"] = f.Repeat,
        ["repeatDelay"] = f.RepeatDelay,
        ["steps"] = f.Steps.Select(Step).ToList<object?>(),
        ["derived"] = f.Derived,
    };

    private static Dictionary<string, object?> Section(SectionMark s) => new()
    {
        ["label"] = s.Label,
        ["at"] = (double)s.At,
        ["accent"] = s.Accent,
    };

    private static void AddKnobs(Dictionary<string, object?> o, PacketKnobs k)
    {
        if (k.Shape != null)
        {
            o["shape"] = Tokens.PacketShape.Wire(k.Shape.Value);
        }

        if (k.Size != null)
        {
            o["size"] = k.Size.Value;
        }

        if (k.Speed != null)
        {
            o["speed"] = k.Speed.Value;
        }

        if (k.Glow != null)
        {
            o["glow"] = k.Glow.Value;
        }

        if (k.Impact != null)
        {
            o["impact"] = k.Impact.Value;
        }

        if (k.Ease != null)
        {
            o["ease"] = Tokens.PacketEase.Wire(k.Ease.Value);
        }
    }

    private static Dictionary<string, object?> Step(FlowStep s)
    {
        var o = new Dictionary<string, object?> { ["type"] = s.Kind };
        switch (s)
        {
            case PacketStep p:
                o["from"] = p.From;
                o["to"] = p.To;
                if (p.Via != null)
                {
                    o["via"] = p.Via.ToList<object?>();
                }

                if (p.Edge != null)
                {
                    o["edge"] = p.Edge;
                }

                if (p.Color != null)
                {
                    o["color"] = p.Color;
                }

                if (p.Label != null)
                {
                    o["label"] = p.Label;
                }

                AddKnobs(o, p.Knobs);
                break;
            case BurstStep b:
                o["from"] = b.From;
                o["to"] = b.ToList != null ? b.ToList.ToList<object?>() : b.To;
                if (b.Via != null)
                {
                    o["via"] = b.Via.ToList<object?>();
                }

                o["count"] = (double)b.Count;
                o["stagger"] = b.Stagger;
                if (b.Color != null)
                {
                    o["color"] = b.Color;
                }

                if (b.Label != null)
                {
                    o["label"] = b.Label;
                }

                AddKnobs(o, b.Knobs);
                break;
            case StatusStep st:
                o["node"] = st.Node;
                o["text"] = st.Text;
                if (st.Color != null)
                {
                    o["color"] = st.Color;
                }

                break;
            case HighlightStep h:
                o["node"] = h.Node;
                if (h.Color != null)
                {
                    o["color"] = h.Color;
                }

                break;
            case PulseStep pu:
                o["node"] = pu.Node;
                if (pu.Color != null)
                {
                    o["color"] = pu.Color;
                }

                break;
            case ActivateStep a:
                o["from"] = a.From;
                o["to"] = a.To;
                if (a.Color != null)
                {
                    o["color"] = a.Color;
                }

                break;
            case StreamStep sr:
                o["from"] = sr.From;
                o["to"] = sr.To;
                if (sr.Color != null)
                {
                    o["color"] = sr.Color;
                }

                break;
            case WorkingStep w:
                o["node"] = w.Node;
                if (w.Color != null)
                {
                    o["color"] = w.Color;
                }

                break;
            case IdleStep idle:
                o["node"] = idle.Node;
                break;
            case FailStep f:
                o["node"] = f.Node;
                if (f.Text != null)
                {
                    o["text"] = f.Text;
                }

                if (f.Color != null)
                {
                    o["color"] = f.Color;
                }

                break;
            case NarrateStep nar:
                o["text"] = nar.Text;
                if (nar.Hold != null)
                {
                    o["hold"] = nar.Hold.Value;
                }

                if (nar.Color != null)
                {
                    o["color"] = nar.Color;
                }

                break;
            case PhaseStep ph:
                o["label"] = ph.Label;
                break;
            case WaitStep wa:
                o["seconds"] = wa.Seconds;
                break;
            case ResetStep:
                break;
            case ParallelStep par:
                o["steps"] = par.Steps.Select(Step).ToList<object?>();
                break;
        }
        return o;
    }

    // ---- canonical serializer: sorted keys, JS number/string formatting ----

    private static void Write(object? node, StringBuilder sb)
    {
        switch (node)
        {
            case null:
                sb.Append("null");
                break;
            case bool b:
                sb.Append(b ? "true" : "false");
                break;
            case string s:
                WriteString(s, sb);
                break;
            case double d:
                sb.Append(Js.Str(d));
                break;
            case IReadOnlyDictionary<string, object?> obj:
                sb.Append('{');
                var firstK = true;
                foreach (var key in obj.Keys.OrderBy(k => k, StringComparer.Ordinal))
                {
                    if (!firstK)
                    {
                        sb.Append(',');
                    }

                    firstK = false;
                    WriteString(key, sb);
                    sb.Append(':');
                    Write(obj[key], sb);
                }
                sb.Append('}');
                break;
            case System.Collections.IEnumerable list:
                sb.Append('[');
                var firstI = true;
                foreach (var item in list)
                {
                    if (!firstI)
                    {
                        sb.Append(',');
                    }

                    firstI = false;
                    Write(item, sb);
                }
                sb.Append(']');
                break;
            default:
                throw new InvalidOperationException($"Unexpected JSON node type: {node.GetType()}");
        }
    }

    private static void WriteString(string s, StringBuilder sb)
    {
        sb.Append('"');
        foreach (var c in s)
        {
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\b': sb.Append("\\b"); break;
                case '\f': sb.Append("\\f"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (c < 0x20)
                    {
                        sb.Append("\\u").Append(((int)c).ToString("x4", System.Globalization.CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        sb.Append(c);
                    }

                    break;
            }
        }
        sb.Append('"');
    }
}