import { packet } from './packet'
import { pulse } from './effects'
import type { Timeline } from './runtime'

export interface TrailOptions {
  color?: string
  /** Pixels per second (default 400). */
  speed?: number
  /** Travel from the path end toward its start (for edges used backwards). */
  reverse?: boolean
  /** Text payload that rides along with the packet. */
  label?: string
}

export interface TrailState {
  overlays: SVGPathElement[]
  /** Persistent flowing-dash overlays (removed on reset). */
  streams: SVGPathElement[]
}

export function createTrailState(): TrailState {
  return { overlays: [], streams: [] }
}

/**
 * A persistent flowing-dash overlay along an edge — ongoing traffic, distinct
 * from a one-shot packet. CSS-driven dash march so it keeps flowing while the
 * timeline is paused; removed on the next snapshot reset.
 */
export function streamEdge(
  tl: Timeline,
  path: SVGPathElement,
  state: TrailState,
  opts: { color?: string; speed?: number } = {},
  position?: number,
): void {
  const color = opts.color || 'var(--beck-edge)'
  const len = path.getTotalLength()
  const dur = Math.max(0.5, len / (opts.speed || 220))

  const overlay = path.cloneNode() as SVGPathElement
  overlay.setAttribute('stroke', color)
  overlay.setAttribute('stroke-width', '2.5')
  overlay.setAttribute('fill', 'none')
  overlay.removeAttribute('marker-end')
  overlay.setAttribute('stroke-dasharray', '5 9')
  overlay.style.opacity = '0'
  overlay.dataset.beckStream = '1'
  path.parentElement!.appendChild(overlay)
  state.streams.push(overlay)

  tl.call(
    () => {
      overlay.style.opacity = '1'
      overlay.style.animation = `beck-stream ${dur}s linear infinite`
    },
    [],
    position,
  )
}

/**
 * A packet that travels a path at constant speed, revealing a colored trail
 * behind it; optionally pulses the target node on arrival.
 */
export function packetWithTrail(
  tl: Timeline,
  path: SVGPathElement,
  state: TrailState,
  opts: TrailOptions = {},
  targetNode?: HTMLElement,
  position?: number,
): number {
  const color = opts.color || 'var(--beck-packet)'
  const speed = opts.speed || 400
  const reverse = opts.reverse || false
  const len = path.getTotalLength()
  const duration = Math.max(0.3, len / speed)
  const pos = position ?? tl.duration()
  const start = reverse ? -len : len

  const overlay = path.cloneNode() as SVGPathElement
  overlay.setAttribute('stroke', color)
  overlay.setAttribute('stroke-width', '2')
  overlay.setAttribute('fill', 'none')
  overlay.removeAttribute('marker-end')
  overlay.removeAttribute('stroke-dasharray')
  overlay.dataset.beckTrail = '1'
  overlay.style.strokeDasharray = String(len)
  overlay.style.strokeDashoffset = String(start)
  path.parentElement!.appendChild(overlay)
  state.overlays.push(overlay)

  // fromTo pins the start each loop iteration (so reverse trails stay correct).
  tl.fromTo(overlay, { strokeDashoffset: start }, { strokeDashoffset: 0, duration, ease: 'none' }, pos)
  packet(tl, path, { color, duration, noEntry: true, noExit: true, reverse, label: opts.label }, pos)

  if (targetNode) pulse(tl, targetNode, { color })

  return pos + duration
}
