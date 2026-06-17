import type { Point } from '../layout/types'

const dist = (a: Point, b: Point) => Math.hypot(b.x - a.x, b.y - a.y)

function toward(from: Point, to: Point, d: number): Point {
  const len = dist(from, to) || 1
  return { x: from.x + ((to.x - from.x) / len) * d, y: from.y + ((to.y - from.y) / len) * d }
}

function collinear(a: Point, b: Point, c: Point): boolean {
  return Math.abs((b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x)) < 0.01
}

/**
 * Build an SVG path through a polyline, rounding each interior corner with a
 * quarter-circle (quadratic) arc. Radius is clamped to half of each adjacent
 * segment so tight turns stay clean. Generalizes the hand-tuned `step-round`
 * path from the source toolkit to an arbitrary turn-point list.
 */
export function roundedPath(points: Point[], radius: number): string {
  const pts = dedupe(points)
  if (pts.length < 2) return ''
  if (pts.length === 2) return `M ${r(pts[0].x)} ${r(pts[0].y)} L ${r(pts[1].x)} ${r(pts[1].y)}`

  let d = `M ${r(pts[0].x)} ${r(pts[0].y)}`
  for (let i = 1; i < pts.length - 1; i++) {
    const prev = pts[i - 1]
    const cur = pts[i]
    const next = pts[i + 1]
    if (collinear(prev, cur, next)) {
      d += ` L ${r(cur.x)} ${r(cur.y)}`
      continue
    }
    const rad = Math.min(radius, dist(prev, cur) / 2, dist(cur, next) / 2)
    const a = toward(cur, prev, rad)
    const b = toward(cur, next, rad)
    d += ` L ${r(a.x)} ${r(a.y)} Q ${r(cur.x)} ${r(cur.y)} ${r(b.x)} ${r(b.y)}`
  }
  const last = pts[pts.length - 1]
  d += ` L ${r(last.x)} ${r(last.y)}`
  return d
}

function dedupe(points: Point[]): Point[] {
  const out: Point[] = []
  for (const p of points) {
    const prev = out[out.length - 1]
    if (!prev || Math.abs(prev.x - p.x) > 0.5 || Math.abs(prev.y - p.y) > 0.5) out.push(p)
  }
  return out
}

const r = (n: number) => Math.round(n * 100) / 100

export { dist }
