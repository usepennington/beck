import type { DiagramModel, EdgeKind, FlowStep, PacketEase, PacketKnobs } from '../model/schema'
import { PACKET_KIND_STYLE, PACKET_SHAPE_SIZE } from '../model/defaults'
import type { RoutedEdge } from '../route/svg'
import { gsap } from './runtime'
import type { Timeline } from './runtime'
import { resolveColor } from '../util/color'
import { createTrailState, packetWithTrail, streamEdge } from './trail'
import { statusPill } from './status'
import { highlight, pulse, working, clearWorking, fail, colorLine } from './effects'
import { Snapshot, createSnapshot } from './snapshot'

export interface FlowContext {
  /** The `.beck-root` container — used for CSS-var color resolution + snapshot. */
  root: HTMLElement
  /** The canvas that holds node wraps + the SVG overlay. */
  canvas: HTMLElement
  svg: SVGSVGElement
  /** node id → wrapper element. */
  nodes: Map<string, HTMLElement>
  edges: RoutedEdge[]
  model: DiagramModel
}

export interface CompiledFlow {
  timeline: Timeline
  snapshot: Snapshot
}

/** Packet ease token → concrete GSAP ease. Kept to monotonic eases (overshoot
 *  eases like back/elastic would clamp against the path ends mid-travel). Stays
 *  in lockstep with `PACKET_EASES` in `src/model/defaults.ts`. */
const PACKET_EASE: Record<PacketEase, string> = {
  linear: 'none',
  smooth: 'power2.inOut',
  accelerate: 'power2.in',
  decelerate: 'power2.out',
  expo: 'expo.inOut',
  sine: 'sine.inOut',
  steps: 'steps(12)',
  bounce: 'bounce.out',
}

/** Compile a FlowModel into a paused GSAP timeline (+ a reset snapshot). */
export function buildTimeline(ctx: FlowContext): CompiledFlow {
  const g = gsap()
  const flow = ctx.model.flow
  const tl = g.timeline({ paused: true, repeat: flow.repeat, repeatDelay: flow.repeatDelay })
  const trail = createTrailState()
  const snapshot = createSnapshot().captureAll(ctx.canvas).trackSvg(ctx.svg).trackTrails(trail)

  const resolve = (c?: string) => resolveColor(ctx.root, c ?? 'var(--beck-packet)')
  const nodeEl = (id: string) => ctx.nodes.get(id) ?? null
  const accentOf = (id: string) => ctx.model.nodes.find((n) => n.id === id)?.accent
  const groupMembers = (id: string) => ctx.model.groups.find((g) => g.id === id)?.members ?? []

  const pathOf = (
    from: string,
    to: string,
    edgeId?: string,
  ): { path: SVGPathElement; reversed: boolean; kind: EdgeKind } | null => {
    // An explicit edge id wins — sequence flows use it because many messages
    // share the same from/to pair and "first match" would ride the wrong row.
    if (edgeId) {
      const hit = ctx.edges.find((e) => e.edge.id === edgeId)
      if (hit) return { path: hit.path, reversed: false, kind: hit.edge.kind }
    }
    const direct = ctx.edges.find((e) => e.edge.from === from && e.edge.to === to)
    if (direct) return { path: direct.path, reversed: false, kind: direct.edge.kind }
    const rev = ctx.edges.find((e) => e.edge.from === to && e.edge.to === from)
    return rev ? { path: rev.path, reversed: true, kind: rev.edge.kind } : null
  }

  // Merge a step's explicit knobs over the traversed edge's kind defaults, then
  // map the ease token to its GSAP string. This is where data/control/async/
  // dependency edges get their distinct packet motion for free.
  const hopOptions = (kind: EdgeKind, k: PacketKnobs, color: string) => {
    const ks = PACKET_KIND_STYLE[kind]
    const shape = k.shape ?? 'dot'
    return {
      color,
      shape,
      impact: k.impact ?? false,
      // `circle`/`ring` carry a larger baseline radius; `dot` keeps the edge-kind size.
      size: k.size ?? PACKET_SHAPE_SIZE[shape] ?? ks.size,
      speed: k.speed ?? ks.speed,
      glow: k.glow ?? ks.glow,
      ease: PACKET_EASE[k.ease ?? ks.ease],
    }
  }

  // Emit one dot across a (possibly multi-hop) chain, returning the time it
  // arrives. `label` rides the final hop so it reads as the payload landing.
  const emitDot = (
    chain: string[],
    knobs: PacketKnobs,
    color: string,
    label: string | undefined,
    startAt: number | undefined,
    edgeId?: string,
  ): number | undefined => {
    let at = startAt
    for (let i = 0; i < chain.length - 1; i++) {
      const nextId = chain[i + 1]
      const found = pathOf(chain[i], nextId, chain.length === 2 ? edgeId : undefined)
      if (!found) continue
      const target = nodeEl(nextId) ?? undefined
      const hopLabel = i === chain.length - 2 ? label : undefined
      const opts = hopOptions(found.kind, knobs, color)
      at = packetWithTrail(tl, found.path, trail, { ...opts, reverse: found.reversed, label: hopLabel }, target, at)
      // Group endpoint: nodeEl is null, so pulse the members on arrival instead.
      if (!target) for (const m of groupMembers(nextId)) {
        const mel = nodeEl(m)
        if (mel) pulse(tl, mel, { color }, at)
      }
    }
    return at
  }

  const execPacket = (step: Extract<FlowStep, { type: 'packet' }>, position?: number) => {
    emitDot([step.from, ...(step.via ?? []), step.to], step, resolve(step.color), step.label, position, step.edge)
  }

  // A burst fans `count` dots down each target edge, staggered. Every dot is
  // added at an explicit position, so the timeline duration extends to cover
  // them and the following step still falls after the last dot.
  const execBurst = (step: Extract<FlowStep, { type: 'burst' }>, position?: number) => {
    const color = resolve(step.color)
    const targets = Array.isArray(step.to) ? step.to : [step.to]
    const base = position ?? tl.duration()
    // Fire in `count` waves; each wave launches a dot to EVERY target at once, so
    // a fan-out reads as a simultaneous broadcast rather than one drained target
    // after another. A single target just yields `count` staggered dots.
    for (let c = 0; c < step.count; c++) {
      const at = base + c * step.stagger
      targets.forEach((tgt, t) => {
        const chain = [step.from, ...(step.via ?? []), tgt]
        // Only the very first dot carries the label, so a burst isn't a wall of text.
        emitDot(chain, step, color, c === 0 && t === 0 ? step.label : undefined, at)
      })
    }
  }

  const execStep = (step: FlowStep, position?: number): void => {
    switch (step.type) {
      case 'packet':
        execPacket(step, position)
        break
      case 'burst':
        execBurst(step, position)
        break
      case 'status': {
        const el = nodeEl(step.node)
        if (el) statusPill(tl, el, step.text, { color: resolve(step.color ?? accentOf(step.node)) }, position)
        break
      }
      case 'highlight': {
        const el = nodeEl(step.node)
        if (el) highlight(tl, el, { color: resolve(step.color ?? accentOf(step.node)) }, position)
        break
      }
      case 'pulse': {
        const el = nodeEl(step.node)
        if (el) pulse(tl, el, { color: resolve(step.color ?? accentOf(step.node)) }, position)
        break
      }
      case 'activate': {
        const found = pathOf(step.from, step.to)
        if (found) colorLine(tl, found.path, resolve(step.color ?? 'var(--beck-primary)'), position)
        break
      }
      case 'stream': {
        const found = pathOf(step.from, step.to)
        if (found) streamEdge(tl, found.path, trail, { color: resolve(step.color ?? 'var(--beck-primary)') }, position)
        break
      }
      case 'working': {
        const el = nodeEl(step.node)
        if (el) working(tl, el, { color: resolve(step.color ?? accentOf(step.node)) }, position)
        break
      }
      case 'idle': {
        const el = nodeEl(step.node)
        if (el) clearWorking(tl, el, position)
        break
      }
      case 'fail': {
        const el = nodeEl(step.node)
        if (el) {
          const color = resolve(step.color ?? 'var(--beck-danger)')
          fail(tl, el, { color }, position)
          if (step.text) statusPill(tl, el, step.text, { color }, position)
        }
        break
      }
      case 'phase':
        tl.addLabel(step.label, position ?? tl.duration())
        break
      case 'wait':
        tl.to({}, { duration: step.seconds }, position)
        break
      case 'reset':
        tl.call(() => snapshot.restoreNow(), [], position)
        break
      case 'parallel': {
        const base = position ?? tl.duration()
        for (const child of step.steps) execStep(child, base)
        break
      }
    }
  }

  for (const step of flow.steps) execStep(step)

  // Guarantee a clean loop restart when looping and not already reset last.
  if (flow.repeat !== 0 && flow.steps[flow.steps.length - 1]?.type !== 'reset') {
    tl.call(() => snapshot.restoreNow())
  }

  return { timeline: tl, snapshot }
}
