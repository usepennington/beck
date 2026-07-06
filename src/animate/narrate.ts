import { gsap } from './runtime'
import type { Timeline } from './runtime'
import type { NarrationOptions } from '../model/schema'

/**
 * Seconds a caption should linger, from its word count and the pacing knobs:
 * `pad` (lead-in/out) + reading time at `wpm`, floored at `min`. This is the
 * "adjust the timing to the length of the message" behaviour — a longer caption
 * automatically holds longer so the reader can finish it.
 */
export function readingTime(text: string, opts: NarrationOptions): number {
  const words = text.trim().split(/\s+/).filter(Boolean).length || 1
  return Math.max(opts.min, opts.pad + (words / opts.wpm) * 60)
}

/**
 * Add one narration beat to the timeline at `position`: the current caption
 * fades out, the new text swaps in and fades up, then the caption dwells for
 * `hold` seconds. The whole beat is one sub-timeline, so the flow's next step
 * naturally lands after the dwell. Colour is a CSS value (`var(--beck-*)` or a
 * literal) applied inline; empty clears it back to the stylesheet default.
 */
export function narrateBeat(
  tl: Timeline,
  el: HTMLElement,
  text: string,
  color: string | undefined,
  hold: number,
  position: number,
): void {
  const g = gsap()
  const sub = g.timeline()
  sub.to(el, { opacity: 0, duration: 0.12, ease: 'power1.in' })
  sub.call(() => {
    el.textContent = text
    el.style.color = color ?? ''
  })
  sub.to(el, { opacity: 1, duration: 0.3, ease: 'power2.out' })
  sub.to({}, { duration: Math.max(0, hold) })
  tl.add(sub, position)
}
