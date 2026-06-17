import type { Timeline } from './runtime'
import { withAlpha } from '../util/color'

export interface StatusOptions {
  color?: string
}

// Pill reset is handled centrally by Snapshot (it captures every pill's initial
// state), so this module no longer tracks its own.

/** Update a node's status pill text + color. Persists until changed again. */
export function statusPill(
  tl: Timeline,
  nodeEl: HTMLElement,
  text: string,
  opts: StatusOptions = {},
  position?: string | number,
): void {
  const color = opts.color || 'var(--beck-primary)'

  tl.call(
    () => {
      const pill = nodeEl.querySelector('.beck-status') as HTMLElement | null
      if (pill) {
        pill.style.display = ''
        pill.textContent = text
        pill.style.color = color
        pill.style.backgroundColor = withAlpha(color, 14)
      }
      const inline = nodeEl.querySelector('.beck-status-inline') as HTMLElement | null
      if (inline) {
        inline.style.display = ''
        inline.textContent = text
        inline.style.color = color
      }
    },
    [],
    position,
  )
}
