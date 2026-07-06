using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Beck;

/// <summary>
/// Builds a Beck diagram from code and emits it as YAML (or a fenced
/// <c>```beck</c> Markdown block). Dependency-free — any program can turn its
/// own model (an Aspire app graph, an EF model, a service registry, …) into a
/// diagram by walking it into a <see cref="DiagramBuilder"/>.
/// </summary>
/// <example>
/// <code>
/// var fence = new DiagramBuilder("Web Platform")
///     .Direction(Direction.TB)
///     .Node("web", n => n.Title("Web App").Kind(NodeKind.User))
///     .Node("api", "API Server")
///     .Node("db", n => n.Title("Postgres").Kind(NodeKind.Db))
///     .Edge("web", "api")
///     .Edge("api", "db", e => e.Label("query"))
///     .ToFence();
/// </code>
/// </example>
public sealed class DiagramBuilder
{
    private readonly List<NodeBuilder> _nodes = new();
    private readonly List<GroupBuilder> _groups = new();
    private readonly List<EdgeBuilder> _edges = new();
    private readonly MetaOptions _meta = new();
    private FlowBuilder? _flow;

    /// <summary>Create an empty diagram.</summary>
    public DiagramBuilder() { }

    /// <summary>Create a diagram with a title.</summary>
    public DiagramBuilder(string title) => _meta.Title = title;

    /// <summary>Set the diagram title.</summary>
    public DiagramBuilder Title(string title) { _meta.Title = title; return this; }

    /// <summary>Set the diagram subtitle.</summary>
    public DiagramBuilder Subtitle(string subtitle) { _meta.Subtitle = subtitle; return this; }

    /// <summary>Set the layout direction.</summary>
    public DiagramBuilder Direction(Direction direction) { _meta.Direction = direction; return this; }

    /// <summary>Set the theme mode.</summary>
    public DiagramBuilder Theme(ThemeMode theme) { _meta.Theme = theme; return this; }

    /// <summary>Enable or disable animation.</summary>
    public DiagramBuilder Animate(bool animate) { _meta.Animate = animate; return this; }

    /// <summary>Enable or disable looping.</summary>
    public DiagramBuilder Loop(bool loop) { _meta.Loop = loop; return this; }

    /// <summary>How the diagram behaves when wider than its container: <see cref="FitMode.Shrink"/> scales it down to fit (default); <see cref="FitMode.Scroll"/> keeps natural size and scrolls horizontally.</summary>
    public DiagramBuilder Fit(FitMode fit) { _meta.Fit = fit; return this; }

    /// <summary>Toggle + tune the narration caption under the diagram. Captions appear
    /// only where the flow supplies them (a <see cref="FlowBuilder.Narrate"/> step or an
    /// <see cref="EdgeBuilder.Note"/>); the pacing knobs turn each caption's length into
    /// how long it lingers: <paramref name="wpm"/> reading speed, <paramref name="min"/>
    /// floor seconds, <paramref name="pad"/> extra seconds.</summary>
    public DiagramBuilder Narrate(bool enabled = true, int? wpm = null, double? min = null, double? pad = null)
    {
        _meta.Narrate = enabled;
        if (wpm is { } w) _meta.NarrateWpm = w;
        if (min is { } m) _meta.NarrateMin = m;
        if (pad is { } p) _meta.NarratePad = p;
        return this;
    }

    /// <summary>Tune layout spacing: rank gap (along the flow), node gap (across), and corner radius (px).</summary>
    public DiagramBuilder Spacing(int? rank = null, int? node = null, int? cornerRadius = null)
    {
        if (rank is { } r) _meta.SpacingRank = r;
        if (node is { } n) _meta.SpacingNode = n;
        if (cornerRadius is { } c) _meta.SpacingCornerRadius = c;
        return this;
    }

    /// <summary>Script the animation flow (packets, status, effects). Without this the engine auto-derives one.</summary>
    public DiagramBuilder Flow(Action<FlowBuilder> configure)
    {
        _flow ??= new FlowBuilder();
        configure(_flow);
        return this;
    }

    /// <summary>Add a node, configured via a builder callback.</summary>
    public DiagramBuilder Node(string id, Action<NodeBuilder>? configure = null)
    {
        var node = new NodeBuilder(id);
        configure?.Invoke(node);
        _nodes.Add(node);
        return this;
    }

    /// <summary>Add a node with a title and optional kind.</summary>
    public DiagramBuilder Node(string id, string title, NodeKind? kind = null)
    {
        var node = new NodeBuilder(id).Title(title);
        if (kind is { } k) node.Kind(k);
        _nodes.Add(node);
        return this;
    }

    /// <summary>Add a group, configured via a builder callback.</summary>
    public DiagramBuilder Group(string id, Action<GroupBuilder> configure)
    {
        var group = new GroupBuilder(id);
        configure(group);
        _groups.Add(group);
        return this;
    }

    /// <summary>Add an edge from one node/group to another.</summary>
    public DiagramBuilder Edge(string from, string to, Action<EdgeBuilder>? configure = null)
    {
        var edge = new EdgeBuilder(from, to);
        configure?.Invoke(edge);
        _edges.Add(edge);
        return this;
    }

    /// <summary>Render the diagram as Beck YAML.</summary>
    /// <exception cref="InvalidOperationException">
    /// An edge references a <c>from</c>/<c>to</c> id that is not a declared node or group.
    /// Surfacing this here gives a clear C# error instead of an opaque parser failure that
    /// blanks the whole diagram in the browser.
    /// </exception>
    public string ToYaml()
    {
        ValidateEdgeEndpoints();

        var sb = new StringBuilder();
        sb.Append("type: architecture\n");
        _meta.AppendYaml(sb);

        sb.Append("nodes:\n");
        foreach (var node in _nodes)
            sb.Append("  - ").Append(node.ToFlow()).Append('\n');

        if (_groups.Count > 0)
        {
            sb.Append("groups:\n");
            foreach (var group in _groups)
                sb.Append("  - ").Append(group.ToFlow()).Append('\n');
        }

        if (_edges.Count > 0)
        {
            sb.Append("edges:\n");
            foreach (var edge in _edges)
                sb.Append("  - ").Append(edge.ToFlow()).Append('\n');
        }

        _flow?.AppendYaml(sb);

        return sb.ToString();
    }

    /// <summary>Every edge endpoint must resolve to a declared node or group id.</summary>
    private void ValidateEdgeEndpoints()
    {
        if (_edges.Count == 0) return;

        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in _nodes) ids.Add(node.Id);
        foreach (var group in _groups) ids.Add(group.Id);

        foreach (var edge in _edges)
        {
            if (!ids.Contains(edge.From))
                throw new InvalidOperationException(
                    $"Edge references unknown source '{edge.From}' — declare it as a node or group before emitting.");
            if (!ids.Contains(edge.To))
                throw new InvalidOperationException(
                    $"Edge references unknown target '{edge.To}' — declare it as a node or group before emitting.");
        }
    }

    /// <summary>Render the diagram as a fenced <c>```beck</c> Markdown block.</summary>
    public string ToFence() => BeckMarkdown.Fence(ToYaml());

    /// <inheritdoc/>
    public override string ToString() => ToYaml();
}
