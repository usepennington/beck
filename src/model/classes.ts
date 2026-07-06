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
import type { DiagramModel, EdgeModel, FlowModel, NodeModel } from './schema'

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
  // Class diagrams are structural reference material, not a narrative — there is
  // no sequence of events to play, so nothing is auto-derived. An author who
  // deliberately writes a `flow:` block still gets it; otherwise we render a
  // still frame and never load the animation runtime.
  let flow: FlowModel
  if (root.flow != null) {
    flow = buildFlow(asObject(root.flow, 'flow'), ids, groupIdSet)
    if (!meta.loop) flow.repeat = 0
  } else {
    flow = { repeat: 0, repeatDelay: 0, steps: [], derived: false }
    meta.animate = false
  }

  return { meta, nodes, groups, edges, flow, sections: [] }
}
