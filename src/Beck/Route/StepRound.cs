using Beck.Layout;
using Beck.Model;

namespace Beck.Route;

/// <summary>
/// Quadratic corner-rounding path builder — a port of <c>src/route/step-round.ts</c>.
/// Each interior corner is rounded with a quarter-circle (quadratic) arc, radius
/// clamped to half of each adjacent segment. Coordinates round to 2 decimals with
/// JS <c>Math.round</c> semantics.
/// </summary>
internal static class StepRound
{
    public static double Dist(Point a, Point b)
    {
        double dx = b.X - a.X, dy = b.Y - a.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static Point Toward(Point from, Point to, double d)
    {
        double len = Dist(from, to);
        if (len == 0) len = 1;
        return new Point(from.X + (to.X - from.X) / len * d, from.Y + (to.Y - from.Y) / len * d);
    }

    private static bool Collinear(Point a, Point b, Point c) =>
        Math.Abs((b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X)) < 0.01;

    /// <summary>2-decimal coordinate formatting (JS <c>r(n) = Math.round(n*100)/100</c>).</summary>
    public static string R(double n) => Js.Str(Js.Round(n * 100) / 100);

    public static string RoundedPath(IReadOnlyList<Point> points, double radius)
    {
        var pts = Dedupe(points);
        if (pts.Count < 2) return "";
        if (pts.Count == 2) return $"M {R(pts[0].X)} {R(pts[0].Y)} L {R(pts[1].X)} {R(pts[1].Y)}";

        var d = $"M {R(pts[0].X)} {R(pts[0].Y)}";
        for (int i = 1; i < pts.Count - 1; i++)
        {
            Point prev = pts[i - 1], cur = pts[i], next = pts[i + 1];
            if (Collinear(prev, cur, next)) { d += $" L {R(cur.X)} {R(cur.Y)}"; continue; }
            double rad = Math.Min(radius, Math.Min(Dist(prev, cur) / 2, Dist(cur, next) / 2));
            Point a = Toward(cur, prev, rad);
            Point b = Toward(cur, next, rad);
            d += $" L {R(a.X)} {R(a.Y)} Q {R(cur.X)} {R(cur.Y)} {R(b.X)} {R(b.Y)}";
        }
        Point last = pts[^1];
        d += $" L {R(last.X)} {R(last.Y)}";
        return d;
    }

    private static List<Point> Dedupe(IReadOnlyList<Point> points)
    {
        var outp = new List<Point>();
        foreach (var p in points)
        {
            if (outp.Count == 0) { outp.Add(p); continue; }
            Point prev = outp[^1];
            if (Math.Abs(prev.X - p.X) > 0.5 || Math.Abs(prev.Y - p.Y) > 0.5) outp.Add(p);
        }
        return outp;
    }
}
