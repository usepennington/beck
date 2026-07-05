import type { DiagramModel, EdgeModel, MarkerShape, Side } from '../model/schema'
import type { LayoutResult, Point, Rect } from '../layout/types'
import { routeEdge, sidesFor } from './orthogonal'

const SVGNS = 'http://www.w3.org/2000/svg'

export interface RoutedEdge {
  edge: EdgeModel
  /** The visible, animatable path (packets sample it via getPointAtLength). */
  path: SVGPathElement
}

export function createOverlay(width: number, height: number): SVGSVGElement {
  const svg = document.createElementNS(SVGNS, 'svg')
  svg.classList.add('beck-overlay')
  svg.setAttribute('width', String(width))
  svg.setAttribute('height', String(height))
  svg.setAttribute('viewBox', `0 0 ${width} ${height}`)
  svg.appendChild(document.createElementNS(SVGNS, 'defs'))
  return svg
}

const markerCache = new WeakMap<SVGSVGElement, Map<string, string>>()
let markerSeq = 0

/** Marker geometry: every shape points +x with its tip at `refX`, so the same
 *  def works at either end — `orient: auto-start-reverse` flips it 180° for
 *  `marker-start`, which is exactly what a UML triangle-at-the-parent or
 *  diamond-at-the-whole needs. Hollow shapes fill with the surface var so the
 *  edge line doesn't show through them. */
function markerBody(shape: MarkerShape, color: string): { el: SVGElement; viewBox: string; refX: number; w: number; h: number } {
  const make = (tag: string) => document.createElementNS(SVGNS, tag)
  switch (shape) {
    case 'arrow-open': {
      const p = make('polyline')
      p.setAttribute('points', '2,1.5 9,5 2,8.5')
      p.setAttribute('fill', 'none')
      p.setAttribute('stroke', color)
      p.setAttribute('stroke-width', '1.8')
      p.setAttribute('stroke-linecap', 'round')
      p.setAttribute('stroke-linejoin', 'round')
      return { el: p, viewBox: '0 0 10 10', refX: 8, w: 7, h: 7 }
    }
    case 'triangle': {
      const p = make('path')
      p.setAttribute('d', 'M 1 1.5 L 11 6 L 1 10.5 Z')
      p.setAttribute('fill', 'var(--beck-surface)')
      p.setAttribute('stroke', color)
      p.setAttribute('stroke-width', '1.3')
      p.setAttribute('stroke-linejoin', 'round')
      return { el: p, viewBox: '0 0 12 12', refX: 10.5, w: 10, h: 10 }
    }
    case 'diamond':
    case 'diamond-open': {
      const p = make('path')
      p.setAttribute('d', 'M 1 5 L 7 1.2 L 13 5 L 7 8.8 Z')
      if (shape === 'diamond') p.setAttribute('fill', color)
      else {
        p.setAttribute('fill', 'var(--beck-surface)')
        p.setAttribute('stroke', color)
        p.setAttribute('stroke-width', '1.3')
        p.setAttribute('stroke-linejoin', 'round')
      }
      return { el: p, viewBox: '0 0 14 10', refX: 12.5, w: 11, h: 8 }
    }
    default: {
      const p = make('polygon')
      p.setAttribute('points', '0,1 10,5 0,9')
      p.setAttribute('fill', color)
      return { el: p, viewBox: '0 0 10 10', refX: 8, w: 6, h: 6 }
    }
  }
}

/** Create (or reuse) an end marker of a given shape tinted to a color value. */
export function ensureMarker(svg: SVGSVGElement, color: string, shape: MarkerShape = 'arrow'): string {
  let cache = markerCache.get(svg)
  if (!cache) {
    cache = new Map()
    markerCache.set(svg, cache)
  }
  const key = `${shape}|${color}`
  const hit = cache.get(key)
  if (hit) return hit
  const id = `beck-arrow-${markerSeq++}`
  const body = markerBody(shape, color)
  const marker = document.createElementNS(SVGNS, 'marker')
  marker.setAttribute('id', id)
  marker.setAttribute('viewBox', body.viewBox)
  marker.setAttribute('refX', String(body.refX))
  marker.setAttribute('refY', body.viewBox.split(' ')[3] === '12' ? '6' : '5')
  marker.setAttribute('markerWidth', String(body.w))
  marker.setAttribute('markerHeight', String(body.h))
  marker.setAttribute('orient', 'auto-start-reverse')
  marker.appendChild(body.el)
  svg.querySelector('defs')!.appendChild(marker)
  cache.set(key, id)
  return id
}

export function drawEdge(svg: SVGSVGElement, d: string, edge: EdgeModel): SVGPathElement {
  const path = document.createElementNS(SVGNS, 'path')
  path.setAttribute('d', d)
  path.setAttribute('fill', 'none')
  path.setAttribute('stroke', edge.color)
  path.setAttribute('stroke-width', '1.6')
  path.setAttribute('stroke-linecap', 'round')
  path.setAttribute('stroke-linejoin', 'round')
  if (edge.style === 'dashed') path.setAttribute('stroke-dasharray', '7 5')
  // The marker uses orient="auto-start-reverse", so the same def points the
  // right way at either end. Explicit UML markers win over the plain arrow ends.
  const endShape = edge.markerEnd ?? (edge.arrow === 'end' || edge.arrow === 'both' ? 'arrow' : null)
  const startShape = edge.markerStart ?? (edge.arrow === 'start' || edge.arrow === 'both' ? 'arrow' : null)
  if (endShape) path.setAttribute('marker-end', `url(#${ensureMarker(svg, edge.color, endShape)})`)
  if (startShape) path.setAttribute('marker-start', `url(#${ensureMarker(svg, edge.color, startShape)})`)
  path.dataset.from = edge.from
  path.dataset.to = edge.to
  path.dataset.edge = edge.id
  // Expose the semantic kind so hosts can target it (e.g. `.beck-edge--control`) and so kinds
  // that share a default style — control vs data — are still distinguishable in the DOM.
  path.dataset.kind = edge.kind
  path.classList.add('beck-edge', `beck-edge--${edge.kind}`)
  svg.appendChild(path)
  return path
}

function polylineMidpoint(points: Point[]): Point {
  const segs: number[] = []
  let total = 0
  for (let i = 0; i < points.length - 1; i++) {
    const l = Math.hypot(points[i + 1].x - points[i].x, points[i + 1].y - points[i].y)
    segs.push(l)
    total += l
  }
  let half = total / 2
  for (let i = 0; i < segs.length; i++) {
    if (half <= segs[i]) {
      const t = segs[i] ? half / segs[i] : 0
      return {
        x: points[i].x + (points[i + 1].x - points[i].x) * t,
        y: points[i].y + (points[i + 1].y - points[i].y) * t,
      }
    }
    half -= segs[i]
  }
  return points[points.length - 1] ?? { x: 0, y: 0 }
}

const LABEL_GAP = 8 // clear space between the label box and the edge line
const LABEL_PAD_X = 4 // breathing room added around the measured glyph box
const LABEL_PAD_Y = 2
const LABEL_MARGIN = 4 // keep the label this far inside the canvas
const LABEL_END_INSET = 14 // don't place a label right on top of an arrowhead

type LabelAnchor = 'start' | 'middle' | 'end'
interface LabelBox {
  cx: number
  cy: number
  hw: number
  hh: number
  anchor: LabelAnchor
}

/** Signed gap between a label box and a node rect: ≥0 when clear, <0 when overlapping. */
function boxGap(box: LabelBox, r: Rect): number {
  const ix = Math.min(box.cx + box.hw, r.x + r.w) - Math.max(box.cx - box.hw, r.x)
  const iy = Math.min(box.cy + box.hh, r.y + r.h) - Math.max(box.cy - box.hh, r.y)
  if (ix > 0 && iy > 0) return -Math.min(ix, iy)
  return Math.hypot(ix > 0 ? 0 : -ix, iy > 0 ? 0 : -iy)
}

/** Signed gap between a label box and a line segment: ≥0 when clear, <0 when crossing. */
function segGap(box: LabelBox, a: Point, b: Point): number {
  const x1 = box.cx - box.hw
  const y1 = box.cy - box.hh
  const x2 = box.cx + box.hw
  const y2 = box.cy + box.hh
  // Clamp the segment against the box via the slab method (Liang–Barsky).
  const dx = b.x - a.x
  const dy = b.y - a.y
  let t0 = 0
  let t1 = 1
  let inside = true
  for (const [p, q] of [
    [-dx, a.x - x1],
    [dx, x2 - a.x],
    [-dy, a.y - y1],
    [dy, y2 - a.y],
  ] as const) {
    if (p === 0) {
      if (q < 0) {
        inside = false
        break
      }
    } else {
      const r = q / p
      if (p < 0) t0 = Math.max(t0, r)
      else t1 = Math.min(t1, r)
      if (t0 > t1) {
        inside = false
        break
      }
    }
  }
  if (inside) return -4 // crossing; flat penalty (lines are thin — depth is meaningless)
  // Separated: min distance from the box corners to the segment.
  const len2 = dx * dx + dy * dy || 1
  let min = Infinity
  for (const [px, py] of [
    [x1, y1],
    [x2, y1],
    [x1, y2],
    [x2, y2],
  ] as const) {
    const t = Math.max(0, Math.min(1, ((px - a.x) * dx + (py - a.y) * dy) / len2))
    min = Math.min(min, Math.hypot(px - (a.x + dx * t), py - (a.y + dy * t)))
  }
  return min
}

/**
 * Worst signed clearance of a box against node cards AND every other edge's
 * polyline — this is what lets labels breathe: a spot on the empty side of a
 * line scores higher than one crossing a neighbouring edge.
 */
function clearance(box: LabelBox, obstacles: Rect[], lines: Point[][]): number {
  let min = Infinity
  for (const o of obstacles) min = Math.min(min, boxGap(box, o))
  for (const poly of lines) {
    for (let i = 0; i < poly.length - 1; i++) min = Math.min(min, segGap(box, poly[i], poly[i + 1]))
  }
  return min
}

/**
 * Pick where the label sits. Instead of the blind geometric midpoint, sample
 * points along each straight segment and keep the box with the most clearance to
 * any node — preferring, among near-ties, the spot closest to the edge midpoint.
 * On vertical-ish segments the text is anchored to one side so it grows *away*
 * from the line rather than straddling it (the failure mode behind labels that
 * detoured into the canvas-edge lane and ended up behind a node).
 */
function chooseLabelBox(
  points: Point[],
  hw: number,
  hh: number,
  obstacles: Rect[],
  lines: Point[][],
  bounds: { width: number; height: number },
  mid: Point,
): LabelBox {
  const clampCenter = (cx: number, cy: number) => ({
    cx: Math.min(Math.max(cx, LABEL_MARGIN + hw), Math.max(LABEL_MARGIN + hw, bounds.width - LABEL_MARGIN - hw)),
    cy: Math.min(Math.max(cy, LABEL_MARGIN + hh), Math.max(LABEL_MARGIN + hh, bounds.height - LABEL_MARGIN - hh)),
  })

  let best: LabelBox | null = null
  let bestClear = -Infinity
  let bestDist = Infinity
  const consider = (cx0: number, cy0: number, anchor: LabelAnchor, segIdx: number) => {
    const c = clampCenter(cx0, cy0)
    const box: LabelBox = { cx: c.cx, cy: c.cy, hw, hh, anchor }
    let clear = clearance(box, obstacles, lines)
    // The label's own polyline counts too — except the segment it is anchored
    // to (it deliberately sits LABEL_GAP off that one). This keeps labels from
    // straddling their own line's next bend.
    for (let j = 0; j < points.length - 1; j++) {
      if (j === segIdx) continue
      clear = Math.min(clear, segGap(box, points[j], points[j + 1]))
    }
    const dist = Math.hypot(c.cx - mid.x, c.cy - mid.y)
    const tie = clear === bestClear || Math.abs(clear - bestClear) <= 6
    if (best === null || clear > bestClear + 6 || (tie && dist < bestDist)) {
      best = box
      bestClear = clear
      bestDist = dist
    }
  }

  for (let i = 0; i < points.length - 1; i++) {
    const a = points[i]
    const b = points[i + 1]
    const dx = b.x - a.x
    const dy = b.y - a.y
    const len = Math.hypot(dx, dy)
    if (len < 1) continue
    const inset = Math.min(LABEL_END_INSET / len, 0.5)
    const steps = Math.max(1, Math.min(6, Math.floor(len / 40)))
    for (let k = 0; k <= steps; k++) {
      const tt = inset + (1 - 2 * inset) * (k / steps)
      const px = a.x + dx * tt
      const py = a.y + dy * tt
      if (Math.abs(dy) >= Math.abs(dx)) {
        // vertical-ish: park the text to the clearer side of the line
        consider(px - LABEL_GAP - hw, py, 'end', i)
        consider(px + LABEL_GAP + hw, py, 'start', i)
      } else {
        // horizontal-ish: above or below the line
        consider(px, py - LABEL_GAP - hh, 'middle', i)
        consider(px, py + LABEL_GAP + hh, 'middle', i)
      }
    }
  }

  // Last resort: when every off-line spot collides with something, straddling
  // the line itself (the surface halo keeps the glyphs legible) reads better
  // than clipping into a card — e.g. an LR edge label wider than the rank gap.
  if (bestClear < 0) {
    for (let i = 0; i < points.length - 1; i++) {
      const a = points[i]
      const b = points[i + 1]
      if (Math.hypot(b.x - a.x, b.y - a.y) < 1) continue
      const c = clampCenter((a.x + b.x) / 2, (a.y + b.y) / 2)
      const box: LabelBox = { cx: c.cx, cy: c.cy, hw, hh, anchor: 'middle' }
      let clear = clearance(box, obstacles, lines)
      for (let j = 0; j < points.length - 1; j++) {
        if (j === i) continue
        clear = Math.min(clear, segGap(box, points[j], points[j + 1]))
      }
      if (clear > bestClear) {
        best = box
        bestClear = clear
      }
    }
  }

  if (best === null) {
    const c = clampCenter(mid.x, mid.y - LABEL_GAP - hh)
    return { cx: c.cx, cy: c.cy, hw, hh, anchor: 'middle' }
  }
  return best
}

function drawLabel(
  svg: SVGSVGElement,
  points: Point[],
  text: string,
  obstacles: Rect[],
  lines: Point[][],
  bounds: { width: number; height: number },
  placed: Rect[],
): void {
  const t = document.createElementNS(SVGNS, 'text')
  t.classList.add('beck-edge-label')
  t.textContent = text
  // Append before measuring: the overlay is already in the connected DOM at route
  // time (same as the node measure pass), so getBBox reports real host-font metrics.
  svg.appendChild(t)
  let w = 0
  let h = 0
  try {
    const bb = t.getBBox()
    w = bb.width
    h = bb.height
  } catch {
    // getBBox can throw / return empty inside a display:none subtree (collapsed tab,
    // accordion). The halo carries legibility; we just estimate the box below.
  }
  if (!w || !Number.isFinite(w)) w = text.length * 7
  if (!h || !Number.isFinite(h)) h = 12
  const hw = w / 2 + LABEL_PAD_X
  const hh = h / 2 + LABEL_PAD_Y

  // Avoid node cards, already-placed labels, and every other edge's line.
  const all = placed.length ? obstacles.concat(placed) : obstacles
  const box = chooseLabelBox(points, hw, hh, all, lines, bounds, polylineMidpoint(points))
  // Derive the text x from the box + anchor (dominant-baseline centers it on cy).
  const tx = box.anchor === 'start' ? box.cx - w / 2 : box.anchor === 'end' ? box.cx + w / 2 : box.cx
  t.setAttribute('x', String(Math.round(tx)))
  t.setAttribute('y', String(Math.round(box.cy)))
  t.setAttribute('text-anchor', box.anchor)
  t.setAttribute('dominant-baseline', 'central')
  placed.push({ x: box.cx - box.hw, y: box.cy - box.hh, w: box.hw * 2, h: box.hh * 2 })
}

/** A small end annotation (UML multiplicity like "1"/"*"), parked just off the
 *  path near its start or end, on the side away from the line. Its box is
 *  pushed onto `placed` so mid-labels won't sit on top of it. */
function drawEndLabel(svg: SVGSVGElement, points: Point[], text: string, atStart: boolean, placed: Rect[]): void {
  if (points.length < 2) return
  const a = atStart ? points[0] : points[points.length - 1]
  const b = atStart ? points[1] : points[points.length - 2]
  const len = Math.hypot(b.x - a.x, b.y - a.y) || 1
  const dir = { x: (b.x - a.x) / len, y: (b.y - a.y) / len }
  // 18px in from the endpoint (past the marker), 10px perpendicular off the line.
  const px = a.x + dir.x * 18 - dir.y * 10
  const py = a.y + dir.y * 18 + dir.x * 10
  const t = document.createElementNS(SVGNS, 'text')
  t.classList.add('beck-edge-label')
  t.textContent = text
  t.setAttribute('x', String(Math.round(px)))
  t.setAttribute('y', String(Math.round(py)))
  t.setAttribute('text-anchor', 'middle')
  t.setAttribute('dominant-baseline', 'central')
  svg.appendChild(t)
  const hw = text.length * 3.5 + 3
  placed.push({ x: px - hw, y: py - 7, w: hw * 2, h: 14 })
}

/** Per-edge routing context resolved once, up front (indices line up with model.edges). */
interface EdgePrep {
  from: Rect
  to: Rect
  obstacles: Rect[]
  fromSide: Side
  toSide: Side
}

/** Route + draw every edge in the model. Returns the visible paths for animation. */
export function routeEdges(svg: SVGSVGElement, model: DiagramModel, layout: LayoutResult): RoutedEdge[] {
  const radius = model.meta.spacing.cornerRadius
  const primaryHorizontal = model.meta.direction === 'LR' || model.meta.direction === 'RL'
  const rectOf = (id: string): Rect | undefined => layout.nodes.get(id) ?? layout.groups.get(id)
  const groupById = new Map(model.groups.map((g) => [g.id, g]))
  // All leaf-node rects under a group (recursing through nested sub-groups), so
  // an edge to/from a group is never forced to dodge its own descendants.
  const memberRects = (id: string): Rect[] => {
    if (!groupById.has(id)) return []
    const out: Rect[] = []
    const walk = (gid: string) => {
      for (const m of groupById.get(gid)?.members ?? []) {
        const nr = layout.nodes.get(m)
        if (nr) out.push(nr)
        else if (groupById.has(m)) walk(m)
      }
    }
    walk(id)
    return out
  }

  // Labels avoid every node card (not just this edge's endpoints), so a detouring
  // edge's label never lands on an unrelated node.
  const labelObstacles = [...layout.nodes.values()]
  const labelBounds = { width: layout.width, height: layout.height }
  const placedLabels: Rect[] = []

  // Resolve each edge's obstacles and faces ONCE, up front: the anchor spread and
  // the route must agree on which face every edge sits on (a feedback edge diverted
  // to a loop face must be spread — and drawn — on that face, not the shared forward
  // face). Indices line up with model.edges; unresolvable endpoints become null.
  const dir = model.meta.direction
  const allRects = [...layout.nodes.values()]
  const prep: Array<EdgePrep | null> = model.edges.map((edge) => {
    const from = rectOf(edge.from)
    const to = rectOf(edge.to)
    if (!from || !to) return null
    const exclude = new Set<Rect>([from, to, ...memberRects(edge.from), ...memberRects(edge.to)])
    const obstacles = allRects.filter((r) => !exclude.has(r))
    const { fromSide, toSide } = sidesFor(from, to, dir, edge.curve, obstacles, edge.fromSide, edge.toSide)
    return { from, to, obstacles, fromSide, toSide }
  })

  const shifts = anchorShifts(model.edges, prep)

  // Pass 1 - route and draw every path. Labels wait for pass 2, when every
  // polyline is known, so each label can dodge ALL the lines, not just the
  // ones routed before it.
  const out: RoutedEdge[] = []
  const routed: Array<{ edge: EdgeModel; points: Point[] }> = []
  model.edges.forEach((edge, i) => {
    const p = prep[i]
    if (!p) return
    const { d, points } = routeEdge({
      from: p.from,
      to: p.to,
      fromSide: p.fromSide,
      toSide: p.toSide,
      curve: edge.curve,
      obstacles: p.obstacles,
      radius,
      primaryHorizontal,
      bounds: { width: layout.width, height: layout.height },
      fromShift: shifts[i].from,
      toShift: shifts[i].to,
    })
    const path = drawEdge(svg, d, edge)
    out.push({ edge, path })
    routed.push({ edge, points })
  })

  // Pass 2 - end annotations first (multiplicities hug fixed spots), then the
  // mid labels, each avoiding nodes, other lines, and everything already placed.
  routed.forEach((r) => {
    if (r.edge.fromLabel) drawEndLabel(svg, r.points, r.edge.fromLabel, true, placedLabels)
    if (r.edge.toLabel) drawEndLabel(svg, r.points, r.edge.toLabel, false, placedLabels)
  })
  routed.forEach((r, i) => {
    if (!r.edge.label) return
    const otherLines = routed.filter((_, j) => j !== i).map((o) => o.points)
    drawLabel(svg, r.points, r.edge.label, labelObstacles, otherLines, labelBounds, placedLabels)
  })
  return out
}

/**
 * Spread the anchors of edges that share a node face along that face, ordered
 * by their far endpoints (so the spread lines never cross each other). One
 * edge per face keeps the center; opposite-direction pairs between the same
 * two nodes naturally land side by side (both faces sort the pair with the
 * same tie-break, keeping the two lines parallel). Self-loops keep their
 * dedicated off-axis face routing (bottom in LR/RL, right in TB/BT).
 */
function anchorShifts(edges: EdgeModel[], prep: Array<EdgePrep | null>): Array<{ from: number; to: number }> {
  const shifts = edges.map(() => ({ from: 0, to: 0 }))
  const groups = new Map<string, Array<{ idx: number; end: 'from' | 'to' }>>()
  const add = (nodeId: string, side: Side, idx: number, end: 'from' | 'to') => {
    const key = `${nodeId} ${side}`
    let g = groups.get(key)
    if (!g) groups.set(key, (g = []))
    g.push({ idx, end })
  }

  // Bucket by the RESOLVED face (from sidesFor): a diverted feedback edge is
  // spread on its loop face, never on the forward chain's face.
  edges.forEach((e, i) => {
    if (e.from === e.to) return
    const p = prep[i]
    if (!p) return
    add(e.from, p.fromSide, i, 'from')
    add(e.to, p.toSide, i, 'to')
  })

  const faceRect = (r: { idx: number; end: 'from' | 'to' }): Rect => {
    const p = prep[r.idx]!
    return r.end === 'from' ? p.from : p.to
  }
  const farRect = (r: { idx: number; end: 'from' | 'to' }): Rect => {
    const p = prep[r.idx]!
    return r.end === 'from' ? p.to : p.from
  }

  for (const [key, refs] of groups) {
    if (refs.length < 2) continue
    const sep = key.lastIndexOf(' ')
    const side = key.slice(sep + 1) as Side
    const rect = faceRect(refs[0])
    const alongY = side === 'left' || side === 'right'
    const faceLen = alongY ? rect.h : rect.w
    // Fill at most 70% of the face; cap the pitch at 20px so a two-edge face
    // reads as two clearly separate lines without hugging the corners.
    const step = Math.min(20, (faceLen * 0.7) / (refs.length - 1))
    const farCenter = (r: { idx: number; end: 'from' | 'to' }): number => {
      const other = farRect(r)
      return alongY ? other.y + other.h / 2 : other.x + other.w / 2
    }
    refs.sort((r1, r2) => {
      const d = farCenter(r1) - farCenter(r2)
      if (Math.abs(d) > 0.5) return d
      // Same far endpoint (an A<->B pair): a direction-stable tie-break keeps
      // the two lines parallel - both faces sort the pair identically.
      return edges[r1.idx].id < edges[r2.idx].id ? -1 : 1
    })
    refs.forEach((r, i) => {
      const off = (i - (refs.length - 1) / 2) * step
      if (r.end === 'from') shifts[r.idx].from = off
      else shifts[r.idx].to = off
    })
  }
  return shifts
}
