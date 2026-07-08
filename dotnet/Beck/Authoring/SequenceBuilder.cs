using System;
using System.Collections.Generic;
using System.Text;

namespace Beck;

/// <summary>
/// Builds a <c>type: sequence</c> Beck diagram — participants become columns,
/// messages become rows in call order. Request/reply pairs (a
/// <see cref="Message"/> answered by a <see cref="Reply"/>) grow activation
/// bars on the receiver automatically; <see cref="Section"/> inserts a labelled
/// band. Without an explicit flow the message order is the animation.
/// </summary>
/// <example>
/// <code>
/// var fence = new SequenceDiagramBuilder("Checkout")
///     .Participant("web", "Web App", NodeKind.User)
///     .Participant("api", "Orders API")
///     .Participant("db", p => p.Title("Postgres").Kind(NodeKind.Db))
///     .Message("web", "api", "POST /orders")
///     .Message("api", "db", "INSERT order")
///     .Reply("db", "api", "ok")
///     .Reply("api", "web", "201 Created")
///     .ToFence();
/// </code>
/// </example>
public sealed class SequenceDiagramBuilder
{
    private readonly List<NodeBuilder> _participants = new();
    private readonly List<string> _messages = new();
    private readonly HashSet<string> _ids = new(StringComparer.Ordinal);
    private readonly MetaOptions _meta = new();
    private FlowBuilder? _flow;

    /// <summary>Create an empty sequence diagram.</summary>
    public SequenceDiagramBuilder() { }

    /// <summary>Create a sequence diagram with a title.</summary>
    public SequenceDiagramBuilder(string title) => _meta.Title = title;

    /// <summary>Set the diagram title.</summary>
    public SequenceDiagramBuilder Title(string title) { _meta.Title = title; return this; }

    /// <summary>Set the diagram subtitle.</summary>
    public SequenceDiagramBuilder Subtitle(string subtitle) { _meta.Subtitle = subtitle; return this; }

    /// <summary>Set the visual style by its <c>meta.style</c> token (e.g. <c>"classic"</c>).</summary>
    public SequenceDiagramBuilder Style(string name) { _meta.Style = name; return this; }

    /// <summary>Set the visual style from a <see cref="BeckStyle"/> (emits its <see cref="BeckStyle.Name"/>).</summary>
    public SequenceDiagramBuilder Style(BeckStyle style) { _meta.Style = style.Name; return this; }

    /// <summary>Set the theme: <see cref="ThemeMode.Auto"/> (default), <see cref="ThemeMode.Light"/>, or <see cref="ThemeMode.Dark"/>.</summary>
    public SequenceDiagramBuilder Theme(ThemeMode theme) { _meta.Theme = theme; return this; }

    /// <summary>Enable or disable the flow animation.</summary>
    public SequenceDiagramBuilder Animate(bool animate) { _meta.Animate = animate; return this; }

    /// <summary>Loop the flow (default) or play it through once.</summary>
    public SequenceDiagramBuilder Loop(bool loop) { _meta.Loop = loop; return this; }

    /// <summary>How the diagram behaves when wider than its container.</summary>
    public SequenceDiagramBuilder Fit(FitMode fit) { _meta.Fit = fit; return this; }

    /// <summary>Toggle + tune the narration caption. Captions come from a message
    /// <see cref="MessageBuilder.Note"/> (or explicit <see cref="FlowBuilder.Narrate"/> steps);
    /// the knobs pace each caption's on-screen time by its length.</summary>
    public SequenceDiagramBuilder Narrate(bool enabled = true, int? wpm = null, double? min = null, double? pad = null)
    {
        _meta.Narrate = enabled;
        if (wpm is { } w) _meta.NarrateWpm = w;
        if (min is { } m) _meta.NarrateMin = m;
        if (pad is { } p) _meta.NarratePad = p;
        return this;
    }

    /// <summary>Tune spacing: <paramref name="node"/> is the minimum gap between lifelines.</summary>
    public SequenceDiagramBuilder Spacing(int? rank = null, int? node = null, int? cornerRadius = null)
    {
        if (rank is { } r) _meta.SpacingRank = r;
        if (node is { } n) _meta.SpacingNode = n;
        if (cornerRadius is { } c) _meta.SpacingCornerRadius = c;
        return this;
    }

    /// <summary>Add a participant (a lifeline column) and configure it via <see cref="NodeBuilder"/> — participants take the same fields as architecture nodes.</summary>
    public SequenceDiagramBuilder Participant(string id, Action<NodeBuilder>? configure = null)
    {
        var p = new NodeBuilder(id);
        configure?.Invoke(p);
        _participants.Add(p);
        _ids.Add(id);
        return this;
    }

    /// <summary>The terse overload: add a participant with a title and an optional kind.</summary>
    public SequenceDiagramBuilder Participant(string id, string title, NodeKind? kind = null)
    {
        var p = new NodeBuilder(id).Title(title);
        if (kind is { } k) p.Kind(k);
        _participants.Add(p);
        _ids.Add(id);
        return this;
    }

    /// <summary>Send a message. <paramref name="from"/> equal to <paramref name="to"/> draws a self-message loop.</summary>
    /// <exception cref="InvalidOperationException">An endpoint isn't a declared participant — a typo fails loudly in C# instead of blanking the diagram.</exception>
    public SequenceDiagramBuilder Message(string from, string to, string? label = null, Action<MessageBuilder>? configure = null)
        => Add(from, to, label, reply: false, configure);

    /// <summary>Send a reply — dashed with an open arrowhead; closes the receiver's activation bar.</summary>
    /// <exception cref="InvalidOperationException">An endpoint isn't a declared participant.</exception>
    public SequenceDiagramBuilder Reply(string from, string to, string? label = null, Action<MessageBuilder>? configure = null)
        => Add(from, to, label, reply: true, configure);

    /// <summary>Insert a labelled full-width section band before the next message.
    /// The band runs until the next section (or the last message); the optional
    /// <paramref name="accent"/> tints its border, fill, and label. Each section
    /// also becomes a seekable <c>phase</c>.</summary>
    public SequenceDiagramBuilder Section(string label, AccentToken? accent = null)
    {
        var pairs = new List<(string, string)> { ("section", YamlWriter.Scalar(label)) };
        if (accent is { } a) pairs.Add(("accent", Tokens.Of(a)));
        _messages.Add(YamlWriter.FlowMap(pairs));
        return this;
    }

    /// <summary>Script the animation flow explicitly. Without this the engine animates the message order.</summary>
    public SequenceDiagramBuilder Flow(Action<FlowBuilder> configure)
    {
        _flow ??= new FlowBuilder();
        configure(_flow);
        return this;
    }

    private SequenceDiagramBuilder Add(string from, string to, string? label, bool reply, Action<MessageBuilder>? configure)
    {
        Require(from);
        Require(to);
        var m = new MessageBuilder(from, to, reply);
        if (label != null) m.Label(label);
        configure?.Invoke(m);
        _messages.Add(m.ToFlow());
        return this;
    }

    private void Require(string id)
    {
        if (!_ids.Contains(id))
            throw new InvalidOperationException(
                $"Message references unknown participant '{id}' — declare it with Participant() before sending.");
    }

    /// <summary>Render the diagram as Beck YAML.</summary>
    /// <exception cref="InvalidOperationException">The diagram has no participants or no messages.</exception>
    public string ToYaml()
    {
        if (_participants.Count == 0)
            throw new InvalidOperationException("A sequence diagram needs at least one Participant().");
        if (_messages.Count == 0)
            throw new InvalidOperationException("A sequence diagram needs at least one Message().");

        var sb = new StringBuilder();
        sb.Append("type: sequence\n");
        _meta.AppendYaml(sb);
        sb.Append("participants:\n");
        foreach (var p in _participants) sb.Append("  - ").Append(p.ToFlow()).Append('\n');
        sb.Append("messages:\n");
        foreach (var m in _messages) sb.Append("  - ").Append(m).Append('\n');
        _flow?.AppendYaml(sb);
        return sb.ToString();
    }

    /// <summary>Render as a fenced <c>```beck</c> Markdown block — drop it into any Markdown page and it renders to a static SVG.</summary>
    public string ToFence() => BeckMarkdown.Fence(ToYaml());

    /// <inheritdoc/>
    public override string ToString() => ToYaml();
}

/// <summary>Configures one sequence message inside a <c>Message(from, to, label, m => …)</c> callback.</summary>
public sealed class MessageBuilder
{
    private readonly string _from;
    private readonly string _to;
    private readonly bool _reply;
    private string? _label;
    private string? _color;
    private string? _note;
    private EdgeKind? _kind;
    private EdgeStyle? _style;
    private bool? _activate;

    internal MessageBuilder(string from, string to, bool reply)
    {
        _from = from;
        _to = to;
        _reply = reply;
    }

    /// <summary>Set the message label (drawn above the arrow).</summary>
    public MessageBuilder Label(string label) { _label = label; return this; }

    /// <summary>Set the semantic kind — <see cref="EdgeKind.Async"/> renders dashed with an open arrowhead and eased packet motion.</summary>
    public MessageBuilder Kind(EdgeKind kind) { _kind = kind; return this; }

    /// <summary>Override the line style: <see cref="EdgeStyle.Solid"/> or <see cref="EdgeStyle.Dashed"/>.</summary>
    public MessageBuilder Style(EdgeStyle style) { _style = style; return this; }

    /// <summary>Set the stroke to a semantic token (follows the theme).</summary>
    public MessageBuilder Color(AccentToken token) { _color = Tokens.Of(token); return this; }

    /// <summary>Set the stroke to a raw CSS color.</summary>
    public MessageBuilder Color(string color) { _color = color; return this; }

    /// <summary>Force (<c>true</c>) or suppress (<c>false</c>) an activation bar on the receiver.</summary>
    public MessageBuilder Activate(bool activate) { _activate = activate; return this; }

    /// <summary>Narrate this message: the text becomes a caption just before the message
    /// fires (in the derived flow), while the sequence choreography still plays.</summary>
    public MessageBuilder Note(string note) { _note = note; return this; }

    internal string ToFlow()
    {
        var pairs = new List<(string, string)>
        {
            ("from", YamlWriter.Scalar(_from)),
            ("to", YamlWriter.Scalar(_to)),
        };
        if (_label != null) pairs.Add(("label", YamlWriter.Scalar(_label)));
        if (_reply) pairs.Add(("reply", "true"));
        if (_kind is { } k) pairs.Add(("kind", Tokens.Of(k)));
        if (_style is { } s) pairs.Add(("style", Tokens.Of(s)));
        if (_color != null) pairs.Add(("color", YamlWriter.Scalar(_color)));
        if (_note != null) pairs.Add(("note", YamlWriter.Scalar(_note)));
        if (_activate is { } a) pairs.Add(("activate", a ? "true" : "false"));
        return YamlWriter.FlowMap(pairs);
    }
}
