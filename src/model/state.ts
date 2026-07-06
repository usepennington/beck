import { BeckError } from '../util/errors'
import { accentToCss } from '../util/color'
import { asArray, asObject, asString, oneOf, optColor, optNumber, optString } from './coerce'
import { deriveFlow } from './defaults'
import { buildFlow, buildMeta } from './validate'
import type { DiagramModel, EdgeModel, NodeModel } from './schema'

/** The UML entry/exit pseudo-state token, and the internal node ids it maps to. */
const PSEUDO = '[*]'
const START_ID = '#start'
const END_ID = '#end'

function statePill(s: Record<string, unknown>): NodeModel {
  const id = asString(s.id, 'state.id')
  if (id === PSEUDO || id === START_ID || id === END_ID)
    throw new BeckError(`"${id}" is reserved — reference the start/end pseudo-state from a transition instead`)
  return {
    id,
    title: optString(s.title) ?? id,
    subtitle: optString(s.subtitle),
    kind: 'service',
    variant: 'solid',
    accent: accentToCss(optString(s.accent), 'neutral'),
    href: optString(s.href),
    target: optString(s.target),
    surface: optString(s.surface),
    textColor: optString(s.textColor),
    width: optNumber(s.width, `state "${id}" width`),
    rank: optNumber(s.rank, `state "${id}" rank`),
    order: optNumber(s.order, `state "${id}" order`),
    shape: 'pill',
    fields: [],
    methods: [],
  }
}

function pseudoNode(id: string): NodeModel {
  return {
    id,
    title: '',
    kind: 'service',
    variant: 'solid',
    accent: 'var(--beck-text)',
    shape: id === START_ID ? 'start' : 'end',
    fields: [],
    methods: [],
  }
}

/**
 * `type: state` — a state machine on the layered engine. States are pills;
 * transitions are edges; `"[*]"` in a transition is the UML entry/exit
 * pseudo-state (a filled dot / bullseye). States referenced only by transitions
 * are auto-created, so a terse machine needs nothing but `transitions:`.
 */
export function buildStateModel(root: Record<string, unknown>): DiagramModel {
  const meta = buildMeta(asObject(root.meta, 'meta'), 'state')

  // Declared states are collected first but *pushed* in first-reference order
  // (walking the transitions), so the layered engine's rank/cycle-break sees the
  // machine in narrative order — otherwise a declared-late initial state would
  // rank after the states it leads to and the layout would read backwards.
  const declared = new Map<string, NodeModel>()
  for (const rs of asArray(root.states, 'states')) {
    const n = statePill(asObject(rs, 'state'))
    if (declared.has(n.id)) throw new BeckError(`Duplicate state id "${n.id}"`)
    declared.set(n.id, n)
  }

  const nodes: NodeModel[] = []
  const byId = new Map<string, NodeModel>()
  const add = (n: NodeModel) => {
    byId.set(n.id, n)
    nodes.push(n)
  }

  const ensure = (id: string, ctx: string): string => {
    if (id === PSEUDO) {
      const pid = ctx === 'from' ? START_ID : END_ID
      if (!byId.has(pid)) add(pseudoNode(pid))
      return pid
    }
    if (!byId.has(id)) add(declared.get(id) ?? statePill({ id }))
    return id
  }

  const edges: EdgeModel[] = []
  for (const rt of asArray(root.transitions, 'transitions')) {
    const t = asObject(rt, 'transition')
    const from = ensure(asString(t.from, 'transition.from'), 'from')
    const to = ensure(asString(t.to, 'transition.to'), 'to')
    edges.push({
      id: `${from}->${to}#${edges.length}`,
      from,
      to,
      label: optString(t.label),
      style: oneOf(t.style, ['solid', 'dashed'] as const, 'transition.style', 'solid'),
      curve: 'step-round',
      kind: 'control',
      color: optColor(t.color) ?? 'var(--beck-edge)',
      arrow: 'end',
      note: optString(t.note),
      reply: false,
    })
  }
  // Declared states never referenced by a transition still render.
  for (const [id, n] of declared) if (!byId.has(id)) add(n)
  if (nodes.length === 0)
    throw new BeckError('A state diagram needs at least one entry under `states` or `transitions`')

  const flow =
    root.flow != null
      ? buildFlow(asObject(root.flow, 'flow'), new Set(byId.keys()), new Set())
      : deriveFlow(nodes, edges)
  if (!meta.loop) flow.repeat = 0

  return { meta, nodes, groups: [], edges, flow, sections: [] }
}
