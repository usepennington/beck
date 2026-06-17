import { gsap } from './runtime'
import type { Timeline } from './runtime'
import { withAlpha } from '../util/color'

function resolveCard(el: HTMLElement): HTMLElement {
  return (el.querySelector('.beck-node') as HTMLElement) || el
}

export interface HighlightOptions {
  color?: string
  duration?: number
  scale?: number
}

export function highlight(
  tl: Timeline,
  nodeEl: HTMLElement,
  opts: HighlightOptions = {},
  position?: string | number,
): void {
  const g = gsap()
  const card = resolveCard(nodeEl)
  const color = opts.color || 'var(--beck-primary)'
  const duration = opts.duration || 0.7
  const scale = opts.scale || 1.04

  let origBorder = ''
  let origShadow = ''
  let origTransform = ''

  const subTl = g.timeline()
  subTl.call(() => {
    origBorder = card.style.borderColor
    origShadow = card.style.boxShadow
    origTransform = card.style.transform
  })
  subTl.to(card, {
    boxShadow: `0 0 20px ${withAlpha(color, 31)}, 0 0 0 2px ${color}`,
    borderColor: color,
    scale,
    y: -2,
    duration: duration * 0.3,
    ease: 'back.out(2)',
  })
  subTl.to(card, {
    scale: 1,
    y: 0,
    duration: duration * 0.7,
    ease: 'elastic.out(1, 0.4)',
    onComplete() {
      card.style.borderColor = origBorder
      card.style.boxShadow = origShadow
      card.style.transform = origTransform
    },
  })

  tl.add(subTl, position)
}

export interface PulseOptions {
  color?: string
  duration?: number
  scale?: number
}

export function pulse(
  tl: Timeline,
  nodeEl: HTMLElement,
  opts: PulseOptions = {},
  position?: string | number,
): void {
  const g = gsap()
  const card = resolveCard(nodeEl)
  const color = opts.color || 'var(--beck-primary)'
  const duration = opts.duration || 0.6
  const scale = opts.scale || 1.04

  let origShadow = ''

  const subTl = g.timeline()
  subTl.call(() => {
    origShadow = card.style.boxShadow
    const ripple = document.createElement('div')
    ripple.style.cssText = `position:absolute;inset:0;border-radius:inherit;border:2px solid ${color};pointer-events:none;opacity:0.6;`
    card.style.position = 'relative'
    card.style.overflow = 'visible'
    card.appendChild(ripple)
    g.to(ripple, {
      scale: 1.15,
      opacity: 0,
      duration: duration * 0.8,
      ease: 'power2.out',
      onComplete() {
        ripple.remove()
      },
    })
  })
  subTl.to(card, {
    scale,
    y: -2,
    boxShadow: `0 4px 16px ${withAlpha(color, 25)}`,
    duration: duration * 0.3,
    ease: 'back.out(3)',
  })
  subTl.to(card, {
    scale: 1,
    y: 0,
    duration: duration * 0.7,
    ease: 'elastic.out(1, 0.5)',
    onComplete() {
      card.style.boxShadow = origShadow
    },
  })

  tl.add(subTl, position)
}

/** Registry of original line state, for the snapshot reset. */
const lineOrig = new Map<SVGPathElement, string>()
const markerOrig = new Map<SVGPolygonElement, string>()

function findMarkerPolygon(p: SVGPathElement): SVGPolygonElement | null {
  const url = p.getAttribute('marker-end') || p.getAttribute('marker-start')
  const match = url?.match(/url\(#(.+)\)/)
  if (!match) return null
  const svg = p.closest('svg')!
  return svg.querySelector(`#${match[1]}`)?.querySelector('polygon') ?? null
}

/** Recolor one or more edge paths (and their arrowheads) on the timeline. */
export function colorLine(
  tl: Timeline,
  paths: SVGPathElement | SVGPathElement[],
  color: string,
  position?: string | number,
): void {
  const arr = Array.isArray(paths) ? paths : [paths]
  tl.call(
    () => {
      for (const p of arr) {
        if (!lineOrig.has(p)) lineOrig.set(p, p.getAttribute('stroke') || 'var(--beck-edge)')
        p.setAttribute('stroke', color)
        p.style.stroke = color
        const poly = findMarkerPolygon(p)
        if (poly) {
          if (!markerOrig.has(poly)) markerOrig.set(poly, poly.getAttribute('fill') || 'var(--beck-edge)')
          poly.setAttribute('fill', color)
        }
      }
    },
    [],
    position,
  )
}

export function resetLinesNow(): void {
  for (const [p, stroke] of lineOrig) {
    p.setAttribute('stroke', stroke)
    p.style.stroke = ''
  }
  lineOrig.clear()
  for (const [poly, fill] of markerOrig) poly.setAttribute('fill', fill)
  markerOrig.clear()
}

/** Cards currently in the persistent `working` state (for snapshot reset). */
const workingCards = new Set<HTMLElement>()

/** Leave a node breathing (CSS-driven, so it keeps animating while paused). */
export function working(
  tl: Timeline,
  nodeEl: HTMLElement,
  opts: { color?: string } = {},
  position?: string | number,
): void {
  const card = resolveCard(nodeEl)
  tl.call(
    () => {
      if (opts.color) card.style.setProperty('--beck-working', opts.color)
      card.classList.add('beck-working')
      workingCards.add(card)
    },
    [],
    position,
  )
}

/** Clear a node's `working` state. */
export function clearWorking(tl: Timeline, nodeEl: HTMLElement, position?: string | number): void {
  const card = resolveCard(nodeEl)
  tl.call(
    () => {
      card.classList.remove('beck-working')
      card.style.removeProperty('--beck-working')
      workingCards.delete(card)
    },
    [],
    position,
  )
}

/** Immediately clear every working state (snapshot reset). */
export function resetWorkingNow(): void {
  for (const card of workingCards) {
    card.classList.remove('beck-working')
    card.style.removeProperty('--beck-working')
  }
  workingCards.clear()
}

export interface FailOptions {
  color?: string
}

/** A failure beat: a quick red shake + border/glow flash, then restore. */
export function fail(
  tl: Timeline,
  nodeEl: HTMLElement,
  opts: FailOptions = {},
  position?: string | number,
): void {
  const g = gsap()
  const card = resolveCard(nodeEl)
  const color = opts.color || 'var(--beck-danger)'

  let origBorder = ''
  let origShadow = ''
  const sub = g.timeline()
  sub.call(() => {
    origBorder = card.style.borderColor
    origShadow = card.style.boxShadow
  })
  sub.to(card, {
    borderColor: color,
    boxShadow: `0 0 0 2px ${color}, 0 0 18px ${withAlpha(color, 31)}`,
    duration: 0.12,
  })
  sub.to(card, { x: -5, duration: 0.06 })
  sub.to(card, { x: 5, duration: 0.08 })
  sub.to(card, { x: -3, duration: 0.07 })
  sub.to(card, { x: 0, duration: 0.07 })
  sub.to(card, {
    duration: 0.35,
    delay: 0.25,
    onComplete() {
      card.style.borderColor = origBorder
      card.style.boxShadow = origShadow
    },
  })
  tl.add(sub, position)
}
