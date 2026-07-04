import { BeckError } from '../util/errors'
import { accentToCss } from '../util/color'
import {
  asArray,
  asObject,
  asString,
  oneOf,
  optColor,
  optNumber,
  optString,
  stringList,
} from './coerce'
import { buildFlow, buildGroups, buildMeta } from './validate'
import type { DiagramModel, EdgeModel, FlowModel, FlowStep, NodeModel } from './schema'

const RELATION_KINDS = [
  'inherits',
  'implements',
  'association',
  'aggregation',
  'composition',
  'dependency',
] as const
type RelationKind = (typeof RELATION_KINDS)[number]

function classNode(c: Record<string, unknown>): NodeModel {
  const id = asString(c.id, 'class.id')
  return {
    id,
    // `name` is the natural word for a class; `title` also accepted.
    title: optString(c.name) ?? optString(c.title) ?? id,
    subtitle: optString(c.subtitle),
    kind: 'service',
    variant: 'solid',
    accent: accentToCss(optString(c.accent), 'primary'),
    href: optString(c.href),
    target: optString(c.target),
    width: optNumber(c.width, `class "${id}" width`),
    rank: optNumber(c.rank, `class "${id}" rank`),
    order: optNumber(c.order, `class "${id}" order`),
    group: optString(c.group),
    shape: 'class',
    stereotype: optString(c.stereotype),
    fields: stringList(c.fields, `class "${id}" fields`),
    methods: stringList(c.methods, `class "${id}" methods`),
  }
}

/**
 * `type: class` — UML class diagrams on the layered engine. Classes are
 * multi-compartment cards; relations compile to edges with UML end markers.
 *
 * Authoring conventions (all `from` → `to`):
 * - `inherits` / `implements`: from the CHILD to the PARENT (hollow triangle at
 *   the parent). Internally the edge is flipped so parents rank above children.
 * - `aggregation` / `composition`: from the WHOLE to the PART (diamond at the
 *   whole).
 * - `association` / `dependency`: source to target.
 * `fromCard` / `toCard` are multiplicity annotations at the authored ends.
 */
export function buildClassModel(root: Record<string, unknown>): DiagramModel {
  const meta = buildMeta(asObject(root.meta, 'meta'), 'class')

  const rawClasses = asArray(root.classes, 'classes')
  if (rawClasses.length === 0) throw new BeckError('A class diagram needs at least one entry under `classes`')

  const nodes: NodeModel[] = []
  const ids = new Set<string>()
  for (const rc of rawClasses) {
    const n = classNode(asObject(rc, 'class'))
    if (ids.has(n.id)) throw new BeckError(`Duplicate class id "${n.id}"`)
    ids.add(n.id)
    nodes.push(n)
  }

  const groups = buildGroups(asArray(root.groups, 'groups'), nodes, ids)

  const edges: EdgeModel[] = []
  for (const rr of asArray(root.relations, 'relations')) {
    const r = asObject(rr, 'relation')
    const from = asString(r.from, 'relation.from')
    const to = asString(r.to, 'relation.to')
    if (!ids.has(from)) throw new BeckError(`Relation references unknown class "${from}"`)
    if (!ids.has(to)) throw new BeckError(`Relation references unknown class "${to}"`)
    const kind = oneOf(r.kind, RELATION_KINDS, 'relation.kind', 'association') as RelationKind
    const label = optString(r.label)
    const fromCard = optString(r.fromCard)
    const toCard = optString(r.toCard)
    const color = optColor(r.color)
    const i = edges.length

    const base = {
      label,
      curve: 'step-round' as const,
      arrow: 'none' as const,
      reply: false,
    }
    switch (kind) {
      case 'inherits':
      case 'implements':
        // Flip so the parent ranks above the child; the hollow triangle sits at
        // the edge START (the parent end) via `orient: auto-start-reverse`.
        edges.push({
          ...base,
          id: `${to}->${from}#${i}`,
          from: to,
          to: from,
          style: kind === 'implements' ? 'dashed' : 'solid',
          kind: 'data',
          color: color ?? 'var(--beck-neutral)',
          markerStart: 'triangle',
          fromLabel: toCard,
          toLabel: fromCard,
        })
        break
      case 'aggregation':
      case 'composition':
        edges.push({
          ...base,
          id: `${from}->${to}#${i}`,
          from,
          to,
          style: 'solid',
          kind: 'data',
          color: color ?? 'var(--beck-neutral)',
          markerStart: kind === 'composition' ? 'diamond' : 'diamond-open',
          fromLabel: fromCard,
          toLabel: toCard,
        })
        break
      case 'dependency':
        edges.push({
          ...base,
          id: `${from}->${to}#${i}`,
          from,
          to,
          style: 'dashed',
          kind: 'dependency',
          color: color ?? 'var(--beck-neutral)',
          markerEnd: 'arrow-open',
          fromLabel: fromCard,
          toLabel: toCard,
        })
        break
      default:
        edges.push({
          ...base,
          id: `${from}->${to}#${i}`,
          from,
          to,
          style: 'solid',
          kind: 'data',
          color: color ?? 'var(--beck-neutral)',
          arrow: r.arrow === false ? 'none' : 'end',
          fromLabel: fromCard,
          toLabel: toCard,
        })
    }
  }

  const groupIdSet = new Set(groups.map((g) => g.id))
  const flow =
    root.flow != null
      ? buildFlow(asObject(root.flow, 'flow'), ids, groupIdSet)
      : deriveClassFlow(nodes, edges)
  if (!meta.loop) flow.repeat = 0

  return { meta, nodes, groups, edges, flow, sections: [] }
}

/**
 * Class diagrams are structural, so the derived animation is a quiet cascade
 * rather than a packet story: each inheritance level lights up in turn, with
 * the relations into that level recoloring as it does.
 */
function deriveClassFlow(nodes: NodeModel[], edges: EdgeModel[]): FlowModel {
  // Longest-path rank over the compiled edges (which already point top→bottom).
  const rank = new Map<string, number>(nodes.map((n) => [n.id, 0]))
  const indeg = new Map<string, number>(nodes.map((n) => [n.id, 0]))
  const out = new Map<string, string[]>(nodes.map((n) => [n.id, []]))
  for (const e of edges) {
    if (e.from === e.to || !rank.has(e.from) || !rank.has(e.to)) continue
    out.get(e.from)!.push(e.to)
    indeg.set(e.to, (indeg.get(e.to) ?? 0) + 1)
  }
  const queue = nodes.filter((n) => (indeg.get(n.id) ?? 0) === 0).map((n) => n.id)
  while (queue.length) {
    const u = queue.shift()!
    for (const v of out.get(u) ?? []) {
      rank.set(v, Math.max(rank.get(v) ?? 0, (rank.get(u) ?? 0) + 1))
      indeg.set(v, (indeg.get(v) ?? 1) - 1)
      if ((indeg.get(v) ?? 0) === 0) queue.push(v)
    }
  }
  const maxRank = Math.max(0, ...[...rank.values()])
  const edgeRank = (e: EdgeModel) => Math.max(rank.get(e.from) ?? 0, rank.get(e.to) ?? 0)

  const steps: FlowStep[] = []
  for (let r = 0; r <= maxRank; r++) {
    const level: FlowStep[] = []
    for (const e of edges) if (edgeRank(e) === r) level.push({ type: 'activate', from: e.from, to: e.to })
    for (const n of nodes) if ((rank.get(n.id) ?? 0) === r) level.push({ type: 'highlight', node: n.id })
    if (level.length) {
      steps.push({ type: 'parallel', steps: level })
      steps.push({ type: 'wait', seconds: 0.4 })
    }
  }
  if (steps.length) {
    steps.push({ type: 'wait', seconds: 1.4 })
    steps.push({ type: 'reset' })
  }
  return { repeat: -1, repeatDelay: 2.5, steps, derived: true }
}
