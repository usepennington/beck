import { BeckError } from '../util/errors'
import { accentToCss } from '../util/color'
import { isKnownIcon } from '../render/icons'
import {
  DEFAULT_SPACING,
  EDGE_KIND_DEFAULTS,
  KIND_DEFAULTS,
  deriveFlow,
} from './defaults'
import type {
  DiagramMeta,
  DiagramModel,
  EdgeModel,
  FlowModel,
  FlowStep,
  GroupModel,
  NodeKind,
  NodeModel,
  Side,
} from './schema'

const KIND_LIST = [
  'service',
  'db',
  'queue',
  'cache',
  'gateway',
  'external',
  'user',
  'ghost',
] as const
const EDGE_KIND_LIST = ['data', 'control', 'async', 'dependency'] as const
const SIDES = ['top', 'bottom', 'left', 'right'] as const

// ---- low-level coercion helpers (friendly errors) ----

function asObject(v: unknown, field: string): Record<string, unknown> {
  if (v == null) return {}
  if (typeof v !== 'object' || Array.isArray(v)) throw new BeckError(`\`${field}\` must be a mapping`)
  return v as Record<string, unknown>
}

function asArray(v: unknown, field: string): unknown[] {
  if (v == null) return []
  if (!Array.isArray(v)) throw new BeckError(`\`${field}\` must be a list`)
  return v
}

function asString(v: unknown, field: string): string {
  if (typeof v === 'string') return v
  if (typeof v === 'number' || typeof v === 'boolean') return String(v)
  throw new BeckError(`\`${field}\` must be a string`)
}

function optString(v: unknown): string | undefined {
  if (v == null) return undefined
  if (typeof v === 'string') return v
  if (typeof v === 'number' || typeof v === 'boolean') return String(v)
  return undefined
}

function optColor(v: unknown): string | undefined {
  const s = optString(v)
  return s == null ? undefined : accentToCss(s, 'primary')
}

function optNumber(v: unknown, field: string): number | undefined {
  if (v == null) return undefined
  if (typeof v === 'number') return v
  if (typeof v === 'string' && v.trim() !== '' && !Number.isNaN(Number(v))) return Number(v)
  throw new BeckError(`\`${field}\` must be a number`)
}

function optBool(v: unknown, field: string, dflt: boolean): boolean {
  if (v == null) return dflt
  if (typeof v === 'boolean') return v
  if (v === 'true') return true
  if (v === 'false') return false
  throw new BeckError(`\`${field}\` must be true or false`)
}

function oneOf<T extends string>(v: unknown, allowed: readonly T[], field: string, dflt: T): T {
  if (v == null) return dflt
  const s = String(v)
  if ((allowed as readonly string[]).includes(s)) return s as T
  throw new BeckError(`\`${field}\` must be one of: ${allowed.join(', ')} (got "${s}")`)
}

// ---- builders ----

function buildMeta(m: Record<string, unknown>): DiagramMeta {
  const sp = asObject(m.spacing, 'meta.spacing')
  return {
    title: optString(m.title),
    subtitle: optString(m.subtitle),
    direction: oneOf(m.direction, ['TB', 'BT', 'LR', 'RL'] as const, 'meta.direction', 'TB'),
    theme: oneOf(m.theme, ['auto', 'light', 'dark'] as const, 'meta.theme', 'auto'),
    animate: optBool(m.animate, 'meta.animate', true),
    loop: optBool(m.loop, 'meta.loop', true),
    spacing: {
      rank: optNumber(sp.rank, 'meta.spacing.rank') ?? DEFAULT_SPACING.rank,
      node: optNumber(sp.node, 'meta.spacing.node') ?? DEFAULT_SPACING.node,
      cornerRadius:
        optNumber(sp.cornerRadius, 'meta.spacing.cornerRadius') ?? DEFAULT_SPACING.cornerRadius,
    },
  }
}

function buildNode(n: Record<string, unknown>): NodeModel {
  const id = asString(n.id, 'node.id')
  const kind = oneOf(n.kind, KIND_LIST, `node "${id}" kind`, 'service') as NodeKind
  const kd = KIND_DEFAULTS[kind]
  // An explicit but unknown icon key falls back to the kind default (a typo
  // shouldn't silently drop the icon); inline `<svg>` and known keys pass through.
  const rawIcon = optString(n.icon)
  return {
    id,
    title: optString(n.title) ?? id,
    subtitle: optString(n.subtitle),
    icon: rawIcon != null && isKnownIcon(rawIcon) ? rawIcon : kd.icon,
    kind,
    variant: oneOf(n.variant, ['solid', 'subtle', 'ghost'] as const, `node "${id}" variant`, kd.variant),
    status: optString(n.status),
    accent: accentToCss(optString(n.accent), kd.accent),
    href: optString(n.href),
    target: optString(n.target),
    surface: optString(n.surface),
    textColor: optString(n.textColor),
    width: optNumber(n.width, `node "${id}" width`),
    rank: optNumber(n.rank, `node "${id}" rank`),
    order: optNumber(n.order, `node "${id}" order`),
    group: optString(n.group),
  }
}

function buildGroups(
  rawGroups: unknown[],
  nodes: NodeModel[],
  nodeIds: Set<string>,
): GroupModel[] {
  const groups = new Map<string, GroupModel>()
  const order: string[] = []

  const ensure = (id: string, label?: string, accent?: string): GroupModel => {
    let g = groups.get(id)
    if (!g) {
      g = { id, label: label ?? id, members: [], accent: accentToCss(accent, 'neutral') }
      groups.set(id, g)
      order.push(id)
    } else if (label) {
      g.label = label
    }
    return g
  }

  for (const rg of rawGroups) {
    const g = asObject(rg, 'group')
    const id = asString(g.id, 'group.id')
    const grp = ensure(id, optString(g.label), optString(g.accent))
    for (const m of asArray(g.members, `group "${id}" members`)) {
      const mid = asString(m, `group "${id}" member`)
      if (!nodeIds.has(mid)) throw new BeckError(`Group "${id}" references unknown node "${mid}"`)
      if (!grp.members.includes(mid)) grp.members.push(mid)
    }
  }

  // Inline `node.group` membership.
  for (const n of nodes) {
    if (!n.group) continue
    const grp = ensure(n.group)
    if (!grp.members.includes(n.id)) grp.members.push(n.id)
  }

  // A node may belong to at most one group (layout treats groups as contiguous bands).
  const seen = new Map<string, string>()
  for (const id of order) {
    for (const m of groups.get(id)!.members) {
      const prev = seen.get(m)
      if (prev) throw new BeckError(`Node "${m}" is in two groups ("${prev}" and "${id}")`)
      seen.set(m, id)
    }
  }

  return order.map((id) => groups.get(id)!)
}

function buildEdges(rawEdges: unknown[], validTargets: Set<string>): EdgeModel[] {
  return rawEdges.map((re, i) => {
    const e = asObject(re, 'edge')
    const from = asString(e.from, 'edge.from')
    const to = asString(e.to, 'edge.to')
    if (!validTargets.has(from)) throw new BeckError(`Edge references unknown source "${from}"`)
    if (!validTargets.has(to)) throw new BeckError(`Edge references unknown target "${to}"`)
    const kind = oneOf(e.kind, EDGE_KIND_LIST, 'edge.kind', 'data')
    const kd = EDGE_KIND_DEFAULTS[kind]
    return {
      id: `${from}->${to}#${i}`,
      from,
      to,
      label: optString(e.label),
      style: oneOf(e.style, ['solid', 'dashed'] as const, 'edge.style', kd.style),
      curve: oneOf(e.curve, ['step-round', 'straight', 's'] as const, 'edge.curve', 'step-round'),
      kind,
      color: optColor(e.color) ?? kd.color,
      arrow: optBool(e.arrow, 'edge.arrow', true),
      fromSide: e.fromSide != null ? (oneOf(e.fromSide, SIDES, 'edge.fromSide', 'bottom') as Side) : undefined,
      toSide: e.toSide != null ? (oneOf(e.toSide, SIDES, 'edge.toSide', 'top') as Side) : undefined,
    }
  })
}

function parseStep(s: Record<string, unknown>, nodeIds: Set<string>, groupIds: Set<string>): FlowStep {
  const node = (id: string, ctx: string): string => {
    if (!nodeIds.has(id)) throw new BeckError(`Flow ${ctx} references unknown node "${id}"`)
    return id
  }
  // Edge endpoints (packet/activate/stream) may also be a group id, since an
  // edge can target a group — keep this in lockstep with edge from/to validation.
  const endpoint = (id: string, ctx: string): string => {
    if (!nodeIds.has(id) && !groupIds.has(id))
      throw new BeckError(`Flow ${ctx} references unknown node or group "${id}"`)
    return id
  }

  if ('packet' in s) {
    const p = asObject(s.packet, 'flow packet')
    const via = asArray(p.via, 'packet.via').map((v) => endpoint(asString(v, 'packet.via'), 'packet via'))
    return {
      type: 'packet',
      from: endpoint(asString(p.from, 'packet.from'), 'packet'),
      to: endpoint(asString(p.to, 'packet.to'), 'packet'),
      via: via.length ? via : undefined,
      color: optColor(p.color),
      label: optString(p.label),
    }
  }
  if ('status' in s) {
    const p = asObject(s.status, 'flow status')
    return {
      type: 'status',
      node: node(asString(p.node, 'status.node'), 'status'),
      text: asString(p.text, 'status.text'),
      color: optColor(p.color),
    }
  }
  if ('highlight' in s) {
    const p = asObject(s.highlight, 'flow highlight')
    return { type: 'highlight', node: node(asString(p.node, 'highlight.node'), 'highlight'), color: optColor(p.color) }
  }
  if ('pulse' in s) {
    const p = asObject(s.pulse, 'flow pulse')
    return { type: 'pulse', node: node(asString(p.node, 'pulse.node'), 'pulse'), color: optColor(p.color) }
  }
  if ('activate' in s) {
    const p = asObject(s.activate, 'flow activate')
    return {
      type: 'activate',
      from: endpoint(asString(p.from, 'activate.from'), 'activate'),
      to: endpoint(asString(p.to, 'activate.to'), 'activate'),
      color: optColor(p.color),
    }
  }
  if ('stream' in s) {
    const p = asObject(s.stream, 'flow stream')
    return {
      type: 'stream',
      from: endpoint(asString(p.from, 'stream.from'), 'stream'),
      to: endpoint(asString(p.to, 'stream.to'), 'stream'),
      color: optColor(p.color),
    }
  }
  if ('working' in s) {
    const p = asObject(s.working, 'flow working')
    return { type: 'working', node: node(asString(p.node, 'working.node'), 'working'), color: optColor(p.color) }
  }
  if ('idle' in s) {
    const p = asObject(s.idle, 'flow idle')
    return { type: 'idle', node: node(asString(p.node, 'idle.node'), 'idle') }
  }
  if ('fail' in s) {
    const p = asObject(s.fail, 'flow fail')
    return {
      type: 'fail',
      node: node(asString(p.node, 'fail.node'), 'fail'),
      text: optString(p.text),
      color: optColor(p.color),
    }
  }
  if ('phase' in s) {
    return { type: 'phase', label: asString(s.phase, 'flow phase') }
  }
  if ('wait' in s) {
    return { type: 'wait', seconds: optNumber(s.wait, 'flow wait') ?? 0.5 }
  }
  if ('reset' in s) {
    return { type: 'reset' }
  }
  if ('parallel' in s) {
    const steps = asArray(s.parallel, 'flow parallel').map((p) => parseStep(asObject(p, 'parallel step'), nodeIds, groupIds))
    return { type: 'parallel', steps }
  }
  throw new BeckError(
    'A flow step must have one of: packet, status, highlight, pulse, activate, stream, working, idle, fail, phase, wait, reset, parallel',
  )
}

function buildFlow(f: Record<string, unknown>, nodeIds: Set<string>, groupIds: Set<string>): FlowModel {
  const steps = asArray(f.steps, 'flow.steps').map((s) => parseStep(asObject(s, 'flow step'), nodeIds, groupIds))
  return {
    repeat: optNumber(f.repeat, 'flow.repeat') ?? -1,
    repeatDelay: optNumber(f.repeatDelay, 'flow.repeatDelay') ?? 1.5,
    steps,
    derived: false,
  }
}

/** Validate and normalize a raw (parsed-YAML) object into a full DiagramModel. */
export function buildModel(raw: unknown): DiagramModel {
  const root = asObject(raw, 'document')

  const meta = buildMeta(asObject(root.meta, 'meta'))

  const rawNodes = asArray(root.nodes, 'nodes')
  if (rawNodes.length === 0) throw new BeckError('A diagram needs at least one node under `nodes`')

  const nodes: NodeModel[] = []
  const nodeIds = new Set<string>()
  for (const rn of rawNodes) {
    const n = buildNode(asObject(rn, 'node'))
    if (nodeIds.has(n.id)) throw new BeckError(`Duplicate node id "${n.id}"`)
    nodeIds.add(n.id)
    nodes.push(n)
  }

  const groups = buildGroups(asArray(root.groups, 'groups'), nodes, nodeIds)

  const validTargets = new Set<string>([...nodeIds, ...groups.map((g) => g.id)])
  const edges = buildEdges(asArray(root.edges, 'edges'), validTargets)

  const groupIdSet = new Set(groups.map((g) => g.id))
  const flow = root.flow != null ? buildFlow(asObject(root.flow, 'flow'), nodeIds, groupIdSet) : deriveFlow(nodes, edges)

  // `meta.loop: false` disables looping regardless of how the flow was authored
  // (an explicit flow.repeat otherwise wins; the default is infinite).
  if (!meta.loop) flow.repeat = 0

  return { meta, nodes, groups, edges, flow }
}
