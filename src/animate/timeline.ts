import type { DiagramModel, FlowStep } from '../model/schema'
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

  const pathOf = (from: string, to: string): { path: SVGPathElement; reversed: boolean } | null => {
    const direct = ctx.edges.find((e) => e.edge.from === from && e.edge.to === to)
    if (direct) return { path: direct.path, reversed: false }
    const rev = ctx.edges.find((e) => e.edge.from === to && e.edge.to === from)
    return rev ? { path: rev.path, reversed: true } : null
  }

  const execPacket = (
    from: string,
    to: string,
    via: string[] | undefined,
    color: string,
    label: string | undefined,
    position?: number,
  ) => {
    const chain = [from, ...(via ?? []), to]
    let at = position
    for (let i = 0; i < chain.length - 1; i++) {
      const found = pathOf(chain[i], chain[i + 1])
      if (!found) continue
      const target = nodeEl(chain[i + 1]) ?? undefined
      // The label rides the final hop so it reads as the payload arriving.
      const hopLabel = i === chain.length - 2 ? label : undefined
      at = packetWithTrail(tl, found.path, trail, { color, reverse: found.reversed, label: hopLabel }, target, at)
    }
  }

  const execStep = (step: FlowStep, position?: number): void => {
    switch (step.type) {
      case 'packet':
        execPacket(step.from, step.to, step.via, resolve(step.color), step.label, position)
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
