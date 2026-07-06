using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Beck;

/// <summary>
/// Builds a <c>type: state</c> Beck diagram — a state machine of pills and
/// labelled transitions. <see cref="Initial"/>/<see cref="Final"/> wire the UML
/// entry dot and exit bullseye; states referenced only by transitions are
/// auto-created by the engine, so <c>State()</c> is only needed to refine a
/// state's title or accent.
/// </summary>
/// <example>
/// <code>
/// var fence = new StateDiagramBuilder("Order Lifecycle")
///     .Direction(Direction.LR)
///     .State("review", s => s.Title("In Review").Accent(AccentToken.Warn))
///     .Initial("draft")
///     .Transition("draft", "review", "submit")
///     .Transition("review", "draft", "reject")
///     .Transition("review", "published", "approve")
///     .Final("published")
///     .ToFence();
/// </code>
/// </example>
public sealed class StateDiagramBuilder
{
    /// <summary>The UML entry/exit pseudo-state token (usable directly in <see cref="Transition"/>).</summary>
    public const string Pseudo = "[*]";

    private readonly List<StateBuilder> _states = new();
    private readonly List<string> _transitions = new();
    private readonly MetaOptions _meta = new();
    private FlowBuilder? _flow;

    /// <summary>Create an empty state diagram.</summary>
    public StateDiagramBuilder() { }

    /// <summary>Create a state diagram with a title.</summary>
    public StateDiagramBuilder(string title) => _meta.Title = title;

    /// <summary>Set the diagram title.</summary>
    public StateDiagramBuilder Title(string title) { _meta.Title = title; return this; }

    /// <summary>Set the diagram subtitle.</summary>
    public StateDiagramBuilder Subtitle(string subtitle) { _meta.Subtitle = subtitle; return this; }

    /// <summary>Set the layout direction (state machines often read best LR).</summary>
    public StateDiagramBuilder Direction(Direction direction) { _meta.Direction = direction; return this; }

    /// <summary>Set the theme mode.</summary>
    public StateDiagramBuilder Theme(ThemeMode theme) { _meta.Theme = theme; return this; }

    /// <summary>Enable or disable animation.</summary>
    public StateDiagramBuilder Animate(bool animate) { _meta.Animate = animate; return this; }

    /// <summary>Enable or disable looping.</summary>
    public StateDiagramBuilder Loop(bool loop) { _meta.Loop = loop; return this; }

    /// <summary>How the diagram behaves when wider than its container.</summary>
    public StateDiagramBuilder Fit(FitMode fit) { _meta.Fit = fit; return this; }

    /// <summary>Toggle + tune the narration caption. Captions come from a transition
    /// <see cref="TransitionBuilder.Note"/> (or explicit <see cref="FlowBuilder.Narrate"/> steps);
    /// the knobs pace each caption's on-screen time by its length.</summary>
    public StateDiagramBuilder Narrate(bool enabled = true, int? wpm = null, double? min = null, double? pad = null)
    {
        _meta.Narrate = enabled;
        if (wpm is { } w) _meta.NarrateWpm = w;
        if (min is { } m) _meta.NarrateMin = m;
        if (pad is { } p) _meta.NarratePad = p;
        return this;
    }

    /// <summary>Tune layout spacing.</summary>
    public StateDiagramBuilder Spacing(int? rank = null, int? node = null, int? cornerRadius = null)
    {
        if (rank is { } r) _meta.SpacingRank = r;
        if (node is { } n) _meta.SpacingNode = n;
        if (cornerRadius is { } c) _meta.SpacingCornerRadius = c;
        return this;
    }

    /// <summary>Declare (or refine) a state, configured via a builder callback.</summary>
    public StateDiagramBuilder State(string id, Action<StateBuilder>? configure = null)
    {
        var s = new StateBuilder(id);
        configure?.Invoke(s);
        _states.Add(s);
        return this;
    }

    /// <summary>Declare a state with a title and optional accent.</summary>
    public StateDiagramBuilder State(string id, string title, AccentToken? accent = null)
    {
        var s = new StateBuilder(id).Title(title);
        if (accent is { } a) s.Accent(a);
        _states.Add(s);
        return this;
    }

    /// <summary>Add a transition between two states (states are auto-created as needed).</summary>
    public StateDiagramBuilder Transition(string from, string to, string? label = null, Action<TransitionBuilder>? configure = null)
    {
        var t = new TransitionBuilder(from, to);
        if (label != null) t.Label(label);
        configure?.Invoke(t);
        _transitions.Add(t.ToFlow());
        return this;
    }

    /// <summary>Mark the machine's initial state (draws the entry dot).</summary>
    public StateDiagramBuilder Initial(string stateId) => Transition(Pseudo, stateId);

    /// <summary>Mark a final state (draws the exit bullseye), with an optional transition label.</summary>
    public StateDiagramBuilder Final(string stateId, string? label = null) => Transition(stateId, Pseudo, label);

    /// <summary>Script the animation flow explicitly. Without this the engine walks the transitions.</summary>
    public StateDiagramBuilder Flow(Action<FlowBuilder> configure)
    {
        _flow ??= new FlowBuilder();
        configure(_flow);
        return this;
    }

    /// <summary>Render the diagram as Beck YAML.</summary>
    public string ToYaml()
    {
        if (_states.Count == 0 && _transitions.Count == 0)
            throw new InvalidOperationException("A state diagram needs at least one State() or Transition().");

        var sb = new StringBuilder();
        sb.Append("type: state\n");
        _meta.AppendYaml(sb);
        if (_states.Count > 0)
        {
            sb.Append("states:\n");
            foreach (var s in _states) sb.Append("  - ").Append(s.ToFlow()).Append('\n');
        }
        if (_transitions.Count > 0)
        {
            sb.Append("transitions:\n");
            foreach (var t in _transitions) sb.Append("  - ").Append(t).Append('\n');
        }
        _flow?.AppendYaml(sb);
        return sb.ToString();
    }

    /// <summary>Render the diagram as a fenced <c>```beck</c> Markdown block.</summary>
    public string ToFence() => BeckMarkdown.Fence(ToYaml());

    /// <inheritdoc/>
    public override string ToString() => ToYaml();
}

/// <summary>Fluent builder for a state pill.</summary>
public sealed class StateBuilder
{
    private readonly string _id;
    private string? _title;
    private string? _subtitle;
    private string? _accent;
    private int? _width;
    private int? _rank;
    private int? _order;

    internal StateBuilder(string id) => _id = id;

    /// <summary>Set the display title (defaults to the id).</summary>
    public StateBuilder Title(string title) { _title = title; return this; }

    /// <summary>Set the muted subtitle line.</summary>
    public StateBuilder Subtitle(string subtitle) { _subtitle = subtitle; return this; }

    /// <summary>Set the accent to a semantic token.</summary>
    public StateBuilder Accent(AccentToken token) { _accent = Tokens.Of(token); return this; }

    /// <summary>Set the accent to a raw color.</summary>
    public StateBuilder Accent(string color) { _accent = color; return this; }

    /// <summary>Fix the pill width in pixels.</summary>
    public StateBuilder Width(int px) { _width = px; return this; }

    /// <summary>Force the state into a specific layout rank.</summary>
    public StateBuilder Rank(int rank) { _rank = rank; return this; }

    /// <summary>Tie-break order within the rank.</summary>
    public StateBuilder Order(int order) { _order = order; return this; }

    internal string ToFlow()
    {
        var pairs = new List<(string, string)> { ("id", YamlWriter.Scalar(_id)) };
        if (_title != null) pairs.Add(("title", YamlWriter.Scalar(_title)));
        if (_subtitle != null) pairs.Add(("subtitle", YamlWriter.Scalar(_subtitle)));
        if (_accent != null) pairs.Add(("accent", YamlWriter.Scalar(_accent)));
        if (_width is { } w) pairs.Add(("width", w.ToString(CultureInfo.InvariantCulture)));
        if (_rank is { } r) pairs.Add(("rank", r.ToString(CultureInfo.InvariantCulture)));
        if (_order is { } o) pairs.Add(("order", o.ToString(CultureInfo.InvariantCulture)));
        return YamlWriter.FlowMap(pairs);
    }
}

/// <summary>Fluent builder for a state transition.</summary>
public sealed class TransitionBuilder
{
    private readonly string _from;
    private readonly string _to;
    private string? _label;
    private string? _color;
    private string? _note;
    private EdgeStyle? _style;

    internal TransitionBuilder(string from, string to)
    {
        _from = from;
        _to = to;
    }

    /// <summary>Set the transition label.</summary>
    public TransitionBuilder Label(string label) { _label = label; return this; }

    /// <summary>Override the line style.</summary>
    public TransitionBuilder Style(EdgeStyle style) { _style = style; return this; }

    /// <summary>Set the stroke to a semantic token.</summary>
    public TransitionBuilder Color(AccentToken token) { _color = Tokens.Of(token); return this; }

    /// <summary>Set the stroke to a raw color.</summary>
    public TransitionBuilder Color(string color) { _color = color; return this; }

    /// <summary>Narrate this transition: the text becomes a caption just before the
    /// transition fires in an auto-derived flow (ignored when a <c>Flow</c> is scripted).</summary>
    public TransitionBuilder Note(string note) { _note = note; return this; }

    internal string ToFlow()
    {
        var pairs = new List<(string, string)>
        {
            ("from", YamlWriter.Scalar(_from)),
            ("to", YamlWriter.Scalar(_to)),
        };
        if (_label != null) pairs.Add(("label", YamlWriter.Scalar(_label)));
        if (_style is { } s) pairs.Add(("style", Tokens.Of(s)));
        if (_color != null) pairs.Add(("color", YamlWriter.Scalar(_color)));
        if (_note != null) pairs.Add(("note", YamlWriter.Scalar(_note)));
        return YamlWriter.FlowMap(pairs);
    }
}
