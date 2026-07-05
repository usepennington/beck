import type { DiagramModel } from '../model/schema'
import {
  BAR_HALF,
  LEVEL_STEP,
  SELF_H,
  SELF_LOOP,
  activationOffset,
  type SequenceLayout,
} from '../layout/sequence'
import { roundedPath } from './step-round'
import { drawEdge, type RoutedEdge } from './svg'

const SVGNS = 'http://www.w3.org/2000/svg'
let gradientSeq = 0

function el<K extends keyof SVGElementTagNameMap>(tag: K, attrs: Record<string, string> = {}): SVGElementTagNameMap[K] {
  const node = document.createElementNS(SVGNS, tag)
  for (const [k, v] of Object.entries(attrs)) node.setAttribute(k, v)
  return node
}

/** Measure a connected SVG text element, with an estimate fallback for
 *  display:none subtrees (same contract as the edge-label measure in svg.ts). */
function measureText(t: SVGTextElement): { w: number; h: number } {
  let w = 0
  let h = 0
  try {
    const bb = t.getBBox()
    w = bb.width
    h = bb.height
  } catch {
    /* estimated below */
  }
  if (!w || !Number.isFinite(w)) w = (t.textContent?.length ?? 0) * 6.6
  if (!h || !Number.isFinite(h)) h = 12
  return { w, h }
}

/** A rounded-full chip behind a text element, centered at (cx, cy). Returns the
 *  chip so the caller can class it; the text is (re)positioned here. */
function chipBehind(parent: SVGGElement, t: SVGTextElement, cx: number, cy: number, padX: number, padY: number): SVGRectElement {
  const { w, h } = measureText(t)
  const bw = w + padX * 2
  const bh = h + padY * 2
  const chip = el('rect', {
    x: String(Math.round(cx - bw / 2)),
    y: String(Math.round(cy - bh / 2)),
    width: String(Math.round(bw)),
    height: String(Math.round(bh)),
    rx: String(bh / 2),
  })
  parent.insertBefore(chip, t)
  t.setAttribute('x', String(Math.round(cx)))
  t.setAttribute('y', String(Math.round(cy)))
  t.setAttribute('text-anchor', 'middle')
  t.setAttribute('dominant-baseline', 'central')
  return chip
}

/**
 * A vertical fade-to-transparent gradient for the lifelines. userSpaceOnUse so
 * one def serves every (zero-width) line; stop-color rides the CSS var so the
 * theme keeps owning the color.
 */
function lifelineStroke(svg: SVGSVGElement, y1: number, y2: number): string {
  const id = `beck-fade-${gradientSeq++}`
  const grad = el('linearGradient', {
    id,
    gradientUnits: 'userSpaceOnUse',
    x1: '0',
    y1: String(y1),
    x2: '0',
    y2: String(y2),
  })
  for (const [offset, opacity] of [
    ['0', '1'],
    ['0.8', '1'],
    ['1', '0'],
  ] as const) {
    const stop = el('stop', { offset, 'stop-opacity': opacity })
    stop.style.stopColor = 'var(--beck-edge)'
    grad.appendChild(stop)
  }
  svg.querySelector('defs')!.appendChild(grad)
  return `url(#${id})`
}

/** Per-accent vertical gradient for activation bars (bright at the call site,
 *  fading down the bar). The accent var is set on the gradient element itself so
 *  the stops inherit it. */
function activationFill(svg: SVGSVGElement, cache: Map<string, string>, accent: string): string {
  const hit = cache.get(accent)
  if (hit) return hit
  const id = `beck-act-${gradientSeq++}`
  const grad = el('linearGradient', { id, x1: '0', y1: '0', x2: '0', y2: '1' })
  grad.style.setProperty('--beck-accent', accent)
  for (const [offset, opacity] of [
    ['0', '0.95'],
    ['1', '0.35'],
  ] as const) {
    const stop = el('stop', { offset, 'stop-opacity': opacity })
    stop.style.stopColor = 'var(--beck-accent)'
    grad.appendChild(stop)
  }
  svg.querySelector('defs')!.appendChild(grad)
  const url = `url(#${id})`
  cache.set(accent, url)
  return url
}

/**
 * Draw the sequence scenery (section bands, lifelines, activation bars) and the
 * messages. Every message is one `<g class="beck-msg">` — its single continuous
 * `SVGPathElement` (straight line, or a rounded loop for self-messages) plus a
 * pill label — so the shared packet/trail animation rides the path unchanged
 * and the choreography can dim/reveal the whole row.
 */
export function routeSequenceEdges(
  svg: SVGSVGElement,
  model: DiagramModel,
  layout: SequenceLayout,
): RoutedEdge[] {
  // ---- section bands (behind everything) ----
  layout.bands.forEach((b, i) => {
    const g = el('g')
    g.classList.add('beck-band')
    g.dataset.band = String(i)
    g.style.setProperty('--beck-accent', b.accent)
    const box = el('rect', {
      x: String(b.x),
      y: String(b.y),
      width: String(b.w),
      height: String(b.h),
      rx: '14',
    })
    box.classList.add('beck-band-box')
    g.appendChild(box)
    const t = el('text')
    t.classList.add('beck-band-label')
    t.textContent = b.label.toUpperCase()
    g.appendChild(t)
    svg.appendChild(g)
    // The label pill floats on the band's top border, inset from the left.
    const { w } = measureText(t)
    const chip = chipBehind(g, t, b.x + 24 + w / 2, b.y, 10, 4)
    chip.classList.add('beck-band-chip')
  })

  // ---- lifelines ----
  const cardBottom = Math.min(...model.nodes.map((p) => layout.nodes.get(p.id)!).map((r) => r.y + r.h))
  const stroke = lifelineStroke(svg, cardBottom, layout.lifelineBottom)
  for (const p of model.nodes) {
    const cx = layout.centers.get(p.id)!
    const card = layout.nodes.get(p.id)!
    const line = el('line', {
      x1: String(cx),
      y1: String(card.y + card.h + 2),
      x2: String(cx),
      y2: String(layout.lifelineBottom),
    })
    line.classList.add('beck-lifeline')
    line.style.stroke = stroke
    svg.appendChild(line)
  }

  // ---- activation bars ----
  const actFills = new Map<string, string>()
  for (const b of layout.activations) {
    const cx = layout.centers.get(b.participant)!
    const x = cx - BAR_HALF + b.level * LEVEL_STEP
    const rect = el('rect', {
      x: String(x),
      y: String(b.y1),
      width: String(BAR_HALF * 2),
      height: String(Math.max(BAR_HALF * 2, b.y2 - b.y1)),
      rx: String(BAR_HALF),
    })
    rect.classList.add('beck-activation')
    rect.setAttribute('fill', activationFill(svg, actFills, b.accent))
    rect.style.setProperty('--beck-accent', b.accent)
    rect.dataset.start = b.startEdge
    rect.dataset.end = b.endEdge
    svg.appendChild(rect)
  }

  // ---- messages ----
  const out: RoutedEdge[] = []
  for (const row of layout.rows) {
    const edge = model.edges[row.index]
    const cxFrom = layout.centers.get(edge.from)!
    const cxTo = layout.centers.get(edge.to)!

    const g = el('g')
    g.classList.add('beck-msg')
    if (edge.reply) g.classList.add('beck-msg--reply')
    g.dataset.msg = edge.id
    g.style.setProperty('--beck-accent', edge.color)
    svg.appendChild(g)

    if (row.self) {
      const off = activationOffset(layout.activations, edge.from, row.y)
      const x = cxFrom + off
      const poly = [
        { x, y: row.y },
        { x: cxFrom + SELF_LOOP, y: row.y },
        { x: cxFrom + SELF_LOOP, y: row.y + SELF_H },
        { x, y: row.y + SELF_H },
      ]
      const path = drawEdge(svg, roundedPath(poly, 9), edge)
      path.setAttribute('stroke-width', '2')
      g.appendChild(path)
      if (edge.label) {
        const t = el('text', {
          x: String(cxFrom + SELF_LOOP + 12),
          y: String(row.y + SELF_H / 2),
          'text-anchor': 'start',
          'dominant-baseline': 'central',
        })
        t.classList.add('beck-msg-text', 'beck-msg-text--bare')
        t.textContent = edge.label
        g.appendChild(t)
      }
      out.push({ edge, path })
      continue
    }

    const dir = Math.sign(cxTo - cxFrom) || 1
    const x1 = cxFrom + dir * activationOffset(layout.activations, edge.from, row.y)
    const x2 = cxTo - dir * activationOffset(layout.activations, edge.to, row.y)
    const path = drawEdge(svg, `M ${x1} ${row.y} L ${x2} ${row.y}`, edge)
    path.setAttribute('stroke-width', '2')
    g.appendChild(path)
    if (edge.label) {
      const t = el('text')
      t.classList.add('beck-msg-text')
      t.textContent = edge.label
      g.appendChild(t)
      const chip = chipBehind(g, t, (x1 + x2) / 2, row.y - 17, 10, 4)
      chip.classList.add('beck-msg-chip')
    }
    out.push({ edge, path })
  }
  return out
}
