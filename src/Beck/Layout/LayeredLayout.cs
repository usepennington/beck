using Beck.Model;

namespace Beck.Layout;

/// <summary>
/// The Sugiyama-lite layered engine — a faithful port of <c>src/layout/layered.ts</c>.
/// <c>LayoutLayer</c> is the group-free single-level engine (rank → order with
/// virtual nodes → coords → direction transform); <c>Compute</c> is the recursive
/// compound driver that lays each group out as a sized super-node. Constants,
/// iteration counts (6 barycenter sweeps, 6 coordinate iterations), and tie-breaks
/// mirror the TS exactly, with two deliberate departures in coordinate assignment:
/// separation resolution uses pool-adjacent-violators so nodes contending for one
/// position center as a block instead of left-anchoring, and a node's desired
/// position is the MEDIAN of its neighbors rather than their mean, so an odd fan
/// lands its middle edge dead straight (see LayoutCenteringTests).
/// </summary>
internal static class LayeredLayout
{
    private static readonly Size _fallback = new(180, 64);
    private const double GroupPadTop = 28, GroupPadSide = 16, GroupPadBottom = 16;
    private const double CanvasPad = 16;
    private const double LaneReserve = 22, LabelReserveGap = 10, SelfLoopReserve = 30;

    internal sealed record LayItem(string Id, double W, double H, double? Rank, double? Order);
    internal sealed record LayerResult(Dictionary<string, Rect> Rects, double Width, double Height);
    private sealed record Composed(Dictionary<string, Rect> NodeRects, Dictionary<string, Rect> GroupRects, double Width, double Height);

    public static LayoutResult Compute(DiagramModel model, IReadOnlyDictionary<string, Size> sizes)
    {
        var dir = model.Meta.Direction;
        var gap = model.Meta.Spacing.Node;
        var rankGap = model.Meta.Spacing.Rank;

        var nodeById = model.Nodes.ToDictionary(n => n.Id);
        var groupById = model.Groups.ToDictionary(g => g.Id);
        Size SizeOf(string id) => sizes.GetValueOrDefault(id, _fallback);

        // member -> containing group (first declared parent wins).
        var parent = new Dictionary<string, string>();
        foreach (var g in model.Groups)
        {
            foreach (var m in g.Members)
            {
                if ((nodeById.ContainsKey(m) || groupById.ContainsKey(m)) && !parent.ContainsKey(m))
                {
                    parent[m] = g.Id;
                }
            }
        }

        string? ParentOf(string id) => parent.GetValueOrDefault(id);

        List<string> DirectChildren(string? containerId)
        {
            if (containerId is null)
            {
                var kids = new List<string>();
                foreach (var n in model.Nodes)
                {
                    if (ParentOf(n.Id) is null)
                    {
                        kids.Add(n.Id);
                    }
                }

                foreach (var g in model.Groups)
                {
                    if (ParentOf(g.Id) is null)
                    {
                        kids.Add(g.Id);
                    }
                }

                return kids;
            }
            return (groupById.TryGetValue(containerId, out var grp) ? grp.Members : new List<string>())
                .Where(m => nodeById.ContainsKey(m) || groupById.ContainsKey(m)).ToList();
        }

        string? RepIn(string x, string? containerId)
        {
            var cur = x;
            var guard = 0;
            while (ParentOf(cur) != containerId)
            {
                var p = ParentOf(cur);
                if (p is null || ++guard > 10000)
                {
                    return null;
                }

                cur = p;
            }
            return cur;
        }

        List<(string, string)> ProjectedEdges(string? containerId, HashSet<string> childSet)
        {
            var outp = new List<(string, string)>();
            var seen = new HashSet<string>();
            foreach (var e in model.Edges)
            {
                var cu = RepIn(e.From, containerId);
                var cv = RepIn(e.To, containerId);
                if (cu is null || cv is null || cu == cv)
                {
                    continue;
                }

                if (!childSet.Contains(cu) || !childSet.Contains(cv))
                {
                    continue;
                }

                var key = cu + " " + cv;
                if (!seen.Add(key))
                {
                    continue;
                }

                outp.Add((cu, cv));
            }
            return outp;
        }

        var visiting = new HashSet<string>();

        Composed LayoutContainer(string? containerId)
        {
            if (containerId is not null)
            {
                if (visiting.Contains(containerId))
                {
                    return new Composed(new(), new(), 0, 0);
                }

                visiting.Add(containerId);
            }
            var children = DirectChildren(containerId);
            var subs = new Dictionary<string, Composed>();
            var items = new List<LayItem>();
            foreach (var c in children)
            {
                if (groupById.ContainsKey(c))
                {
                    var sub = LayoutContainer(c);
                    if (sub.NodeRects.Count == 0)
                    {
                        continue; // skip empty groups
                    }

                    subs[c] = sub;
                    items.Add(new LayItem(c, sub.Width, sub.Height, null, null));
                }
                else
                {
                    var s = SizeOf(c);
                    var n = nodeById[c];
                    items.Add(new LayItem(c, s.W, s.H, n.Rank, n.Order));
                }
            }
            if (containerId is not null)
            {
                visiting.Remove(containerId);
            }

            var childSet = items.Select(i => i.Id).ToHashSet();
            var edges = ProjectedEdges(containerId, childSet);
            var layer = items.Count > 0
                ? LayoutLayer(items, edges, dir, gap, rankGap)
                : new LayerResult(new(), 0, 0);

            var inset = containerId is not null;
            double padL = inset ? GroupPadSide : 0, padT = inset ? GroupPadTop : 0;
            double padR = inset ? GroupPadSide : 0, padB = inset ? GroupPadBottom : 0;

            var nodeRects = new Dictionary<string, Rect>();
            var groupRects = new Dictionary<string, Rect>();
            foreach (var c in childSet)
            {
                if (!layer.Rects.TryGetValue(c, out var lr))
                {
                    continue;
                }

                var r = lr.Offset(padL, padT);
                if (groupById.ContainsKey(c))
                {
                    var sub = subs[c];
                    foreach (var (nid, nr) in sub.NodeRects)
                    {
                        nodeRects[nid] = nr.Offset(r.X, r.Y);
                    }

                    foreach (var (gid, gr) in sub.GroupRects)
                    {
                        groupRects[gid] = gr.Offset(r.X, r.Y);
                    }

                    groupRects[c] = r;
                }
                else
                {
                    nodeRects[c] = r;
                }
            }
            return new Composed(nodeRects, groupRects, layer.Width + padL + padR, layer.Height + padT + padB);
        }

        var root = LayoutContainer(null);
        var nodes = new Dictionary<string, Rect>();
        var groups = new Dictionary<string, Rect>();
        foreach (var (id, r) in root.NodeRects)
        {
            nodes[id] = r.Offset(CanvasPad, CanvasPad);
        }

        foreach (var (id, r) in root.GroupRects)
        {
            groups[id] = r.Offset(CanvasPad, CanvasPad);
        }

        var width = Math.Ceiling(root.Width + CanvasPad * 2);
        var height = Math.Ceiling(root.Height + CanvasPad * 2);

        var gutter = BackEdgeGutter(model, nodes);
        if (gutter > 0)
        {
            var horizontalSecondary = dir is Direction.Tb or Direction.Bt;
            var dx = horizontalSecondary ? gutter : 0;
            var dy = horizontalSecondary ? 0 : gutter;
            foreach (var id in nodes.Keys.ToList())
            {
                nodes[id] = nodes[id].Offset(dx, dy);
            }

            foreach (var id in groups.Keys.ToList())
            {
                groups[id] = groups[id].Offset(dx, dy);
            }

            if (horizontalSecondary)
            {
                width += gutter * 2;
            }
            else
            {
                height += gutter * 2;
            }
        }

        return new LayoutResult(nodes, groups, width, height);
    }

    private static double BackEdgeGutter(DiagramModel model, Dictionary<string, Rect> nodes)
    {
        var dir = model.Meta.Direction;
        var horizontalSecondary = dir is Direction.Tb or Direction.Bt;
        double need = 0;
        foreach (var e in model.Edges)
        {
            if (!nodes.TryGetValue(e.From, out var f) || !nodes.TryGetValue(e.To, out var t))
            {
                continue;
            }

            if (e.From == e.To)
            {
                double loopLabel = !string.IsNullOrEmpty(e.Label) ? (horizontalSecondary ? e.Label.Length * 7 + 8 : 14) : 0;
                need = Math.Max(need, SelfLoopReserve + (loopLabel > 0 ? LabelReserveGap + loopLabel : 0));
                continue;
            }
            if (!Geometry.AgainstFlow(f, t, dir))
            {
                continue;
            }

            double labelExtent = !string.IsNullOrEmpty(e.Label) ? (horizontalSecondary ? e.Label.Length * 7 + 8 : 14) : 0;
            var want = LaneReserve + (labelExtent > 0 ? LabelReserveGap + labelExtent : LaneReserve);
            need = Math.Max(need, want);
        }
        return need > 0 ? Math.Max(0, Math.Ceiling(need - CanvasPad)) : 0;
    }

    internal static LayerResult LayoutLayer(
        List<LayItem> items, List<(string F, string T)> edges, Direction dir, double gap, double rankGap)
    {
        var horizontal = dir is Direction.Lr or Direction.Rl;
        var nodeIds = items.Select(i => i.Id).ToList();
        var itemMap = items.ToDictionary(i => i.Id);
        var idIndex = new Dictionary<string, int>();
        for (var i = 0; i < nodeIds.Count; i++)
        {
            idIndex[nodeIds[i]] = i;
        }

        Size SizeOf(string id) => itemMap.TryGetValue(id, out var it) ? new Size(it.W, it.H) : _fallback;
        double DepthOf(string id) => horizontal ? SizeOf(id).W : SizeOf(id).H;
        double BreadthOf(string id) => horizontal ? SizeOf(id).H : SizeOf(id).W;

        var expanded = new List<(string F, string T)>();
        foreach (var (f, t) in edges)
        {
            if (f != t && idIndex.ContainsKey(f) && idIndex.ContainsKey(t))
            {
                expanded.Add((f, t));
            }
        }

        // ---- cycle break: mark back edges via iterative DFS (3-color) ----
        var adj = new Dictionary<string, List<string>>();
        foreach (var id in nodeIds)
        {
            adj[id] = new();
        }

        foreach (var (f, t) in expanded)
        {
            adj[f].Add(t);
        }

        var color = new Dictionary<string, int>();
        foreach (var id in nodeIds)
        {
            color[id] = 0;
        }

        var back = new HashSet<string>();
        var stack = new List<(string Id, int I)>();
        foreach (var start in nodeIds)
        {
            if (color[start] != 0)
            {
                continue;
            }

            stack.Add((start, 0));
            color[start] = 1;
            while (stack.Count > 0)
            {
                var top = stack[^1];
                var neigh = adj[top.Id];
                if (top.I < neigh.Count)
                {
                    var v = neigh[top.I];
                    stack[^1] = (top.Id, top.I + 1);
                    var c = color[v];
                    if (c == 1)
                    {
                        back.Add(top.Id + " " + v);
                    }
                    else if (c == 0) { color[v] = 1; stack.Add((v, 0)); }
                }
                else { color[top.Id] = 2; stack.RemoveAt(stack.Count - 1); }
            }
        }
        var forward = expanded.Where(e => !back.Contains(e.F + " " + e.T)).ToList();

        // ---- longest-path ranking over forward edges (Kahn) ----
        var rank = new Dictionary<string, double>();
        foreach (var id in nodeIds)
        {
            rank[id] = 0;
        }

        {
            var indeg = new Dictionary<string, int>();
            var fAdj = new Dictionary<string, List<string>>();
            foreach (var id in nodeIds) { indeg[id] = 0; fAdj[id] = new(); }
            foreach (var (f, t) in forward) { fAdj[f].Add(t); indeg[t]++; }
            var q = new Queue<string>(nodeIds.Where(id => indeg[id] == 0));
            while (q.Count > 0)
            {
                var u = q.Dequeue();
                foreach (var v in fAdj[u])
                {
                    rank[v] = Math.Max(rank[v], rank[u] + 1);
                    indeg[v]--;
                    if (indeg[v] == 0)
                    {
                        q.Enqueue(v);
                    }
                }
            }
        }
        foreach (var it in items)
        {
            if (it.Rank is { } rr)
            {
                rank[it.Id] = rr;
            }
        }

        var distinct = nodeIds.Select(id => rank[id]).Distinct().OrderBy(x => x).ToList();
        var compress = new Dictionary<double, int>();
        for (var i = 0; i < distinct.Count; i++)
        {
            compress[distinct[i]] = i;
        }

        foreach (var id in nodeIds)
        {
            rank[id] = compress[rank[id]];
        }

        var maxRank = distinct.Count - 1;

        // ---- virtual nodes on long edges + adjacency ----
        var order = new List<List<string>>();
        for (var r = 0; r <= maxRank; r++)
        {
            order.Add(new());
        }

        foreach (var id in nodeIds)
        {
            order[(int)rank[id]].Add(id);
        }

        var isVirtual = new HashSet<string>();
        var down = new Dictionary<string, List<string>>();
        var up = new Dictionary<string, List<string>>();
        void Link(string a, string b)
        {
            (down.TryGetValue(a, out var da) ? da : down[a] = new()).Add(b);
            (up.TryGetValue(b, out var ub) ? ub : up[b] = new()).Add(a);
        }

        var vCounter = 0;
        foreach (var (f, t) in forward)
        {
            int rf = (int)rank[f], rt = (int)rank[t];
            if (rf == rt)
            {
                continue;
            }

            int lo = Math.Min(rf, rt), hi = Math.Max(rf, rt);
            string top = rf < rt ? f : t, bottom = rf < rt ? t : f;
            if (hi - lo == 1) { Link(top, bottom); continue; }
            var prev = top;
            for (var r = lo + 1; r < hi; r++)
            {
                var vid = " v" + vCounter++;
                isVirtual.Add(vid);
                order[r].Add(vid);
                rank[vid] = r;
                Link(prev, vid);
                prev = vid;
            }
            Link(prev, bottom);
        }

        double BreadthAny(string id) => isVirtual.Contains(id) ? 0 : BreadthOf(id);
        double DepthAny(string id) => isVirtual.Contains(id) ? 0 : DepthOf(id);

        // ---- ordering: barycenter sweeps ----
        Dictionary<string, int> OrderIndex(int r)
        {
            var m = new Dictionary<string, int>();
            for (var i = 0; i < order[r].Count; i++)
            {
                m[order[r][i]] = i;
            }

            return m;
        }
        double BaryOf(string id, Dictionary<string, int> nri, bool useDown)
        {
            var neigh = (useDown ? down : up).GetValueOrDefault(id);
            if (neigh is null || neigh.Count == 0)
            {
                return -1;
            }

            double sum = 0; var count = 0;
            foreach (var n in neigh)
            {
                if (nri.TryGetValue(n, out var pos)) { sum += pos; count++; }
            }

            return count > 0 ? sum / count : -1;
        }
        double TieOf(string id)
        {
            if (idIndex.TryGetValue(id, out var idx))
            {
                return idx;
            }

            var t = id.Trim();
            return t.Length > 1 && double.TryParse(t.AsSpan(1), System.Globalization.CultureInfo.InvariantCulture, out var n) ? n : 0;
        }
        void ReorderRank(int r, bool adjacentIsDown)
        {
            var adjIndex = OrderIndex(adjacentIsDown ? r + 1 : r - 1);
            var current = order[r];
            var units = current.Select((id, i) =>
            {
                var b = BaryOf(id, adjIndex, adjacentIsDown);
                return (Id: id, Key: b < 0 ? i : b, Tie: TieOf(id));
            });
            order[r] = units.OrderBy(u => u.Key).ThenBy(u => u.Tie).Select(u => u.Id).ToList();
        }

        for (var sweep = 0; sweep < 6; sweep++)
        {
            if (sweep % 2 == 0)
            {
                for (var r = 1; r <= maxRank; r++)
                {
                    ReorderRank(r, false);
                }
            }
            else
            {
                for (var r = maxRank - 1; r >= 0; r--)
                {
                    ReorderRank(r, true);
                }
            }
        }

        // honor explicit item.order as a final per-rank sort
        for (var r = 0; r <= maxRank; r++)
        {
            if (!order[r].Any(id => itemMap.GetValueOrDefault(id)?.Order is not null))
            {
                continue;
            }

            order[r] = order[r].Select((id, i) => (id, i))
                .OrderBy(x => itemMap.GetValueOrDefault(x.id)?.Order is null ? 1 : 0)
                .ThenBy(x => itemMap.GetValueOrDefault(x.id)?.Order ?? 0)
                .ThenBy(x => x.i)
                .Select(x => x.id).ToList();
        }

        // ---- secondary (cross-axis) coordinates ----
        var sec = new Dictionary<string, double>();
        double HalfB(string id) => BreadthAny(id) / 2;
        for (var r = 0; r <= maxRank; r++)
        {
            double cursor = 0;
            var row = order[r];
            var centers = new List<double>();
            foreach (var id in row) { var b = BreadthAny(id); centers.Add(cursor + b / 2); cursor += b + gap; }
            var total = cursor - gap;
            var shift = -total / 2;
            for (var i = 0; i < row.Count; i++)
            {
                sec[row[i]] = centers[i] + shift;
            }
        }

        // Where a node wants to sit given its neighbors on the adjacent rank. The MEDIAN, not the
        // mean: with three sources fanning into one sink, a mean is dragged off-center by whichever
        // source is widest, so the middle edge misses the sink by a few px and only reaches it via
        // the router's anchor-sliding straighten cheat — which then skews the whole fan. The median
        // lands the sink exactly under its middle source, so that edge is straight for free and the
        // fan stays evenly spaced. Even neighbor counts average the two middles, so the two-source
        // case (and every golden that depends on it) is unchanged.
        static double Median(IReadOnlyList<double> values)
        {
            if (values.Count == 1)
            {
                return values[0];
            }

            var s = values.OrderBy(v => v).ToList();
            var mid = s.Count / 2;
            return s.Count % 2 == 1 ? s[mid] : (s[mid - 1] + s[mid]) / 2;
        }

        void ResolveSeparation(int r, Dictionary<string, double> desired)
        {
            // Least-squares placement under min-separation (pool-adjacent-violators):
            // nodes whose desires collide merge into a block placed at the block's mean
            // desire. Anchoring the first colliding node at its desire instead would
            // skew shared targets toward the row start (two sources over one sink left
            // the sink flush under the first source).
            var row = order[r];
            if (row.Count == 0)
            {
                return;
            }

            var cum = new double[row.Count];
            for (var i = 1; i < row.Count; i++)
            {
                cum[i] = cum[i - 1] + HalfB(row[i - 1]) + gap + HalfB(row[i]);
            }

            var blockSum = new double[row.Count];
            var blockCount = new int[row.Count];
            var top = 0;
            for (var i = 0; i < row.Count; i++)
            {
                var q = (desired.TryGetValue(row[i], out var d) ? d : sec[row[i]]) - cum[i];
                blockSum[top] = q;
                blockCount[top] = 1;
                top++;
                while (top > 1 && blockSum[top - 2] / blockCount[top - 2] > blockSum[top - 1] / blockCount[top - 1])
                {
                    blockSum[top - 2] += blockSum[top - 1];
                    blockCount[top - 2] += blockCount[top - 1];
                    top--;
                }
            }
            var idx = 0;
            for (var b = 0; b < top; b++)
            {
                var basePos = blockSum[b] / blockCount[b];
                for (var k = 0; k < blockCount[b]; k++, idx++)
                {
                    sec[row[idx]] = basePos + cum[idx];
                }
            }
        }

        // Sweeps alternate, and the LAST one wins outright — nothing re-balances after it. It must
        // therefore be the up-pass (place each node under its parents), because that is the reading
        // order: a sink lands beneath the sources feeding it. Ending on the down-pass instead left
        // every sink holding the median of where its sources sat one pass ago, so a fan-in never
        // quite closed on its middle parent and the router papered over the gap with a jog.
        for (var it = 0; it < 6; it++)
        {
            if (it % 2 == 1)
            {
                for (var r = 1; r <= maxRank; r++)
                {
                    var desired = new Dictionary<string, double>();
                    foreach (var id in order[r])
                    {
                        var neigh = up.GetValueOrDefault(id);
                        if (neigh is { Count: > 0 })
                        {
                            desired[id] = Median(neigh.Select(n => sec[n]).ToList());
                        }
                    }
                    ResolveSeparation(r, desired);
                }
            }
            else
            {
                for (var r = maxRank - 1; r >= 0; r--)
                {
                    var desired = new Dictionary<string, double>();
                    foreach (var id in order[r])
                    {
                        var neigh = down.GetValueOrDefault(id);
                        if (neigh is { Count: > 0 })
                        {
                            desired[id] = Median(neigh.Select(n => sec[n]).ToList());
                        }
                    }
                    ResolveSeparation(r, desired);
                }
            }
        }

        // ---- primary (rank-axis) coordinates ----
        var rankDepth = new double[maxRank + 1];
        for (var r = 0; r <= maxRank; r++)
        {
            double d = 0;
            foreach (var id in order[r])
            {
                d = Math.Max(d, DepthAny(id));
            }

            rankDepth[r] = d != 0 ? d : _fallback.H;
        }
        var primaryStart = new double[maxRank + 1];
        double acc = 0;
        for (var r = 0; r <= maxRank; r++) { primaryStart[r] = acc; acc += rankDepth[r] + rankGap; }
        var totalPrimary = acc - rankGap;
        double PrimaryCenter(string id) => primaryStart[(int)rank[id]] + rankDepth[(int)rank[id]] / 2;

        (double X, double Y) CenterXy(string id)
        {
            double p = PrimaryCenter(id), s = sec[id];
            if (!horizontal)
            {
                return (s, dir == Direction.Bt ? totalPrimary - p : p);
            }

            return (dir == Direction.Rl ? totalPrimary - p : p, s);
        }

        var rects = new Dictionary<string, Rect>();
        foreach (var id in nodeIds)
        {
            var (cx, cy) = CenterXy(id);
            var sz = SizeOf(id);
            rects[id] = new Rect(cx - sz.W / 2, cy - sz.H / 2, sz.W, sz.H);
        }
        if (rects.Count == 0)
        {
            return new LayerResult(rects, 0, 0);
        }

        double minX = rects.Values.Min(r => r.X), minY = rects.Values.Min(r => r.Y);
        foreach (var id in rects.Keys.ToList())
        {
            rects[id] = rects[id].Offset(-minX, -minY);
        }

        var width = rects.Values.Max(r => r.X + r.W);
        var height = rects.Values.Max(r => r.Y + r.H);
        return new LayerResult(rects, width, height);
    }
}