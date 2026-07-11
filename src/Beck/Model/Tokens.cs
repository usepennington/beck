namespace Beck.Model;

// The closed token vocabularies, ported verbatim from src/model/schema.ts. Each
// enum member maps to the exact wire token the YAML/JSON uses, so the model
// round-trips identically to the TypeScript oracle. Enums (not strings) so the
// downstream layout/route/render/animate stages switch exhaustively.

/// <summary>What the diagram is; picks the layout + routing strategy.</summary>
internal enum DiagramType { Architecture, Sequence, State, Class, Flowchart, MindMap }

/// <summary>Primary layout axis.</summary>
internal enum Direction { Tb, Bt, Lr, Rl }

/// <summary>How a diagram wider than its container behaves.</summary>
internal enum FitMode { Shrink, Scroll }

internal enum NodeKind { Service, Db, Queue, Cache, Gateway, External, User, Ghost }

internal enum NodeVariant { Solid, Subtle, Ghost }

/// <summary>Structural form of a node.</summary>
internal enum NodeShape { Card, Pill, Start, End, Class, Diamond, Parallelogram }

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
        (Model.DiagramType.Architecture, "architecture"),
        (Model.DiagramType.Sequence, "sequence"),
        (Model.DiagramType.State, "state"),
        (Model.DiagramType.Class, "class"),
        (Model.DiagramType.Flowchart, "flowchart"),
        (Model.DiagramType.MindMap, "mindmap"));

    public static readonly TokenMap<Direction> Direction = new(
        (Model.Direction.Tb, "TB"),
        (Model.Direction.Bt, "BT"),
        (Model.Direction.Lr, "LR"),
        (Model.Direction.Rl, "RL"));

    public static readonly TokenMap<ThemeMode> Theme = new(
        (ThemeMode.Auto, "auto"),
        (ThemeMode.Light, "light"),
        (ThemeMode.Dark, "dark"));

    public static readonly TokenMap<FitMode> Fit = new(
        (FitMode.Shrink, "shrink"),
        (FitMode.Scroll, "scroll"));

    public static readonly TokenMap<NodeKind> NodeKind = new(
        (Model.NodeKind.Service, "service"),
        (Model.NodeKind.Db, "db"),
        (Model.NodeKind.Queue, "queue"),
        (Model.NodeKind.Cache, "cache"),
        (Model.NodeKind.Gateway, "gateway"),
        (Model.NodeKind.External, "external"),
        (Model.NodeKind.User, "user"),
        (Model.NodeKind.Ghost, "ghost"));

    public static readonly TokenMap<NodeVariant> NodeVariant = new(
        (Model.NodeVariant.Solid, "solid"),
        (Model.NodeVariant.Subtle, "subtle"),
        (Model.NodeVariant.Ghost, "ghost"));

    public static readonly TokenMap<NodeShape> NodeShape = new(
        (Model.NodeShape.Card, "card"),
        (Model.NodeShape.Pill, "pill"),
        (Model.NodeShape.Start, "start"),
        (Model.NodeShape.End, "end"),
        (Model.NodeShape.Class, "class"),
        (Model.NodeShape.Diamond, "diamond"),
        (Model.NodeShape.Parallelogram, "parallelogram"));

    public static readonly TokenMap<EdgeStyle> EdgeStyle = new(
        (Model.EdgeStyle.Solid, "solid"),
        (Model.EdgeStyle.Dashed, "dashed"));

    public static readonly TokenMap<EdgeCurve> EdgeCurve = new(
        (Model.EdgeCurve.StepRound, "step-round"),
        (Model.EdgeCurve.Straight, "straight"),
        (Model.EdgeCurve.S, "s"));

    public static readonly TokenMap<EdgeKind> EdgeKind = new(
        (Model.EdgeKind.Data, "data"),
        (Model.EdgeKind.Control, "control"),
        (Model.EdgeKind.Async, "async"),
        (Model.EdgeKind.Dependency, "dependency"));

    public static readonly TokenMap<PacketEase> PacketEase = new(
        (Model.PacketEase.Linear, "linear"),
        (Model.PacketEase.Smooth, "smooth"),
        (Model.PacketEase.Accelerate, "accelerate"),
        (Model.PacketEase.Decelerate, "decelerate"),
        (Model.PacketEase.Expo, "expo"),
        (Model.PacketEase.Sine, "sine"),
        (Model.PacketEase.Steps, "steps"),
        (Model.PacketEase.Bounce, "bounce"));

    public static readonly TokenMap<PacketShape> PacketShape = new(
        (Model.PacketShape.Dot, "dot"),
        (Model.PacketShape.Circle, "circle"),
        (Model.PacketShape.Ring, "ring"));

    public static readonly TokenMap<Side> Side = new(
        (Model.Side.Top, "top"),
        (Model.Side.Bottom, "bottom"),
        (Model.Side.Left, "left"),
        (Model.Side.Right, "right"));

    public static readonly TokenMap<ArrowEnds> ArrowEnds = new(
        (Model.ArrowEnds.None, "none"),
        (Model.ArrowEnds.End, "end"),
        (Model.ArrowEnds.Start, "start"),
        (Model.ArrowEnds.Both, "both"));

    public static readonly TokenMap<MarkerShape> MarkerShape = new(
        (Model.MarkerShape.Arrow, "arrow"),
        (Model.MarkerShape.ArrowOpen, "arrow-open"),
        (Model.MarkerShape.Triangle, "triangle"),
        (Model.MarkerShape.Diamond, "diamond"),
        (Model.MarkerShape.DiamondOpen, "diamond-open"));

    public static readonly TokenMap<AccentToken> Accent = new(
        (AccentToken.Primary, "primary"),
        (AccentToken.Success, "success"),
        (AccentToken.Warn, "warn"),
        (AccentToken.Danger, "danger"),
        (AccentToken.Info, "info"),
        (AccentToken.Neutral, "neutral"));
}