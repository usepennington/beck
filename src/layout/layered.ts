import type { DiagramModel, Direction } from '../model/schema'
import type { LayoutResult, Point, Rect, SizeMap } from './types'

const FALLBACK = { w: 180, h: 64 }
const GROUP_PAD = { top: 28, side: 16, bottom: 16 }
const CANVAS_PAD = 16

/** One thing to place in a layer: a node, or a group rendered as a super-node. */
interface LayItem {
  id: string
  w: number
  h: number
  rank?: number
  order?: number
}

interface LayerResult {
  rects: Map<string, Rect>
  width: number
  height: number
}

/**
 * Single-level Sugiyama-style layout (rank → order(+virtual nodes) → coords →
 * direction transform). Group-agnostic: it places a flat set of items connected
 * by item-level edges. Nesting is handled by the recursive driver below, which
 * feeds each group in as a sized super-node. Returns rects in local coordinates
 * with the top-left corner normalized to (0, 0).
 */
function layoutLayer(
  items: LayItem[],
  edges: Array<[string, string]>,
  dir: Direction,
  gap: number,
  rankGap: number,
): LayerResult {
  const horizontal = dir === 'LR' || dir === 'RL'
  const nodeIds = items.map((i) => i.id)
  const itemMap = new Map(items.map((i) => [i.id, i]))
  const idIndex = new Map(nodeIds.map((id, i) => [id, i]))
  const sizeOf = (id: string) => itemMap.get(id) ?? FALLBACK
  const depthOf = (id: string) => (horizontal ? sizeOf(id).w : sizeOf(id).h)
  const breadthOf = (id: string) => (horizontal ? sizeOf(id).h : sizeOf(id).w)

  // ---- expand to valid node->node pairs ----
  const expanded: Array<[string, string]> = []
  for (const [f, t] of edges) if (f !== t && idIndex.has(f) && idIndex.has(t)) expanded.push([f, t])

  // ---- 1a. cycle break: mark back edges via iterative DFS ----
  const adj = new Map<string, string[]>()
  for (const id of nodeIds) adj.set(id, [])
  for (const [f, t] of expanded) adj.get(f)!.push(t)

  const color = new Map<string, 0 | 1 | 2>()
  for (const id of nodeIds) color.set(id, 0)
  const back = new Set<string>()
  const edgeKey = (f: string, t: string) => `${f} ${t}`
  const stack: Array<{ id: string; i: number }> = []
  for (const start of nodeIds) {
    if (color.get(start) !== 0) continue
    stack.push({ id: start, i: 0 })
    color.set(start, 1)
    while (stack.length) {
      const top = stack[stack.length - 1]
      const neigh = adj.get(top.id)!
      if (top.i < neigh.length) {
        const v = neigh[top.i++]
        const c = color.get(v)
        if (c === 1) back.add(edgeKey(top.id, v))
        else if (c === 0) {
          color.set(v, 1)
          stack.push({ id: v, i: 0 })
        }
      } else {
        color.set(top.id, 2)
        stack.pop()
      }
    }
  }
  const forward = expanded.filter(([f, t]) => !back.has(edgeKey(f, t)))

  // ---- 1b. longest-path ranking over forward edges (Kahn) ----
  const rank = new Map<string, number>()
  for (const id of nodeIds) rank.set(id, 0)
  {
    const indeg = new Map<string, number>()
    const fAdj = new Map<string, string[]>()
    for (const id of nodeIds) {
      indeg.set(id, 0)
      fAdj.set(id, [])
    }
    for (const [f, t] of forward) {
      fAdj.get(f)!.push(t)
      indeg.set(t, (indeg.get(t) ?? 0) + 1)
    }
    const q = nodeIds.filter((id) => (indeg.get(id) ?? 0) === 0)
    while (q.length) {
      const u = q.shift()!
      for (const v of fAdj.get(u)!) {
        rank.set(v, Math.max(rank.get(v) ?? 0, (rank.get(u) ?? 0) + 1))
        indeg.set(v, (indeg.get(v) ?? 1) - 1)
        if ((indeg.get(v) ?? 0) === 0) q.push(v)
      }
    }
  }

  // explicit rank overrides
  for (const it of items) if (it.rank != null) rank.set(it.id, it.rank)

  // compress ranks to 0..R (remove empty layers)
  const distinct = [...new Set(nodeIds.map((id) => rank.get(id) ?? 0))].sort((a, b) => a - b)
  const compress = new Map(distinct.map((r, i) => [r, i]))
  for (const id of nodeIds) rank.set(id, compress.get(rank.get(id) ?? 0) ?? 0)
  const maxRank = distinct.length - 1

  // ---- 2a. virtual nodes on long edges + adjacency between consecutive ranks ----
  const order: string[][] = Array.from({ length: maxRank + 1 }, () => [])
  for (const id of nodeIds) order[rank.get(id)!].push(id)

  const isVirtual = new Set<string>()
  const down = new Map<string, string[]>()
  const up = new Map<string, string[]>()
  const link = (a: string, b: string) => {
    ;(down.get(a) ?? down.set(a, []).get(a)!).push(b)
    ;(up.get(b) ?? up.set(b, []).get(b)!).push(a)
  }

  let vCounter = 0
  for (const [f, t] of forward) {
    const rf = rank.get(f)!
    const rt = rank.get(t)!
    if (rf === rt) continue
    const lo = Math.min(rf, rt)
    const hi = Math.max(rf, rt)
    const top = rf < rt ? f : t
    const bottom = rf < rt ? t : f
    if (hi - lo === 1) {
      link(top, bottom)
      continue
    }
    let prev = top
    for (let r = lo + 1; r < hi; r++) {
      const vid = ` v${vCounter++}`
      isVirtual.add(vid)
      order[r].push(vid)
      rank.set(vid, r)
      link(prev, vid)
      prev = vid
    }
    link(prev, bottom)
  }

  const breadthAny = (id: string) => (isVirtual.has(id) ? 0 : breadthOf(id))
  const depthAny = (id: string) => (isVirtual.has(id) ? 0 : depthOf(id))

  // ---- 2b. ordering: barycenter sweeps ----
  const orderIndex = (r: number) => {
    const m = new Map<string, number>()
    order[r].forEach((id, i) => m.set(id, i))
    return m
  }
  const baryOf = (id: string, neighborRankIndex: Map<string, number>, useDown: boolean): number => {
    const neigh = (useDown ? down.get(id) : up.get(id)) ?? []
    if (neigh.length === 0) return -1
    let sum = 0
    let count = 0
    for (const n of neigh) {
      const pos = neighborRankIndex.get(n)
      if (pos != null) {
        sum += pos
        count++
      }
    }
    return count ? sum / count : -1
  }

  const tieOf = (id: string) => idIndex.get(id) ?? (Number(id.trim().slice(1)) || 0)

  const reorderRank = (r: number, adjacentIsDown: boolean) => {
    const adjIndex = orderIndex(adjacentIsDown ? r + 1 : r - 1)
    const current = order[r]
    const units = current.map((id, i) => {
      const b = baryOf(id, adjIndex, adjacentIsDown)
      return { id, key: b < 0 ? i : b, tie: tieOf(id) }
    })
    units.sort((a, b) => a.key - b.key || a.tie - b.tie)
    order[r] = units.map((u) => u.id)
  }

  for (let sweep = 0; sweep < 6; sweep++) {
    if (sweep % 2 === 0) {
      for (let r = 1; r <= maxRank; r++) reorderRank(r, false)
    } else {
      for (let r = maxRank - 1; r >= 0; r--) reorderRank(r, true)
    }
  }

  // honor explicit item.order as a final per-rank sort
  for (let r = 0; r <= maxRank; r++) {
    const hasExplicit = order[r].some((id) => itemMap.get(id)?.order != null)
    if (!hasExplicit) continue
    order[r] = order[r]
      .map((id, i) => ({ id, i }))
      .sort((a, b) => {
        const oa = itemMap.get(a.id)?.order
        const ob = itemMap.get(b.id)?.order
        if (oa != null && ob != null) return oa - ob || a.i - b.i
        if (oa != null) return -1
        if (ob != null) return 1
        return a.i - b.i
      })
      .map((x) => x.id)
  }

  // ---- 3a. secondary (cross-axis) coordinates ----
  const sec = new Map<string, number>()
  const halfB = (id: string) => breadthAny(id) / 2

  for (let r = 0; r <= maxRank; r++) {
    let cursor = 0
    const row = order[r]
    const centers: number[] = []
    for (const id of row) {
      const b = breadthAny(id)
      centers.push(cursor + b / 2)
      cursor += b + gap
    }
    const total = cursor - gap
    const shift = -total / 2
    row.forEach((id, i) => sec.set(id, centers[i] + shift))
  }

  const resolveSeparation = (r: number, desired: Map<string, number>) => {
    const row = order[r]
    const pos: number[] = []
    for (let i = 0; i < row.length; i++) {
      const id = row[i]
      let p = desired.get(id) ?? sec.get(id)!
      if (i > 0) {
        const min = pos[i - 1] + halfB(row[i - 1]) + gap + halfB(id)
        if (p < min) p = min
      }
      pos[i] = p
    }
    for (let i = row.length - 2; i >= 0; i--) {
      const id = row[i]
      const want = desired.get(id)
      if (want == null) continue
      const max = pos[i + 1] - halfB(row[i + 1]) - gap - halfB(id)
      if (want < pos[i]) pos[i] = Math.max(want, i > 0 ? pos[i - 1] + halfB(row[i - 1]) + gap + halfB(id) : -Infinity)
      else pos[i] = Math.min(want, max)
    }
    row.forEach((id, i) => sec.set(id, pos[i]))
  }

  for (let it = 0; it < 6; it++) {
    if (it % 2 === 0) {
      for (let r = 1; r <= maxRank; r++) {
        const desired = new Map<string, number>()
        for (const id of order[r]) {
          const neigh = up.get(id) ?? []
          if (neigh.length) desired.set(id, neigh.reduce((s, n) => s + sec.get(n)!, 0) / neigh.length)
        }
        resolveSeparation(r, desired)
      }
    } else {
      for (let r = maxRank - 1; r >= 0; r--) {
        const desired = new Map<string, number>()
        for (const id of order[r]) {
          const neigh = down.get(id) ?? []
          if (neigh.length) desired.set(id, neigh.reduce((s, n) => s + sec.get(n)!, 0) / neigh.length)
        }
        resolveSeparation(r, desired)
      }
    }
  }

  // ---- 3b. primary (rank-axis) coordinates ----
  const rankDepth: number[] = []
  for (let r = 0; r <= maxRank; r++) {
    let d = 0
    for (const id of order[r]) d = Math.max(d, depthAny(id))
    rankDepth[r] = d || FALLBACK.h
  }
  const primaryStart: number[] = []
  let acc = 0
  for (let r = 0; r <= maxRank; r++) {
    primaryStart[r] = acc
    acc += rankDepth[r] + rankGap
  }
  const totalPrimary = acc - rankGap
  const primaryCenter = (id: string) => primaryStart[rank.get(id)!] + rankDepth[rank.get(id)!] / 2

  // ---- 4. map (primary, secondary) -> x/y per direction ----
  const centerXY = (id: string): Point => {
    const p = primaryCenter(id)
    const s = sec.get(id)!
    if (!horizontal) return { x: s, y: dir === 'BT' ? totalPrimary - p : p }
    return { x: dir === 'RL' ? totalPrimary - p : p, y: s }
  }

  const rects = new Map<string, Rect>()
  for (const id of nodeIds) {
    const c = centerXY(id)
    const sz = sizeOf(id)
    rects.set(id, { x: c.x - sz.w / 2, y: c.y - sz.h / 2, w: sz.w, h: sz.h })
  }

  // normalize so the top-left corner sits at (0, 0)
  const all = [...rects.values()]
  if (all.length === 0) return { rects, width: 0, height: 0 }
  const minX = Math.min(...all.map((r) => r.x))
  const minY = Math.min(...all.map((r) => r.y))
  for (const [id, r] of rects) rects.set(id, { x: r.x - minX, y: r.y - minY, w: r.w, h: r.h })
  const width = Math.max(...[...rects.values()].map((r) => r.x + r.w))
  const height = Math.max(...[...rects.values()].map((r) => r.y + r.h))
  return { rects, width, height }
}

interface Composed {
  nodeRects: Map<string, Rect>
  groupRects: Map<string, Rect>
  width: number
  height: number
}

/**
 * Recursive compound layout. A group is laid out as its own sub-graph, then
 * fed into its parent as a single sized super-node; the result is un-nested into
 * absolute node/group rects. Groups can therefore span ranks and nest
 * arbitrarily, and a group box provably never encloses a non-member (a foreign
 * node is simply a different super-node at that level). Edges are projected to
 * each container level (an edge lifts to the topmost ancestor-child that holds
 * its endpoint); cross-boundary edges are routed globally afterward.
 */
export function layeredLayout(model: DiagramModel, sizes: SizeMap): LayoutResult {
  const dir = model.meta.direction
  const gap = model.meta.spacing.node
  const rankGap = model.meta.spacing.rank

  const nodeById = new Map(model.nodes.map((n) => [n.id, n]))
  const groupById = new Map(model.groups.map((g) => [g.id, g]))
  const sizeOf = (id: string) => sizes.get(id) ?? FALLBACK

  // member -> containing group (first declared parent wins; validate guards the tree)
  const parent = new Map<string, string>()
  for (const g of model.groups)
    for (const m of g.members)
      if ((nodeById.has(m) || groupById.has(m)) && !parent.has(m)) parent.set(m, g.id)
  const parentOf = (id: string): string | null => parent.get(id) ?? null

  const directChildren = (containerId: string | null): string[] => {
    if (containerId === null) {
      const kids: string[] = []
      for (const n of model.nodes) if (parentOf(n.id) === null) kids.push(n.id)
      for (const g of model.groups) if (parentOf(g.id) === null) kids.push(g.id)
      return kids
    }
    return (groupById.get(containerId)?.members ?? []).filter((m) => nodeById.has(m) || groupById.has(m))
  }

  // The direct child of `containerId` that holds `x` (x itself or an ancestor group of it).
  const repIn = (x: string, containerId: string | null): string | null => {
    let cur = x
    let guard = 0
    while (parentOf(cur) !== containerId) {
      const p = parentOf(cur)
      if (p === null || ++guard > 10000) return null // x is not inside this container
      cur = p
    }
    return cur
  }

  const projectedEdges = (containerId: string | null, childSet: Set<string>): Array<[string, string]> => {
    const out: Array<[string, string]> = []
    const seen = new Set<string>()
    for (const e of model.edges) {
      const cu = repIn(e.from, containerId)
      const cv = repIn(e.to, containerId)
      if (cu === null || cv === null || cu === cv) continue
      if (!childSet.has(cu) || !childSet.has(cv)) continue
      const key = `${cu} ${cv}`
      if (seen.has(key)) continue
      seen.add(key)
      out.push([cu, cv])
    }
    return out
  }

  const visiting = new Set<string>()
  const offset = (r: Rect, dx: number, dy: number): Rect => ({ x: r.x + dx, y: r.y + dy, w: r.w, h: r.h })

  const layoutContainer = (containerId: string | null): Composed => {
    if (containerId !== null) {
      if (visiting.has(containerId)) return { nodeRects: new Map(), groupRects: new Map(), width: 0, height: 0 }
      visiting.add(containerId)
    }
    const children = directChildren(containerId)
    const subs = new Map<string, Composed>()
    const items: LayItem[] = []
    for (const c of children) {
      if (groupById.has(c)) {
        const sub = layoutContainer(c)
        if (sub.nodeRects.size === 0) continue // skip empty groups (no renderable members)
        subs.set(c, sub)
        items.push({ id: c, w: sub.width, h: sub.height })
      } else {
        const s = sizeOf(c)
        const n = nodeById.get(c)!
        items.push({ id: c, w: s.w, h: s.h, rank: n.rank, order: n.order })
      }
    }
    if (containerId !== null) visiting.delete(containerId)

    const childSet = new Set(items.map((i) => i.id))
    const edges = projectedEdges(containerId, childSet)
    const layer = items.length
      ? layoutLayer(items, edges, dir, gap, rankGap)
      : { rects: new Map<string, Rect>(), width: 0, height: 0 }

    const inset = containerId !== null
    const padL = inset ? GROUP_PAD.side : 0
    const padT = inset ? GROUP_PAD.top : 0
    const padR = inset ? GROUP_PAD.side : 0
    const padB = inset ? GROUP_PAD.bottom : 0

    const nodeRects = new Map<string, Rect>()
    const groupRects = new Map<string, Rect>()
    for (const c of childSet) {
      const lr = layer.rects.get(c)
      if (!lr) continue
      const r = offset(lr, padL, padT)
      if (groupById.has(c)) {
        const sub = subs.get(c)!
        for (const [nid, nr] of sub.nodeRects) nodeRects.set(nid, offset(nr, r.x, r.y))
        for (const [gid, gr] of sub.groupRects) groupRects.set(gid, offset(gr, r.x, r.y))
        groupRects.set(c, r)
      } else {
        nodeRects.set(c, r)
      }
    }
    return { nodeRects, groupRects, width: layer.width + padL + padR, height: layer.height + padT + padB }
  }

  const root = layoutContainer(null)
  const nodes = new Map<string, Rect>()
  const groups = new Map<string, Rect>()
  for (const [id, r] of root.nodeRects) nodes.set(id, offset(r, CANVAS_PAD, CANVAS_PAD))
  for (const [id, r] of root.groupRects) groups.set(id, offset(r, CANVAS_PAD, CANVAS_PAD))

  return {
    nodes,
    groups,
    width: Math.ceil(root.width + CANVAS_PAD * 2),
    height: Math.ceil(root.height + CANVAS_PAD * 2),
  }
}
