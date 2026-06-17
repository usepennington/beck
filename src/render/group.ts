import type { GroupModel } from '../model/schema'

export interface RenderedGroup {
  box: HTMLElement
  label: HTMLElement | null
}

/**
 * Build a group's box + label. Positioned later by the mount stage (the box is
 * absolutely placed as the bounding rect of its members + padding). The group's
 * accent tints the dashed border and the label.
 */
export function createGroup(group: GroupModel): RenderedGroup {
  const box = document.createElement('div')
  box.className = 'beck-group'
  box.dataset.group = group.id
  box.style.setProperty('--beck-group-border', `color-mix(in srgb, ${group.accent} 45%, transparent)`)

  // The label is returned detached; the caller positions it on the canvas above
  // the edges (a child of the box would sit behind them in stacking order).
  let label: HTMLElement | null = null
  if (group.label) {
    label = document.createElement('div')
    label.className = 'beck-group-label'
    label.textContent = group.label
    label.style.color = group.accent
  }

  return { box, label }
}
