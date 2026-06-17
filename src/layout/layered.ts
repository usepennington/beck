import type { DiagramModel, Direction } from '../model/schema'
import type { LayoutResult, Point, Rect, SizeMap } from './types'

const FALLBACK = { w: 180, h: 64 }
const GROUP_PAD = { top: 28, side: 16, bottom: 16 }
const CANVAS_PAD = 16

/**
 * A small Sugiyama-style layered layout:
 *   1. rank assignment (cycle-break, longest-path, group snap + repair)
 *   2. ordering within ranks (virtual nodes on long edges, barycenter sweeps,
 *      group members kept contiguous)
 *   3. coordinate assignment (measured sizes, neighbor alignment)
 *   4. direction transform (TB/BT/LR/RL)
 *
 * Works internally in (primary = along ranks, secondary = across) coordinates,
 * then maps to x/y based on `direction` so the router/animation layers stay
 * orientation-agnostic.
 */
export function layeredLayout(model: DiagramModel, sizes: SizeMap): LayoutResult {
  const dir: Direction = model.meta.direction
  const horizontal = dir === 'LR' || dir === 'RL'
  const gap = model.meta.spacing.node
  const rankGap = model.meta.spacing.rank

  const nodes = model.nodes
  const nodeIds = nodes.map((n) => n.id)
  const idIndex = new Map(nodeIds.map((id, i) => [id, i]))
  const sizeOf = (id: string) => sizes.get(id) ?? FALLBACK
  const depthOf = (id: string) => (horizontal ? sizeOf(id).w : sizeOf(id).h)
  const breadthOf = (id: string) => (horizontal ? sizeOf(id).h : sizeOf(id).w)

  // node id -> group id (a node is in at most one group)
  const groupOf = new Map<string, string>()
  for (const g of model.groups) for (const m of g.members) groupOf.set(m, g.id)
  const groupIds = new Set(model.groups.map((g) => g.id))
  const membersOf = (id: string) => model.groups.find((g) => g.id === id)?.members ?? []

  // ---- expand edges to node->node pairs (group endpoints -> their members) ----
  const expanded: Array<[string, string]> = []
  for (const e of model.edges) {
    const froms = groupIds.has(e.from) ? membersOf(e.from) : [e.from]
    const tos = groupIds.has(e.to) ? membersOf(e.to) : [e.to]
    for (const f of froms) for (const t of tos) if (f !== t && idIndex.has(f) && idIndex.has(t)) expanded.push([f, t])
  }

  // ---- 1a. cycle break: mark back edges via iterative DFS ----
  const adj = new Map<string, string[]>()
  for (const id of nodeIds) adj.set(id, [])
  for (const [f, t] of expanded) adj.get(f)!.push(t)

  const color = new Map<string, 0 | 1 | 2>() // 0 white, 1 gray, 2 black
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
  for (const n of nodes) if (n.rank != null) rank.set(n.id, n.rank)

  // ---- 1c. group snap to a shared rank, then repair forward monotonicity ----
  for (let iter = 0; iter < 4; iter++) {
    let changed = false
    for (const g of model.groups) {
      if (g.members.length < 2) continue
      const r = Math.max(...g.members.map((m) => rank.get(m) ?? 0))
      for (const m of g.members)
        if ((rank.get(m) ?? 0) !== r) {
          rank.set(m, r)
          changed = true
        }
    }
    for (let pass = 0; pass < nodeIds.length; pass++) {
      let moved = false
      for (const [f, t] of forward) {
        if ((rank.get(t) ?? 0) <= (rank.get(f) ?? 0)) {
          rank.set(t, (rank.get(f) ?? 0) + 1)
          moved = true
          changed = true
        }
      }
      if (!moved) break
    }
    if (!changed) break
  }

  // compress ranks to 0..R (remove empty layers)
  const distinct = [...new Set(nodeIds.map((id) => rank.get(id) ?? 0))].sort((a, b) => a - b)
  const compress = new Map(distinct.map((r, i) => [r, i]))
  for (const id of nodeIds) rank.set(id, compress.get(rank.get(id) ?? 0) ?? 0)
  const maxRank = distinct.length - 1

  // ---- 2a. virtual nodes on long edges + adjacency between consecutive ranks ----
  const order: string[][] = Array.from({ length: maxRank + 1 }, () => [])
  for (const id of nodeIds) order[rank.get(id)!].push(id)

  const isVirtual = new Set<string>()
  const down = new Map<string, string[]>() // node -> neighbors one rank below
  const up = new Map<string, string[]>() // node -> neighbors one rank above
  const link = (a: string, b: string) => {
    ;(down.get(a) ?? down.set(a, []).get(a)!).push(b)
    ;(up.get(b) ?? up.set(b, []).get(b)!).push(a)
  }

  // Build adjacency + virtual chains from every forward (node->node, group-expanded)
  // pair so ordering and alignment account for long and group-to-group edges.
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

  // ---- 2b. ordering: barycenter sweeps with group contiguity ----
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
    const bary = new Map<string, number>()
    current.forEach((id, i) => {
      const b = baryOf(id, adjIndex, adjacentIsDown)
      bary.set(id, b < 0 ? i : b)
    })

    type Unit = { ids: string[]; key: number; tie: number }
    const units: Unit[] = []
    const clusters = new Map<string, string[]>()
    for (const id of current) {
      const g = groupOf.get(id)
      if (g && membersOf(g).filter((m) => rank.get(m) === r).length > 1) {
        if (!clusters.has(g)) clusters.set(g, [])
        clusters.get(g)!.push(id)
      } else {
        units.push({ ids: [id], key: bary.get(id)!, tie: tieOf(id) })
      }
    }
    for (const [, ids] of clusters) {
      ids.sort((a, b) => bary.get(a)! - bary.get(b)! || tieOf(a) - tieOf(b))
      const key = ids.reduce((s, id) => s + bary.get(id)!, 0) / ids.length
      const tie = Math.min(...ids.map((id) => tieOf(id)))
      units.push({ ids, key, tie })
    }
    units.sort((a, b) => a.key - b.key || a.tie - b.tie)
    order[r] = units.flatMap((u) => u.ids)
  }

  for (let sweep = 0; sweep < 6; sweep++) {
    if (sweep % 2 === 0) {
      for (let r = 1; r <= maxRank; r++) reorderRank(r, false)
    } else {
      for (let r = maxRank - 1; r >= 0; r--) reorderRank(r, true)
    }
  }

  // honor explicit node.order as a final per-rank sort
  for (let r = 0; r <= maxRank; r++) {
    const hasExplicit = order[r].some((id) => nodes[idIndex.get(id) ?? -1]?.order != null)
    if (!hasExplicit) continue
    order[r] = order[r]
      .map((id, i) => ({ id, i }))
      .sort((a, b) => {
        const oa = nodes[idIndex.get(a.id) ?? -1]?.order
        const ob = nodes[idIndex.get(b.id) ?? -1]?.order
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

  const nodeRects = new Map<string, Rect>()
  for (const id of nodeIds) {
    const c = centerXY(id)
    const sz = sizeOf(id)
    nodeRects.set(id, { x: c.x - sz.w / 2, y: c.y - sz.h / 2, w: sz.w, h: sz.h })
  }

  const groupRects = new Map<string, Rect>()
  for (const g of model.groups) {
    const rects = g.members.map((m) => nodeRects.get(m)).filter((r): r is Rect => !!r)
    if (!rects.length) continue
    const minX = Math.min(...rects.map((r) => r.x))
    const minY = Math.min(...rects.map((r) => r.y))
    const maxX = Math.max(...rects.map((r) => r.x + r.w))
    const maxY = Math.max(...rects.map((r) => r.y + r.h))
    groupRects.set(g.id, {
      x: minX - GROUP_PAD.side,
      y: minY - GROUP_PAD.top,
      w: maxX - minX + GROUP_PAD.side * 2,
      h: maxY - minY + GROUP_PAD.top + GROUP_PAD.bottom,
    })
  }

  // ---- normalize: shift so everything starts at CANVAS_PAD ----
  const allRects = [...nodeRects.values(), ...groupRects.values()]
  const minX = Math.min(...allRects.map((r) => r.x))
  const minY = Math.min(...allRects.map((r) => r.y))
  const dx = CANVAS_PAD - minX
  const dy = CANVAS_PAD - minY
  const shiftRect = (r: Rect): Rect => ({ x: r.x + dx, y: r.y + dy, w: r.w, h: r.h })
  for (const [id, r] of nodeRects) nodeRects.set(id, shiftRect(r))
  for (const [id, r] of groupRects) groupRects.set(id, shiftRect(r))

  const placed = [...nodeRects.values(), ...groupRects.values()]
  const maxXEnd = Math.max(...placed.map((r) => r.x + r.w))
  const maxYEnd = Math.max(...placed.map((r) => r.y + r.h))

  return {
    nodes: nodeRects,
    groups: groupRects,
    width: Math.ceil(maxXEnd + CANVAS_PAD),
    height: Math.ceil(maxYEnd + CANVAS_PAD),
  }
}
