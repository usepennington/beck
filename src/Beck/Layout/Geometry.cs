using Beck.Model;

namespace Beck.Layout;

/// <summary>An intrinsic size in px (a measured card, a laid-out box).</summary>
internal readonly record struct Size(double W, double H);

/// <summary>A 2D point in canvas coordinates.</summary>
internal readonly record struct Point(double X, double Y);

/// <summary>A placed rectangle (top-left corner + size), in canvas coordinates.</summary>
internal readonly record struct Rect(double X, double Y, double W, double H)
{
    public Rect Offset(double dx, double dy) => new(X + dx, Y + dy, W, H);
}

/// <summary>The layered/sequence layout output.</summary>
internal sealed record LayoutResult(
    IReadOnlyDictionary<string, Rect> Nodes,
    IReadOnlyDictionary<string, Rect> Groups,
    double Width,
    double Height);

internal static class Geometry
{
    /// <summary>
    /// True when <paramref name="to"/> sits entirely behind <paramref name="from"/>
    /// on the primary axis for <paramref name="dir"/> — a feedback / "back" edge
    /// running against the flow. Shared by layout (reserves an outer gutter) and
    /// routing (diverts onto a secondary face). Port of <c>types.ts:againstFlow</c>.
    /// </summary>
    public static bool AgainstFlow(Rect from, Rect to, Direction dir) => dir switch
    {
        Direction.TB => to.Y + to.H <= from.Y,
        Direction.BT => to.Y >= from.Y + from.H,
        Direction.LR => to.X + to.W <= from.X,
        Direction.RL => to.X >= from.X + from.W,
        _ => false,
    };
}
