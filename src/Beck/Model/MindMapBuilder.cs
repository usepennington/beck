using static Beck.Model.Coerce;

namespace Beck.Model;

/// <summary>
/// <c>type: mindmap</c> — a nested topic tree drawn as a two-sided "butterfly": a central root,
/// first-level branches split left/right, subtrees fanning outward. Compiles onto the layered
/// engine (each half is a <see cref="Layout.LayeredLayout"/> single-level layout; see
/// <see cref="Layout.MindMapLayout"/>).
///
/// <code>
/// type: mindmap
/// meta: { title: ..., ... }        # shared meta (direction is IGNORED — the layout is fixed LR)
/// root:                            # the centre topic
///   title: Beck                    #   or `root: Beck` shorthand (a plain string is the title)
///   # optional: id, subtitle, accent, icon, items, body, href, target, surface, textColor, width
/// topics:                          # the first-level branches
///   - title: Rendering             #   heading only
///     accent: info                 #   optional; else cycled per first-level branch
///     children:                    #   arbitrary nesting depth
///       - title: Pipeline
///         items: [Model, Text, Layout]     # bulleted card
///       - title: Determinism
///         body: >                          # wrapped-paragraph card
///           Same YAML, same SVG.
///   - title: Packages
///     children: [ ... ]
/// flow: ...                        # optional authored flow; else derived (packets root → leaves)
/// </code>
///
/// <para><b>Shape mapping.</b> The root is always a <see cref="NodeShape.Card"/> (the centrepiece).
/// Any topic carrying <c>items</c>/<c>body</c> is a Card. A heading-only topic that HAS children is a
/// Card too — it anchors a subtree, so it reads as a sub-root with visible weight. A heading-only
/// LEAF topic is a <see cref="NodeShape.Pill"/> (light, terminal).</para>
///
/// <para><b>Accent cycling + inheritance.</b> The root resolves to <see cref="AccentToken.Primary"/>.
/// Each first-level branch takes the next token from the fixed cycle
/// <c>[Primary, Info, Success, Warn, Danger, Neutral]</c> (wrapping). Every descendant inherits its
/// parent's RESOLVED accent unless it authors <c>accent:</c> explicitly, which then flows to ITS
/// children. All values are CSS <c>var(--beck-*)</c> tokens (or a passed-through raw colour) — no
/// theme or stylesheet changes.</para>
///
/// <para>Edges are parent → child, coloured by the child's resolved accent so each branch reads as
/// one continuous coloured thread; they are <see cref="EdgeCurve.S"/> curves with no arrowhead (a
/// mind map is undirected reading).</para>
/// </summary>
internal static class MindMapBuilder
{
    /// <summary>First-level branch accent cycle (design handoff "Branch accents"): each branch takes the
    /// next token, wrapping. Neutral is deliberately excluded — it is reserved for ghost branches.</summary>
    private static readonly AccentToken[] _cycle =
        [AccentToken.Info, AccentToken.Primary, AccentToken.Success, AccentToken.Warn, AccentToken.Danger];

    /// <summary>A topic is a mapping, or a plain string that is shorthand for its title.</summary>
    private static (string? Title, IReadOnlyDictionary<string, object?> Map) AsTopic(object? raw)
    {
        if (raw is IReadOnlyDictionary<string, object?> d)
        {
            return (OptString(d.GetValueOrDefault("title")), d);
        }

        var s = OptString(raw);
        if (s != null)
        {
            return (s, AsObject(null, "topic"));
        }

        throw new BeckYamlException("A mindmap topic must be a string or a mapping");
    }

    public static DiagramModel Build(IReadOnlyDictionary<string, object?> root)
    {
        var meta = Validate.BuildMeta(AsObject(root.GetValueOrDefault("meta"), "meta"), DiagramType.MindMap);
        // Direction is fixed: the butterfly is always laid out horizontally, and the router keys off
        // Meta.Direction (LR ⇒ primary-horizontal) to pick left/right node faces via AutoSides.
        meta = meta with { Direction = Direction.Lr };

        var rawRoot = root.GetValueOrDefault("root");
        if (rawRoot is null)
        {
            throw new BeckYamlException("A mindmap needs a `root:` topic");
        }

        var nodes = new List<NodeModel>();
        var edges = new List<EdgeModel>();
        var ids = new HashSet<string>();
        var order = 0;

        // Recursive tree walk → flat node/edge lists. Rank = depth (root 0); Order = stable traversal
        // index. `childrenOverride` lets the root pull its first-level branches from top-level `topics:`
        // while every deeper node pulls from its own `children:`.
        void Walk(object? raw, int depth, string path, int branchIndex, string? parentId, string parentAccent,
            bool parentGhost, IReadOnlyList<object?>? childrenOverride)
        {
            var (title, map) = AsTopic(raw);
            var authoredId = OptString(map.GetValueOrDefault("id"));
            var id = authoredId ?? path;

            var children = childrenOverride ?? AsArray(map.GetValueOrDefault("children"), $"topic \"{id}\" children");
            var items = StringList(map.GetValueOrDefault("items"), $"topic \"{id}\" items");
            var body = OptString(map.GetValueOrDefault("body"));
            var hasContent = items.Count > 0 || body != null;

            // Depth roles (handoff): root + rank-1 are always cards; rank 2+ is a label-only pill
            // UNLESS it authors items/body (content-aware — then it stays an accent-tinted card).
            var shape = depth <= 1 || hasContent ? NodeShape.Card : NodeShape.Pill;

            // Ghost branch (handoff): `variant: ghost` / `ghost: true` marks a not-yet-real branch; it and
            // its whole subtree render neutral + dashed + shadowless. The flag inherits like the accent.
            var ghost = parentGhost
                || string.Equals(OptString(map.GetValueOrDefault("variant")), "ghost", StringComparison.OrdinalIgnoreCase)
                || OptBool(map.GetValueOrDefault("ghost"), $"topic \"{id}\" ghost", false);

            var authoredAccent = OptString(map.GetValueOrDefault("accent"));
            var accent = ghost
                ? Colors.AccentToCss(null, AccentToken.Neutral)
                : depth switch
                {
                    0 => Colors.AccentToCss(authoredAccent, AccentToken.Primary),
                    1 => Colors.AccentToCss(authoredAccent, _cycle[branchIndex % _cycle.Length]),
                    // Deeper: an explicit accent overrides (and then flows on); else inherit the parent's.
                    _ => authoredAccent != null ? Colors.AccentToCss(authoredAccent, AccentToken.Neutral) : parentAccent,
                };

            // A known icon key or raw inline <svg> passes through; anything else is dropped. Icons appear
            // only at the root and rank 1 (handoff depth roles) — deeper nodes never carry one.
            var rawIcon = depth <= 1 ? OptString(map.GetValueOrDefault("icon")) : null;
            var icon = rawIcon != null && (Svg.Icons.IsKnownIcon(rawIcon) || rawIcon.TrimStart().StartsWith('<'))
                ? rawIcon
                : null;

            if (!ids.Add(id))
            {
                throw new BeckYamlException($"Duplicate topic id \"{id}\"");
            }

            nodes.Add(new NodeModel
            {
                Id = id,
                Title = title ?? id,
                Subtitle = OptString(map.GetValueOrDefault("subtitle")),
                Items = items,
                Body = body,
                Icon = icon,
                Status = OptString(map.GetValueOrDefault("status")),
                Kind = NodeKind.Service,
                Variant = ghost ? NodeVariant.Ghost : NodeVariant.Solid,
                Accent = accent,
                Href = OptString(map.GetValueOrDefault("href")),
                Target = OptString(map.GetValueOrDefault("target")),
                Surface = OptString(map.GetValueOrDefault("surface")),
                TextColor = OptString(map.GetValueOrDefault("textColor")),
                Width = OptNumber(map.GetValueOrDefault("width"), $"topic \"{id}\" width"),
                Rank = depth,
                Order = order++,
                Shape = shape,
                Fields = [],
                Methods = [],
            });

            if (parentId != null)
            {
                edges.Add(new EdgeModel
                {
                    Id = $"{parentId}->{id}#{edges.Count}",
                    From = parentId,
                    To = id,
                    Curve = EdgeCurve.S,
                    Arrow = ArrowEnds.None,
                    // A ghost subtree's edges dash too (the child carries the ghost accent = neutral).
                    Style = ghost ? EdgeStyle.Dashed : EdgeStyle.Solid,
                    Kind = EdgeKind.Data,
                    // A muted branch thread (handoff): the child's accent blended 55% into the edge token,
                    // so the hierarchy reads as colour without competing with the nodes.
                    Color = $"color-mix(in srgb, {accent} 55%, var(--beck-edge))",
                    Reply = false,
                });
            }

            for (var i = 0; i < children.Count; i++)
            {
                // First-level branch index is the child's position under the root; deeper nodes carry
                // their branch's index through unchanged (accent cycling only reads it at depth 1).
                Walk(children[i], depth + 1, $"{path}-{i}", depth == 0 ? i : branchIndex, id, accent, ghost, null);
            }
        }

        var topics = AsArray(root.GetValueOrDefault("topics"), "topics");
        Walk(rawRoot, 0, "root", 0, null, Colors.AccentToCss(null, AccentToken.Primary), false, topics);

        var flow = root.GetValueOrDefault("flow") != null
            ? Validate.BuildFlow(AsObject(root["flow"], "flow"), [..ids], [])
            : Defaults.DeriveFlow(nodes, edges);
        if (!meta.Loop)
        {
            flow.Repeat = 0;
        }

        // Mindmaps ship STATIC (handoff "Motion & determinism"): the render emits only the fully-revealed
        // frame — no packets/trails/narration/node-bounce. Forcing the flag here funnels through the three
        // animation gates in SvgRenderer, exactly as ClassBuilder does for a flow-less class diagram.
        meta.Animate = false;

        return new DiagramModel { Meta = meta, Nodes = nodes, Groups = [], Edges = edges, Flow = flow, Sections = [] };
    }
}
