import type { EdgeCurve, Side } from '../model/schema'
import type { Point, Rect } from '../layout/types'
import { roundedPath } from './step-round'

export interface RouteRequest {
  from: Rect
  to: Rect
  fromSide?: Side
  toSide?: Side
  curve: EdgeCurve
  /** Node rects to route around (endpoints excluded by the caller). */
  obstacles: Rect[]
  radius: number
  /** True for LR/RL layouts; biases auto-anchoring along the primary axis. */
  primaryHorizontal?: boolean
  /** Canvas extent; detour lanes are clamped inside it so edges never escape. */
  bounds?: { width: number; height: number }
  /**
   * Perpendicular anchor shift (px) for A⇄B edge pairs: each direction gets an
   * opposite sign so the two lines run side by side instead of overlapping.
   */
  pairOffset?: number
}

export interface RoutedPath {
  d: string
  points: Point[]
}

const CHANNEL_OFFSET = 18
const LANE_PAD = 22
/** Keep detour lanes at least this far inside the canvas edge. */
const LANE_MARGIN = 6

const center = (r: Rect): Point => ({ x: r.x + r.w / 2, y: r.y + r.h / 2 })
const isVertical = (s: Side) => s === 'top' || s === 'bottom'

function anchor(rect: Rect, side: Side): Point {
  const c = center(rect)
  switch (side) {
    case 'top':
      return { x: c.x, y: rect.y }
    case 'bottom':
      return { x: c.x, y: rect.y + rect.h }
    case 'left':
      return { x: rect.x, y: c.y }
    case 'right':
      return { x: rect.x + rect.w, y: c.y }
  }
}

const SAME_RANK_EPS = 6

/**
 * Choose exit/entry sides. In a layered layout, cross-rank edges must travel
 * along the primary axis (e.g. bottom→top for TB) even when a wide fan-out
 * makes the edge more horizontal than vertical — otherwise they'd cut sideways
 * through intervening nodes. Same-rank edges (no primary separation) route
 * across the secondary axis.
 */
function autoSides(from: Rect, to: Rect, primaryHorizontal: boolean): { fromSide: Side; toSide: Side } {
  const f = center(from)
  const t = center(to)
  const dx = t.x - f.x
  const dy = t.y - f.y
  if (primaryHorizontal) {
    if (Math.abs(dx) > SAME_RANK_EPS) {
      return dx >= 0 ? { fromSide: 'right', toSide: 'left' } : { fromSide: 'left', toSide: 'right' }
    }
    return dy >= 0 ? { fromSide: 'bottom', toSide: 'top' } : { fromSide: 'top', toSide: 'bottom' }
  }
  if (Math.abs(dy) > SAME_RANK_EPS) {
    return dy >= 0 ? { fromSide: 'bottom', toSide: 'top' } : { fromSide: 'top', toSide: 'bottom' }
  }
  return dx >= 0 ? { fromSide: 'right', toSide: 'left' } : { fromSide: 'left', toSide: 'right' }
}

/** Axis-aligned segment vs rect, with a small inset so edge-touching doesn't count. */
function segHitsRect(a: Point, b: Point, rect: Rect, inset = 3): boolean {
  const x1 = rect.x + inset
  const y1 = rect.y + inset
  const x2 = rect.x + rect.w - inset
  const y2 = rect.y + rect.h - inset
  if (x2 <= x1 || y2 <= y1) return false
  if (Math.abs(a.y - b.y) < 0.5) {
    const y = a.y
    if (y <= y1 || y >= y2) return false
    return Math.max(a.x, b.x) > x1 && Math.min(a.x, b.x) < x2
  }
  if (Math.abs(a.x - b.x) < 0.5) {
    const x = a.x
    if (x <= x1 || x >= x2) return false
    return Math.max(a.y, b.y) > y1 && Math.min(a.y, b.y) < y2
  }
  return Math.max(a.x, b.x) > x1 && Math.min(a.x, b.x) < x2 && Math.max(a.y, b.y) > y1 && Math.min(a.y, b.y) < y2
}

function polylineHits(points: Point[], obstacles: Rect[]): boolean {
  for (let i = 0; i < points.length - 1; i++) {
    for (const o of obstacles) if (segHitsRect(points[i], points[i + 1], o)) return true
  }
  return false
}

/**
 * Clamp a detour lane so it stays inside the canvas. A lane chosen at
 * `obstacle.bbox ± LANE_PAD` can fall outside the canvas (e.g. above the top
 * row, at a negative coordinate) — the overlay is `overflow: visible`, so an
 * un-clamped lane paints over the title/subtitle. We floor at `LANE_MARGIN` and,
 * when the canvas extent is known, cap at `extent − LANE_MARGIN`.
 */
function clampLane(value: number, extent?: number): number {
  const lo = LANE_MARGIN
  const hi = extent != null ? Math.max(lo, extent - LANE_MARGIN) : Infinity
  return Math.min(Math.max(value, lo), hi)
}

/** Route around obstacles via a clear side lane (used when the direct route is blocked). */
function laneDetour(
  a: Point,
  b: Point,
  vertical: boolean,
  obstacles: Rect[],
  bounds?: { width: number; height: number },
): Point[] | null {
  if (obstacles.length === 0) return null
  if (vertical) {
    const dirY = Math.sign(b.y - a.y) || 1
    const ch1 = a.y + dirY * CHANNEL_OFFSET
    const ch2 = b.y - dirY * CHANNEL_OFFSET
    const minX = Math.min(...obstacles.map((o) => o.x))
    const maxX = Math.max(...obstacles.map((o) => o.x + o.w))
    const left = clampLane(minX - LANE_PAD, bounds?.width)
    const right = clampLane(maxX + LANE_PAD, bounds?.width)
    const preferLeft = (a.x + b.x) / 2 <= (minX + maxX) / 2
    for (const laneX of preferLeft ? [left, right] : [right, left]) {
      const poly = [a, { x: a.x, y: ch1 }, { x: laneX, y: ch1 }, { x: laneX, y: ch2 }, { x: b.x, y: ch2 }, b]
      if (!polylineHits(poly, obstacles)) return poly
    }
  } else {
    const dirX = Math.sign(b.x - a.x) || 1
    const ch1 = a.x + dirX * CHANNEL_OFFSET
    const ch2 = b.x - dirX * CHANNEL_OFFSET
    const minY = Math.min(...obstacles.map((o) => o.y))
    const maxY = Math.max(...obstacles.map((o) => o.y + o.h))
    const top = clampLane(minY - LANE_PAD, bounds?.height)
    const bottom = clampLane(maxY + LANE_PAD, bounds?.height)
    const preferTop = (a.y + b.y) / 2 <= (minY + maxY) / 2
    for (const laneY of preferTop ? [top, bottom] : [bottom, top]) {
      const poly = [a, { x: ch1, y: a.y }, { x: ch1, y: laneY }, { x: ch2, y: laneY }, { x: ch2, y: b.y }, b]
      if (!polylineHits(poly, obstacles)) return poly
    }
  }
  return null
}

/** Shift an anchor along the node face it sits on (perpendicular to travel). */
function shiftAnchor(p: Point, side: Side, off: number): Point {
  if (!off) return p
  return isVertical(side) ? { x: p.x + off, y: p.y } : { x: p.x, y: p.y + off }
}

function orthogonalPolyline(
  from: Rect,
  to: Rect,
  fromSide: Side,
  toSide: Side,
  obstacles: Rect[],
  bounds?: { width: number; height: number },
  pairOffset = 0,
): Point[] {
  const a = shiftAnchor(anchor(from, fromSide), fromSide, pairOffset)
  const b = shiftAnchor(anchor(to, toSide), toSide, pairOffset)
  const vert = isVertical(fromSide) && isVertical(toSide)
  const horz = !isVertical(fromSide) && !isVertical(toSide)

  if (vert) {
    const channelY = (a.y + b.y) / 2
    const simple = [a, { x: a.x, y: channelY }, { x: b.x, y: channelY }, b]
    if (!polylineHits(simple, obstacles)) return simple
    return laneDetour(a, b, true, obstacles, bounds) ?? simple
  }
  if (horz) {
    const channelX = (a.x + b.x) / 2
    const simple = [a, { x: channelX, y: a.y }, { x: channelX, y: b.y }, b]
    if (!polylineHits(simple, obstacles)) return simple
    return laneDetour(a, b, false, obstacles, bounds) ?? simple
  }
  // mixed sides → single elbow
  const corner = isVertical(fromSide) ? { x: a.x, y: b.y } : { x: b.x, y: a.y }
  return [a, corner, b]
}

function sCurve(a: Point, b: Point, fromSide: Side): string {
  const dx = b.x - a.x
  const dy = b.y - a.y
  if (isVertical(fromSide)) {
    const off = dy * 0.4
    return `M ${a.x} ${a.y} C ${a.x} ${a.y + off}, ${b.x} ${b.y - off}, ${b.x} ${b.y}`
  }
  const off = dx * 0.4
  return `M ${a.x} ${a.y} C ${a.x + off} ${a.y}, ${b.x - off} ${b.y}, ${b.x} ${b.y}`
}

/** Route one edge between two rects, returning the path data + turn points. */
export function routeEdge(req: RouteRequest): RoutedPath {
  // Self-loop (a state's transition to itself): a small rounded detour off the
  // right side of the rect, entering back a little lower. One continuous path.
  const self =
    req.from === req.to ||
    (req.from.x === req.to.x && req.from.y === req.to.y && req.from.w === req.to.w && req.from.h === req.to.h)
  if (self) {
    const r = req.from
    const x = r.x + r.w
    const y1 = r.y + r.h * 0.3
    const y2 = r.y + r.h * 0.7
    const poly = [
      { x, y: y1 },
      { x: x + 30, y: y1 },
      { x: x + 30, y: y2 },
      { x, y: y2 },
    ]
    return { d: roundedPath(poly, Math.min(req.radius, 10)), points: poly }
  }

  const auto = autoSides(req.from, req.to, req.primaryHorizontal ?? false)
  const fromSide = req.fromSide ?? auto.fromSide
  const toSide = req.toSide ?? auto.toSide
  const off = req.pairOffset ?? 0
  const a = shiftAnchor(anchor(req.from, fromSide), fromSide, off)
  const b = shiftAnchor(anchor(req.to, toSide), toSide, off)

  if (req.curve === 'straight') {
    return { d: `M ${a.x} ${a.y} L ${b.x} ${b.y}`, points: [a, b] }
  }
  if (req.curve === 's') {
    return { d: sCurve(a, b, fromSide), points: [a, b] }
  }
  const poly = orthogonalPolyline(req.from, req.to, fromSide, toSide, req.obstacles, req.bounds, off)
  return { d: roundedPath(poly, req.radius), points: poly }
}
