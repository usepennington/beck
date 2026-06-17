import type { TrailState } from './trail'
import { resetLinesNow, resetWorkingNow } from './effects'

interface PillSnapshot {
  el: HTMLElement
  text: string
  color: string
  backgroundColor: string
  display: string
}

interface NodeSnapshot {
  card: HTMLElement
  borderColor: string
  boxShadow: string
  transform: string
}

/**
 * Captures the diagram's initial state so a single `restoreNow()` returns it to
 * the start: status pills, node card styles, SVG packet/trail artifacts, and
 * recolored lines. Used for clean loop restarts and the reduced-motion frame.
 */
export class Snapshot {
  private pills: PillSnapshot[] = []
  private nodes: NodeSnapshot[] = []
  private svgs: SVGSVGElement[] = []
  private trailStates: TrailState[] = []

  capturePills(container: HTMLElement): this {
    for (const el of container.querySelectorAll('.beck-status, .beck-status-inline')) {
      const pill = el as HTMLElement
      this.pills.push({
        el: pill,
        text: pill.textContent || '',
        color: pill.style.color || '',
        backgroundColor: pill.style.backgroundColor || '',
        display: pill.style.display || '',
      })
    }
    return this
  }

  captureNodes(container: HTMLElement): this {
    for (const el of container.querySelectorAll('.beck-node')) {
      const card = el as HTMLElement
      this.nodes.push({
        card,
        borderColor: card.style.borderColor,
        boxShadow: card.style.boxShadow,
        transform: card.style.transform,
      })
    }
    return this
  }

  captureAll(container: HTMLElement): this {
    return this.capturePills(container).captureNodes(container)
  }

  trackSvg(svg: SVGSVGElement): this {
    this.svgs.push(svg)
    return this
  }

  trackTrails(state: TrailState): this {
    this.trailStates.push(state)
    return this
  }

  /** Immediately restore everything to its captured initial state. */
  restoreNow(): void {
    for (const s of this.pills) {
      s.el.textContent = s.text
      s.el.style.color = s.color
      s.el.style.backgroundColor = s.backgroundColor
      s.el.style.display = s.display
    }
    for (const s of this.nodes) {
      s.card.style.borderColor = s.borderColor
      s.card.style.boxShadow = s.boxShadow
      s.card.style.transform = s.transform
    }
    for (const svg of this.svgs) {
      svg.querySelectorAll('circle[data-beck-packet]').forEach((c) => c.remove())
      svg.querySelectorAll('text[data-beck-packet]').forEach((t) => t.remove())
      svg.querySelectorAll('filter[id^="beck-glow-"]').forEach((f) => f.remove())
    }
    for (const state of this.trailStates) {
      for (const o of state.overlays) o.style.strokeDashoffset = o.style.strokeDasharray
      // Hide (don't remove) streams so the same overlay replays on the next loop.
      for (const s of state.streams) {
        s.style.opacity = '0'
        s.style.animation = ''
      }
    }
    resetLinesNow()
    resetWorkingNow()
  }
}

export function createSnapshot(): Snapshot {
  return new Snapshot()
}
