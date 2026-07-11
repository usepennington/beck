using System.Globalization;
using System.Text;

namespace Beck.Authoring;

/// <summary>
/// Builds a <c>type: flowchart</c> Beck diagram — a decision/process graph of
/// steps and labelled links. Links auto-create the steps they mention (as plain
/// <see cref="StepKind.Process"/> cards), so <see cref="Step(string, Action{StepBuilder})"/> is only
/// needed to refine a step's title, kind, or accent; <see cref="Start(string?)"/> and
/// <see cref="End(string?)"/> declare terminator pseudo-steps, and the <c>"[*]"</c> pseudo-step (see
/// <see cref="Pseudo"/>) works from a link exactly like <see cref="StateDiagramBuilder"/>'s
/// entry/exit pseudo-state.
/// </summary>
/// <example>
/// <code>
/// var fence = new FlowchartDiagramBuilder("Checkout")
///     .Direction(Direction.Tb)
///     .Decision("valid", "Payment valid?")
///     .Link(FlowchartDiagramBuilder.Pseudo, "valid")
///     .Link("valid", "charge", "yes")
///     .Link("valid", "retry", "no")
///     .Link("charge", FlowchartDiagramBuilder.Pseudo)
///     .Link("retry", "valid")
///     .ToFence();
/// </code>
/// </example>
public sealed class FlowchartDiagramBuilder
{
    /// <summary>The start/end pseudo-step token (usable directly in <see cref="Link"/>).</summary>
    public const string Pseudo = "[*]";

    private readonly List<StepBuilder> _steps = new();
    private readonly List<string> _links = new();
    private readonly MetaOptions _meta = new();
    private FlowBuilder? _flow;

    /// <summary>Create an empty flowchart.</summary>
    public FlowchartDiagramBuilder() { }

    /// <summary>Create a flowchart with a title.</summary>
    public FlowchartDiagramBuilder(string title) => _meta._title = title;

    /// <summary>Set the diagram title.</summary>
    public FlowchartDiagramBuilder Title(string title) { _meta._title = title; return this; }

    /// <summary>Set the diagram subtitle.</summary>
    public FlowchartDiagramBuilder Subtitle(string subtitle) { _meta._subtitle = subtitle; return this; }

    /// <summary>Set the visual style by its <c>meta.style</c> token (e.g. <c>"classic"</c>).</summary>
    public FlowchartDiagramBuilder Style(string name) { _meta._style = name; return this; }

    /// <summary>Set the visual style from a <see cref="BeckStyle"/> (emits its <see cref="BeckStyle.Name"/>).</summary>
    public FlowchartDiagramBuilder Style(BeckStyle style) { _meta._style = style.Name; return this; }

    /// <summary>Set the layout direction — <see cref="Authoring.Direction.Tb"/> (default) reads like a procedure, <see cref="Authoring.Direction.Lr"/> like a pipeline.</summary>
    public FlowchartDiagramBuilder Direction(Direction direction) { _meta._direction = direction; return this; }

    /// <summary>Set the theme: <see cref="ThemeMode.Auto"/> (default), <see cref="ThemeMode.Light"/>, or <see cref="ThemeMode.Dark"/>.</summary>
    public FlowchartDiagramBuilder Theme(ThemeMode theme) { _meta._theme = theme; return this; }

    /// <summary>Enable or disable the flow animation.</summary>
    public FlowchartDiagramBuilder Animate(bool animate) { _meta._animate = animate; return this; }

    /// <summary>Loop the flow (default) or play it through once.</summary>
    public FlowchartDiagramBuilder Loop(bool loop) { _meta._loop = loop; return this; }

    /// <summary>How the diagram behaves when wider than its container.</summary>
    public FlowchartDiagramBuilder Fit(FitMode fit) { _meta._fit = fit; return this; }

    /// <summary>Toggle + tune the narration caption. Captions come from a link
    /// <see cref="LinkBuilder.Note"/> (or explicit <see cref="FlowBuilder.Narrate"/> steps);
    /// the knobs pace each caption's on-screen time by its length.</summary>
    public FlowchartDiagramBuilder Narrate(bool enabled = true, int? wpm = null, double? min = null, double? pad = null)
    {
        _meta._narrate = enabled;
        if (wpm is { } w)
        {
            _meta._narrateWpm = w;
        }

        if (min is { } m)
        {
            _meta._narrateMin = m;
        }

        if (pad is { } p)
        {
            _meta._narratePad = p;
        }

        return this;
    }

    /// <summary>Tune layout spacing: rank gap (along the flow), node gap (across), and corner radius (px).</summary>
    public FlowchartDiagramBuilder Spacing(int? rank = null, int? node = null, int? cornerRadius = null)
    {
        if (rank is { } r)
        {
            _meta._spacingRank = r;
        }

        if (node is { } n)
        {
            _meta._spacingNode = n;
        }

        if (cornerRadius is { } c)
        {
            _meta._spacingCornerRadius = c;
        }

        return this;
    }

    /// <summary>Declare a step and refine it via <see cref="StepBuilder"/> — title, kind, accent, subtitle. Undeclared steps still render as plain process cards.</summary>
    public FlowchartDiagramBuilder Step(string id, Action<StepBuilder>? configure = null)
    {
        var s = new StepBuilder(id);
        configure?.Invoke(s);
        _steps.Add(s);
        return this;
    }

    /// <summary>Declare a <see cref="StepKind.Process"/> step (a rectangular action card).</summary>
    public FlowchartDiagramBuilder Process(string id, string? text = null) => Step(id, StepKind.Process, text);

    /// <summary>Declare a <see cref="StepKind.Decision"/> step (a diamond branch point).</summary>
    public FlowchartDiagramBuilder Decision(string id, string? text = null) => Step(id, StepKind.Decision, text);

    /// <summary>Declare a <see cref="StepKind.Terminator"/> step (a pipeline entry/exit pill).</summary>
    public FlowchartDiagramBuilder Terminator(string id, string? text = null) => Step(id, StepKind.Terminator, text);

    /// <summary>Declare an <see cref="StepKind.Io"/> step (a parallelogram).</summary>
    public FlowchartDiagramBuilder Io(string id, string? text = null) => Step(id, StepKind.Io, text);

    /// <summary>Declare a <see cref="StepKind.Start"/> pseudo-step (defaults its id to <c>"start"</c>).</summary>
    public FlowchartDiagramBuilder Start(string? id = null) => Step(id ?? "start", StepKind.Start, null);

    /// <summary>Declare an <see cref="StepKind.End"/> pseudo-step (defaults its id to <c>"end"</c>).</summary>
    public FlowchartDiagramBuilder End(string? id = null) => Step(id ?? "end", StepKind.End, null);

    private FlowchartDiagramBuilder Step(string id, StepKind kind, string? text)
    {
        var s = new StepBuilder(id).Kind(kind);
        if (text != null)
        {
            s.Text(text);
        }

        _steps.Add(s);
        return this;
    }

    /// <summary>Add a link (steps are auto-created as needed). Reference <see cref="Pseudo"/> as
    /// <paramref name="from"/> or <paramref name="to"/> to draw from/to the start or end pseudo-step.</summary>
    public FlowchartDiagramBuilder Link(string from, string to, string? label = null, Action<LinkBuilder>? configure = null)
    {
        var l = new LinkBuilder(from, to);
        if (label != null)
        {
            l.Label(label);
        }

        configure?.Invoke(l);
        _links.Add(l.ToFlow());
        return this;
    }

    /// <summary>Script the animation explicitly. Without this the engine walks the links in declared order.</summary>
    public FlowchartDiagramBuilder Flow(Action<FlowBuilder> configure)
    {
        _flow ??= new FlowBuilder();
        configure(_flow);
        return this;
    }

    /// <summary>Render the diagram as Beck YAML.</summary>
    /// <exception cref="InvalidOperationException">The diagram has no steps and no links.</exception>
    public string ToYaml()
    {
        if (_steps.Count == 0 && _links.Count == 0)
        {
            throw new InvalidOperationException("A flowchart needs at least one Step()/Process()/Decision()/... or Link().");
        }

        var sb = new StringBuilder();
        sb.Append("type: flowchart\n");
        _meta.AppendYaml(sb);
        if (_steps.Count > 0)
        {
            sb.Append("steps:\n");
            foreach (var s in _steps)
            {
                sb.Append("  - ").Append(s.ToFlow()).Append('\n');
            }
        }
        if (_links.Count > 0)
        {
            sb.Append("links:\n");
            foreach (var l in _links)
            {
                sb.Append("  - ").Append(l).Append('\n');
            }
        }
        _flow?.AppendYaml(sb);
        return sb.ToString();
    }

    /// <summary>Render as a fenced <c>```beck</c> Markdown block — drop it into any Markdown page and it renders to a static SVG.</summary>
    public string ToFence() => BeckMarkdown.Fence(ToYaml());

    /// <inheritdoc/>
    public override string ToString() => ToYaml();
}

/// <summary>
/// Refines a single step inside a <c>Step(id, s => …)</c> callback. Steps a
/// link mentions but you never declare keep their auto-created process card.
/// </summary>
public sealed class StepBuilder
{
    private readonly string _id;
    private string? _text;
    private StepKind? _kind;
    private string? _subtitle;
    private string? _icon;
    private string? _accent;
    private string? _href;
    private string? _target;
    private string? _surface;
    private string? _textColor;
    private int? _width;
    private int? _rank;
    private int? _order;

    internal StepBuilder(string id) => _id = id;

    /// <summary>Set the display title (defaults to the id).</summary>
    public StepBuilder Text(string text) { _text = text; return this; }

    /// <summary>Set the step's shape: <see cref="StepKind.Process"/> (default), <see cref="StepKind.Decision"/>, <see cref="StepKind.Terminator"/>, <see cref="StepKind.Io"/>, <see cref="StepKind.Start"/>, or <see cref="StepKind.End"/>.</summary>
    public StepBuilder Kind(StepKind kind) { _kind = kind; return this; }

    /// <summary>Set the muted subtitle line.</summary>
    public StepBuilder Subtitle(string subtitle) { _subtitle = subtitle; return this; }

    /// <summary>Set a named icon key or raw inline <c>&lt;svg&gt;</c> markup.</summary>
    public StepBuilder Icon(string icon) { _icon = icon; return this; }

    /// <summary>Set the accent to a semantic token (follows the theme).</summary>
    public StepBuilder Accent(AccentToken token) { _accent = Tokens.Of(token); return this; }

    /// <summary>Set the accent to a raw CSS color.</summary>
    public StepBuilder Accent(string color) { _accent = color; return this; }

    /// <summary>Make the step a link. Pass <c>target: "_blank"</c> to open in a new tab.</summary>
    public StepBuilder Link(string href, string? target = null) { _href = href; _target = target; return this; }

    /// <summary>Override the card background (a raw CSS color).</summary>
    public StepBuilder Surface(string color) { _surface = color; return this; }

    /// <summary>Override the card text color (a raw CSS color).</summary>
    public StepBuilder TextColor(string color) { _textColor = color; return this; }

    /// <summary>Fix the step width in pixels.</summary>
    public StepBuilder Width(int px) { _width = px; return this; }

    /// <summary>Force the step into a specific layout rank.</summary>
    public StepBuilder Rank(int rank) { _rank = rank; return this; }

    /// <summary>Tie-break order within the rank.</summary>
    public StepBuilder Order(int order) { _order = order; return this; }

    internal string ToFlow()
    {
        var pairs = new List<(string, string)> { ("id", YamlWriter.Scalar(_id)) };
        if (_text != null)
        {
            pairs.Add(("text", YamlWriter.Scalar(_text)));
        }

        if (_kind is { } k)
        {
            pairs.Add(("kind", Tokens.Of(k)));
        }

        if (_subtitle != null)
        {
            pairs.Add(("subtitle", YamlWriter.Scalar(_subtitle)));
        }

        if (_icon != null)
        {
            pairs.Add(("icon", YamlWriter.Scalar(_icon)));
        }

        if (_accent != null)
        {
            pairs.Add(("accent", YamlWriter.Scalar(_accent)));
        }

        if (_href != null)
        {
            pairs.Add(("href", YamlWriter.Scalar(_href)));
        }

        if (_target != null)
        {
            pairs.Add(("target", YamlWriter.Scalar(_target)));
        }

        if (_surface != null)
        {
            pairs.Add(("surface", YamlWriter.Scalar(_surface)));
        }

        if (_textColor != null)
        {
            pairs.Add(("textColor", YamlWriter.Scalar(_textColor)));
        }

        if (_width is { } w)
        {
            pairs.Add(("width", w.ToString(CultureInfo.InvariantCulture)));
        }

        if (_rank is { } r)
        {
            pairs.Add(("rank", r.ToString(CultureInfo.InvariantCulture)));
        }

        if (_order is { } o)
        {
            pairs.Add(("order", o.ToString(CultureInfo.InvariantCulture)));
        }

        return YamlWriter.FlowMap(pairs);
    }
}

/// <summary>Configures one link inside a <c>Link(from, to, label, l => …)</c> callback.</summary>
public sealed class LinkBuilder
{
    private readonly string _from;
    private readonly string _to;
    private string? _label;
    private string? _color;
    private string? _note;
    private EdgeStyle? _style;

    internal LinkBuilder(string from, string to)
    {
        _from = from;
        _to = to;
    }

    /// <summary>Set the link label (e.g. a decision branch's "yes"/"no").</summary>
    public LinkBuilder Label(string label) { _label = label; return this; }

    /// <summary>Override the line style: <see cref="EdgeStyle.Solid"/> or <see cref="EdgeStyle.Dashed"/>.</summary>
    public LinkBuilder Style(EdgeStyle style) { _style = style; return this; }

    /// <summary>Set the stroke to a semantic token (follows the theme).</summary>
    public LinkBuilder Color(AccentToken token) { _color = Tokens.Of(token); return this; }

    /// <summary>Set the stroke to a raw CSS color.</summary>
    public LinkBuilder Color(string color) { _color = color; return this; }

    /// <summary>Narrate this link: the text becomes a caption just before the
    /// link fires in an auto-derived flow (ignored when a <c>Flow</c> is scripted).</summary>
    public LinkBuilder Note(string note) { _note = note; return this; }

    internal string ToFlow()
    {
        var pairs = new List<(string, string)>
        {
            ("from", YamlWriter.Scalar(_from)),
            ("to", YamlWriter.Scalar(_to)),
        };
        if (_label != null)
        {
            pairs.Add(("label", YamlWriter.Scalar(_label)));
        }

        if (_style is { } s)
        {
            pairs.Add(("style", Tokens.Of(s)));
        }

        if (_color != null)
        {
            pairs.Add(("color", YamlWriter.Scalar(_color)));
        }

        if (_note != null)
        {
            pairs.Add(("note", YamlWriter.Scalar(_note)));
        }

        return YamlWriter.FlowMap(pairs);
    }
}
