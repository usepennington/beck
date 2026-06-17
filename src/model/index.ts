import { parseYaml } from './parse'
import { buildModel } from './validate'
import type { DiagramModel } from './schema'

export * from './schema'
export { parseYaml } from './parse'
export { buildModel } from './validate'
export { deriveFlow, topoOrder, KIND_DEFAULTS, EDGE_KIND_DEFAULTS, DEFAULT_SPACING } from './defaults'

/** Parse + validate a YAML diagram source into a normalized DiagramModel. */
export function loadDiagram(src: string): DiagramModel {
  return buildModel(parseYaml(src))
}
