import type { Timeline } from './runtime'
import { withAlpha } from '../util/color'

export interface StatusOptions {
  color?: string
}

const initialPillState = new Map<HTMLElement, { text: string; color: string; backgroundColor: string; display: string }>()

/** Update a node's status pill text + color. Persists until changed again. */
export function statusPill(
  tl: Timeline,
  nodeEl: HTMLElement,
  text: string,
  opts: StatusOptions = {},
  position?: string | number,
): void {
  const color = opts.color || 'var(--beck-primary)'

  if (!initialPillState.has(nodeEl)) {
    const pill = nodeEl.querySelector('.beck-status, .beck-status-inline') as HTMLElement | null
    if (pill) {
      initialPillState.set(nodeEl, {
        text: pill.textContent || '',
        color: pill.style.color || '',
        backgroundColor: pill.style.backgroundColor || '',
        display: pill.style.display || '',
      })
    }
  }

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

/** Reset all status pills to their captured initial state. */
export function resetStatusPills(tl: Timeline, position?: string | number): void {
  tl.call(
    () => {
      for (const [nodeEl, state] of initialPillState) {
        const pill = nodeEl.querySelector('.beck-status') as HTMLElement | null
        if (pill) {
          pill.textContent = state.text
          pill.style.color = state.color
          pill.style.backgroundColor = state.backgroundColor
          pill.style.display = state.display
        }
        const inline = nodeEl.querySelector('.beck-status-inline') as HTMLElement | null
        if (inline) {
          inline.textContent = state.text
          inline.style.color = state.color
          inline.style.display = state.display
        }
      }
    },
    [],
    position,
  )
}
