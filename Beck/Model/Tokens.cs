namespace Beck.Rendering;

// The closed token vocabularies, ported verbatim from src/model/schema.ts. Each
// enum member maps to the exact wire token the YAML/JSON uses, so the model
// round-trips identically to the TypeScript oracle. Enums (not strings) so the
// downstream layout/route/render/animate stages switch exhaustively.

/// <summary>What the diagram is; picks the layout + routing strategy.</summary>
internal enum DiagramType { Architecture, Sequence, State, Class }

/// <summary>Primary layout axis.</summary>
internal enum Direction { TB, BT, LR, RL }

/// <summary>How a diagram wider than its container behaves.</summary>
internal enum FitMode { Shrink, Scroll }

internal enum NodeKind { Service, Db, Queue, Cache, Gateway, External, User, Ghost }

internal enum NodeVariant { Solid, Subtle, Ghost }

/// <summary>Structural form of a node.</summary>
internal enum NodeShape { Card, Pill, Start, End, Class }

internal enum EdgeStyle { Solid, Dashed }

internal enum EdgeCurve { StepRound, Straight, S }

internal enum EdgeKind { Data, Control, Async, Dependency }

internal enum PacketEase { Linear, Smooth, Accelerate, Decelerate, Expo, Sine, Steps, Bounce }

// Square/Train are not author-facing tokens (no YAML/authoring-API value maps to them) — they exist
// only as StyleMotion.PacketGlyph defaults a style can pick for its identity glyph: the "block packet"
// (terminal) and the path-oriented "train capsule" (metro).
internal enum PacketShape { Dot, Circle, Ring, Square, Train }

internal enum Side { Top, Bottom, Left, Right }

/// <summary>Which ends of an edge carry an arrowhead.</summary>
internal enum ArrowEnds { None, End, Start, Both }

/// <summary>End decoration on an edge (wins over <see cref="ArrowEnds"/> at that end).</summary>
internal enum MarkerShape { Arrow, ArrowOpen, Triangle, Diamond, DiamondOpen }

internal enum AccentToken { Primary, Success, Warn, Danger, Info, Neutral }

/// <summary>Bidirectional map between a token enum and its wire string, preserving canonical order.</summary>
internal sealed class TokenMap<TEnum> where TEnum : struct, Enum
{
    private readonly Dictionary<TEnum, string> _toWire = new();
    private readonly Dictionary<string, TEnum> _fromWire = new(StringComparer.Ordinal);

    /// <summary>The wire tokens in declaration order (drives <c>oneOf</c> error messages).</summary>
    public IReadOnlyList<string> Tokens { get; }

    public TokenMap(params (TEnum Value, string Wire)[] pairs)
    {
        var toks = new List<string>(pairs.Length);
        foreach (var (value, wire) in pairs)
        {
            _toWire[value] = wire;
            _fromWire[wire] = value;
            toks.Add(wire);
        }
        Tokens = toks;
    }

    public string Wire(TEnum value) => _toWire[value];

    public bool TryParse(string s, out TEnum value) => _fromWire.TryGetValue(s, out value);
}

/// <summary>The canonical token maps. Orders match the TS union / <c>oneOf</c> arrays exactly.</summary>
internal static class Tokens
{
    public static readonly TokenMap<DiagramType> DiagramType = new(
        (Beck.Rendering.DiagramType.Architecture, "architecture"),
        (Beck.Rendering.DiagramType.Sequence, "sequence"),
        (Beck.Rendering.DiagramType.State, "state"),
        (Beck.Rendering.DiagramType.Class, "class"));

    public static readonly TokenMap<Direction> Direction = new(
        (Beck.Rendering.Direction.TB, "TB"),
        (Beck.Rendering.Direction.BT, "BT"),
        (Beck.Rendering.Direction.LR, "LR"),
        (Beck.Rendering.Direction.RL, "RL"));

    public static readonly TokenMap<ThemeMode> Theme = new(
        (ThemeMode.Auto, "auto"),
        (ThemeMode.Light, "light"),
        (ThemeMode.Dark, "dark"));

    public static readonly TokenMap<FitMode> Fit = new(
        (FitMode.Shrink, "shrink"),
        (FitMode.Scroll, "scroll"));

    public static readonly TokenMap<NodeKind> NodeKind = new(
        (Beck.Rendering.NodeKind.Service, "service"),
        (Beck.Rendering.NodeKind.Db, "db"),
        (Beck.Rendering.NodeKind.Queue, "queue"),
        (Beck.Rendering.NodeKind.Cache, "cache"),
        (Beck.Rendering.NodeKind.Gateway, "gateway"),
        (Beck.Rendering.NodeKind.External, "external"),
        (Beck.Rendering.NodeKind.User, "user"),
        (Beck.Rendering.NodeKind.Ghost, "ghost"));

    public static readonly TokenMap<NodeVariant> NodeVariant = new(
        (Beck.Rendering.NodeVariant.Solid, "solid"),
        (Beck.Rendering.NodeVariant.Subtle, "subtle"),
        (Beck.Rendering.NodeVariant.Ghost, "ghost"));

    public static readonly TokenMap<NodeShape> NodeShape = new(
        (Beck.Rendering.NodeShape.Card, "card"),
        (Beck.Rendering.NodeShape.Pill, "pill"),
        (Beck.Rendering.NodeShape.Start, "start"),
        (Beck.Rendering.NodeShape.End, "end"),
        (Beck.Rendering.NodeShape.Class, "class"));

    public static readonly TokenMap<EdgeStyle> EdgeStyle = new(
        (Beck.Rendering.EdgeStyle.Solid, "solid"),
        (Beck.Rendering.EdgeStyle.Dashed, "dashed"));

    public static readonly TokenMap<EdgeCurve> EdgeCurve = new(
        (Beck.Rendering.EdgeCurve.StepRound, "step-round"),
        (Beck.Rendering.EdgeCurve.Straight, "straight"),
        (Beck.Rendering.EdgeCurve.S, "s"));

    public static readonly TokenMap<EdgeKind> EdgeKind = new(
        (Beck.Rendering.EdgeKind.Data, "data"),
        (Beck.Rendering.EdgeKind.Control, "control"),
        (Beck.Rendering.EdgeKind.Async, "async"),
        (Beck.Rendering.EdgeKind.Dependency, "dependency"));

    public static readonly TokenMap<PacketEase> PacketEase = new(
        (Beck.Rendering.PacketEase.Linear, "linear"),
        (Beck.Rendering.PacketEase.Smooth, "smooth"),
        (Beck.Rendering.PacketEase.Accelerate, "accelerate"),
        (Beck.Rendering.PacketEase.Decelerate, "decelerate"),
        (Beck.Rendering.PacketEase.Expo, "expo"),
        (Beck.Rendering.PacketEase.Sine, "sine"),
        (Beck.Rendering.PacketEase.Steps, "steps"),
        (Beck.Rendering.PacketEase.Bounce, "bounce"));

    public static readonly TokenMap<PacketShape> PacketShape = new(
        (Beck.Rendering.PacketShape.Dot, "dot"),
        (Beck.Rendering.PacketShape.Circle, "circle"),
        (Beck.Rendering.PacketShape.Ring, "ring"));

    public static readonly TokenMap<Side> Side = new(
        (Beck.Rendering.Side.Top, "top"),
        (Beck.Rendering.Side.Bottom, "bottom"),
        (Beck.Rendering.Side.Left, "left"),
        (Beck.Rendering.Side.Right, "right"));

    public static readonly TokenMap<ArrowEnds> ArrowEnds = new(
        (Beck.Rendering.ArrowEnds.None, "none"),
        (Beck.Rendering.ArrowEnds.End, "end"),
        (Beck.Rendering.ArrowEnds.Start, "start"),
        (Beck.Rendering.ArrowEnds.Both, "both"));

    public static readonly TokenMap<MarkerShape> MarkerShape = new(
        (Beck.Rendering.MarkerShape.Arrow, "arrow"),
        (Beck.Rendering.MarkerShape.ArrowOpen, "arrow-open"),
        (Beck.Rendering.MarkerShape.Triangle, "triangle"),
        (Beck.Rendering.MarkerShape.Diamond, "diamond"),
        (Beck.Rendering.MarkerShape.DiamondOpen, "diamond-open"));

    public static readonly TokenMap<AccentToken> Accent = new(
        (AccentToken.Primary, "primary"),
        (AccentToken.Success, "success"),
        (AccentToken.Warn, "warn"),
        (AccentToken.Danger, "danger"),
        (AccentToken.Info, "info"),
        (AccentToken.Neutral, "neutral"));
}
