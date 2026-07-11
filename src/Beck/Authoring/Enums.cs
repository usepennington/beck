namespace Beck.Authoring;

/// <summary>Primary layout direction.</summary>
public enum Direction
{
    /// <summary>Top to bottom (default).</summary>
    Tb,
    /// <summary>Bottom to top.</summary>
    Bt,
    /// <summary>Left to right.</summary>
    Lr,
    /// <summary>Right to left.</summary>
    Rl,
}

/// <summary>Node archetype; drives the default icon and accent color.</summary>
public enum NodeKind
{
    /// <summary>A running service or application — the default. Primary accent, service icon.</summary>
    Service,
    /// <summary>A database or datastore. Info accent, database icon.</summary>
    Db,
    /// <summary>A message queue. Warn accent, queue icon.</summary>
    Queue,
    /// <summary>A cache such as Redis or memcached. Warn accent, cache icon.</summary>
    Cache,
    /// <summary>An API gateway, load balancer, or edge. Primary accent, gateway icon.</summary>
    Gateway,
    /// <summary>A third-party or external system. Neutral accent, external icon.</summary>
    External,
    /// <summary>A person or client. Success accent, user icon.</summary>
    User,
    /// <summary>A faint placeholder — dashed and transparent. Neutral accent, ghost variant.</summary>
    Ghost,
}

/// <summary>Visual weight of a node card.</summary>
public enum NodeVariant
{
    /// <summary>A full-weight card — the default.</summary>
    Solid,
    /// <summary>A dimmed, lower-emphasis card.</summary>
    Subtle,
    /// <summary>Dashed and transparent — a placeholder.</summary>
    Ghost,
}

/// <summary>Edge line style.</summary>
public enum EdgeStyle
{
    /// <summary>A solid line.</summary>
    Solid,
    /// <summary>A dashed line.</summary>
    Dashed,
}

/// <summary>Edge routing curve.</summary>
public enum EdgeCurve
{
    /// <summary>Orthogonal with rounded corners (default).</summary>
    StepRound,
    /// <summary>A straight line between endpoints.</summary>
    Straight,
    /// <summary>Smooth S-curve.</summary>
    S,
}

/// <summary>Semantic edge kind; sets a default color and style.</summary>
public enum EdgeKind
{
    /// <summary>A data flow — the default. Solid line; a medium, steady packet.</summary>
    Data,
    /// <summary>A control signal. Solid line; a small, fast, accelerating packet.</summary>
    Control,
    /// <summary>An asynchronous message. Dashed line; a large, slow, eased packet.</summary>
    Async,
    /// <summary>A dependency relationship. Dashed neutral line; a small packet with no glow.</summary>
    Dependency,
}

/// <summary>Easing for a travelling packet (maps to a GSAP ease in the engine).</summary>
public enum PacketEase
{
    /// <summary>Constant speed (default).</summary>
    Linear,
    /// <summary>Ease in and out.</summary>
    Smooth,
    /// <summary>Start slow, then speed up.</summary>
    Accelerate,
    /// <summary>Start fast, then slow down.</summary>
    Decelerate,
    /// <summary>Exponential ease in and out.</summary>
    Expo,
    /// <summary>Gentle sine ease in and out.</summary>
    Sine,
    /// <summary>Discrete stepping — a digital tick.</summary>
    Steps,
    /// <summary>Decelerating bounce on arrival.</summary>
    Bounce,
}

/// <summary>Visual form of a travelling packet.</summary>
public enum PacketShape
{
    /// <summary>A small glowing dot (default).</summary>
    Dot,
    /// <summary>A larger filled disc.</summary>
    Circle,
    /// <summary>A hollow stroked circle.</summary>
    Ring,
}

/// <summary>Theme mode for a diagram.</summary>
public enum ThemeMode
{
    /// <summary>Follow the host page's theme (default).</summary>
    Auto,
    /// <summary>Force the light palette.</summary>
    Light,
    /// <summary>Force the dark palette.</summary>
    Dark,
}

/// <summary>How a diagram wider than its container behaves.</summary>
public enum FitMode
{
    /// <summary>Scale the whole diagram down to fit the available width (default).</summary>
    Shrink,
    /// <summary>Keep natural size and scroll horizontally when too narrow.</summary>
    Scroll,
}

/// <summary>A semantic accent color token (maps to a <c>--beck-*</c> CSS variable).</summary>
public enum AccentToken
{
    /// <summary>Your brand colour — the primary token.</summary>
    Primary,
    /// <summary>A positive or healthy state (emerald by default).</summary>
    Success,
    /// <summary>A warning state (amber by default).</summary>
    Warn,
    /// <summary>An error or critical state (red by default).</summary>
    Danger,
    /// <summary>An informational accent (violet by default).</summary>
    Info,
    /// <summary>A muted, neutral grey.</summary>
    Neutral,
}

/// <summary>A node side an edge can be pinned to anchor at.</summary>
public enum Side
{
    /// <summary>The top edge of the node.</summary>
    Top,
    /// <summary>The bottom edge of the node.</summary>
    Bottom,
    /// <summary>The left edge of the node.</summary>
    Left,
    /// <summary>The right edge of the node.</summary>
    Right,
}

/// <summary>Which ends of an edge carry an arrowhead.</summary>
public enum ArrowEnds
{
    /// <summary>No arrowheads — an undirected link.</summary>
    None,
    /// <summary>An arrowhead at the target end — the default.</summary>
    End,
    /// <summary>An arrowhead at the source end.</summary>
    Start,
    /// <summary>Arrowheads at both ends — a two-way link.</summary>
    Both,
}

/// <summary>The shape a <c>type: flowchart</c> step renders as.</summary>
public enum StepKind
{
    /// <summary>A rectangular action step — the default. Renders as a card.</summary>
    Process,
    /// <summary>A branch point. Renders as a diamond.</summary>
    Decision,
    /// <summary>A pipeline entry/exit. Renders as a pill.</summary>
    Terminator,
    /// <summary>An input/output step. Renders as a parallelogram.</summary>
    Io,
    /// <summary>The flow's start pseudo-step.</summary>
    Start,
    /// <summary>The flow's end pseudo-step.</summary>
    End,
}

/// <summary>How two classes in a <c>type: class</c> diagram relate.</summary>
public enum RelationKind
{
    /// <summary>Child extends parent (hollow triangle at the parent).</summary>
    Inherits,
    /// <summary>Class implements an interface (dashed, hollow triangle).</summary>
    Implements,
    /// <summary>A plain directed association.</summary>
    Association,
    /// <summary>Whole–part where the part outlives the whole (hollow diamond at the whole).</summary>
    Aggregation,
    /// <summary>Whole–part where the part's lifetime is owned (filled diamond at the whole).</summary>
    Composition,
    /// <summary>A usage dependency (dashed, open arrowhead).</summary>
    Dependency,
}

internal static class Tokens
{
    public static string Of(Direction d) => d.ToString().ToUpperInvariant();
    public static string Of(ThemeMode t) => t.ToString().ToLowerInvariant();
    public static string Of(FitMode f) => f.ToString().ToLowerInvariant();
    public static string Of(NodeKind k) => k.ToString().ToLowerInvariant();
    public static string Of(NodeVariant v) => v.ToString().ToLowerInvariant();
    public static string Of(PacketEase e) => e.ToString().ToLowerInvariant();
    public static string Of(PacketShape s) => s.ToString().ToLowerInvariant();
    public static string Of(EdgeStyle s) => s.ToString().ToLowerInvariant();
    public static string Of(EdgeKind k) => k.ToString().ToLowerInvariant();
    public static string Of(AccentToken a) => a.ToString().ToLowerInvariant();
    public static string Of(Side s) => s.ToString().ToLowerInvariant();
    public static string Of(ArrowEnds a) => a.ToString().ToLowerInvariant();
    public static string Of(RelationKind r) => r.ToString().ToLowerInvariant();
    public static string Of(StepKind k) => k.ToString().ToLowerInvariant();

    public static string Of(EdgeCurve c) => c switch
    {
        EdgeCurve.StepRound => "step-round",
        EdgeCurve.Straight => "straight",
        EdgeCurve.S => "s",
        _ => "step-round",
    };
}