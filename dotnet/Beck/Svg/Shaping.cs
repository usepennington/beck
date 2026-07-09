using System.Text;

namespace Beck.Rendering.Svg;

/// <summary>
/// The per-style path-<em>shaping</em> seam (<see cref="StyleEdges.BowAmplitude"/> /
/// <see cref="StyleEdges.Lifeline"/> / <see cref="StyleEdges.WobblySeparators"/>): deterministic
/// geometry transforms applied at the SVG layer, keyed off the content hash so the same input shapes
/// the same way forever (no RNG, no clock). The router is never touched — a bow is rebuilt from the
/// already-computed route polyline with the endpoints and every interior elbow anchor preserved, and
/// the result is still <em>one</em> continuous <c>&lt;path&gt;</c> (so packets/trails that ride the
/// path via <c>offset-path</c> stay valid). At classic values (zero amplitude) every helper returns
/// the input verbatim, so classic output is byte-identical.
/// </summary>
internal static class Shaping
{
    private static string N(double n) => SvgWriter.Num(n);
    private static string F((double X, double Y) p) => N(p.X) + " " + N(p.Y);

    /// <summary>
    /// The effective edge path: the router's <paramref name="originalD"/> verbatim when the style
    /// bows nothing, else a bowed single path rebuilt from <paramref name="pts"/>. Reused for both the
    /// rendered edge and the flow edge so a packet rides exactly the drawn curve.
    /// </summary>
    public static string EdgePath(BeckStyle style, string originalD, IReadOnlyList<Point> pts, string seed)
    {
        double amp = style.Edges.BowAmplitude;
        if (amp <= 0) return originalD;
        var simple = Simplify(pts);
        if (simple.Count < 2) return originalD;
        return Bow(simple, amp, seed);
    }

    /// <summary>
    /// A single continuous path that bows through each straight run of <paramref name="pts"/>: every
    /// segment becomes a quadratic through a perpendicular-displaced midpoint (amplitude
    /// <paramref name="amp"/>, sign/size hash-seeded), so the drawn line reads hand-wobbled while its
    /// endpoints and every anchor in <paramref name="pts"/> land exactly on the curve.
    /// </summary>
    public static string Bow(IReadOnlyList<Point> pts, double amp, string seed)
    {
        var rng = new Rng(seed);
        var sb = new StringBuilder();
        sb.Append('M').Append(F((pts[0].X, pts[0].Y)));
        for (int i = 1; i < pts.Count; i++)
        {
            Point a = pts[i - 1], b = pts[i];
            double dx = b.X - a.X, dy = b.Y - a.Y;
            double len = Math.Sqrt(dx * dx + dy * dy);
            if (len < 1e-6) { sb.Append('L').Append(F((b.X, b.Y))); continue; }
            double nx = -dy / len, ny = dx / len;                 // unit perpendicular
            double bow = (rng.Next() - 0.5) * 2 * amp;            // signed, hash-seeded
            double mx = (a.X + b.X) / 2 + nx * bow, my = (a.Y + b.Y) / 2 + ny * bow;
            sb.Append('Q').Append(F((mx, my))).Append(' ').Append(F((b.X, b.Y)));
        }
        return sb.ToString();
    }

    /// <summary>A straight two-point run bowed once (sketch's wobbly lifelines + class separators): the
    /// endpoints are preserved exactly, one sideways bow between them.</summary>
    public static string BowLine(double x1, double y1, double x2, double y2, double amp, string seed) =>
        Bow(new[] { new Point(x1, y1), new Point(x2, y2) }, amp, seed);

    /// <summary>A deterministic phase fraction in [0,1) for an overlay element, from the content hash +
    /// its edge index — the baked per-edge comet offset (no <c>animation-delay</c>).</summary>
    public static double Phase(string seed) => new Rng(seed).Next();

    /// <summary>Reduce a route polyline to <c>[first, …corners…, last]</c>: drop near-duplicate points
    /// and any collinear straight-through vertex, keeping the true endpoints. Mirrors the router's own
    /// corner test so a bow segment spans each genuine straight run.</summary>
    private static List<Point> Simplify(IReadOnlyList<Point> pts)
    {
        var dedup = new List<Point>();
        foreach (Point p in pts)
        {
            if (dedup.Count == 0) { dedup.Add(p); continue; }
            Point prev = dedup[^1];
            if (Math.Abs(prev.X - p.X) > 0.5 || Math.Abs(prev.Y - p.Y) > 0.5) dedup.Add(p);
        }
        if (dedup.Count <= 2) return dedup;
        var outp = new List<Point> { dedup[0] };
        for (int i = 1; i < dedup.Count - 1; i++)
        {
            Point a = dedup[i - 1], c = dedup[i], d = dedup[i + 1];
            double cross = (c.X - a.X) * (d.Y - a.Y) - (c.Y - a.Y) * (d.X - a.X);
            if (Math.Abs(cross) >= 0.01) outp.Add(c);
        }
        outp.Add(dedup[^1]);
        return outp;
    }

    /// <summary>
    /// A tiny deterministic PRNG seeded from a string (FNV-1a → xorshift32) — the same generator
    /// <see cref="Artwork"/> uses for node wobble, so shaping jitter is reproducible from the content
    /// hash and never touches <see cref="System.Random"/> or the clock.
    /// </summary>
    private sealed class Rng
    {
        private uint _s;
        public Rng(string seed)
        {
            uint hsh = 2166136261;
            foreach (char c in seed) { hsh ^= c; hsh *= 16777619; }
            _s = hsh == 0 ? 1u : hsh;
        }
        public double Next()
        {
            _s ^= _s << 13;
            _s ^= _s >> 17;
            _s ^= _s << 5;
            return (_s & 0xFFFFFF) / (double)0x1000000;
        }
    }
}
