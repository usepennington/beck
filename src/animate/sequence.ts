import type { Timeline } from './runtime'
import type { DiagramModel } from '../model/schema'

/** Compile-time dim levels for the not-yet-told parts of the story. Lines sit
 *  faintest; labels stay readable; bands and bars are quiet scenery. */
const DIM = { line: 0.15, label: 0.35, act: 0.25, band: 0.45 }

/**
 * The sequence "storytelling" choreography: everything starts dimmed, and each
 * message row lights up as its packet fires — activation bars brighten while
 * their participant works, section bands as their phase begins — then the whole
 * story fades back down before a looping reset.
 *
 * Applies only to sequence models animating their DERIVED flow (the authored
 * message order). A hand-written `flow:` keeps today's full-opacity rendering —
 * it may visit rows in any order, so pre-dimming would strand scenery unlit.
 */
export interface SequenceChoreo {
  /** Everything dimmed at compile time — capture these in the reset snapshot. */
  dimmed: SVGElement[]
  /** Light up a message row as its packet departs; toggle activation bars keyed
   *  to this edge (brighten just before arrival, fade after the closing reply). */
  onPacket(edgeId: string, tl: Timeline, at: number, arrival: number): void
  /** Light up the next section band as its phase begins. */
  onPhase(tl: Timeline, at: number): void
  /** Fade the story back down inside the trailing wait, before the loop reset. */
  finale(tl: Timeline): void
}

export function setupSequenceChoreo(svg: SVGSVGElement, model: DiagramModel): SequenceChoreo | null {
  if (model.meta.type !== 'sequence' || !model.flow.derived) return null

  const dimmed: SVGElement[] = []
  const dim = (el: SVGElement, v: number) => {
    el.style.opacity = String(v)
    dimmed.push(el)
  }

  // Message rows: the line dims hardest, its label parts stay legible.
  const lineEls: SVGElement[] = []
  const labelEls: SVGElement[] = []
  const msgParts = new Map<string, SVGElement[]>()
  for (const gEl of svg.querySelectorAll<SVGGElement>('g.beck-msg')) {
    const id = gEl.dataset.msg
    if (!id) continue
    const parts: SVGElement[] = []
    for (const child of Array.from(gEl.children) as SVGElement[]) {
      const isLine = child.tagName === 'path'
      dim(child, isLine ? DIM.line : DIM.label)
      ;(isLine ? lineEls : labelEls).push(child)
      parts.push(child)
    }
    msgParts.set(id, parts)
  }

  const bandEls = Array.from(svg.querySelectorAll<SVGGElement>('g.beck-band'))
  for (const b of bandEls) dim(b, DIM.band)

  const barEls = Array.from(svg.querySelectorAll<SVGRectElement>('rect.beck-activation'))
  for (const b of barEls) dim(b, DIM.act)

  let phaseIndex = 0

  return {
    dimmed,
    onPacket(edgeId, tl, at, arrival) {
      const parts = msgParts.get(edgeId)
      if (parts?.length) tl.to(parts, { opacity: 1, duration: 0.25 }, at)
      for (const bar of barEls) {
        if (bar.dataset.start === edgeId)
          tl.to(bar, { opacity: 1, duration: 0.3 }, Math.max(at, arrival - 0.15))
        if (bar.dataset.end === edgeId) tl.to(bar, { opacity: DIM.act, duration: 0.35 }, arrival)
      }
    },
    onPhase(tl, at) {
      const band = bandEls[phaseIndex++]
      if (band) tl.to(band, { opacity: 1, duration: 0.4 }, at)
    },
    finale(tl) {
      const at = Math.max(0, tl.duration() - 0.75)
      if (lineEls.length) tl.to(lineEls, { opacity: DIM.line, duration: 0.6 }, at)
      if (labelEls.length) tl.to(labelEls, { opacity: DIM.label, duration: 0.6 }, at)
      if (bandEls.length) tl.to(bandEls, { opacity: DIM.band, duration: 0.6 }, at)
      if (barEls.length) tl.to(barEls, { opacity: DIM.act, duration: 0.6 }, at)
    },
  }
}
