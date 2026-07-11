using Beck.Model;

namespace Beck.Layout;

/// <summary>
/// The two-sided "butterfly" layout for <c>type: mindmap</c>. A central root, its first-level
/// branches partitioned left/right, and each half laid out with the shared single-level engine
/// (<see cref="LayeredLayout.LayoutLayer"/>) — the right half flowing <see cref="Direction.Lr"/>,
/// the left half <see cref="Direction.Rl"/> (a mirror) — then unified so both halves' root rects
/// coincide on one central node.
///
/// <para><b>Partition.</b> Each first-level branch owns a subtree; its weight is the subtree's total
/// node height. Branches are assigned by longest-processing-time (heaviest first, declaration order
/// breaking ties) to whichever side is currently lighter, ties going right — so a lone branch lands
/// on the right and the two halves stay balanced.</para>
///
/// <para><b>Compose.</b> The root is included in BOTH half-layouts (as the rank-0 anchor), so rank
/// spacing is identical on each side. The left half is translated so its root rect lands exactly on
/// the right half's root rect; everything is then shifted into positive space with the same
/// <c>CanvasPad</c> the layered engine uses. No coordinate is ever negative.</para>
/// </summary>
internal static class MindMapLayout
{
    private static readonly Size _fallback = new(180, 64);
    private const double CanvasPad = 16;

    internal static LayoutResult Compute(DiagramModel model, IReadOnlyDictionary<string, Size> sizes)
    {
        var gap = model.Meta.Spacing.Node;
        var rankGap = model.Meta.Spacing.Rank;
        Size SizeOf(string id) => sizes.GetValueOrDefault(id, _fallback);

        // 1. Root = the rank-0 topic (exactly one; fall back to declaration order defensively).
        var rootNode = model.Nodes.FirstOrDefault(n => n.Rank == 0) ?? model.Nodes[0];
        var rootId = rootNode.Id;

        // 2. parent → children adjacency (edges are parent→child in declaration order).
        var childrenOf = new Dictionary<string, List<string>>();
        foreach (var e in model.Edges)
        {
            (childrenOf.TryGetValue(e.From, out var kids) ? kids : childrenOf[e.From] = new()).Add(e.To);
        }

        List<string> Subtree(string branch)
        {
            var acc = new List<string>();
            var seen = new HashSet<string>();
            void Go(string x)
            {
                if (!seen.Add(x))
                {
                    return;
                }

                acc.Add(x);
                foreach (var c in childrenOf.GetValueOrDefault(x) ?? [])
                {
                    Go(c);
                }
            }
            Go(branch);
            return acc;
        }

        var branches = childrenOf.GetValueOrDefault(rootId) ?? [];
        var subtrees = branches.ToDictionary(b => b, Subtree);

        // 3. Balance branches left/right by subtree weight (total node height), heaviest first.
        var indexed = branches
            .Select((b, i) => (Branch: b, Index: i, Weight: subtrees[b].Sum(id => SizeOf(id).H)))
            .OrderByDescending(x => x.Weight)
            .ThenBy(x => x.Index)
            .ToList();

        double leftW = 0, rightW = 0;
        var leftBranches = new List<string>();
        var rightBranches = new List<string>();
        foreach (var x in indexed)
        {
            if (rightW <= leftW) // ties → right (so a single branch lands right)
            {
                rightBranches.Add(x.Branch);
                rightW += x.Weight;
            }
            else
            {
                leftBranches.Add(x.Branch);
                leftW += x.Weight;
            }
        }

        // 4. Each half = root + its branches' subtrees.
        HashSet<string> HalfSet(List<string> halfBranches)
        {
            var set = new HashSet<string> { rootId };
            foreach (var b in halfBranches)
            {
                foreach (var id in subtrees[b])
                {
                    set.Add(id);
                }
            }

            return set;
        }

        var rightSet = HalfSet(rightBranches);
        var leftSet = HalfSet(leftBranches);

        List<LayeredLayout.LayItem> ItemsFor(HashSet<string> set) => model.Nodes
            .Where(n => set.Contains(n.Id))
            .Select(n => { var s = SizeOf(n.Id); return new LayeredLayout.LayItem(n.Id, s.W, s.H, n.Rank, n.Order); })
            .ToList();

        List<(string, string)> EdgesFor(HashSet<string> set) => model.Edges
            .Where(e => set.Contains(e.From) && set.Contains(e.To))
            .Select(e => (e.From, e.To))
            .ToList();

        // 5. Lay out each half — right flows LR, left flows RL (root at the half's inner edge either way).
        var right = LayeredLayout.LayoutLayer(ItemsFor(rightSet), EdgesFor(rightSet), Direction.Lr, gap, rankGap);
        var left = LayeredLayout.LayoutLayer(ItemsFor(leftSet), EdgesFor(leftSet), Direction.Rl, gap, rankGap);

        // 6. Unify: translate the left half so its root rect lands exactly on the right half's root.
        var rightRoot = right.Rects[rootId];
        var leftRoot = left.Rects[rootId];
        double lox = rightRoot.X - leftRoot.X, loy = rightRoot.Y - leftRoot.Y;

        var placed = new Dictionary<string, Rect>();
        foreach (var (id, r) in right.Rects)
        {
            placed[id] = r;
        }

        foreach (var (id, r) in left.Rects)
        {
            if (id == rootId)
            {
                continue; // the shared root is already placed by the right half (coincident)
            }

            placed[id] = r.Offset(lox, loy);
        }

        // 7. Shift everything into positive space with a consistent canvas pad. Never negative.
        var minX = placed.Values.Min(r => r.X);
        var minY = placed.Values.Min(r => r.Y);
        double dx = CanvasPad - minX, dy = CanvasPad - minY;

        var nodes = new Dictionary<string, Rect>();
        foreach (var (id, r) in placed)
        {
            nodes[id] = r.Offset(dx, dy);
        }

        var width = Math.Ceiling(nodes.Values.Max(r => r.X + r.W) + CanvasPad);
        var height = Math.Ceiling(nodes.Values.Max(r => r.Y + r.H) + CanvasPad);

        return new LayoutResult(nodes, new Dictionary<string, Rect>(), width, height);
    }
}
