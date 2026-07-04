import { BeckError } from '../util/errors'
import { asArray, asObject, asString, oneOf, optColor, optString, triBool } from './coerce'
import { EDGE_KIND_DEFAULTS } from './defaults'
import { buildFlow, buildMeta, buildNode } from './validate'
import type { DiagramModel, EdgeModel, FlowModel, FlowStep, NodeModel, SectionMark } from './schema'

const MESSAGE_KINDS = ['data', 'control', 'async', 'dependency'] as const

/**
 * `type: sequence` — participants across the top, lifelines down, messages in
 * authored order. Messages compile to EdgeModel entries (one per row, in order)
 * so the shared animation layer can ride them; the sequence layout/router give
 * each one its horizontal path. `- section: Label` entries between messages
 * become full-width band labels (and phases in the derived flow).
 */
export function buildSequenceModel(root: Record<string, unknown>): DiagramModel {
  const meta = buildMeta(asObject(root.meta, 'meta'), 'sequence')

  const rawParts = asArray(root.participants, 'participants')
  if (rawParts.length === 0)
    throw new BeckError('A sequence diagram needs at least one participant under `participants`')

  const nodes: NodeModel[] = []
  const ids = new Set<string>()
  for (const rp of rawParts) {
    const n = buildNode(asObject(rp, 'participant'))
    if (ids.has(n.id)) throw new BeckError(`Duplicate participant id "${n.id}"`)
    ids.add(n.id)
    nodes.push(n)
  }

  const edges: EdgeModel[] = []
  const sections: SectionMark[] = []
  for (const rm of asArray(root.messages, 'messages')) {
    const m = asObject(rm, 'message')
    if ('section' in m) {
      sections.push({ label: asString(m.section, 'message section'), at: edges.length })
      continue
    }
    const from = asString(m.from, 'message.from')
    const to = asString(m.to, 'message.to')
    if (!ids.has(from)) throw new BeckError(`Message references unknown participant "${from}"`)
    if (!ids.has(to)) throw new BeckError(`Message references unknown participant "${to}"`)
    const reply = m.reply === true
    const kind = oneOf(m.kind, MESSAGE_KINDS, 'message.kind', reply ? 'control' : 'data')
    // Replies and async sends read as "lighter": dashed line, open arrowhead.
    const dashed = reply || kind === 'async'
    edges.push({
      id: `msg${edges.length}`,
      from,
      to,
      label: optString(m.label),
      style: oneOf(m.style, ['solid', 'dashed'] as const, 'message.style', dashed ? 'dashed' : 'solid'),
      curve: 'straight',
      kind,
      color: optColor(m.color) ?? EDGE_KIND_DEFAULTS[kind].color,
      arrow: 'end',
      markerEnd: dashed ? 'arrow-open' : 'arrow',
      reply,
      activate: triBool(m.activate, 'message.activate'),
    })
  }
  if (edges.length === 0) throw new BeckError('A sequence diagram needs at least one entry under `messages`')

  const flow =
    root.flow != null
      ? buildFlow(asObject(root.flow, 'flow'), ids, new Set())
      : deriveSequenceFlow(edges, sections)
  if (!meta.loop) flow.repeat = 0

  return { meta, nodes, groups: [], edges, flow, sections }
}

/**
 * The authored message order IS the story: one packet per message, in order,
 * with section labels as phases. Replies land green so request/response pairs
 * read at a glance.
 */
function deriveSequenceFlow(edges: EdgeModel[], sections: SectionMark[]): FlowModel {
  const steps: FlowStep[] = []
  edges.forEach((e, i) => {
    for (const s of sections) if (s.at === i) steps.push({ type: 'phase', label: s.label })
    steps.push({
      type: 'packet',
      from: e.from,
      to: e.to,
      edge: e.id,
      color: e.reply ? 'var(--beck-success)' : undefined,
      ease: e.reply ? 'decelerate' : undefined,
    })
  })
  steps.push({ type: 'wait', seconds: 1.2 })
  steps.push({ type: 'reset' })
  return { repeat: -1, repeatDelay: 2, steps, derived: true }
}
