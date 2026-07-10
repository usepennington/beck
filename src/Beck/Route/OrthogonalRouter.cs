namespace Beck.Rendering.Route;

internal sealed record RouteRequest(
    Rect From, Rect To, Side? FromSide, Side? ToSide, EdgeCurve Curve,
    IReadOnlyList<Rect> Obstacles, double Radius, bool PrimaryHorizontal,
    Size? Bounds, double FromShift, double ToShift,
    // Whether each anchor sits on a face shared with other edges. A fanned face's anchors are
    // evenly spread by AnchorShifts, and sliding any one of them — including the middle edge,
    // whose shift is 0 — breaks that spacing. Not inferable from the shift alone.
    bool FromFanned = false, bool ToFanned = false);

internal sealed record RoutedPath(string D, IReadOnlyList<Point> Points);

/// <summary>
/// Auto orthogonal step-round edge routing with obstacle avoidance — a port of
/// <c>src/route/orthogonal.ts</c>. Constants and geometry mirror the TS exactly so
/// the emitted path <c>d</c> matches.
/// </summary>
internal static class OrthogonalRouter
{
    private const double ChannelOffset = 18;
    private const double LanePad = 22;
    private const double LaneMargin = 6;
    private const double SelfLoopExtent = 30;
    private const double SameRankEps = 6;
    private const double StraightenInset = 6;    // keep a nudged anchor this far off a face corner
    private const double StraightenTotal = 24;   // most the anchors may slide, combined

    private static Point Center(Rect r) => new(r.X + r.W / 2, r.Y + r.H / 2);
    private static bool IsVertical(Side s) => s is Side.Top or Side.Bottom;

    private static Point Anchor(Rect rect, Side side)
    {
        Point c = Center(rect);
        return side switch
        {
            Side.Top => new Point(c.X, rect.Y),
            Side.Bottom => new Point(c.X, rect.Y + rect.H),
            Side.Left => new Point(rect.X, c.Y),
            _ => new Point(rect.X + rect.W, c.Y), // right
        };
    }

    public static (Side FromSide, Side ToSide) AutoSides(Rect from, Rect to, bool primaryHorizontal)
    {
        Point f = Center(from), t = Center(to);
        double dx = t.X - f.X, dy = t.Y - f.Y;
        if (primaryHorizontal)
        {
            if (Math.Abs(dx) > SameRankEps)
                return dx >= 0 ? (Side.Right, Side.Left) : (Side.Left, Side.Right);
            return dy >= 0 ? (Side.Bottom, Side.Top) : (Side.Top, Side.Bottom);
        }
        if (Math.Abs(dy) > SameRankEps)
            return dy >= 0 ? (Side.Bottom, Side.Top) : (Side.Top, Side.Bottom);
        return dx >= 0 ? (Side.Right, Side.Left) : (Side.Left, Side.Right);
    }

    public static (Side FromSide, Side ToSide) SidesFor(
        Rect from, Rect to, Direction dir, EdgeCurve curve, IReadOnlyList<Rect> obstacles,
        Side? explicitFrom, Side? explicitTo, Size? bounds = null)
    {
        bool primaryHorizontal = dir is Direction.LR or Direction.RL;
        var auto = AutoSides(from, to, primaryHorizontal);
        if (curve == EdgeCurve.StepRound && explicitFrom is null && explicitTo is null
            && Geometry.AgainstFlow(from, to, dir))
        {
            // A back edge keeps its direct opposite-face route when the corridor is
            // genuinely clear (e.g. a rank-pinned child sitting behind its parents);
            // it diverts to the gutter face only when that route would cross a node,
            // as a feedback edge over intermediate ranks does.
            Point a = Anchor(from, auto.FromSide), b = Anchor(to, auto.ToSide);
            List<Point> simple = IsVertical(auto.FromSide)
                ? new() { a, new(a.X, Channel(a.Y, b.Y, 0, 0)), new(b.X, Channel(a.Y, b.Y, 0, 0)), b }
                : new() { a, new(Channel(a.X, b.X, 0, 0), a.Y), new(Channel(a.X, b.X, 0, 0), b.Y), b };
            if (PolylineHits(simple, obstacles))
            {
                // Both gutters are reserved (BackEdgeGutter widens the canvas on either side), so
                // take whichever one this edge can actually reach: the run from each anchor out to
                // the lane travels along its own rank, and a sibling card sitting between the node
                // and the near gutter would be sliced straight through. Near face first, so a clear
                // route keeps the historical side.
                Side near = primaryHorizontal ? Side.Top : Side.Left;
                Side far = primaryHorizontal ? Side.Bottom : Side.Right;
                foreach (Side face in new[] { near, far })
                    if (!PolylineHits(SameFaceLoop(Anchor(from, face), Anchor(to, face), face, obstacles, bounds), obstacles))
                        return (face, face);
                return (near, near);
            }
        }
        return (explicitFrom ?? auto.FromSide, explicitTo ?? auto.ToSide);
    }

    private static bool SegHitsRect(Point a, Point b, Rect rect, double inset = 3)
    {
        double x1 = rect.X + inset, y1 = rect.Y + inset;
        double x2 = rect.X + rect.W - inset, y2 = rect.Y + rect.H - inset;
        if (x2 <= x1 || y2 <= y1) return false;
        if (Math.Abs(a.Y - b.Y) < 0.5)
        {
            double y = a.Y;
            if (y <= y1 || y >= y2) return false;
            return Math.Max(a.X, b.X) > x1 && Math.Min(a.X, b.X) < x2;
        }
        if (Math.Abs(a.X - b.X) < 0.5)
        {
            double x = a.X;
            if (x <= x1 || x >= x2) return false;
            return Math.Max(a.Y, b.Y) > y1 && Math.Min(a.Y, b.Y) < y2;
        }
        return Math.Max(a.X, b.X) > x1 && Math.Min(a.X, b.X) < x2 && Math.Max(a.Y, b.Y) > y1 && Math.Min(a.Y, b.Y) < y2;
    }

    private static bool PolylineHits(IReadOnlyList<Point> points, IReadOnlyList<Rect> obstacles)
    {
        for (int i = 0; i < points.Count - 1; i++)
            foreach (var o in obstacles) if (SegHitsRect(points[i], points[i + 1], o)) return true;
        return false;
    }

    private static double ClampLane(double value, double? extent)
    {
        double lo = LaneMargin;
        double hi = extent is double e ? Math.Max(lo, e - LaneMargin) : double.PositiveInfinity;
        return Math.Min(Math.Max(value, lo), hi);
    }

    private static List<Point>? LaneDetour(Point a, Point b, bool vertical, IReadOnlyList<Rect> obstacles, Size? bounds)
    {
        if (obstacles.Count == 0) return null;
        if (vertical)
        {
            double s = Math.Sign(b.Y - a.Y); double dirY = s != 0 ? s : 1;
            double ch1 = a.Y + dirY * ChannelOffset, ch2 = b.Y - dirY * ChannelOffset;
            double minX = obstacles.Min(o => o.X), maxX = obstacles.Max(o => o.X + o.W);
            double left = ClampLane(minX - LanePad, bounds?.W), right = ClampLane(maxX + LanePad, bounds?.W);
            bool preferLeft = (a.X + b.X) / 2 <= (minX + maxX) / 2;
            foreach (double laneX in preferLeft ? new[] { left, right } : new[] { right, left })
            {
                var poly = new List<Point> { a, new(a.X, ch1), new(laneX, ch1), new(laneX, ch2), new(b.X, ch2), b };
                if (!PolylineHits(poly, obstacles)) return poly;
            }
        }
        else
        {
            double s = Math.Sign(b.X - a.X); double dirX = s != 0 ? s : 1;
            double ch1 = a.X + dirX * ChannelOffset, ch2 = b.X - dirX * ChannelOffset;
            double minY = obstacles.Min(o => o.Y), maxY = obstacles.Max(o => o.Y + o.H);
            double top = ClampLane(minY - LanePad, bounds?.H), bottom = ClampLane(maxY + LanePad, bounds?.H);
            bool preferTop = (a.Y + b.Y) / 2 <= (minY + maxY) / 2;
            foreach (double laneY in preferTop ? new[] { top, bottom } : new[] { bottom, top })
            {
                var poly = new List<Point> { a, new(ch1, a.Y), new(ch1, laneY), new(ch2, laneY), new(ch2, b.Y), b };
                if (!PolylineHits(poly, obstacles)) return poly;
            }
        }
        return null;
    }

    private static Point ShiftAnchor(Point p, Side side, double off)
    {
        if (off == 0) return p;
        return IsVertical(side) ? new Point(p.X + off, p.Y) : new Point(p.X, p.Y + off);
    }

    private static List<Point> SameFaceLoop(Point a, Point b, Side side, IReadOnlyList<Rect> obstacles, Size? bounds)
    {
        if (IsVertical(side))
        {
            double lo = Math.Min(a.X, b.X), hi = Math.Max(a.X, b.X);
            var spanned = obstacles.Where(o => o.X < hi && o.X + o.W > lo).ToList();
            double laneY = ClampLane(
                side == Side.Top
                    ? new[] { a.Y, b.Y }.Concat(spanned.Select(o => o.Y)).Min() - LanePad
                    : new[] { a.Y, b.Y }.Concat(spanned.Select(o => o.Y + o.H)).Max() + LanePad,
                bounds?.H);
            return new List<Point> { a, new(a.X, laneY), new(b.X, laneY), b };
        }
        double loY = Math.Min(a.Y, b.Y), hiY = Math.Max(a.Y, b.Y);
        var spannedH = obstacles.Where(o => o.Y < hiY && o.Y + o.H > loY).ToList();
        double laneX = ClampLane(
            side == Side.Left
                ? new[] { a.X, b.X }.Concat(spannedH.Select(o => o.X)).Min() - LanePad
                : new[] { a.X, b.X }.Concat(spannedH.Select(o => o.X + o.W)).Max() + LanePad,
            bounds?.W);
        return new List<Point> { a, new(laneX, a.Y), new(laneX, b.Y), b };
    }

    /// <summary>
    /// Straighten a nearly-aligned edge by sliding its anchors along their faces — a small "cheat"
    /// so a short cross-axis jog reads as one clean straight line instead of a stepped Z. Applies
    /// only to opposite parallel faces, only when the anchors' perpendicular gap is within budget,
    /// and only when the resulting straight run clears every obstacle.
    ///
    /// <para>An anchor on a fanned face never moves. AnchorShifts spreads such a face's anchors
    /// evenly, and sliding any one of them — the middle edge included, whose shift is 0 — trades a
    /// straight line for a visibly uneven fan. So a lone anchor may slide to meet a fanned one, two
    /// lone anchors split the difference, and two fanned anchors leave the jog alone: with layout
    /// placing nodes on their median neighbor, a fan that still needs a cheat is one the router
    /// should not be papering over.</para>
    /// </summary>
    private static bool TryStraighten(
        Rect from, Rect to, Side fromSide, Side toSide, bool fromFanned, bool toFanned,
        IReadOnlyList<Rect> obstacles, ref Point a, ref Point b)
    {
        if (fromSide == toSide) return false;
        bool vert = IsVertical(fromSide) && IsVertical(toSide);   // Top/Bottom faces → perp axis = X
        bool horz = !IsVertical(fromSide) && !IsVertical(toSide); // Left/Right faces → perp axis = Y
        if (!vert && !horz) return false;

        double aPerp = vert ? a.X : a.Y, bPerp = vert ? b.X : b.Y;
        double off = bPerp - aPerp;
        if (Math.Abs(off) < 0.5) return false;             // already straight
        if (Math.Abs(off) > StraightenTotal) return false; // genuine jog — leave it stepped

        // Reachable band = the two faces' overlap on the perp axis, inset off the corners.
        double aLo = (vert ? from.X : from.Y) + StraightenInset, aHi = (vert ? from.X + from.W : from.Y + from.H) - StraightenInset;
        double bLo = (vert ? to.X : to.Y) + StraightenInset, bHi = (vert ? to.X + to.W : to.Y + to.H) - StraightenInset;
        double lo = Math.Max(aLo, bLo), hi = Math.Min(aHi, bHi);
        if (hi < lo) return false; // faces don't overlap — no shared straight line exists

        // Move a lone anchor in preference to a fanned one; only when both faces are fanned (so
        // there is no free anchor to give) do we split the nudge and accept the uneven spread.
        bool aFree = !fromFanned, bFree = !toFanned;
        double target = aFree && bFree ? (aPerp + bPerp) / 2
            : aFree ? bPerp
            : bFree ? aPerp
            : (aPerp + bPerp) / 2;
        target = Math.Clamp(target, lo, hi);

        // One anchor may absorb the whole gap when it is the only one free to move; when both move
        // they split it, so neither travels far. Either way the combined slide stays in budget.
        if (Math.Abs(target - aPerp) + Math.Abs(target - bPerp) > StraightenTotal + 0.01) return false;

        Point na = vert ? new Point(target, a.Y) : new Point(a.X, target);
        Point nb = vert ? new Point(target, b.Y) : new Point(b.X, target);
        if (PolylineHits(new[] { na, nb }, obstacles)) return false;
        a = na; b = nb;
        return true;
    }

    /// <summary>
    /// The turn column (or row) for a step between opposite parallel faces. Starts at the gap's
    /// midpoint, then staggers fanned edges (nonzero anchor shifts) toward the fanning node,
    /// outermost anchor first, so a fan's parallel runs occupy parallel channels instead of
    /// merging into one visual trunk — collinear runs never cross (their spans are disjoint),
    /// but they read as a single wire. Clamped clear of both faces; degenerate gaps keep the mid.
    /// </summary>
    private static double Channel(double aC, double bC, double fromShift, double toShift)
    {
        double mid = (aC + bC) / 2;
        double channel = mid + Math.Sign(bC - aC) * (Math.Abs(toShift) - Math.Abs(fromShift));
        double lo = Math.Min(aC, bC) + ChannelOffset, hi = Math.Max(aC, bC) - ChannelOffset;
        return lo <= hi ? Math.Clamp(channel, lo, hi) : mid;
    }

    private static List<Point> OrthogonalPolyline(
        Point a, Point b, Side fromSide, Side toSide, double fromShift, double toShift,
        IReadOnlyList<Rect> obstacles, Size? bounds)
    {
        if (fromSide == toSide) return SameFaceLoop(a, b, fromSide, obstacles, bounds);

        bool vert = IsVertical(fromSide) && IsVertical(toSide);
        bool horz = !IsVertical(fromSide) && !IsVertical(toSide);

        if (vert)
        {
            double channelY = Channel(a.Y, b.Y, fromShift, toShift);
            var simple = new List<Point> { a, new(a.X, channelY), new(b.X, channelY), b };
            if (!PolylineHits(simple, obstacles)) return simple;
            return LaneDetour(a, b, true, obstacles, bounds) ?? simple;
        }
        if (horz)
        {
            double channelX = Channel(a.X, b.X, fromShift, toShift);
            var simple = new List<Point> { a, new(channelX, a.Y), new(channelX, b.Y), b };
            if (!PolylineHits(simple, obstacles)) return simple;
            return LaneDetour(a, b, false, obstacles, bounds) ?? simple;
        }
        Point corner = IsVertical(fromSide) ? new Point(a.X, b.Y) : new Point(b.X, a.Y);
        return new List<Point> { a, corner, b };
    }

    private static string SCurve(Point a, Point b, Side fromSide)
    {
        string S(double n) => Js.Str(n);
        if (IsVertical(fromSide))
        {
            double off = (b.Y - a.Y) * 0.4;
            return $"M {S(a.X)} {S(a.Y)} C {S(a.X)} {S(a.Y + off)}, {S(b.X)} {S(b.Y - off)}, {S(b.X)} {S(b.Y)}";
        }
        double offx = (b.X - a.X) * 0.4;
        return $"M {S(a.X)} {S(a.Y)} C {S(a.X + offx)} {S(a.Y)}, {S(b.X - offx)} {S(b.Y)}, {S(b.X)} {S(b.Y)}";
    }

    public static RoutedPath RouteEdge(RouteRequest req)
    {
        bool self = req.From == req.To;
        if (self)
        {
            Rect r = req.From;
            List<Point> selfPoly = req.PrimaryHorizontal
                ? new()
                {
                    new(r.X + r.W * 0.3, r.Y + r.H),
                    new(r.X + r.W * 0.3, r.Y + r.H + SelfLoopExtent),
                    new(r.X + r.W * 0.7, r.Y + r.H + SelfLoopExtent),
                    new(r.X + r.W * 0.7, r.Y + r.H),
                }
                : new()
                {
                    new(r.X + r.W, r.Y + r.H * 0.3),
                    new(r.X + r.W + SelfLoopExtent, r.Y + r.H * 0.3),
                    new(r.X + r.W + SelfLoopExtent, r.Y + r.H * 0.7),
                    new(r.X + r.W, r.Y + r.H * 0.7),
                };
            return new RoutedPath(StepRound.RoundedPath(selfPoly, Math.Min(req.Radius, 10)), selfPoly);
        }

        var auto = AutoSides(req.From, req.To, req.PrimaryHorizontal);
        Side fromSide = req.FromSide ?? auto.FromSide;
        Side toSide = req.ToSide ?? auto.ToSide;
        Point a = ShiftAnchor(Anchor(req.From, fromSide), fromSide, req.FromShift);
        Point b = ShiftAnchor(Anchor(req.To, toSide), toSide, req.ToShift);

        if (req.Curve == EdgeCurve.Straight)
            return new RoutedPath($"M {Js.Str(a.X)} {Js.Str(a.Y)} L {Js.Str(b.X)} {Js.Str(b.Y)}", new[] { a, b });
        if (req.Curve == EdgeCurve.S)
            return new RoutedPath(SCurve(a, b, fromSide), new[] { a, b });

        var poly = TryStraighten(req.From, req.To, fromSide, toSide, req.FromFanned, req.ToFanned, req.Obstacles, ref a, ref b)
            ? new List<Point> { a, b }
            : OrthogonalPolyline(a, b, fromSide, toSide, req.FromShift, req.ToShift, req.Obstacles, req.Bounds);
        return new RoutedPath(StepRound.RoundedPath(poly, req.Radius), poly);
    }
}
