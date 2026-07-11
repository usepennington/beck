using System.Globalization;
using System.Text;

namespace Beck.Authoring;

/// <summary>
/// Builds a <c>type: mindmap</c> Beck diagram — a nested topic tree drawn as a
/// two-sided "butterfly" (root centred, branches fanning left/right).
/// <see cref="Root(string)"/> declares the centre topic; <see cref="Topic(string)"/>
/// (on this builder, or nested on a <see cref="TopicBuilder"/>) declares a
/// first-level branch or a deeper child — arbitrary nesting is supported.
/// </summary>
/// <example>
/// <code>
/// var fence = new MindMapDiagramBuilder("Beck")
///     .Root("Beck")
///     .Topic("Rendering", t => t
///         .Accent(AccentToken.Info)
///         .Topic("Pipeline", p => p.Items("Model", "Text", "Layout"))
///         .Topic("Determinism", d => d.Body("Same YAML, same SVG.")))
///     .Topic("Packages", t => t
///         .Topic("Beck")
///         .Topic("Beck.Skia"))
///     .ToFence();
/// </code>
/// </example>
public sealed class MindMapDiagramBuilder
{
    private readonly MetaOptions _meta = new();
    private TopicBuilder? _root;
    private readonly List<TopicBuilder> _topics = new();
    private FlowBuilder? _flow;

    /// <summary>Create an empty mindmap.</summary>
    public MindMapDiagramBuilder() { }

    /// <summary>Create a mindmap with a diagram title.</summary>
    public MindMapDiagramBuilder(string title) => _meta._title = title;

    /// <summary>Set the diagram title.</summary>
    public MindMapDiagramBuilder Title(string title) { _meta._title = title; return this; }

    /// <summary>Set the diagram subtitle.</summary>
    public MindMapDiagramBuilder Subtitle(string subtitle) { _meta._subtitle = subtitle; return this; }

    /// <summary>Set the visual style by its <c>meta.style</c> token (e.g. <c>"classic"</c>).</summary>
    public MindMapDiagramBuilder Style(string name) { _meta._style = name; return this; }

    /// <summary>Set the visual style from a <see cref="BeckStyle"/> (emits its <see cref="BeckStyle.Name"/>).</summary>
    public MindMapDiagramBuilder Style(BeckStyle style) { _meta._style = style.Name; return this; }

    /// <summary>Set the theme: <see cref="ThemeMode.Auto"/> (default), <see cref="ThemeMode.Light"/>, or <see cref="ThemeMode.Dark"/>.</summary>
    public MindMapDiagramBuilder Theme(ThemeMode theme) { _meta._theme = theme; return this; }

    /// <summary>Enable or disable the flow animation.</summary>
    public MindMapDiagramBuilder Animate(bool animate) { _meta._animate = animate; return this; }

    /// <summary>Loop the flow (default) or play it through once.</summary>
    public MindMapDiagramBuilder Loop(bool loop) { _meta._loop = loop; return this; }

    /// <summary>How the diagram behaves when wider than its container.</summary>
    public MindMapDiagramBuilder Fit(FitMode fit) { _meta._fit = fit; return this; }

    /// <summary>Toggle + tune the narration caption. Captions come from explicit
    /// <see cref="FlowBuilder.Narrate"/> steps; the knobs pace each caption's on-screen time by its length.</summary>
    public MindMapDiagramBuilder Narrate(bool enabled = true, int? wpm = null, double? min = null, double? pad = null)
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
    public MindMapDiagramBuilder Spacing(int? rank = null, int? node = null, int? cornerRadius = null)
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

    /// <summary>Declare the centre topic by title.</summary>
    public MindMapDiagramBuilder Root(string title) => Root(title, null);

    /// <summary>Declare the centre topic and refine it via <see cref="TopicBuilder"/>.</summary>
    public MindMapDiagramBuilder Root(string title, Action<TopicBuilder>? configure)
    {
        var t = new TopicBuilder(title);
        configure?.Invoke(t);
        _root = t;
        return this;
    }

    /// <summary>Declare a first-level branch by title.</summary>
    public MindMapDiagramBuilder Topic(string title) => Topic(title, null);

    /// <summary>Declare a first-level branch and refine it (and its nested children) via <see cref="TopicBuilder"/>.</summary>
    public MindMapDiagramBuilder Topic(string title, Action<TopicBuilder>? configure)
    {
        var t = new TopicBuilder(title);
        configure?.Invoke(t);
        _topics.Add(t);
        return this;
    }

    /// <summary>Script the animation explicitly. Without this the engine derives packets root → leaves.</summary>
    public MindMapDiagramBuilder Flow(Action<FlowBuilder> configure)
    {
        _flow ??= new FlowBuilder();
        configure(_flow);
        return this;
    }

    /// <summary>Render the diagram as Beck YAML.</summary>
    /// <exception cref="InvalidOperationException">The diagram has no <see cref="Root(string)"/> topic.</exception>
    public string ToYaml()
    {
        if (_root == null)
        {
            throw new InvalidOperationException("A mindmap needs a Root(...) topic.");
        }

        var sb = new StringBuilder();
        sb.Append("type: mindmap\n");
        _meta.AppendYaml(sb);
        sb.Append("root: ").Append(_root.ToFlow()).Append('\n');
        if (_topics.Count > 0)
        {
            sb.Append("topics:\n");
            foreach (var t in _topics)
            {
                sb.Append("  - ").Append(t.ToFlow()).Append('\n');
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
/// Configures a single topic inside a <c>Root(title, t => …)</c> or
/// <c>Topic(title, t => …)</c> callback. Call <see cref="Topic(string)"/> again
/// on the same builder to nest children — arbitrary depth is supported, and
/// each level emits as a nested flow-style YAML mapping (still parses as plain
/// YAML; the block-style form in the schema doc is just one equivalent spelling).
/// </summary>
public sealed class TopicBuilder
{
    private readonly string _title;
    private string? _id;
    private string? _subtitle;
    private readonly List<string> _items = new();
    private string? _body;
    private string? _status;
    private bool _ghost;
    private string? _accent;
    private string? _icon;
    private string? _href;
    private string? _target;
    private string? _surface;
    private string? _textColor;
    private int? _width;
    private readonly List<TopicBuilder> _children = new();

    internal TopicBuilder(string title) => _title = title;

    /// <summary>Set an explicit id (defaults to the path-derived id the engine assigns).</summary>
    public TopicBuilder Id(string id) { _id = id; return this; }

    /// <summary>Set the muted subtitle line.</summary>
    public TopicBuilder Subtitle(string subtitle) { _subtitle = subtitle; return this; }

    /// <summary>Add one bullet to the topic card's item list.</summary>
    public TopicBuilder Item(string item) { _items.Add(item); return this; }

    /// <summary>Add several bullets to the topic card's item list at once.</summary>
    public TopicBuilder Items(params string[] items) { _items.AddRange(items); return this; }

    /// <summary>Set the wrapped body paragraph rendered under the items.</summary>
    public TopicBuilder Body(string body) { _body = body; return this; }

    /// <summary>Set the topic's status — a semantic pill on a rank-1 card (or inline after a leaf).</summary>
    public TopicBuilder Status(string status) { _status = status; return this; }

    /// <summary>Mark this branch (and its whole subtree) a ghost: neutral, dashed, shadowless "planned".</summary>
    public TopicBuilder Ghost(bool ghost = true) { _ghost = ghost; return this; }

    /// <summary>Set the accent to a semantic token (follows the theme); flows to descendants unless they override it.</summary>
    public TopicBuilder Accent(AccentToken token) { _accent = Tokens.Of(token); return this; }

    /// <summary>Set the accent to a raw CSS color; flows to descendants unless they override it.</summary>
    public TopicBuilder Accent(string color) { _accent = color; return this; }

    /// <summary>Set a named icon key or raw inline <c>&lt;svg&gt;</c> markup.</summary>
    public TopicBuilder Icon(string icon) { _icon = icon; return this; }

    /// <summary>Make the topic a link. Pass <c>target: "_blank"</c> to open in a new tab.</summary>
    public TopicBuilder Link(string href, string? target = null) { _href = href; _target = target; return this; }

    /// <summary>Override the card background (a raw CSS color).</summary>
    public TopicBuilder Surface(string color) { _surface = color; return this; }

    /// <summary>Override the card text color (a raw CSS color).</summary>
    public TopicBuilder TextColor(string color) { _textColor = color; return this; }

    /// <summary>Fix the card width in pixels.</summary>
    public TopicBuilder Width(int px) { _width = px; return this; }

    /// <summary>Declare a child topic by title.</summary>
    public TopicBuilder Topic(string title) => Topic(title, null);

    /// <summary>Declare a child topic and refine it (and its own nested children) via <see cref="TopicBuilder"/>.</summary>
    public TopicBuilder Topic(string title, Action<TopicBuilder>? configure)
    {
        var t = new TopicBuilder(title);
        configure?.Invoke(t);
        _children.Add(t);
        return this;
    }

    internal string ToFlow()
    {
        var pairs = new List<(string, string)> { ("title", YamlWriter.Scalar(_title)) };
        if (_id != null)
        {
            pairs.Add(("id", YamlWriter.Scalar(_id)));
        }

        if (_subtitle != null)
        {
            pairs.Add(("subtitle", YamlWriter.Scalar(_subtitle)));
        }

        if (_items.Count > 0)
        {
            pairs.Add(("items", YamlWriter.FlowSeq(_items.Select(YamlWriter.Scalar))));
        }

        if (_body != null)
        {
            pairs.Add(("body", YamlWriter.Scalar(_body)));
        }

        if (_status != null)
        {
            pairs.Add(("status", YamlWriter.Scalar(_status)));
        }

        if (_ghost)
        {
            pairs.Add(("ghost", "true"));
        }

        if (_accent != null)
        {
            pairs.Add(("accent", YamlWriter.Scalar(_accent)));
        }

        if (_icon != null)
        {
            pairs.Add(("icon", YamlWriter.Scalar(_icon)));
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

        if (_children.Count > 0)
        {
            pairs.Add(("children", YamlWriter.FlowSeq(_children.Select(c => c.ToFlow()))));
        }

        return YamlWriter.FlowMap(pairs);
    }
}
