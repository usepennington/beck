using Beck.Layout;
using Beck.Model;

namespace Beck.Route;

/// <summary>
/// One edge's turn lane on a node face: which channel it takes among the <paramref name="Count"/>
/// edges sharing that face, ordered outermost-bend first. Lane 0 turns nearest its own node.
/// </summary>
internal readonly record struct Lane(int Index, int Count)
{
    public static readonly Lane Solo = new(0, 1);

    /// <summary>This lane's signed offset from the gap's midpoint, spread over <paramref name="usable"/> px.</summary>
    public double Offset(double usable)
    {
        if (Count < 2)
        {
            return 0;
        }

        var step = Math.Min(MaxStep, usable / (Count - 1));
        return (Index - (Count - 1) / 2.0) * step;
    }

    private const double MaxStep = 20;
}

internal sealed record RouteRequest(
    Rect From, Rect To, Side? FromSide, Side? ToSide, EdgeCurve Curve,
    IReadOnlyList<Rect> Obstacles, double Radius, bool PrimaryHorizontal,
    Size? Bounds, double FromShift, double ToShift,
    // Whether each anchor sits on a face shared with other edges. A fanned face's anchors are
    // evenly spread by AnchorShifts, and sliding any one of them — including the middle edge,
    // whose shift is 0 — breaks that spacing. Not inferable from the shift alone.
    bool FromFanned = false, bool ToFanned = false,
    Lane FromLane = default, Lane ToLane = default);

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
    private const double LaneSnap = 28;      // a lane this close to an anchor snaps onto it, erasing the stub
    private const double LaneSnapClear = 12; // ...but only if the snapped route still clears cards by this much
    private const double SelfLoopExtent = 30;
    private const double SameRankEps = 6;
    private const double StraightenInset = 6;    // keep a nudged anchor this far off a face corner
    private const double StraightenLone = 28;    // most a lone anchor may slide to erase a kink
    private const double StraightenSplit = 24;   // most two fanned anchors may slide, combined

    private static Point Center(Rect r) => new(r.X + r.W / 2, r.Y + r.H / 2);
    private static bool IsVertical(Side s) => s is Side.Top or Side.Bottom;

    private static Point Anchor(Rect rect, Side side)
    {
        var c = Center(rect);
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
            {
                return dx >= 0 ? (Side.Right, Side.Left) : (Side.Left, Side.Right);
            }

            return dy >= 0 ? (Side.Bottom, Side.Top) : (Side.Top, Side.Bottom);
        }
        if (Math.Abs(dy) > SameRankEps)
        {
            return dy >= 0 ? (Side.Bottom, Side.Top) : (Side.Top, Side.Bottom);
        }

        return dx >= 0 ? (Side.Right, Side.Left) : (Side.Left, Side.Right);
    }

    public static (Side FromSide, Side ToSide) SidesFor(
        Rect from, Rect to, Direction dir, EdgeCurve curve, IReadOnlyList<Rect> obstacles,
        Side? explicitFrom, Side? explicitTo, Size? bounds = null)
    {
        var primaryHorizontal = dir is Direction.Lr or Direction.Rl;
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
                ? new() { a, new(a.X, Channel(a.Y, b.Y, Lane.Solo, Lane.Solo)), new(b.X, Channel(a.Y, b.Y, Lane.Solo, Lane.Solo)), b }
                : new() { a, new(Channel(a.X, b.X, Lane.Solo, Lane.Solo), a.Y), new(Channel(a.X, b.X, Lane.Solo, Lane.Solo), b.Y), b };
            if (PolylineHits(simple, obstacles))
            {
                // Both gutters are reserved (BackEdgeGutter widens the canvas on either side), so
                // take whichever one this edge can actually reach: the run from each anchor out to
                // the lane travels along its own rank, and a sibling card sitting between the node
                // and the near gutter would be sliced straight through. Near face first, so a clear
                // route keeps the historical side.
                var near = primaryHorizontal ? Side.Top : Side.Left;
                var far = primaryHorizontal ? Side.Bottom : Side.Right;
                foreach (var face in new[] { near, far })
                {
                    if (!PolylineHits(SameFaceLoop(Anchor(from, face), Anchor(to, face), face, obstacles, bounds), obstacles))
                    {
                        return (face, face);
                    }
                }

                // Neither gutter escapes — a sibling blocks this node's rank on both sides. Keep the
                // opposite faces instead: the anchors leave into the inter-rank gaps, which are empty
                // by construction, and the lane detour threads a free column between them. Only if
                // that fails too do we settle for the near gutter and its bruised card.
                if (LaneDetour(a, b, IsVertical(auto.FromSide), obstacles, bounds) is not null)
                {
                    return auto;
                }

                return (near, near);
            }
        }
        return (explicitFrom ?? auto.FromSide, explicitTo ?? auto.ToSide);
    }

    private static bool SegHitsRect(Point a, Point b, Rect rect, double inset = 3)
    {
        double x1 = rect.X + inset, y1 = rect.Y + inset;
        double x2 = rect.X + rect.W - inset, y2 = rect.Y + rect.H - inset;
        if (x2 <= x1 || y2 <= y1)
        {
            return false;
        }

        if (Math.Abs(a.Y - b.Y) < 0.5)
        {
            var y = a.Y;
            if (y <= y1 || y >= y2)
            {
                return false;
            }

            return Math.Max(a.X, b.X) > x1 && Math.Min(a.X, b.X) < x2;
        }
        if (Math.Abs(a.X - b.X) < 0.5)
        {
            var x = a.X;
            if (x <= x1 || x >= x2)
            {
                return false;
            }

            return Math.Max(a.Y, b.Y) > y1 && Math.Min(a.Y, b.Y) < y2;
        }
        return Math.Max(a.X, b.X) > x1 && Math.Min(a.X, b.X) < x2 && Math.Max(a.Y, b.Y) > y1 && Math.Min(a.Y, b.Y) < y2;
    }

    private static bool PolylineHits(IReadOnlyList<Point> points, IReadOnlyList<Rect> obstacles)
    {
        for (var i = 0; i < points.Count - 1; i++)
        {
            foreach (var o in obstacles)
            {
                if (SegHitsRect(points[i], points[i + 1], o))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Distance from an axis-aligned segment to a rect; 0 when they touch or overlap.</summary>
    private static double SegRectDist(Point a, Point b, Rect r)
    {
        var dx = Math.Max(0, Math.Max(r.X - Math.Max(a.X, b.X), Math.Min(a.X, b.X) - (r.X + r.W)));
        var dy = Math.Max(0, Math.Max(r.Y - Math.Max(a.Y, b.Y), Math.Min(a.Y, b.Y) - (r.Y + r.H)));
        return Math.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>How close this polyline comes to any obstacle — the room it leaves, not just whether it fits.</summary>
    private static double PolylineClearance(IReadOnlyList<Point> points, IReadOnlyList<Rect> obstacles)
    {
        var min = double.PositiveInfinity;
        for (var i = 0; i < points.Count - 1; i++)
        {
            foreach (var o in obstacles)
            {
                min = Math.Min(min, SegRectDist(points[i], points[i + 1], o));
            }
        }

        return min;
    }

    private static double ClampLane(double value, double? extent)
    {
        var lo = LaneMargin;
        var hi = extent is { } e ? Math.Max(lo, e - LaneMargin) : double.PositiveInfinity;
        return Math.Min(Math.Max(value, lo), hi);
    }

    /// <summary>
    /// Which of the two escape lanes to try first. Both sit <see cref="LanePad"/> clear of every
    /// obstacle in principle, but <see cref="ClampLane"/> pins a lane inside the canvas, so one can
    /// end up squeezed against the border with only a few px of room while the other side is wide
    /// open. Take the roomier lane; only when they are equally roomy does the shorter detour — the
    /// side the edge's own midpoint already leans toward — decide.
    /// </summary>
    private static bool PreferLow(double lowLane, double highLane, double obsLow, double obsHigh, double travelMid)
    {
        double lowClear = obsLow - lowLane, highClear = highLane - obsHigh;
        if (Math.Abs(lowClear - highClear) > 1)
        {
            return lowClear > highClear;
        }

        return travelMid <= (obsLow + obsHigh) / 2;
    }

    /// <summary>
    /// The obstacles actually standing between the two anchors — those overlapping the corridor the
    /// edge travels through. Detouring around the bounding box of <em>every</em> node, as this once
    /// did, walks an edge all the way around the diagram to clear one card in its path, and lands
    /// the lane hard against the canvas border (every node is inset by the same canvas padding, so
    /// both escape sides are equally cramped and there is no roomier one to pick). Falls back to the
    /// full set when nothing overlaps, so a lane always exists.
    /// </summary>
    private static IReadOnlyList<Rect> Blocking(Point a, Point b, IReadOnlyList<Rect> obstacles)
    {
        double x1 = Math.Min(a.X, b.X), x2 = Math.Max(a.X, b.X);
        double y1 = Math.Min(a.Y, b.Y), y2 = Math.Max(a.Y, b.Y);
        var hit = obstacles.Where(o => o.X < x2 && o.X + o.W > x1 && o.Y < y2 && o.Y + o.H > y1).ToList();
        return hit.Count > 0 ? hit : obstacles;
    }

    /// <summary>
    /// The lane positions to try, in order: escape just past the cards actually in the way (a short
    /// detour with real clearance), then — if that corridor turns out to be occupied by a node that
    /// was not in the way — past every card, which always exists but hugs the canvas border.
    /// </summary>
    private static IEnumerable<double> LaneCandidates(
        IReadOnlyList<Rect> blockers, IReadOnlyList<Rect> obstacles, bool vertical, Size? bounds, double travelMid)
    {
        var extent = vertical ? bounds?.W ?? 0 : bounds?.H ?? 0;
        double? Extent() => bounds is null ? null : extent;
        var seen = new List<double>();
        foreach (var set in ReferenceEquals(blockers, obstacles) ? new[] { obstacles } : new[] { blockers, obstacles })
        {
            var lo = vertical ? set.Min(o => o.X) : set.Min(o => o.Y);
            var hi = vertical ? set.Max(o => o.X + o.W) : set.Max(o => o.Y + o.H);
            double low = ClampLane(lo - LanePad, Extent()), high = ClampLane(hi + LanePad, Extent());
            foreach (var lane in PreferLow(low, high, lo, hi, travelMid) ? new[] { low, high } : new[] { high, low })
            {
                if (!seen.Any(v => Math.Abs(v - lane) < 0.5)) { seen.Add(lane); yield return lane; }
            }
        }
    }

    /// <summary>
    /// A lane sitting a hair off one of the anchors leaves a stub run before the turn — a kink, not
    /// a detour. Snapping the lane onto that anchor collapses the stub and the edge leaves its node
    /// straight. But the snap moves the lane toward the very card it was clearing, so take it only
    /// when the snapped route still leaves <see cref="LaneSnapClear"/> of room: a straight line that
    /// grazes a card is not the trade we want. Otherwise keep the lane where the padding put it.
    /// </summary>
    private static List<Point>? LaneDetour(Point a, Point b, bool vertical, IReadOnlyList<Rect> obstacles, Size? bounds)
    {
        if (obstacles.Count == 0)
        {
            return null;
        }

        var blockers = Blocking(a, b, obstacles);
        double aC = vertical ? a.X : a.Y, bC = vertical ? b.X : b.Y;

        List<Point> Build(double lane)
        {
            if (vertical)
            {
                double s = Math.Sign(b.Y - a.Y); var dirY = s != 0 ? s : 1;
                double ch1 = a.Y + dirY * ChannelOffset, ch2 = b.Y - dirY * ChannelOffset;
                return [a, new(a.X, ch1), new(lane, ch1), new(lane, ch2), new(b.X, ch2), b];
            }
            double sx = Math.Sign(b.X - a.X); var dirX = sx != 0 ? sx : 1;
            double cx1 = a.X + dirX * ChannelOffset, cx2 = b.X - dirX * ChannelOffset;
            return [a, new(cx1, a.Y), new(cx1, lane), new(cx2, lane), new(cx2, b.Y), b];
        }

        // Nudge a lane that sits inside the stub zone of an anchor outward, away from that anchor,
        // so the run into the turn is a segment rather than a nub. Snapping onto the anchor is the
        // better answer when it is safe; this is the other way out of the same zone.
        double Push(double lane)
        {
            foreach (var c in new[] { aC, bC })
            {
                var d = lane - c;
                if (Math.Abs(d) < LaneSnap)
                {
                    lane = c + (d < 0 ? -LaneSnap : LaneSnap);
                }
            }
            return lane;
        }

        foreach (var raw in LaneCandidates(blockers, obstacles, vertical, bounds, (aC + bC) / 2))
        {
            double? snap = Math.Abs(raw - aC) < LaneSnap ? aC : Math.Abs(raw - bC) < LaneSnap ? bC : null;
            if (snap is { } s)
            {
                var snapped = Build(s);
                if (!PolylineHits(snapped, obstacles) && PolylineClearance(snapped, obstacles) >= LaneSnapClear)
                {
                    return snapped;
                }
            }
            foreach (var lane in new[] { Push(raw), raw }.Distinct())
            {
                var poly = Build(lane);
                if (!PolylineHits(poly, obstacles))
                {
                    return poly;
                }
            }
        }
        return null;
    }

    private static Point ShiftAnchor(Point p, Side side, double off)
    {
        if (off == 0)
        {
            return p;
        }

        return IsVertical(side) ? new Point(p.X + off, p.Y) : new Point(p.X, p.Y + off);
    }

    private static List<Point> SameFaceLoop(Point a, Point b, Side side, IReadOnlyList<Rect> obstacles, Size? bounds)
    {
        if (IsVertical(side))
        {
            double lo = Math.Min(a.X, b.X), hi = Math.Max(a.X, b.X);
            var spanned = obstacles.Where(o => o.X < hi && o.X + o.W > lo).ToList();
            var laneY = ClampLane(
                side == Side.Top
                    ? new[] { a.Y, b.Y }.Concat(spanned.Select(o => o.Y)).Min() - LanePad
                    : new[] { a.Y, b.Y }.Concat(spanned.Select(o => o.Y + o.H)).Max() + LanePad,
                bounds?.H);
            return [a, new(a.X, laneY), new(b.X, laneY), b];
        }
        double loY = Math.Min(a.Y, b.Y), hiY = Math.Max(a.Y, b.Y);
        var spannedH = obstacles.Where(o => o.Y < hiY && o.Y + o.H > loY).ToList();
        var laneX = ClampLane(
            side == Side.Left
                ? new[] { a.X, b.X }.Concat(spannedH.Select(o => o.X)).Min() - LanePad
                : new[] { a.X, b.X }.Concat(spannedH.Select(o => o.X + o.W)).Max() + LanePad,
            bounds?.W);
        return [a, new(laneX, a.Y), new(laneX, b.Y), b];
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
        if (fromSide == toSide)
        {
            return false;
        }

        var vert = IsVertical(fromSide) && IsVertical(toSide);   // Top/Bottom faces → perp axis = X
        var horz = !IsVertical(fromSide) && !IsVertical(toSide); // Left/Right faces → perp axis = Y
        if (!vert && !horz)
        {
            return false;
        }

        double aPerp = vert ? a.X : a.Y, bPerp = vert ? b.X : b.Y;
        var off = bPerp - aPerp;
        if (Math.Abs(off) < 0.5)
        {
            return false; // already straight
        }

        // A lone anchor absorbs the whole kink on its own and disturbs nothing, so it gets the
        // wider budget. Two fanned anchors must split it, and every px they move is spacing lost
        // on both their faces — so they buy less straightness before we call the jog genuine.
        bool aFree = !fromFanned, bFree = !toFanned;
        var budget = aFree || bFree ? StraightenLone : StraightenSplit;
        if (Math.Abs(off) > budget)
        {
            return false; // genuine jog — leave it stepped
        }

        // Reachable band = the two faces' overlap on the perp axis, inset off the corners.
        double aLo = (vert ? from.X : from.Y) + StraightenInset, aHi = (vert ? from.X + from.W : from.Y + from.H) - StraightenInset;
        double bLo = (vert ? to.X : to.Y) + StraightenInset, bHi = (vert ? to.X + to.W : to.Y + to.H) - StraightenInset;
        double lo = Math.Max(aLo, bLo), hi = Math.Min(aHi, bHi);
        if (hi < lo)
        {
            return false; // faces don't overlap — no shared straight line exists
        }

        // Move a lone anchor in preference to a fanned one; only when both faces are fanned (so
        // there is no free anchor to give) do we split the nudge and accept the uneven spread.
        var target = aFree && bFree ? (aPerp + bPerp) / 2
            : aFree ? bPerp
            : bFree ? aPerp
            : (aPerp + bPerp) / 2;
        target = Math.Clamp(target, lo, hi);

        // One anchor may absorb the whole gap when it is the only one free to move; when both move
        // they split it, so neither travels far. Either way the combined slide stays in budget.
        if (Math.Abs(target - aPerp) + Math.Abs(target - bPerp) > budget + 0.01)
        {
            return false;
        }

        var na = vert ? new Point(target, a.Y) : new Point(a.X, target);
        var nb = vert ? new Point(target, b.Y) : new Point(b.X, target);
        if (PolylineHits([na, nb], obstacles))
        {
            return false;
        }

        a = na; b = nb;
        return true;
    }

    /// <summary>
    /// The turn column (or row) for a step between opposite parallel faces. Edges sharing a face
    /// take separate lanes, spread symmetrically about the gap's midpoint so the bundle uses the
    /// whole corridor instead of crowding one node.
    ///
    /// <para>Lane order is by bend magnitude, outermost first (see <c>EdgePainter.AnchorShifts</c>):
    /// an edge that must travel far across the rank turns nearest its own node, tucking its long
    /// run behind the shorter edges' turns so no sibling's straight run crosses it. Ranking by the
    /// anchor's |shift| instead — as this once did — collapses a symmetric pair onto one channel,
    /// and when both of them happen to bend the *same* way their runs overlap and read as one wire.
    /// The two lanes' spans only diverge when the pair bends apart, which a fan off-center from its
    /// targets does not.</para>
    ///
    /// <para>Clamped clear of both faces; degenerate gaps keep the mid.</para>
    /// </summary>
    private static double Channel(double aC, double bC, Lane fromLane, Lane toLane)
    {
        var mid = (aC + bC) / 2;
        double lo = Math.Min(aC, bC) + ChannelOffset, hi = Math.Max(aC, bC) - ChannelOffset;
        if (lo > hi)
        {
            return mid;
        }
        // The from-fan spreads about the mid; a to-fan spreads the opposite way, so an arriving
        // edge's long run also tucks nearest its own node.
        var usable = hi - lo;
        var offset = fromLane.Offset(usable) - toLane.Offset(usable);
        return Math.Clamp(mid + Math.Sign(bC - aC) * offset, lo, hi);
    }

    private static List<Point> OrthogonalPolyline(
        Point a, Point b, Side fromSide, Side toSide, Lane fromLane, Lane toLane,
        IReadOnlyList<Rect> obstacles, Size? bounds)
    {
        if (fromSide == toSide)
        {
            return SameFaceLoop(a, b, fromSide, obstacles, bounds);
        }

        var vert = IsVertical(fromSide) && IsVertical(toSide);
        var horz = !IsVertical(fromSide) && !IsVertical(toSide);

        if (vert)
        {
            var channelY = Channel(a.Y, b.Y, fromLane, toLane);
            var simple = new List<Point> { a, new(a.X, channelY), new(b.X, channelY), b };
            if (!PolylineHits(simple, obstacles))
            {
                return simple;
            }

            return LaneDetour(a, b, true, obstacles, bounds) ?? simple;
        }
        if (horz)
        {
            var channelX = Channel(a.X, b.X, fromLane, toLane);
            var simple = new List<Point> { a, new(channelX, a.Y), new(channelX, b.Y), b };
            if (!PolylineHits(simple, obstacles))
            {
                return simple;
            }

            return LaneDetour(a, b, false, obstacles, bounds) ?? simple;
        }
        var corner = IsVertical(fromSide) ? new Point(a.X, b.Y) : new Point(b.X, a.Y);
        return [a, corner, b];
    }

    private static string SCurve(Point a, Point b, Side fromSide)
    {
        string S(double n) => Js.Str(n);
        if (IsVertical(fromSide))
        {
            var off = (b.Y - a.Y) * 0.4;
            return $"M {S(a.X)} {S(a.Y)} C {S(a.X)} {S(a.Y + off)}, {S(b.X)} {S(b.Y - off)}, {S(b.X)} {S(b.Y)}";
        }
        var offx = (b.X - a.X) * 0.4;
        return $"M {S(a.X)} {S(a.Y)} C {S(a.X + offx)} {S(a.Y)}, {S(b.X - offx)} {S(b.Y)}, {S(b.X)} {S(b.Y)}";
    }

    public static RoutedPath RouteEdge(RouteRequest req)
    {
        var self = req.From == req.To;
        if (self)
        {
            var r = req.From;
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
        var fromSide = req.FromSide ?? auto.FromSide;
        var toSide = req.ToSide ?? auto.ToSide;
        var a = ShiftAnchor(Anchor(req.From, fromSide), fromSide, req.FromShift);
        var b = ShiftAnchor(Anchor(req.To, toSide), toSide, req.ToShift);

        if (req.Curve == EdgeCurve.Straight)
        {
            return new RoutedPath($"M {Js.Str(a.X)} {Js.Str(a.Y)} L {Js.Str(b.X)} {Js.Str(b.Y)}", [a, b]);
        }

        if (req.Curve == EdgeCurve.S)
        {
            return new RoutedPath(SCurve(a, b, fromSide), [a, b]);
        }

        var poly = TryStraighten(req.From, req.To, fromSide, toSide, req.FromFanned, req.ToFanned, req.Obstacles, ref a, ref b)
            ? [a, b]
            : OrthogonalPolyline(a, b, fromSide, toSide, req.FromLane, req.ToLane, req.Obstacles, req.Bounds);
        return new RoutedPath(StepRound.RoundedPath(poly, req.Radius), poly);
    }
}