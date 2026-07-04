import type { DiagramModel } from '../model/schema'
import {
  BAR_HALF,
  LEVEL_STEP,
  SELF_LOOP,
  activationOffset,
  type SequenceLayout,
} from '../layout/sequence'
import { roundedPath } from './step-round'
import { drawEdge, type RoutedEdge } from './svg'

const SVGNS = 'http://www.w3.org/2000/svg'

function line(svg: SVGSVGElement, x1: number, y1: number, x2: number, y2: number, cls: string): void {
  const l = document.createElementNS(SVGNS, 'line')
  l.setAttribute('x1', String(x1))
  l.setAttribute('y1', String(y1))
  l.setAttribute('x2', String(x2))
  l.setAttribute('y2', String(y2))
  l.classList.add(cls)
  svg.appendChild(l)
}

function text(svg: SVGSVGElement, x: number, y: number, content: string, cls: string, anchor = 'middle'): void {
  const t = document.createElementNS(SVGNS, 'text')
  t.classList.add(cls)
  t.textContent = content
  t.setAttribute('x', String(Math.round(x)))
  t.setAttribute('y', String(Math.round(y)))
  t.setAttribute('text-anchor', anchor)
  t.setAttribute('dominant-baseline', 'central')
  svg.appendChild(t)
}

/**
 * Draw the sequence scenery (lifelines, activation bars, section bands) and the
 * message paths. Every message is one continuous `SVGPathElement` — a straight
 * horizontal line, or a small rounded loop for self-messages — so the shared
 * packet/trail animation rides them unchanged.
 */
export function routeSequenceEdges(
  svg: SVGSVGElement,
  model: DiagramModel,
  layout: SequenceLayout,
): RoutedEdge[] {
  // ---- lifelines (behind everything) ----
  for (const p of model.nodes) {
    const cx = layout.centers.get(p.id)!
    const card = layout.nodes.get(p.id)!
    line(svg, cx, card.y + card.h + 2, cx, layout.lifelineBottom, 'beck-lifeline')
  }

  // ---- activation bars ----
  for (const b of layout.activations) {
    const cx = layout.centers.get(b.participant)!
    const x = cx - BAR_HALF + b.level * LEVEL_STEP
    const rect = document.createElementNS(SVGNS, 'rect')
    rect.setAttribute('x', String(x))
    rect.setAttribute('y', String(b.y1))
    rect.setAttribute('width', String(BAR_HALF * 2))
    rect.setAttribute('height', String(Math.max(6, b.y2 - b.y1)))
    rect.setAttribute('rx', '2')
    rect.classList.add('beck-activation')
    rect.style.setProperty('--beck-accent', b.accent)
    svg.appendChild(rect)
  }

  // ---- section bands ----
  for (const s of layout.sectionRows) {
    line(svg, 8, s.y, layout.width - 8, s.y, 'beck-seq-rule')
    text(svg, layout.width / 2, s.y, s.label, 'beck-seq-section')
  }

  // ---- messages ----
  const out: RoutedEdge[] = []
  for (const row of layout.rows) {
    const edge = model.edges[row.index]
    const cxFrom = layout.centers.get(edge.from)!
    const cxTo = layout.centers.get(edge.to)!

    if (row.self) {
      const off = activationOffset(layout.activations, edge.from, row.y)
      const x = cxFrom + off
      const poly = [
        { x, y: row.y },
        { x: cxFrom + SELF_LOOP, y: row.y },
        { x: cxFrom + SELF_LOOP, y: row.y + 16 },
        { x, y: row.y + 16 },
      ]
      const path = drawEdge(svg, roundedPath(poly, 8), edge)
      if (edge.label) text(svg, cxFrom + SELF_LOOP + 8, row.y + 8, edge.label, 'beck-edge-label', 'start')
      out.push({ edge, path })
      continue
    }

    const dir = Math.sign(cxTo - cxFrom) || 1
    const x1 = cxFrom + dir * activationOffset(layout.activations, edge.from, row.y)
    const x2 = cxTo - dir * activationOffset(layout.activations, edge.to, row.y)
    const path = drawEdge(svg, `M ${x1} ${row.y} L ${x2} ${row.y}`, edge)
    if (edge.label) text(svg, (x1 + x2) / 2, row.y - 9, edge.label, 'beck-edge-label')
    out.push({ edge, path })
  }
  return out
}
