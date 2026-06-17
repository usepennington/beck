import { resolveIcon } from './icons'
import type { NodeModel } from '../model/schema'

export interface RenderedNode {
  /** Outer wrapper used for layout positioning. */
  wrap: HTMLElement
  /** The card element (the visual box; edge anchors resolve to this). */
  card: HTMLElement
  /** The status pill (always present; hidden when there is no status). */
  pill: HTMLElement
}

function setIcon(host: HTMLElement, node: NodeModel): void {
  const markup = resolveIcon(node.icon)
  if (!markup) return
  host.innerHTML = markup
}

/** Build a node's DOM. Theming is entirely via the `--beck-accent` var + classes. */
export function createNode(node: NodeModel): RenderedNode {
  const wrap = document.createElement('div')
  wrap.className = 'beck-node-wrap'
  wrap.dataset.node = node.id

  const card = document.createElement('div')
  card.className = 'beck-node'
  card.style.setProperty('--beck-accent', node.accent)
  if (node.width) card.style.width = `${node.width}px`

  const isGhost = node.variant === 'ghost' || node.kind === 'ghost'
  if (isGhost) card.classList.add('beck-node--ghost')
  else if (node.kind === 'external') card.classList.add('beck-node--external')
  if (node.variant === 'subtle') card.classList.add('beck-node--subtle')

  let pill: HTMLElement

  if (isGhost) {
    // Compact pill: [icon] label, with an inline status line below.
    const row = document.createElement('div')
    row.className = 'beck-ghost-row'

    const iconMarkup = resolveIcon(node.icon)
    if (iconMarkup) {
      const icon = document.createElement('span')
      icon.className = 'beck-icon'
      icon.innerHTML = iconMarkup
      row.appendChild(icon)
    }

    const label = document.createElement('span')
    label.className = 'beck-ghost-label'
    label.textContent = node.title
    row.appendChild(label)
    card.appendChild(row)

    pill = document.createElement('span')
    pill.className = 'beck-status-inline'
    if (node.status) pill.textContent = node.status
    else pill.style.display = 'none'
    card.appendChild(pill)
  } else {
    const icon = document.createElement('div')
    icon.className = 'beck-icon'
    setIcon(icon, node)
    if (icon.childElementCount > 0) card.appendChild(icon)

    const text = document.createElement('div')
    text.className = 'beck-node-text'

    const title = document.createElement('div')
    title.className = 'beck-node-title'
    title.textContent = node.title
    text.appendChild(title)

    if (node.subtitle) {
      const sub = document.createElement('div')
      sub.className = 'beck-node-subtitle'
      sub.textContent = node.subtitle
      text.appendChild(sub)
    }

    pill = document.createElement('span')
    pill.className = 'beck-status'
    if (node.status) pill.textContent = node.status
    else pill.style.display = 'none'
    text.appendChild(pill)

    card.appendChild(text)
  }

  wrap.appendChild(card)
  return { wrap, card, pill }
}
