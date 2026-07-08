using System.Collections.Generic;
using System.Linq;
using Beck.Rendering;

namespace Beck;

/// <summary>
/// Authoring-time schema metadata for Beck YAML — the token vocabularies, per-section
/// key lists, and one-line docs that power editor completions and hover in tooling
/// (the docs playground's Monaco editor). The value vocabularies are sourced from the
/// engine's own <see cref="Rendering.Tokens"/> maps and icon registry, so they never drift from
/// what the parser actually accepts; the key lists and docs are curated here to match
/// <c>Model/Schema.cs</c>.
/// </summary>
public static class BeckSchema
{
    // ---- value vocabularies (sourced from the engine — always in sync) ------
    /// <summary>Root <c>type:</c> values.</summary>
    public static IReadOnlyList<string> Types => Rendering.Tokens.DiagramType.Tokens;
    /// <summary>Node <c>kind:</c> presets.</summary>
    public static IReadOnlyList<string> Kinds => Rendering.Tokens.NodeKind.Tokens;
    /// <summary>Edge <c>kind:</c> values.</summary>
    public static IReadOnlyList<string> EdgeKinds => Rendering.Tokens.EdgeKind.Tokens;
    /// <summary>Accent / colour tokens.</summary>
    public static IReadOnlyList<string> Accents => Rendering.Tokens.Accent.Tokens;
    /// <summary>Layout <c>direction:</c> values.</summary>
    public static IReadOnlyList<string> Directions => Rendering.Tokens.Direction.Tokens;
    /// <summary><c>theme:</c> values.</summary>
    public static IReadOnlyList<string> Themes => Rendering.Tokens.Theme.Tokens;
    /// <summary><c>fit:</c> values.</summary>
    public static IReadOnlyList<string> Fits => Rendering.Tokens.Fit.Tokens;
    /// <summary>Node <c>variant:</c> values.</summary>
    public static IReadOnlyList<string> Variants => Rendering.Tokens.NodeVariant.Tokens;
    /// <summary>Edge <c>style:</c> values.</summary>
    public static IReadOnlyList<string> EdgeStyles => Rendering.Tokens.EdgeStyle.Tokens;
    /// <summary>Edge <c>curve:</c> values.</summary>
    public static IReadOnlyList<string> Curves => Rendering.Tokens.EdgeCurve.Tokens;
    /// <summary>Edge <c>arrow:</c> values (plus the <c>true</c>/<c>false</c> shorthands the parser accepts).</summary>
    public static IReadOnlyList<string> Arrows => Rendering.Tokens.ArrowEnds.Tokens.Concat(Bool).ToArray();
    /// <summary>Edge side pins (<c>fromSide</c>/<c>toSide</c>).</summary>
    public static IReadOnlyList<string> Sides => Rendering.Tokens.Side.Tokens;
    /// <summary>Boolean values.</summary>
    public static IReadOnlyList<string> Bool { get; } = new[] { "true", "false" };
    /// <summary>Named icon keys (an <c>icon:</c> may also be raw inline <c>&lt;svg&gt;</c>).</summary>
    public static IReadOnlyList<string> Icons { get; } = Rendering.Svg.Icons.Registry.Keys.ToArray();
    /// <summary>Flow step discriminators (the key that opens each flow entry).</summary>
    public static IReadOnlyList<string> FlowSteps { get; } = new[]
    {
        "packet", "burst", "status", "highlight", "pulse", "activate", "stream",
        "working", "idle", "fail", "narrate", "phase", "wait", "reset", "parallel",
    };

    // ---- per-section key lists (curated against Model/Schema.cs) -------------
    /// <summary>Top-level document keys.</summary>
    public static IReadOnlyList<string> TopKeys { get; } = new[] { "type", "meta", "nodes", "edges", "groups", "flow" };
    /// <summary><c>meta:</c> keys.</summary>
    public static IReadOnlyList<string> MetaKeys { get; } = new[] { "title", "subtitle", "direction", "theme", "animate", "loop", "fit", "spacing", "narration" };
    /// <summary>Node entry keys.</summary>
    public static IReadOnlyList<string> NodeKeys { get; } = new[] { "id", "title", "subtitle", "icon", "kind", "variant", "status", "accent", "href", "target", "surface", "textColor", "width", "rank", "order", "group", "stereotype", "fields", "methods" };
    /// <summary>Edge entry keys.</summary>
    public static IReadOnlyList<string> EdgeKeys { get; } = new[] { "from", "to", "label", "style", "curve", "kind", "color", "arrow", "fromSide", "toSide" };
    /// <summary>Group entry keys.</summary>
    public static IReadOnlyList<string> GroupKeys { get; } = new[] { "id", "label", "members", "accent" };

    /// <summary>Fields whose <em>value</em> is a declared node/group id — completed from the document.</summary>
    public static IReadOnlyList<string> IdValuedFields { get; } = new[] { "from", "to", "via", "members", "node", "group" };

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
        "style" => EdgeStyles,
        "curve" => Curves,
        "arrow" => Arrows,
        "icon" => Icons,
        "accent" or "color" => Accents,
        "fromSide" or "toSide" => Sides,
        "animate" or "loop" => Bool,
        "kind" => section == "edges" ? EdgeKinds : Kinds,
        _ => null,
    };

    // ---- one-line docs (hover + completion detail) --------------------------
    /// <summary>Short markdown docs keyed by field name or token value.</summary>
    public static IReadOnlyDictionary<string, string> Docs { get; } = new Dictionary<string, string>
    {
        // sections
        ["type"] = "Root diagram type: `architecture`, `sequence`, `state` or `class`.",
        ["meta"] = "Diagram-wide settings — title, direction, theme, animation.",
        ["nodes"] = "The boxes in your diagram. Each needs an `id`; `title`/`kind`/`icon` are optional.",
        ["edges"] = "Connections between nodes. `from` + `to` are required.",
        ["groups"] = "Boundaries that wrap members (nodes or nested groups) in a labelled box.",
        ["flow"] = "A scripted animation: packets, highlights and status changes over time.",
        // meta fields
        ["title"] = "Display name on the card / diagram heading.",
        ["subtitle"] = "Secondary line under the title.",
        ["direction"] = "Layout flow: `TB`, `BT`, `LR` or `RL`.",
        ["theme"] = "`auto`, `light` or `dark`.",
        ["animate"] = "Set `false` to render a static frame (no flow animation).",
        ["loop"] = "Set `false` to play the flow once instead of looping.",
        ["fit"] = "`shrink` scales to fit; `scroll` keeps full size and scrolls.",
        ["spacing"] = "Fine-tune `rank`/`node` gaps and `cornerRadius`.",
        ["narration"] = "Caption pacing: `wpm`, `min`, `pad` reading-time knobs.",
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
        ["style"] = "Line style: `solid` or `dashed`.",
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
        ["service"] = "A generic service box.", ["db"] = "A database (cylinder).", ["queue"] = "A message queue.",
        ["cache"] = "A cache store.", ["gateway"] = "An API gateway / entry point.", ["external"] = "A third-party / external system.",
        ["user"] = "A person or client.", ["ghost"] = "A faded placeholder node.",
        ["data"] = "Solid data edge (default).", ["control"] = "A control-flow edge.", ["async"] = "A dashed asynchronous edge.",
        ["dependency"] = "A dashed dependency edge.",
        ["TB"] = "Top → bottom.", ["BT"] = "Bottom → top.", ["LR"] = "Left → right.", ["RL"] = "Right → left.",
    };
}
