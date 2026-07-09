# Cross-group edge straightening — design notes

Context: with per-card auto-width (cards size to their own content, no whole-diagram
bloat), connected nodes that live in **different groups** can end up slightly out of
line, so the vertical/horizontal edge between them jogs by a few px. Root cause: each
group is laid out independently and then **center-aligned as an opaque block** when it
is composed into its parent. Two connected groups of differing total width are centered
on the same midpoint but their member columns land at different cross-axis coordinates.
When every card was a fixed 180px this never showed (identical blocks → identical
columns); per-card widths expose it.

The fix must let **connected nodes share a cross-axis coordinate across group
boundaries** — something the "group = pre-baked centered block" model cannot express.

**Outcome:** Approach C was implemented and then reverted — a global re-solve broke fan-in
centring the hierarchical layout already got right (e.g. a node fed by three parents ended up
under the left parent instead of centred under the middle one). What shipped instead is the
hierarchical layout **plus a router-level anchor-nudge cheat** (see "Shipped solution" below).
Approaches A/B/C are kept for reference.

## Shipped solution — anchor-nudge cheat (in `Route/OrthogonalRouter.cs`)

The hierarchical layout already places nodes well (it centres fans and stacks groups). The
only thing left is the last few px of cross-axis misalignment on otherwise-straight edges. So
`TryStraighten` slides an edge's anchors along their faces to close a *small* perpendicular gap
(≤ `StraightenTotal`), preferring to nudge a single free-face anchor and leaving a fanned face's
spread intact; gaps beyond the budget stay as genuine jogs, and the straightened run is rejected
if it would hit an obstacle. This fixes the grid columns and short chains without moving nodes,
and without the fan-in regression Approach C introduced.

## Approach C — global coordinate rewrite (tried, reverted)

Replace the per-group opaque coordinate assignment with a single global cross-axis solve
over every leaf node (Brandes–Köpf-style vertical alignment / bend minimisation), with
groups expressed as **contiguity constraints** (a group's members occupy one contiguous
cross-axis band and don't interleave with outsiders) rather than as pre-centered blocks.
Group boxes are then derived from the final member positions.

- Pro: straightness becomes a property of the whole layout — grids, chains, fans and
  nested groups all benefit uniformly; eliminates the root cause; no downstream patching.
- Con: largest, highest-risk change; it rewrites the coordinate stage every diagram type
  flows through; group-contiguity constraints are fiddly under nesting; broad golden churn.

## Approach A — global align post-pass (fallback, stashed for reference)

Keep the existing layout stages untouched. After `LayeredLayout.Compute` produces the
absolute node/group rects, run one additive pass:

1. Build the leaf-node graph from the **real** edges (not the group-projected ones).
2. Bucket leaf nodes by rank (their primary-axis band).
3. For each edge that is a clean cross-rank 1:1 connection (from-node has one out-edge on
   that side, to-node has one in-edge), pull the two endpoints toward a shared cross-axis
   coordinate — barycenter desire + exact snap for lone pairs — moving nodes into
   adjacent **free space**, enforcing min-gap separation within each rank bucket
   (reuse the `ResolveSeparation` idea, but globally over leaves instead of per sub-layer).
4. Iterate a few times (stable order) so aligning one column doesn't leave the next
   oscillating.
5. **Refit every group box** to the bounding box of its (possibly moved) members plus the
   group pads — innermost groups first for nesting, preserving the asymmetric top pad that
   reserves room for the on-border label.

- Pro: general (grids *and* arbitrary chains); matches the "move the nodes, not the
  anchors" intent; localized/additive so it's easy to bound or disable; no card bloat.
- Con: the group-box refit is the fiddly part (nesting + asymmetric label padding +
  canvas centering must stay consistent); needs a solid collision check and a stable
  iteration order to avoid oscillation.

The per-layer 1:1 chain-straightening already in `LayeredLayout` (the `OneToOne` snap
after the barycenter sweeps) is a seed of A: it straightens chains *within* a single
sub-layer but cannot cross a group boundary. Approach A generalises it to the composed,
cross-group level; Approach C dissolves the need for it by never centering groups opaquely
in the first place.

## Approach B — shared column grid (narrowest, also stashed)

Detect sibling groups connected 1:1 (a parallel grid) and lay both on shared column slots
(slot width = max of the paired members); each member centers in its slot, so columns
line up by construction. Lowest risk, no refit machinery, exact alignment — but only fixes
the parallel-grid shape, not arbitrary cross-group chains.
