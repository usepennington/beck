namespace Beck;

/// <summary>Primary layout direction.</summary>
public enum Direction
{
    /// <summary>Top to bottom (default).</summary>
    TB,
    /// <summary>Bottom to top.</summary>
    BT,
    /// <summary>Left to right.</summary>
    LR,
    /// <summary>Right to left.</summary>
    RL,
}

/// <summary>Node archetype; drives the default icon and accent color.</summary>
public enum NodeKind
{
    Service,
    Db,
    Queue,
    Cache,
    Gateway,
    External,
    User,
    Ghost,
}

/// <summary>Visual weight of a node card.</summary>
public enum NodeVariant
{
    Solid,
    Subtle,
    Ghost,
}

/// <summary>Edge line style.</summary>
public enum EdgeStyle
{
    Solid,
    Dashed,
}

/// <summary>Edge routing curve.</summary>
public enum EdgeCurve
{
    /// <summary>Orthogonal with rounded corners (default).</summary>
    StepRound,
    Straight,
    /// <summary>Smooth S-curve.</summary>
    S,
}

/// <summary>Semantic edge kind; sets a default color and style.</summary>
public enum EdgeKind
{
    Data,
    Control,
    Async,
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
    Auto,
    Light,
    Dark,
}

/// <summary>A semantic accent color token (maps to a <c>--beck-*</c> CSS variable).</summary>
public enum AccentToken
{
    Primary,
    Success,
    Warn,
    Danger,
    Info,
    Neutral,
}

/// <summary>A node side an edge can be pinned to anchor at.</summary>
public enum Side
{
    Top,
    Bottom,
    Left,
    Right,
}

/// <summary>Which ends of an edge carry an arrowhead.</summary>
public enum ArrowEnds
{
    None,
    End,
    Start,
    Both,
}

internal static class Tokens
{
    public static string Of(Direction d) => d.ToString();
    public static string Of(ThemeMode t) => t.ToString().ToLowerInvariant();
    public static string Of(NodeKind k) => k.ToString().ToLowerInvariant();
    public static string Of(NodeVariant v) => v.ToString().ToLowerInvariant();
    public static string Of(PacketEase e) => e.ToString().ToLowerInvariant();
    public static string Of(PacketShape s) => s.ToString().ToLowerInvariant();
    public static string Of(EdgeStyle s) => s.ToString().ToLowerInvariant();
    public static string Of(EdgeKind k) => k.ToString().ToLowerInvariant();
    public static string Of(AccentToken a) => a.ToString().ToLowerInvariant();
    public static string Of(Side s) => s.ToString().ToLowerInvariant();
    public static string Of(ArrowEnds a) => a.ToString().ToLowerInvariant();

    public static string Of(EdgeCurve c) => c switch
    {
        EdgeCurve.StepRound => "step-round",
        EdgeCurve.Straight => "straight",
        EdgeCurve.S => "s",
        _ => "step-round",
    };
}
