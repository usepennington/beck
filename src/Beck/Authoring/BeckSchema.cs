namespace Beck.Authoring;

/// <summary>
/// Authoring-time schema metadata for Beck YAML — the token vocabularies, per-section
/// key lists, and one-line docs that power editor completions and hover in tooling
/// (the docs playground's Monaco editor). The value vocabularies are sourced from the
/// engine's own <see cref="Model.Tokens"/> maps and icon registry, so they never drift from
/// what the parser actually accepts; the key lists and docs are curated here to match
/// <c>Model/Schema.cs</c>.
/// </summary>
public static class BeckSchema
{
    // ---- value vocabularies (sourced from the engine — always in sync) ------
    /// <summary>Root <c>type:</c> values.</summary>
    public static IReadOnlyList<string> Types => Model.Tokens.DiagramType.Tokens;
    /// <summary>Node <c>kind:</c> presets.</summary>
    public static IReadOnlyList<string> Kinds => Model.Tokens.NodeKind.Tokens;
    /// <summary>Edge <c>kind:</c> values.</summary>
    public static IReadOnlyList<string> EdgeKinds => Model.Tokens.EdgeKind.Tokens;
    /// <summary>Accent / colour tokens.</summary>
    public static IReadOnlyList<string> Accents => Model.Tokens.Accent.Tokens;
    /// <summary>Layout <c>direction:</c> values.</summary>
    public static IReadOnlyList<string> Directions => Model.Tokens.Direction.Tokens;
    /// <summary><c>theme:</c> values.</summary>
    public static IReadOnlyList<string> Themes => Model.Tokens.Theme.Tokens;
    /// <summary><c>fit:</c> values.</summary>
    public static IReadOnlyList<string> Fits => Model.Tokens.Fit.Tokens;
    /// <summary>Node <c>variant:</c> values.</summary>
    public static IReadOnlyList<string> Variants => Model.Tokens.NodeVariant.Tokens;
    /// <summary>Edge <c>style:</c> values.</summary>
    public static IReadOnlyList<string> EdgeStyles => Model.Tokens.EdgeStyle.Tokens;
    /// <summary>Edge <c>curve:</c> values.</summary>
    public static IReadOnlyList<string> Curves => Model.Tokens.EdgeCurve.Tokens;
    /// <summary>Edge <c>arrow:</c> values (plus the <c>true</c>/<c>false</c> shorthands the parser accepts).</summary>
    public static IReadOnlyList<string> Arrows => Model.Tokens.ArrowEnds.Tokens.Concat(Bool).ToArray();
    /// <summary>Edge side pins (<c>fromSide</c>/<c>toSide</c>).</summary>
    public static IReadOnlyList<string> Sides => Model.Tokens.Side.Tokens;
    /// <summary>Boolean values.</summary>
    public static IReadOnlyList<string> Bool { get; } = ["true", "false"];
    /// <summary>Built-in <c>meta.style</c> names (sourced from the engine registry, never drifts).</summary>
    public static IReadOnlyList<string> StyleNames { get; } = BeckStyles.All.Select(s => s.Name).ToArray();
    /// <summary>Named icon keys (an <c>icon:</c> may also be raw inline <c>&lt;svg&gt;</c>).</summary>
    public static IReadOnlyList<string> Icons { get; } = Svg.Icons.Registry.Keys.ToArray();
    /// <summary>Flow step discriminators (the key that opens each flow entry).</summary>
    public static IReadOnlyList<string> FlowSteps { get; } =
    [
        "packet", "burst", "status", "highlight", "pulse", "activate", "stream",
        "working", "idle", "fail", "narrate", "phase", "wait", "reset", "parallel",
    ];

    // ---- per-section key lists (curated against Model/Schema.cs) -------------
    /// <summary>Top-level document keys.</summary>
    public static IReadOnlyList<string> TopKeys { get; } = ["type", "meta", "nodes", "edges", "groups", "flow", "steps", "links", "root", "topics"];
    /// <summary><c>meta:</c> keys.</summary>
    public static IReadOnlyList<string> MetaKeys { get; } = ["title", "subtitle", "style", "direction", "theme", "animate", "loop", "fit", "spacing", "narrate",
    ];
    /// <summary>Node entry keys.</summary>
    public static IReadOnlyList<string> NodeKeys { get; } = ["id", "title", "subtitle", "icon", "kind", "variant", "status", "accent", "href", "target", "surface", "textColor", "width", "rank", "order", "group", "stereotype", "fields", "methods",
    ];
    /// <summary>Edge entry keys.</summary>
    public static IReadOnlyList<string> EdgeKeys { get; } = ["from", "to", "label", "style", "curve", "kind", "color", "arrow", "fromSide", "toSide",
    ];
    /// <summary>Group entry keys.</summary>
    public static IReadOnlyList<string> GroupKeys { get; } = ["id", "label", "members", "accent"];
    /// <summary>Flowchart <c>steps:</c> entry keys.</summary>
    public static IReadOnlyList<string> StepKeys { get; } = ["id", "text", "kind", "subtitle", "icon", "accent", "href", "target", "surface", "textColor", "width", "rank", "order",
    ];
    /// <summary>Flowchart <c>links:</c> entry keys.</summary>
    public static IReadOnlyList<string> LinkKeys { get; } = ["from", "to", "label", "style", "color", "note"];
    /// <summary>Flowchart step <c>kind:</c> values.</summary>
    public static IReadOnlyList<string> StepKinds { get; } = ["process", "decision", "terminator", "io", "start", "end"];
    /// <summary>Mind-map <c>root:</c> / <c>topics:</c> entry keys (a topic may also be a plain title string).</summary>
    public static IReadOnlyList<string> TopicKeys { get; } = ["id", "title", "subtitle", "accent", "icon", "items", "body", "children", "href", "target", "surface", "textColor", "width",
    ];

    /// <summary>Fields whose <em>value</em> is a declared node/group id — completed from the document.</summary>
    public static IReadOnlyList<string> IdValuedFields { get; } = ["from", "to", "via", "members", "node", "group"];

    /// <summary>
    /// The value vocabulary for a given field, or <c>null</c> when the field takes free text
    /// (or an id, handled separately via <see cref="IdValuedFields"/>). <paramref name="section"/>
    /// is the nearest top-level section (e.g. <c>edges</c>) so <c>kind</c> resolves correctly.
    /// </summary>
    public static IReadOnlyList<string>? ValuesFor(string field, string? section) => field switch
    {
        "type" => Types,
        "direction" => Directions,
        "theme" => Themes,
        "fit" => Fits,
        "variant" => Variants,
        // `style` is overloaded: meta.style names a visual style; edge.style is the line style.
        "style" => section == "meta" ? StyleNames : EdgeStyles,
        "curve" => Curves,
        "arrow" => Arrows,
        "icon" => Icons,
        "accent" or "color" => Accents,
        "fromSide" or "toSide" => Sides,
        "animate" or "loop" => Bool,
        "kind" => section switch { "edges" => EdgeKinds, "steps" => StepKinds, _ => Kinds },
        _ => null,
    };

    // ---- one-line docs (hover + completion detail) --------------------------
    /// <summary>Short markdown docs keyed by field name or token value.</summary>
    public static IReadOnlyDictionary<string, string> Docs { get; } = new Dictionary<string, string>
    {
        // sections
        ["type"] = "Root diagram type: `architecture`, `sequence`, `state`, `class`, `flowchart` or `mindmap`.",
        ["meta"] = "Diagram-wide settings — title, direction, theme, animation.",
        ["nodes"] = "The boxes in your diagram. Each needs an `id`; `title`/`kind`/`icon` are optional.",
        ["edges"] = "Connections between nodes. `from` + `to` are required.",
        ["groups"] = "Boundaries that wrap members (nodes or nested groups) in a labelled box.",
        ["flow"] = "A scripted animation: packets, highlights and status changes over time.",
        ["steps"] = "Flowchart steps. Each needs an `id`; `text`/`kind` are optional.",
        ["links"] = "Connections between flowchart steps. `from` + `to` are required.",
        ["root"] = "The centre topic of a mind map (a mapping, or a plain title string).",
        ["topics"] = "First-level mind-map branches, split left/right around the root.",
        ["children"] = "Nested child topics under this topic (arbitrary depth).",
        ["items"] = "Bulleted list rendered on the topic/node card.",
        ["body"] = "Wrapped paragraph rendered on the topic/node card.",
        // meta fields
        ["title"] = "Display name on the card / diagram heading.",
        ["subtitle"] = "Secondary line under the title.",
        ["direction"] = "Layout flow: `TB`, `BT`, `LR` or `RL`.",
        ["theme"] = "`auto`, `light` or `dark`.",
        ["animate"] = "Set `false` to render a static frame (no flow animation).",
        ["loop"] = "Set `false` to play the flow once instead of looping.",
        ["fit"] = "`shrink` scales to fit; `scroll` keeps full size and scrolls.",
        ["spacing"] = "Fine-tune `rank`/`node` gaps and `cornerRadius`.",
        ["narrate"] = "Caption pacing: `wpm`, `min`, `pad` reading-time knobs.",
        // node fields
        ["id"] = "Unique identifier — referenced by edges and group members.",
        ["kind"] = "Preset that picks an icon, accent and shape.",
        ["icon"] = "Named icon key (or inline `<svg>`). e.g. `db`, `gateway`, `brain`, `kafka`.",
        ["accent"] = "Colour token: primary, success, warn, danger, info, neutral.",
        ["variant"] = "Visual weight: `solid`, `subtle` or `ghost`.",
        ["status"] = "A small status pill shown on the card.",
        ["surface"] = "Override the card fill colour.",
        ["textColor"] = "Override the card text colour.",
        ["width"] = "Override the card width, in pixels.",
        ["rank"] = "Pin the node to a layout rank (layer).",
        ["order"] = "Nudge left/right order within a rank.",
        ["group"] = "Inline membership: place this node in the named group.",
        ["href"] = "Make the card a link to this URL.",
        ["target"] = "Link target, e.g. `_blank`.",
        ["stereotype"] = "Class stereotype line, e.g. `«interface»`.",
        ["fields"] = "Class attribute list (class diagrams).",
        ["methods"] = "Class operation list (class diagrams).",
        // edge fields
        ["from"] = "Source node (or group) id.",
        ["to"] = "Target node (or group) id.",
        ["label"] = "Text drawn on the edge (or the group box heading).",
        ["curve"] = "Edge shape: `step-round`, `straight` or `s`.",
        ["arrow"] = "Arrowheads: `none`, `end`, `start`, `both` (or `true`/`false`).",
        ["style"] = "Under `meta`: the visual style (e.g. `classic`). On an edge: line style `solid` or `dashed`.",
        ["color"] = "Edge stroke colour — an accent token or a CSS colour.",
        ["fromSide"] = "Pin the edge start to a side: top, bottom, left, right.",
        ["toSide"] = "Pin the edge end to a side: top, bottom, left, right.",
        // group fields
        ["members"] = "Ids of the nodes (or nested groups) this group contains.",
        // value docs
        ["architecture"] = "The layered node/edge graph (default).",
        ["sequence"] = "Participants exchanging messages over time.",
        ["state"] = "States and transitions.",
        ["class"] = "Classes with compartments and UML relations.",
        ["flowchart"] = "Process/decision flow: steps and links.",
        ["mindmap"] = "A nested topic tree drawn as a two-sided butterfly around a central root.",
        ["text"] = "Step display text — defaults to the step `id`.",
        ["process"] = "A rectangular process step (default).",
        ["decision"] = "A diamond branch point.",
        ["terminator"] = "A pill-shaped terminator step.",
        ["io"] = "A parallelogram input/output step.",
        ["start"] = "A start terminator step.",
        ["end"] = "An end terminator step.",
        ["service"] = "A generic service box.",
        ["db"] = "A database (cylinder).",
        ["queue"] = "A message queue.",
        ["cache"] = "A cache store.",
        ["gateway"] = "An API gateway / entry point.",
        ["external"] = "A third-party / external system.",
        ["user"] = "A person or client.",
        ["ghost"] = "A faded placeholder node.",
        ["data"] = "Solid data edge (default).",
        ["control"] = "A control-flow edge.",
        ["async"] = "A dashed asynchronous edge.",
        ["dependency"] = "A dashed dependency edge.",
        ["TB"] = "Top → bottom.",
        ["BT"] = "Bottom → top.",
        ["LR"] = "Left → right.",
        ["RL"] = "Right → left.",
        ["classic"] = "The default Beck look (unchanged when no `meta.style` is set).",
        // built-in visual styles (meta.style) — sourced as names from BeckStyles.All
        ["minimal"] = "Sober flat look: hairline borders, no shadows, a single travelling dot; rings off.",
        ["terminal"] = "Monospace everything with `[bracketed]` labels, square packets and a green-ramp accent.",
        ["blueprint"] = "Technical drawing: faint grid surface, dashed edges and dimension ticks on groups.",
        ["glow"] = "Luminous: gradient edges, soft packet bloom and a breathing pulse on active nodes.",
        ["editorial"] = "Serif textbook figure: hairlines, no fills, numbered `Fig. N —` captions, slow draw-on.",
        ["brutalist"] = "Heavy strokes, a hard blur-free offset shadow, uppercase type and stepped motion.",
        ["sketch"] = "Hand-drawn: wobbly outlines baked deterministically from the content hash.",
        ["extrude"] = "2.5D slabs with static depth faces; active nodes press down toward the base.",
        ["circuit"] = "PCB look: chip pin stubs on nodes and via dots at every edge bend.",
        ["metro"] = "Transit map: thick line edges, white station dots at endpoints and train packets.",
    };
}