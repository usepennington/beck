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

function drawLabel(svg: SVGSVGElement, points: Point[], text: string): void {
  const mid = polylineMidpoint(points)
  const t = document.createElementNS(SVGNS, 'text')
  t.setAttribute('x', String(Math.round(mid.x)))
  t.setAttribute('y', String(Math.round(mid.y) - 5))
  t.setAttribute('text-anchor', 'middle')
  t.classList.add('beck-edge-label')
  t.textContent = text
  svg.appendChild(t)
}

/** Route + draw every edge in the model. Returns the visible paths for animation. */
export function routeEdges(svg: SVGSVGElement, model: DiagramModel, layout: LayoutResult): RoutedEdge[] {
  const radius = model.meta.spacing.cornerRadius
  const primaryHorizontal = model.meta.direction === 'LR' || model.meta.direction === 'RL'
  const rectOf = (id: string): Rect | undefined => layout.nodes.get(id) ?? layout.groups.get(id)
  const memberRects = (id: string): Rect[] => {
    const g = model.groups.find((gr) => gr.id === id)
    return g ? (g.members.map((m) => layout.nodes.get(m)).filter((r): r is Rect => !!r)) : []
  }

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
    if (edge.label) drawLabel(svg, points, edge.label)
    out.push({ edge, path })
  }
  return out
}
