import type {
  AccentToken,
  EdgeKind,
  EdgeModel,
  EdgeStyle,
  FlowModel,
  FlowStep,
  NodeKind,
  NodeModel,
  NodeVariant,
  PacketEase,
  PacketShape,
  Spacing,
} from './schema'

export const DEFAULT_SPACING: Spacing = { rank: 96, node: 32, cornerRadius: 16 }

/** Per-kind visual defaults: accent token, default icon key, and visual weight. */
export const KIND_DEFAULTS: Record<
  NodeKind,
  { accent: AccentToken; icon: string; variant: NodeVariant }
> = {
  service: { accent: 'primary', icon: 'service', variant: 'solid' },
  db: { accent: 'info', icon: 'db', variant: 'solid' },
  queue: { accent: 'warn', icon: 'queue', variant: 'solid' },
  cache: { accent: 'warn', icon: 'cache', variant: 'solid' },
  gateway: { accent: 'primary', icon: 'gateway', variant: 'solid' },
  external: { accent: 'neutral', icon: 'external', variant: 'solid' },
  user: { accent: 'success', icon: 'user', variant: 'solid' },
  ghost: { accent: 'neutral', icon: 'service', variant: 'ghost' },
}

/** Per-edge-kind defaults: line style and stroke color token. */
export const EDGE_KIND_DEFAULTS: Record<EdgeKind, { style: EdgeStyle; color: string }> = {
  data: { style: 'solid', color: 'var(--beck-edge)' },
  control: { style: 'solid', color: 'var(--beck-edge)' },
  async: { style: 'dashed', color: 'var(--beck-edge)' },
  dependency: { style: 'dashed', color: 'var(--beck-neutral)' },
}

/** Allowed packet ease tokens — kept in lockstep with the C# `PacketEase` enum
 *  and the `PACKET_EASE` token→GSAP map in `src/animate/timeline.ts`. */
export const PACKET_EASES = [
  'linear',
  'smooth',
  'accelerate',
  'decelerate',
  'expo',
  'sine',
  'steps',
  'bounce',
] as const

/** Allowed packet shape tokens — kept in lockstep with the C# `PacketShape` enum
 *  and the renderer in `src/animate/packet.ts`. */
export const PACKET_SHAPES = ['dot', 'circle', 'ring'] as const

/**
 * Default radius per shape. `dot` returns null so it keeps the edge-kind size
 * (small, semantic); the bolder `circle`/`ring` shapes read as deliberate, so
 * they get a larger baseline. An explicit `size` knob always wins over both.
 */
export const PACKET_SHAPE_SIZE: Record<PacketShape, number | null> = {
  dot: null,
  circle: 12,
  ring: 12,
}

/**
 * Per-edge-kind packet motion: radius (px), speed (px/s), glow, and ease token.
 * This is what makes data / control / async / dependency packets read
 * differently with zero authoring — `execPacket` reads the traversed edge's kind
 * and applies these. Explicit packet knobs always win over them.
 */
export const PACKET_KIND_STYLE: Record<
  EdgeKind,
  { size: number; speed: number; glow: boolean; ease: PacketEase }
> = {
  data: { size: 6, speed: 420, glow: true, ease: 'linear' },
  control: { size: 5, speed: 640, glow: true, ease: 'accelerate' },
  async: { size: 7.5, speed: 300, glow: true, ease: 'smooth' },
  dependency: { size: 4, speed: 380, glow: false, ease: 'linear' },
}

/** Kahn topological sort of node ids; falls back to declared order on a cycle. */
export function topoOrder(nodes: NodeModel[], edges: EdgeModel[]): string[] {
  const indegree = new Map<string, number>()
  const out = new Map<string, string[]>()
  for (const n of nodes) {
    indegree.set(n.id, 0)
    out.set(n.id, [])
  }
  for (const e of edges) {
    if (!indegree.has(e.from) || !indegree.has(e.to) || e.from === e.to) continue
    indegree.set(e.to, (indegree.get(e.to) ?? 0) + 1)
    out.get(e.from)!.push(e.to)
  }
  const queue = nodes.filter((n) => (indegree.get(n.id) ?? 0) === 0).map((n) => n.id)
  const order: string[] = []
  const seen = new Set<string>()
  while (queue.length) {
    const id = queue.shift()!
    if (seen.has(id)) continue
    seen.add(id)
    order.push(id)
    for (const next of out.get(id) ?? []) {
      indegree.set(next, (indegree.get(next) ?? 1) - 1)
      if ((indegree.get(next) ?? 0) <= 0) queue.push(next)
    }
  }
  // Append any nodes left out by a cycle, in declared order.
  for (const n of nodes) if (!seen.has(n.id)) order.push(n.id)
  return order
}

/**
 * Auto-derive a flow when none is authored: a packet traverses each edge in
 * topological order (roots → leaves), then the diagram resets and loops. This
 * makes `nodes` + `edges` alone animate beautifully with zero authoring.
 */
export function deriveFlow(nodes: NodeModel[], edges: EdgeModel[]): FlowModel {
  const order = topoOrder(nodes, edges)
  const pos = new Map(order.map((id, i) => [id, i]))
  const sorted = [...edges].sort(
    (a, b) => (pos.get(a.from) ?? 0) - (pos.get(b.from) ?? 0) || (pos.get(a.to) ?? 0) - (pos.get(b.to) ?? 0),
  )
  // Emit a `phase` label the first time each source node sends, so `seek(id)`
  // works even on an auto-derived flow (no authored phases otherwise exist).
  const steps: FlowStep[] = []
  const labeled = new Set<string>()
  for (const e of sorted) {
    if (!labeled.has(e.from)) {
      steps.push({ type: 'phase', label: e.from })
      labeled.add(e.from)
    }
    steps.push({ type: 'packet', from: e.from, to: e.to })
  }
  if (steps.length) {
    steps.push({ type: 'wait', seconds: 1 })
    steps.push({ type: 'reset' })
  }
  return { repeat: -1, repeatDelay: 1.2, steps, derived: true }
}
