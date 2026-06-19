import type { DiagramModel, EdgeModel } from '../model/schema'
import type { LayoutResult, Point, Rect } from '../layout/types'
import { routeEdge } from './orthogonal'

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

/** Create (or reuse) an arrowhead marker tinted to a color value. */
export function ensureMarker(svg: SVGSVGElement, color: string): string {
  let cache = markerCache.get(svg)
  if (!cache) {
    cache = new Map()
    markerCache.set(svg, cache)
  }
  const hit = cache.get(color)
  if (hit) return hit
  const id = `beck-arrow-${markerSeq++}`
  const marker = document.createElementNS(SVGNS, 'marker')
  marker.setAttribute('id', id)
  marker.setAttribute('viewBox', '0 0 10 10')
  marker.setAttribute('refX', '8')
  marker.setAttribute('refY', '5')
  marker.setAttribute('markerWidth', '6')
  marker.setAttribute('markerHeight', '6')
  marker.setAttribute('orient', 'auto-start-reverse')
  const poly = document.createElementNS(SVGNS, 'polygon')
  poly.setAttribute('points', '0,1 10,5 0,9')
  poly.setAttribute('fill', color)
  marker.appendChild(poly)
  svg.querySelector('defs')!.appendChild(marker)
  cache.set(color, id)
  return id
}

function drawEdge(svg: SVGSVGElement, d: string, edge: EdgeModel): SVGPathElement {
  const path = document.createElementNS(SVGNS, 'path')
  path.setAttribute('d', d)
  path.setAttribute('fill', 'none')
  path.setAttribute('stroke', edge.color)
  path.setAttribute('stroke-width', '1.6')
  path.setAttribute('stroke-linecap', 'round')
  path.setAttribute('stroke-linejoin', 'round')
  if (edge.style === 'dashed') path.setAttribute('stroke-dasharray', '7 5')
  // The marker uses orient="auto-start-reverse", so the same def points the
  // right way at either end.
  if (edge.arrow === 'end' || edge.arrow === 'both')
    path.setAttribute('marker-end', `url(#${ensureMarker(svg, edge.color)})`)
  if (edge.arrow === 'start' || edge.arrow === 'both')
    path.setAttribute('marker-start', `url(#${ensureMarker(svg, edge.color)})`)
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

const LABEL_GAP = 6 // clear space between the label box and the edge line
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

function clearance(box: LabelBox, obstacles: Rect[]): number {
  let min = Infinity
  for (const o of obstacles) min = Math.min(min, boxGap(box, o))
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
  const consider = (cx0: number, cy0: number, anchor: LabelAnchor) => {
    const c = clampCenter(cx0, cy0)
    const box: LabelBox = { cx: c.cx, cy: c.cy, hw, hh, anchor }
    const clear = clearance(box, obstacles)
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
        consider(px - LABEL_GAP - hw, py, 'end')
        consider(px + LABEL_GAP + hw, py, 'start')
      } else {
        // horizontal-ish: above or below the line
        consider(px, py - LABEL_GAP - hh, 'middle')
        consider(px, py + LABEL_GAP + hh, 'middle')
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

  // Avoid both node cards and already-placed labels, so labels never stack.
  const all = placed.length ? obstacles.concat(placed) : obstacles
  const box = chooseLabelBox(points, hw, hh, all, bounds, polylineMidpoint(points))
  // Derive the text x from the box + anchor (dominant-baseline centers it on cy).
  const tx = box.anchor === 'start' ? box.cx - w / 2 : box.anchor === 'end' ? box.cx + w / 2 : box.cx
  t.setAttribute('x', String(Math.round(tx)))
  t.setAttribute('y', String(Math.round(box.cy)))
  t.setAttribute('text-anchor', box.anchor)
  t.setAttribute('dominant-baseline', 'central')
  placed.push({ x: box.cx - box.hw, y: box.cy - box.hh, w: box.hw * 2, h: box.hh * 2 })
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

  const out: RoutedEdge[] = []
  for (const edge of model.edges) {
    const from = rectOf(edge.from)
    const to = rectOf(edge.to)
    if (!from || !to) continue
    const exclude = new Set<Rect>([from, to, ...memberRects(edge.from), ...memberRects(edge.to)])
    const obstacles = [...layout.nodes.values()].filter((r) => !exclude.has(r))
    const { d, points } = routeEdge({
      from,
      to,
      fromSide: edge.fromSide,
      toSide: edge.toSide,
      curve: edge.curve,
      obstacles,
      radius,
      primaryHorizontal,
      bounds: { width: layout.width, height: layout.height },
    })
    const path = drawEdge(svg, d, edge)
    if (edge.label) drawLabel(svg, points, edge.label, labelObstacles, labelBounds, placedLabels)
    out.push({ edge, path })
  }
  return out
}
