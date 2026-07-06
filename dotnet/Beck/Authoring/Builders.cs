using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Beck;

/// <summary>Fluent builder for a diagram node.</summary>
public sealed class NodeBuilder
{
    private readonly string _id;
    private string? _title;
    private string? _subtitle;
    private string? _icon;
    private string? _status;
    private string? _accent;
    private string? _group;
    private string? _href;
    private string? _target;
    private string? _surface;
    private string? _textColor;
    private NodeKind? _kind;
    private NodeVariant? _variant;
    private int? _width;
    private int? _rank;
    private int? _order;

    internal NodeBuilder(string id) => _id = id;

    /// <summary>The node's id.</summary>
    internal string Id => _id;

    /// <summary>Set the display title (defaults to the id).</summary>
    public NodeBuilder Title(string title) { _title = title; return this; }

    /// <summary>Set the muted subtitle line.</summary>
    public NodeBuilder Subtitle(string subtitle) { _subtitle = subtitle; return this; }

    /// <summary>Set a named icon key or raw inline <c>&lt;svg&gt;</c> markup.</summary>
    public NodeBuilder Icon(string icon) { _icon = icon; return this; }

    /// <summary>Set the node archetype (default <see cref="NodeKind.Service"/>).</summary>
    public NodeBuilder Kind(NodeKind kind) { _kind = kind; return this; }

    /// <summary>Set the visual weight.</summary>
    public NodeBuilder Variant(NodeVariant variant) { _variant = variant; return this; }

    /// <summary>Set the initial status-pill text.</summary>
    public NodeBuilder Status(string status) { _status = status; return this; }

    /// <summary>Set the accent to a semantic token.</summary>
    public NodeBuilder Accent(AccentToken token) { _accent = Tokens.Of(token); return this; }

    /// <summary>Set the accent to a raw color (e.g. a hex string).</summary>
    public NodeBuilder Accent(string color) { _accent = color; return this; }

    /// <summary>Fix the card width in pixels (prevents reflow on status change).</summary>
    public NodeBuilder Width(int px) { _width = px; return this; }

    /// <summary>Force the node into a specific layout rank.</summary>
    public NodeBuilder Rank(int rank) { _rank = rank; return this; }

    /// <summary>Tie-break order within the rank.</summary>
    public NodeBuilder Order(int order) { _order = order; return this; }

    /// <summary>Assign the node to a group inline (alternative to group members).</summary>
    public NodeBuilder Group(string groupId) { _group = groupId; return this; }

    /// <summary>Make the card a link. Pass <c>target: "_blank"</c> to open in a new tab.</summary>
    public NodeBuilder Link(string href, string? target = null) { _href = href; _target = target; return this; }

    /// <summary>Override the card background (a CSS color).</summary>
    public NodeBuilder Surface(string color) { _surface = color; return this; }

    /// <summary>Override the card text color (a CSS color).</summary>
    public NodeBuilder TextColor(string color) { _textColor = color; return this; }

    internal string ToFlow()
    {
        var pairs = new List<(string, string)> { ("id", YamlWriter.Scalar(_id)) };
        if (_title != null) pairs.Add(("title", YamlWriter.Scalar(_title)));
        if (_subtitle != null) pairs.Add(("subtitle", YamlWriter.Scalar(_subtitle)));
        if (_kind is { } k) pairs.Add(("kind", Tokens.Of(k)));
        if (_variant is { } v) pairs.Add(("variant", Tokens.Of(v)));
        if (_icon != null) pairs.Add(("icon", YamlWriter.Scalar(_icon)));
        if (_status != null) pairs.Add(("status", YamlWriter.Scalar(_status)));
        if (_accent != null) pairs.Add(("accent", YamlWriter.Scalar(_accent)));
        if (_href != null) pairs.Add(("href", YamlWriter.Scalar(_href)));
        if (_target != null) pairs.Add(("target", YamlWriter.Scalar(_target)));
        if (_surface != null) pairs.Add(("surface", YamlWriter.Scalar(_surface)));
        if (_textColor != null) pairs.Add(("textColor", YamlWriter.Scalar(_textColor)));
        if (_width is { } w) pairs.Add(("width", w.ToString(CultureInfo.InvariantCulture)));
        if (_rank is { } r) pairs.Add(("rank", r.ToString(CultureInfo.InvariantCulture)));
        if (_order is { } o) pairs.Add(("order", o.ToString(CultureInfo.InvariantCulture)));
        if (_group != null) pairs.Add(("group", YamlWriter.Scalar(_group)));
        return YamlWriter.FlowMap(pairs);
    }
}

/// <summary>Fluent builder for a node group (a labelled, boxed cluster).</summary>
public sealed class GroupBuilder
{
    private readonly string _id;
    private readonly List<string> _members = new();
    private string? _label;
    private string? _accent;

    internal GroupBuilder(string id) => _id = id;

    /// <summary>The group's id.</summary>
    internal string Id => _id;

    /// <summary>Set the group label.</summary>
    public GroupBuilder Label(string label) { _label = label; return this; }

    /// <summary>Add member node ids.</summary>
    public GroupBuilder Members(params string[] ids) { _members.AddRange(ids); return this; }

    /// <summary>Add a single member node id.</summary>
    public GroupBuilder Member(string id) { _members.Add(id); return this; }

    /// <summary>Set the accent to a semantic token.</summary>
    public GroupBuilder Accent(AccentToken token) { _accent = Tokens.Of(token); return this; }

    /// <summary>Set the accent to a raw color.</summary>
    public GroupBuilder Accent(string color) { _accent = color; return this; }

    internal string ToFlow()
    {
        var pairs = new List<(string, string)> { ("id", YamlWriter.Scalar(_id)) };
        if (_label != null) pairs.Add(("label", YamlWriter.Scalar(_label)));
        pairs.Add(("members", YamlWriter.FlowSeq(_members.Select(YamlWriter.Scalar))));
        if (_accent != null) pairs.Add(("accent", YamlWriter.Scalar(_accent)));
        return YamlWriter.FlowMap(pairs);
    }
}

/// <summary>Fluent builder for an edge between two nodes (or groups).</summary>
public sealed class EdgeBuilder
{
    private readonly string _from;
    private readonly string _to;
    private string? _label;
    private string? _color;
    private EdgeStyle? _style;
    private EdgeCurve? _curve;
    private EdgeKind? _kind;
    private string? _arrow;
    private string? _note;
    private Side? _fromSide;
    private Side? _toSide;

    internal EdgeBuilder(string from, string to)
    {
        _from = from;
        _to = to;
    }

    /// <summary>The edge's source endpoint id.</summary>
    internal string From => _from;

    /// <summary>The edge's target endpoint id.</summary>
    internal string To => _to;

    /// <summary>Set the edge label.</summary>
    public EdgeBuilder Label(string label) { _label = label; return this; }

    /// <summary>Set the line style.</summary>
    public EdgeBuilder Style(EdgeStyle style) { _style = style; return this; }

    /// <summary>Set the routing curve.</summary>
    public EdgeBuilder Curve(EdgeCurve curve) { _curve = curve; return this; }

    /// <summary>Set the semantic edge kind (defaults color + style).</summary>
    public EdgeBuilder Kind(EdgeKind kind) { _kind = kind; return this; }

    /// <summary>Set the stroke to a semantic token.</summary>
    public EdgeBuilder Color(AccentToken token) { _color = Tokens.Of(token); return this; }

    /// <summary>Set the stroke to a raw color.</summary>
    public EdgeBuilder Color(string color) { _color = color; return this; }

    /// <summary>Toggle the arrowhead (default on). <c>false</c> draws no arrows.</summary>
    public EdgeBuilder Arrow(bool arrow) { _arrow = arrow ? "end" : "none"; return this; }

    /// <summary>Choose which ends carry an arrowhead (e.g. <see cref="ArrowEnds.Both"/>).</summary>
    public EdgeBuilder Arrows(ArrowEnds ends) { _arrow = Tokens.Of(ends); return this; }

    /// <summary>Narrate this edge: the text becomes a caption just before the edge's
    /// packet in an auto-derived flow (ignored when an explicit <c>Flow</c> is scripted).</summary>
    public EdgeBuilder Note(string note) { _note = note; return this; }

    /// <summary>Pin the edge's exit side on the source node.</summary>
    public EdgeBuilder FromSide(Side side) { _fromSide = side; return this; }

    /// <summary>Pin the edge's entry side on the target node.</summary>
    public EdgeBuilder ToSide(Side side) { _toSide = side; return this; }

    internal string ToFlow()
    {
        var pairs = new List<(string, string)>
        {
            ("from", YamlWriter.Scalar(_from)),
            ("to", YamlWriter.Scalar(_to)),
        };
        if (_label != null) pairs.Add(("label", YamlWriter.Scalar(_label)));
        if (_style is { } s) pairs.Add(("style", Tokens.Of(s)));
        if (_curve is { } c) pairs.Add(("curve", Tokens.Of(c)));
        if (_kind is { } k) pairs.Add(("kind", Tokens.Of(k)));
        if (_color != null) pairs.Add(("color", YamlWriter.Scalar(_color)));
        if (_arrow != null) pairs.Add(("arrow", _arrow));
        if (_note != null) pairs.Add(("note", YamlWriter.Scalar(_note)));
        if (_fromSide is { } fs) pairs.Add(("fromSide", Tokens.Of(fs)));
        if (_toSide is { } ts) pairs.Add(("toSide", Tokens.Of(ts)));
        return YamlWriter.FlowMap(pairs);
    }
}
