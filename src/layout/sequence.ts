import type { DiagramModel, EdgeModel } from '../model/schema'
import type { LayoutResult, Rect, SizeMap } from './types'

const FALLBACK = { w: 160, h: 56 }
const CANVAS_PAD = 16
/** Extra air between the participant cards and the first message row (the row's
 *  own LABEL_ROOM is added on top of this). */
const HEAD_GAP = 20
/** Headroom above each message line for its pill label. */
const LABEL_ROOM = 40
/** How far a message row's ink (line + arrowhead) extends below its y. */
const ROW_TAIL = 4
/** Extra height a self-message loop needs (the loop's drop below its row.y). */
export const SELF_H = 22
/** Band top border → the band's first message row. */
const BAND_TOP_PAD = 6
/** Last row's ink → the band's bottom border. */
const BAND_BOTTOM_PAD = 16
/** Air between a band border and whatever precedes/follows it. */
const BAND_GAP = 20
/** Bands span the canvas minus this inset per side. */
const BAND_INSET = 20
/** Lifeline tail below the last row (the fade-out runs through it). */
const TAIL = 40
/** Half-width of an activation bar; nested bars step out by LEVEL_STEP. */
export const BAR_HALF = 5
export const LEVEL_STEP = 4
/** How far a self-message loops out from the lifeline. */
export const SELF_LOOP = 32

/** Estimated rendered width of a message label pill (labels aren't measured —
 *  the SVG doesn't exist yet at layout time). Slightly generous on purpose. */
const labelEst = (label?: string) => (label ? label.length * 6.8 + 40 : 0)

export interface MessageRow {
  /** Index into model.edges (message order). */
  index: number
  /** The y of the message line. */
  y: number
  self: boolean
}

export interface SectionBand {
  label: string
  /** CSS color value tinting the band + its label pill. */
  accent: string
  x: number
  y: number
  w: number
  h: number
}

export interface ActivationBar {
  participant: string
  /** Nesting depth (0 = directly on the lifeline). */
  level: number
  y1: number
  y2: number
  accent: string
  /** Edge ids of the messages that open/close this bar (for the choreography). */
  startEdge: string
  endEdge: string
}

export interface SequenceLayout extends LayoutResult {
  /** participant id → lifeline x (the column center). */
  centers: Map<string, number>
  rows: MessageRow[]
  bands: SectionBand[]
  activations: ActivationBar[]
  /** Lifelines run from each card's bottom to this y. */
  lifelineBottom: number
}

/**
 * Fixed-grid sequence layout: participants are columns (in declared order),
 * messages are rows (in authored order). No ranking or crossing minimization —
 * the author's order IS the layout. Column pitch stretches to fit the widest
 * label between adjacent lifelines; activation bars come from request/reply
 * pairing (see `computeActivations`); `- section:` marks open a band that runs
 * until the next mark (or the last message).
 */
export function sequenceLayout(model: DiagramModel, sizes: SizeMap): SequenceLayout {
  const parts = model.nodes
  const size = (id: string) => sizes.get(id) ?? FALLBACK
  const gap = Math.max(48, model.meta.spacing.node)

  // ---- columns ----
  const col = new Map<string, number>() // id → column index
  parts.forEach((p, i) => col.set(p.id, i))

  // Between adjacent columns, reserve room for the widest label that lives
  // entirely in that gap (messages spanning further get room for free), plus
  // self-loop overhang from the left column.
  const gapNeed: number[] = Array.from({ length: Math.max(0, parts.length - 1) }, () => gap)
  for (const e of model.edges) {
    const a = col.get(e.from)!
    const b = col.get(e.to)!
    if (e.from === e.to) {
      const need = SELF_LOOP + 10 + labelEst(e.label)
      if (a < gapNeed.length) gapNeed[a] = Math.max(gapNeed[a], need)
      continue
    }
    if (Math.abs(a - b) === 1) {
      const g = Math.min(a, b)
      gapNeed[g] = Math.max(gapNeed[g], labelEst(e.label) + 18)
    }
  }

  const centers = new Map<string, number>()
  let x = CANVAS_PAD
  parts.forEach((p, i) => {
    const w = size(p.id).w
    if (i === 0) x += w / 2
    else x += size(parts[i - 1].id).w / 2 + gapNeed[i - 1] + w / 2
    centers.set(p.id, x)
  })

  // ---- rows + bands (messages and section marks, in authored order) ----
  const maxCardH = Math.max(...parts.map((p) => size(p.id).h), FALLBACK.h)
  const rows: MessageRow[] = []
  const bands: SectionBand[] = []
  // `bottom` tracks the lowest ink so far; each row line sits LABEL_ROOM below it.
  let bottom = CANVAS_PAD + maxCardH + HEAD_GAP
  let open: { label: string; accent: string; top: number } | null = null
  const closeBand = () => {
    if (!open) return
    bottom += BAND_BOTTOM_PAD
    bands.push({ label: open.label, accent: open.accent, x: 0, y: open.top, w: 0, h: bottom - open.top })
    open = null
  }
  model.edges.forEach((e, i) => {
    for (const s of model.sections) {
      if (s.at === i) {
        closeBand()
        open = { label: s.label, accent: s.accent, top: bottom + BAND_GAP }
        bottom = open.top + BAND_TOP_PAD
      }
    }
    const self = e.from === e.to
    const y = bottom + LABEL_ROOM
    rows.push({ index: i, y, self })
    bottom = y + (self ? SELF_H : ROW_TAIL)
  })
  // Trailing sections (at === edges.length) still render as a slim empty band.
  for (const s of model.sections) {
    if (s.at >= model.edges.length) {
      closeBand()
      open = { label: s.label, accent: s.accent, top: bottom + BAND_GAP }
      bottom = open.top + LABEL_ROOM
    }
  }
  closeBand()
  const lifelineBottom = bottom + TAIL

  // ---- activations ----
  const accentOf = new Map(parts.map((p) => [p.id, p.accent]))
  const activations = computeActivations(model.edges, rows, accentOf)

  // ---- assemble ----
  const nodes = new Map<string, Rect>()
  for (const p of parts) {
    const s = size(p.id)
    nodes.set(p.id, { x: centers.get(p.id)! - s.w / 2, y: CANVAS_PAD, w: s.w, h: s.h })
  }

  let width = 0
  for (const p of parts) {
    const r = nodes.get(p.id)!
    width = Math.max(width, r.x + r.w)
  }
  // Self-loops / labels overhanging the last column extend the canvas.
  const last = parts[parts.length - 1]
  for (const e of model.edges) {
    if (e.from === e.to && e.from === last.id)
      width = Math.max(width, centers.get(last.id)! + SELF_LOOP + 10 + labelEst(e.label))
  }
  width = Math.ceil(width + CANVAS_PAD)
  const height = Math.ceil(lifelineBottom + CANVAS_PAD)

  // Bands span the canvas now that the width is known.
  for (const b of bands) {
    b.x = BAND_INSET
    b.w = width - BAND_INSET * 2
  }

  return {
    nodes,
    groups: new Map(),
    width,
    height,
    centers,
    rows,
    bands,
    activations,
    lifelineBottom,
  }
}

/**
 * Request/reply pairing: a non-reply message activates its receiver when a
 * later, not-yet-claimed `reply: true` from that receiver back to the sender
 * closes it. An explicit `activate: true` starts a bar even without a matching
 * reply (it runs to the receiver's last involvement); `activate: false`
 * suppresses one. Nested bars step outward so re-entrancy stays readable.
 */
function computeActivations(
  edges: EdgeModel[],
  rows: MessageRow[],
  accentOf: Map<string, string>,
): ActivationBar[] {
  const bars: ActivationBar[] = []
  const claimed = new Set<number>()
  const lastTouch = new Map<string, number>()
  edges.forEach((e, i) => {
    lastTouch.set(e.from, i)
    lastTouch.set(e.to, i)
  })

  const openBars: Array<{ participant: string; start: number; end: number; level: number }> = []

  edges.forEach((e, i) => {
    if (e.reply || e.from === e.to) return
    if (e.activate === false) return
    // Find the first unclaimed reply to→from after i.
    let end = -1
    for (let j = i + 1; j < edges.length; j++) {
      if (claimed.has(j)) continue
      const r = edges[j]
      if (r.reply && r.from === e.to && r.to === e.from) {
        end = j
        claimed.add(j)
        break
      }
    }
    if (end === -1) {
      if (e.activate !== true) return
      end = lastTouch.get(e.to) ?? i
    }
    // Level = how many already-open bars on this participant cover row i.
    const level = openBars.filter((b) => b.participant === e.to && b.start <= i && i <= b.end).length
    openBars.push({ participant: e.to, start: i, end, level })
  })

  const rowY = (i: number) => rows[i]?.y ?? 0
  for (const b of openBars) {
    bars.push({
      participant: b.participant,
      level: b.level,
      y1: rowY(b.start) - 8,
      y2: rowY(b.end) + 8,
      accent: accentOf.get(b.participant) ?? 'var(--beck-neutral)',
      startEdge: edges[b.start].id,
      endEdge: edges[b.end].id,
    })
  }
  return bars
}

/** The bar-edge x offset for a message anchor on `participant` at row `row`,
 *  or 0 when no bar covers that row. Positive = the bar's half-width. */
export function activationOffset(bars: ActivationBar[], participant: string, y: number): number {
  let depth = 0
  for (const b of bars) {
    if (b.participant === participant && b.y1 <= y && y <= b.y2) depth = Math.max(depth, b.level + 1)
  }
  return depth === 0 ? 0 : BAR_HALF + (depth - 1) * LEVEL_STEP
}
