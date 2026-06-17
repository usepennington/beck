import yaml from 'js-yaml'
import { BeckError } from '../util/errors'

/** Parse a YAML diagram source into a raw object (defaults/validation come later). */
export function parseYaml(src: string): unknown {
  try {
    return yaml.load(src) ?? {}
  } catch (err) {
    const e = err as { message?: string; mark?: { line?: number } }
    const line = e.mark?.line != null ? e.mark.line + 1 : undefined
    throw new BeckError(`YAML parse error: ${e.message ?? String(err)}`, line)
  }
}
