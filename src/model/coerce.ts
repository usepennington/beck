import { BeckError } from '../util/errors'
import { accentToCss } from '../util/color'

// Low-level coercion helpers shared by every diagram-type builder. Each throws
// a BeckError with a friendly field path instead of letting a TypeError leak.

export function asObject(v: unknown, field: string): Record<string, unknown> {
  if (v == null) return {}
  if (typeof v !== 'object' || Array.isArray(v)) throw new BeckError(`\`${field}\` must be a mapping`)
  return v as Record<string, unknown>
}

export function asArray(v: unknown, field: string): unknown[] {
  if (v == null) return []
  if (!Array.isArray(v)) throw new BeckError(`\`${field}\` must be a list`)
  return v
}

export function asString(v: unknown, field: string): string {
  if (typeof v === 'string') return v
  if (typeof v === 'number' || typeof v === 'boolean') return String(v)
  throw new BeckError(`\`${field}\` must be a string`)
}

export function optString(v: unknown): string | undefined {
  if (v == null) return undefined
  if (typeof v === 'string') return v
  if (typeof v === 'number' || typeof v === 'boolean') return String(v)
  return undefined
}

export function optColor(v: unknown): string | undefined {
  const s = optString(v)
  return s == null ? undefined : accentToCss(s, 'primary')
}

export function optNumber(v: unknown, field: string): number | undefined {
  if (v == null) return undefined
  if (typeof v === 'number') return v
  if (typeof v === 'string' && v.trim() !== '' && !Number.isNaN(Number(v))) return Number(v)
  throw new BeckError(`\`${field}\` must be a number`)
}

export function optBool(v: unknown, field: string, dflt: boolean): boolean {
  if (v == null) return dflt
  if (typeof v === 'boolean') return v
  if (v === 'true') return true
  if (v === 'false') return false
  throw new BeckError(`\`${field}\` must be true or false`)
}

/** Tri-state boolean: undefined when unset (so a heuristic can decide later). */
export function triBool(v: unknown, field: string): boolean | undefined {
  if (v == null) return undefined
  return optBool(v, field, false)
}

export function oneOf<T extends string>(v: unknown, allowed: readonly T[], field: string, dflt: T): T {
  if (v == null) return dflt
  const s = String(v)
  if ((allowed as readonly string[]).includes(s)) return s as T
  throw new BeckError(`\`${field}\` must be one of: ${allowed.join(', ')} (got "${s}")`)
}

/** Coerce a list of scalars (class fields/methods and similar line lists). */
export function stringList(v: unknown, field: string): string[] {
  return asArray(v, field).map((s) => asString(s, field))
}
