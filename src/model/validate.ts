import { BeckError } from '../util/errors'
import { accentToCss } from '../util/color'
import { isKnownIcon } from '../render/icons'
import {
  DEFAULT_SPACING,
  EDGE_KIND_DEFAULTS,
  KIND_DEFAULTS,
  PACKET_EASES,
  PACKET_SHAPES,
  deriveFlow,
} from './defaults'
import {
  asArray,
  asObject,
  asString,
  oneOf,
  optBool,
  optColor,
  optNumber,
  optString,
} from './coerce'
import { buildSequenceModel } from './sequence'
import { buildStateModel } from './state'
import { buildClassModel } from './classes'
import type {
  DiagramMeta,
  DiagramModel,
  DiagramType,
  EdgeModel,
  FlowModel,
  FlowStep,
  GroupModel,
  NodeKind,
  NodeModel,
  PacketKnobs,
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
const TYPE_LIST = ['architecture', 'sequence', 'state', 'class'] as const

// ---- builders shared across diagram types ----

export function buildMeta(m: Record<string, unknown>, type: DiagramType): DiagramMeta {
  const sp = asObject(m.spacing, 'meta.spacing')
  return {
    type,
    title: optString(m.title),
    subtitle: optString(m.subtitle),
    direction: oneOf(m.direction, ['TB', 'BT', 'LR', 'RL'] as const, 'meta.direction', 'TB'),
    theme: oneOf(m.theme, ['auto', 'light', 'dark'] as const, 'meta.theme', 'auto'),
    animate: optBool(m.animate, 'meta.animate', true),
    loop: optBool(m.loop, 'meta.loop', true),
    fit: oneOf(m.fit, ['shrink', 'scroll'] as const, 'meta.fit', 'shrink'),
    spacing: {
      rank: optNumber(sp.rank, 'meta.spacing.rank') ?? DEFAULT_SPACING.rank,
      node: optNumber(sp.node, 'meta.spacing.node') ?? DEFAULT_SPACING.node,
      cornerRadius:
        optNumber(sp.cornerRadius, 'meta.spacing.cornerRadius') ?? DEFAULT_SPACING.cornerRadius,
    },
  }
}

export function buildNode(n: Record<string, unknown>): NodeModel {
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
    shape: 'card',
    fields: [],
    methods: [],
  }
}

export function buildGroups(
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

  // Pass 1: register every explicit group + any inline `node.group`, so members
  // may reference a (possibly nested) group declared later in the list.
  for (const rg of rawGroups) {
    const g = asObject(rg, 'group')
    ensure(asString(g.id, 'group.id'), optString(g.label), optString(g.accent))
  }
  for (const n of nodes) if (n.group) ensure(n.group)

  const groupIds = new Set(groups.keys())
  const isMember = (mid: string) => nodeIds.has(mid) || groupIds.has(mid)

  // Pass 2: members may be node ids OR group ids (nesting).
  for (const rg of rawGroups) {
    const g = asObject(rg, 'group')
    const id = asString(g.id, 'group.id')
    const grp = groups.get(id)!
    for (const m of asArray(g.members, `group "${id}" members`)) {
      const mid = asString(m, `group "${id}" member`)
      if (!isMember(mid)) throw new BeckError(`Group "${id}" references unknown node or group "${mid}"`)
      if (mid === id) throw new BeckError(`Group "${id}" cannot contain itself`)
      if (!grp.members.includes(mid)) grp.members.push(mid)
    }
  }

  // Inline `node.group` membership.
  for (const n of nodes) {
    if (!n.group) continue
    const grp = groups.get(n.group)!
    if (!grp.members.includes(n.id)) grp.members.push(n.id)
  }

  // Each node/group belongs to at most one parent (membership is a tree).
  const parentOf = new Map<string, string>()
  for (const id of order) {
    for (const m of groups.get(id)!.members) {
      const prev = parentOf.get(m)
      if (prev) throw new BeckError(`"${m}" is in two groups ("${prev}" and "${id}")`)
      parentOf.set(m, id)
    }
  }

  // No cycles: a group cannot be its own ancestor.
  for (const id of order) {
    let cur = parentOf.get(id)
    let guard = 0
    while (cur) {
      if (cur === id) throw new BeckError(`Group "${id}" is nested inside itself`)
      cur = parentOf.get(cur)
      if (++guard > order.length + 1) break
    }
  }

  return order.map((id) => groups.get(id)!)
}

const ARROW_ENDS = ['none', 'end', 'start', 'both'] as const

/** Arrowheads: accept the legacy bool (true→end, false→none) or an end token. */
function arrowEnds(v: unknown): 'none' | 'end' | 'start' | 'both' {
  if (v == null || v === true) return 'end'
  if (v === false) return 'none'
  return oneOf(v, ARROW_ENDS, 'edge.arrow', 'end')
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
      arrow: arrowEnds(e.arrow),
      fromSide: e.fromSide != null ? (oneOf(e.fromSide, SIDES, 'edge.fromSide', 'bottom') as Side) : undefined,
      toSide: e.toSide != null ? (oneOf(e.toSide, SIDES, 'edge.toSide', 'top') as Side) : undefined,
      reply: false,
    }
  })
}

/** Shared `packet`/`burst` motion knobs — each left undefined when unset so the
 *  animator can fall back to the edge-kind defaults. */
function packetKnobs(p: Record<string, unknown>): PacketKnobs {
  return {
    shape: p.shape == null ? undefined : oneOf(p.shape, PACKET_SHAPES, 'packet.shape', 'dot'),
    size: optNumber(p.size, 'packet.size'),
    speed: optNumber(p.speed, 'packet.speed'),
    glow: p.glow == null ? undefined : optBool(p.glow, 'packet.glow', true),
    impact: p.impact == null ? undefined : optBool(p.impact, 'packet.impact', false),
    ease: p.ease == null ? undefined : oneOf(p.ease, PACKET_EASES, 'packet.ease', 'linear'),
  }
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
      edge: optString(p.edge),
      color: optColor(p.color),
      label: optString(p.label),
      ...packetKnobs(p),
    }
  }
  if ('burst' in s) {
    const p = asObject(s.burst, 'flow burst')
    const to = Array.isArray(p.to)
      ? p.to.map((v) => endpoint(asString(v, 'burst.to'), 'burst to'))
      : endpoint(asString(p.to, 'burst.to'), 'burst')
    const via = asArray(p.via, 'burst.via').map((v) => endpoint(asString(v, 'burst.via'), 'burst via'))
    // Clamp count to a sane range so one step can't spawn an unbounded fleet.
    const count = Math.max(1, Math.min(24, Math.round(optNumber(p.count, 'burst.count') ?? 3)))
    const stagger = Math.max(0, optNumber(p.stagger, 'burst.stagger') ?? 0.12)
    return {
      type: 'burst',
      from: endpoint(asString(p.from, 'burst.from'), 'burst'),
      to,
      via: via.length ? via : undefined,
      count,
      stagger,
      color: optColor(p.color),
      label: optString(p.label),
      ...packetKnobs(p),
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
    'A flow step must have one of: packet, burst, status, highlight, pulse, activate, stream, working, idle, fail, phase, wait, reset, parallel',
  )
}

export function buildFlow(f: Record<string, unknown>, nodeIds: Set<string>, groupIds: Set<string>): FlowModel {
  const steps = asArray(f.steps, 'flow.steps').map((s) => parseStep(asObject(s, 'flow step'), nodeIds, groupIds))
  return {
    repeat: optNumber(f.repeat, 'flow.repeat') ?? -1,
    repeatDelay: optNumber(f.repeatDelay, 'flow.repeatDelay') ?? 1.5,
    steps,
    derived: false,
  }
}

// ---- the architecture builder (the original Beck diagram type) ----

function buildArchitectureModel(root: Record<string, unknown>): DiagramModel {
  const meta = buildMeta(asObject(root.meta, 'meta'), 'architecture')

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

  return { meta, nodes, groups, edges, flow, sections: [] }
}

let warnedUntyped = false

/** Validate and normalize a raw (parsed-YAML) object into a full DiagramModel. */
export function buildModel(raw: unknown): DiagramModel {
  const root = asObject(raw, 'document')

  let type: DiagramType
  if (root.type == null) {
    // Legacy untyped documents render as architecture diagrams, but the explicit
    // root `type:` is the only documented syntax — nudge (once per page load).
    type = 'architecture'
    if (!warnedUntyped && typeof console !== 'undefined') {
      warnedUntyped = true
      console.warn(
        'Beck: document has no root `type:` — rendering as `type: architecture`. ' +
          'Untyped documents are deprecated; declare `type: architecture` (or sequence / state / class).',
      )
    }
  } else {
    type = oneOf(root.type, TYPE_LIST, 'type', 'architecture')
  }

  switch (type) {
    case 'sequence':
      return buildSequenceModel(root)
    case 'state':
      return buildStateModel(root)
    case 'class':
      return buildClassModel(root)
    default:
      return buildArchitectureModel(root)
  }
}
