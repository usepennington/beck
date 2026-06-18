using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Beck;

/// <summary>
/// Fluent builder for a diagram's animation <c>flow:</c> — the scripted sequence
/// of packets, status changes, and effects the engine plays. Mirrors the flow
/// steps the TypeScript engine parses (packet/burst/status/highlight/pulse/
/// activate/stream/working/idle/fail/phase/wait/reset/parallel). Without an
/// explicit flow the engine auto-derives one from the edges.
/// </summary>
/// <example>
/// <code>
/// new DiagramBuilder("Read path")
///     .Node("client", "Client").Node("api", "API").Node("db", n => n.Kind(NodeKind.Db))
///     .Edge("client", "api").Edge("api", "db")
///     .Flow(f => f
///         .Packet("client", "api", label: "GET /item")
///         .Working("db")
///         .Packet("api", "db", color: "info")
///         .Idle("db")
///         .Packet("db", "api", color: "success")
///         .Wait(1))
///     .ToFence();
/// </code>
/// </example>
public sealed class FlowBuilder
{
    private readonly List<(string Key, string Value)> _steps = new();
    private int? _repeat;
    private double? _repeatDelay;

    /// <summary>Repeat count: -1 loops forever (default), 0 plays once.</summary>
    public FlowBuilder Repeat(int repeat) { _repeat = repeat; return this; }

    /// <summary>Delay between repeats, in seconds.</summary>
    public FlowBuilder RepeatDelay(double seconds) { _repeatDelay = seconds; return this; }

    /// <summary>
    /// Send a packet from one node to another, optionally via waypoints. The
    /// motion/look knobs (<paramref name="shape"/>, <paramref name="ease"/>,
    /// <paramref name="size"/>, <paramref name="speed"/>, <paramref name="glow"/>,
    /// <paramref name="impact"/>) are optional and, when left null, fall back to
    /// the traversed edge's kind defaults.
    /// </summary>
    public FlowBuilder Packet(
        string from,
        string to,
        string? color = null,
        string? label = null,
        string[]? via = null,
        PacketEase? ease = null,
        double? size = null,
        double? speed = null,
        bool? glow = null,
        PacketShape? shape = null,
        bool? impact = null)
    {
        var pairs = new List<(string, string)>
        {
            ("from", YamlWriter.Scalar(from)),
            ("to", YamlWriter.Scalar(to)),
        };
        if (via is { Length: > 0 }) pairs.Add(("via", YamlWriter.FlowSeq(via.Select(YamlWriter.Scalar))));
        if (color != null) pairs.Add(("color", YamlWriter.Scalar(color)));
        if (label != null) pairs.Add(("label", YamlWriter.Scalar(label)));
        AppendPacketKnobs(pairs, shape, ease, size, speed, glow, impact);
        return Step("packet", YamlWriter.FlowMap(pairs));
    }

    /// <summary>
    /// Emit a burst of <paramref name="count"/> dots down an edge with a
    /// <paramref name="stagger"/> delay between them — a batch or a load spike.
    /// </summary>
    public FlowBuilder Burst(
        string from,
        string to,
        int count = 3,
        double stagger = 0.12,
        string? color = null,
        string? label = null,
        PacketEase? ease = null,
        double? size = null,
        double? speed = null,
        bool? glow = null,
        PacketShape? shape = null,
        bool? impact = null) =>
        BurstStep(from, YamlWriter.Scalar(to), count, stagger, color, label, ease, size, speed, glow, shape, impact);

    /// <summary>Fan a burst out from one source to several targets at once, staggered.</summary>
    public FlowBuilder Burst(
        string from,
        IEnumerable<string> to,
        int count = 3,
        double stagger = 0.12,
        string? color = null,
        string? label = null,
        PacketEase? ease = null,
        double? size = null,
        double? speed = null,
        bool? glow = null,
        PacketShape? shape = null,
        bool? impact = null) =>
        BurstStep(from, YamlWriter.FlowSeq(to.Select(YamlWriter.Scalar)), count, stagger, color, label, ease, size, speed, glow, shape, impact);

    private FlowBuilder BurstStep(
        string from,
        string toValue,
        int count,
        double stagger,
        string? color,
        string? label,
        PacketEase? ease,
        double? size,
        double? speed,
        bool? glow,
        PacketShape? shape,
        bool? impact)
    {
        var pairs = new List<(string, string)>
        {
            ("from", YamlWriter.Scalar(from)),
            ("to", toValue),
            ("count", count.ToString(CultureInfo.InvariantCulture)),
            ("stagger", stagger.ToString(CultureInfo.InvariantCulture)),
        };
        if (color != null) pairs.Add(("color", YamlWriter.Scalar(color)));
        if (label != null) pairs.Add(("label", YamlWriter.Scalar(label)));
        AppendPacketKnobs(pairs, shape, ease, size, speed, glow, impact);
        return Step("burst", YamlWriter.FlowMap(pairs));
    }

    /// <summary>Set a node's status-pill text (persists until changed).</summary>
    public FlowBuilder Status(string node, string text, string? color = null) =>
        Step("status", NodeMap(node, ("text", YamlWriter.Scalar(text)), Color(color)));

    /// <summary>Briefly highlight a node.</summary>
    public FlowBuilder Highlight(string node, string? color = null) =>
        Step("highlight", NodeMap(node, Color(color)));

    /// <summary>Pulse a node (a ripple on arrival).</summary>
    public FlowBuilder Pulse(string node, string? color = null) =>
        Step("pulse", NodeMap(node, Color(color)));

    /// <summary>Persistently recolor an edge (and its arrowhead) until the next reset.</summary>
    public FlowBuilder Activate(string from, string to, string? color = null) =>
        Step("activate", EdgeMap(from, to, color));

    /// <summary>Continuous flowing dashes along an edge (ongoing traffic), until reset.</summary>
    public FlowBuilder Stream(string from, string to, string? color = null) =>
        Step("stream", EdgeMap(from, to, color));

    /// <summary>Leave a node visibly busy (breathing glow) until <see cref="Idle"/> or reset.</summary>
    public FlowBuilder Working(string node, string? color = null) =>
        Step("working", NodeMap(node, Color(color)));

    /// <summary>Clear a node's <see cref="Working"/> state.</summary>
    public FlowBuilder Idle(string node) =>
        Step("idle", NodeMap(node));

    /// <summary>A failure beat: red shake + flash, with optional status text.</summary>
    public FlowBuilder Fail(string node, string? text = null, string? color = null)
    {
        var extra = new List<(string? Key, string Value)>();
        if (text != null) extra.Add(("text", YamlWriter.Scalar(text)));
        extra.Add(Color(color));
        return Step("fail", NodeMap(node, extra.ToArray()));
    }

    /// <summary>A named seek label (lets the handle's <c>seek(label)</c> jump here).</summary>
    public FlowBuilder Phase(string label) => Step("phase", YamlWriter.Scalar(label));

    /// <summary>Pause for a number of seconds.</summary>
    public FlowBuilder Wait(double seconds) => Step("wait", seconds.ToString(CultureInfo.InvariantCulture));

    /// <summary>Reset the diagram to its initial state.</summary>
    public FlowBuilder Reset() => Step("reset", "true");

    /// <summary>Run a group of steps simultaneously.</summary>
    public FlowBuilder Parallel(Action<FlowBuilder> build)
    {
        var sub = new FlowBuilder();
        build(sub);
        var items = sub._steps.Select(s => YamlWriter.FlowMap(new[] { (s.Key, s.Value) }));
        return Step("parallel", YamlWriter.FlowSeq(items));
    }

    // ---- helpers ----

    private FlowBuilder Step(string key, string value)
    {
        _steps.Add((key, value));
        return this;
    }

    private static (string? Key, string Value) Color(string? color) =>
        color == null ? (null, "") : ("color", YamlWriter.Scalar(color));

    private static void AppendPacketKnobs(
        List<(string Key, string Value)> pairs,
        PacketShape? shape,
        PacketEase? ease,
        double? size,
        double? speed,
        bool? glow,
        bool? impact)
    {
        if (shape is { } sh) pairs.Add(("shape", Tokens.Of(sh)));
        if (ease is { } e) pairs.Add(("ease", Tokens.Of(e)));
        if (size is { } sz) pairs.Add(("size", sz.ToString(CultureInfo.InvariantCulture)));
        if (speed is { } sp) pairs.Add(("speed", sp.ToString(CultureInfo.InvariantCulture)));
        if (glow is { } g) pairs.Add(("glow", g ? "true" : "false"));
        if (impact is { } im) pairs.Add(("impact", im ? "true" : "false"));
    }

    private static string NodeMap(string node, params (string? Key, string Value)[] extra)
    {
        var pairs = new List<(string, string)> { ("node", YamlWriter.Scalar(node)) };
        foreach (var (k, v) in extra)
            if (k != null) pairs.Add((k, v));
        return YamlWriter.FlowMap(pairs);
    }

    private static string EdgeMap(string from, string to, string? color)
    {
        var pairs = new List<(string, string)>
        {
            ("from", YamlWriter.Scalar(from)),
            ("to", YamlWriter.Scalar(to)),
        };
        if (color != null) pairs.Add(("color", YamlWriter.Scalar(color)));
        return YamlWriter.FlowMap(pairs);
    }

    /// <summary>Emit the <c>flow:</c> block (only when at least one step was added).</summary>
    internal void AppendYaml(StringBuilder sb)
    {
        if (_steps.Count == 0 && _repeat == null && _repeatDelay == null) return;
        sb.Append("flow:\n");
        if (_repeat is { } r) sb.Append("  repeat: ").Append(r.ToString(CultureInfo.InvariantCulture)).Append('\n');
        if (_repeatDelay is { } rd) sb.Append("  repeatDelay: ").Append(rd.ToString(CultureInfo.InvariantCulture)).Append('\n');
        sb.Append("  steps:\n");
        foreach (var (key, value) in _steps)
            sb.Append("    - ").Append(key).Append(": ").Append(value).Append('\n');
    }
}
