import type { AccentToken } from '../model/schema'

const ACCENT_TOKENS: readonly string[] = [
  'primary',
  'success',
  'warn',
  'danger',
  'info',
  'neutral',
]

export function isAccentToken(value: string): value is AccentToken {
  return ACCENT_TOKENS.includes(value)
}

/**
 * Map an accent input to a CSS color value. A known token name becomes a
 * `var(--beck-<token>)` reference; anything else (a hex/rgb/named color) is
 * passed through verbatim. `undefined` falls back to the given token.
 */
export function accentToCss(accent: string | undefined, fallback: AccentToken): string {
  if (!accent) return `var(--beck-${fallback})`
  if (isAccentToken(accent)) return `var(--beck-${accent})`
  return accent
}

/** A translucent version of a color, safe for `rgb()`/`var()`/hex inputs alike. */
export function withAlpha(color: string, percent: number): string {
  return `color-mix(in srgb, ${color} ${percent}%, transparent)`
}

/**
 * Resolve a CSS color value (including `var(--beck-*)`) to a concrete computed
 * color string. GSAP needs a real color; this lets the var indirection stay
 * authoritative while animations honor the live theme. The probe is attached
 * inside `contextEl` so it inherits the same custom-property scope.
 */
export function resolveColor(contextEl: Element, value: string): string {
  const probe = document.createElement('span')
  probe.style.setProperty('color', value)
  probe.style.position = 'absolute'
  probe.style.width = '0'
  probe.style.height = '0'
  probe.style.opacity = '0'
  probe.style.pointerEvents = 'none'
  contextEl.appendChild(probe)
  const resolved = getComputedStyle(probe).color
  probe.remove()
  return resolved || value
}
