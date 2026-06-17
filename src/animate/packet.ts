import { gsap } from './runtime'
import type { Timeline } from './runtime'

export interface PacketOptions {
  color?: string
  radius?: number
  duration?: number
  ease?: string
  glow?: boolean
  reverse?: boolean
  noEntry?: boolean
  noExit?: boolean
  /** Text payload drawn above the dot as it travels. */
  label?: string
}

let glowSeq = 0

/** A glowing dot that travels along an SVG path (sampled via getPointAtLength). */
export function packet(
  tl: Timeline,
  arrowPath: SVGPathElement,
  opts: PacketOptions = {},
  position?: string | number,
): void {
  const g = gsap()
  const svg = arrowPath.closest('svg')!
  const color = opts.color || 'var(--beck-packet)'
  const radius = opts.radius || 6
  const duration = opts.duration || 0.8
  const noEntry = opts.noEntry || false
  const noExit = opts.noExit || false

  const circle = document.createElementNS('http://www.w3.org/2000/svg', 'circle')
  circle.setAttribute('r', String(radius))
  circle.setAttribute('fill', color)
  circle.setAttribute('opacity', '0')
  circle.dataset.beckPacket = '1'

  if (opts.glow !== false) {
    const filterId = `beck-glow-${glowSeq++}`
    const defs =
      svg.querySelector('defs') ||
      svg.appendChild(document.createElementNS('http://www.w3.org/2000/svg', 'defs'))
    const filter = document.createElementNS('http://www.w3.org/2000/svg', 'filter')
    filter.setAttribute('id', filterId)
    filter.setAttribute('x', '-200%')
    filter.setAttribute('y', '-200%')
    filter.setAttribute('width', '500%')
    filter.setAttribute('height', '500%')
    const blur = document.createElementNS('http://www.w3.org/2000/svg', 'feGaussianBlur')
    blur.setAttribute('in', 'SourceGraphic')
    blur.setAttribute('stdDeviation', '3')
    blur.setAttribute('result', 'blur')
    const merge = document.createElementNS('http://www.w3.org/2000/svg', 'feMerge')
    const m1 = document.createElementNS('http://www.w3.org/2000/svg', 'feMergeNode')
    m1.setAttribute('in', 'blur')
    const m2 = document.createElementNS('http://www.w3.org/2000/svg', 'feMergeNode')
    m2.setAttribute('in', 'SourceGraphic')
    merge.appendChild(m1)
    merge.appendChild(m2)
    filter.appendChild(blur)
    filter.appendChild(merge)
    defs.appendChild(filter)
    circle.setAttribute('filter', `url(#${filterId})`)
  }

  svg.appendChild(circle)

  // Optional label that rides above the dot (shares the packet's lifecycle so
  // the snapshot reset removes it via the `data-beck-packet` marker).
  let label: SVGTextElement | null = null
  if (opts.label) {
    label = document.createElementNS('http://www.w3.org/2000/svg', 'text')
    label.classList.add('beck-packet-label')
    label.setAttribute('text-anchor', 'middle')
    label.setAttribute('fill', color)
    label.setAttribute('opacity', '0')
    label.dataset.beckPacket = '1'
    label.textContent = opts.label
    svg.appendChild(label)
  }

  const pathLength = arrowPath.getTotalLength()
  const reverse = opts.reverse || false
  const proxy = { t: reverse ? 1 : 0 }
  const rampTime = duration * 0.15
  const entryTime = noEntry ? 0 : rampTime
  const exitTime = noExit ? 0 : rampTime

  const place = () => {
    const point = arrowPath.getPointAtLength(proxy.t * pathLength)
    circle.setAttribute('cx', String(point.x))
    circle.setAttribute('cy', String(point.y))
    if (label) {
      label.setAttribute('x', String(point.x))
      label.setAttribute('y', String(point.y - radius - 6))
    }
  }

  const subTl = g.timeline()
  subTl.set(proxy, { t: reverse ? 1 : 0 })
  subTl.set(circle, { attr: { opacity: 1, r: noEntry ? radius : 0 } })
  if (label) subTl.set(label, { attr: { opacity: 1 } })
  subTl.call(place)

  if (!noEntry) {
    subTl.to(circle, { attr: { r: radius }, duration: entryTime, ease: 'power2.out' })
  }

  subTl.to(proxy, {
    t: reverse ? 0 : 1,
    duration: duration - entryTime - exitTime,
    ease: 'none',
    onUpdate: place,
  })

  if (!noExit) {
    subTl.to(circle, { attr: { r: 0 }, duration: exitTime, ease: 'power2.in' })
  }
  subTl.set(circle, { attr: { opacity: 0 } })
  if (label) subTl.set(label, { attr: { opacity: 0 } })

  tl.add(subTl, position)
}
