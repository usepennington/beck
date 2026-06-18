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

/**
 * Prefix a root-relative href (`/foo`) with the deploy base path. Absolute URLs,
 * protocol-relative (`//`), fragments/queries, and already-prefixed paths are left
 * alone, so it is a no-op when `baseUrl` is empty.
 */
function withBase(href: string, baseUrl: string): string {
  if (!baseUrl || !href.startsWith('/') || href.startsWith('//')) return href
  const base = baseUrl.replace(/\/+$/, '')
  if (!base || href === base || href.startsWith(base + '/')) return href
  return base + href
}

/**
 * Structural (color-independent) utility classes, one literal string per element/
 * variant. The host's Tailwind/MonorailCSS generates these — MonorailCSS discovers
 * them by scanning this shipped bundle, so they must stay COMPLETE literals (never
 * concatenated from parts). Everything colour/accent/theme is left to the `--beck-*`
 * var system in styles.css; the `beck-*` class on each element is the behavioural
 * hook the animation layer + router query, and never carries styling here.
 */
const CLS = {
  cardMain: 'relative flex items-center gap-3 min-w-[180px] px-4 py-3.5 rounded-[14px]',
  cardGhost: 'relative flex flex-col items-center gap-[3px] min-w-0 px-3.5 py-2 rounded-2xl',
  iconMain: 'flex items-center justify-center flex-none w-[34px] h-[34px] rounded-[9px]',
  iconGhost: 'flex items-center justify-center flex-none w-4 h-4 rounded-md',
  text: 'flex flex-col gap-[3px] min-w-0 flex-1',
  title: 'text-sm font-semibold leading-[1.3]',
  subtitle: 'text-xs leading-[1.35]',
  status: 'self-start mt-0.5 px-2 py-[3px] rounded-full text-[0.65rem] font-medium leading-[1.2] whitespace-nowrap',
  ghostRow: 'flex items-center gap-[7px]',
  ghostLabel: 'text-[0.72rem] font-medium whitespace-nowrap',
  statusInline: 'text-[0.62rem] font-medium whitespace-nowrap',
}

/** Build a node's DOM. Theming is entirely via the `--beck-accent` var + classes. */
export function createNode(node: NodeModel, baseUrl = ''): RenderedNode {
  const wrap = document.createElement('div')
  wrap.className = 'beck-node-wrap'
  wrap.dataset.node = node.id

  const isGhost = node.variant === 'ghost' || node.kind === 'ghost'

  const card = document.createElement(node.href ? 'a' : 'div')
  card.className = `beck-node ${isGhost ? CLS.cardGhost : CLS.cardMain}`
  if (isGhost) card.classList.add('beck-node--ghost')
  else if (node.kind === 'external') card.classList.add('beck-node--external')
  if (node.variant === 'subtle') card.classList.add('beck-node--subtle')
  if (node.href) {
    const a = card as HTMLAnchorElement
    a.href = withBase(node.href, baseUrl)
    if (node.target) a.target = node.target
    if (node.target === '_blank') a.rel = 'noopener noreferrer'
  }
  card.style.setProperty('--beck-accent', node.accent)
  if (node.width) card.style.width = `${node.width}px`
  if (isGhost) {
    // A ghost paints onto a transparent pill with a muted label, so the --beck-node-bg /
    // --beck-text vars the solid card reads never reach it. Apply the overrides directly:
    // surface as the pill background, textColor onto the label (set where it's created below).
    if (node.surface) card.style.background = node.surface
  } else {
    if (node.surface) card.style.setProperty('--beck-node-bg', node.surface)
    if (node.textColor) card.style.setProperty('--beck-text', node.textColor)
  }

  let pill: HTMLElement

  if (isGhost) {
    // Compact pill: [icon] label, with an inline status line below.
    const row = document.createElement('div')
    row.className = `beck-ghost-row ${CLS.ghostRow}`

    const iconMarkup = resolveIcon(node.icon)
    if (iconMarkup) {
      const icon = document.createElement('span')
      icon.className = `beck-icon ${CLS.iconGhost}`
      icon.innerHTML = iconMarkup
      row.appendChild(icon)
    }

    const label = document.createElement('span')
    label.className = `beck-ghost-label ${CLS.ghostLabel}`
    label.textContent = node.title
    if (node.textColor) label.style.color = node.textColor
    row.appendChild(label)
    card.appendChild(row)

    pill = document.createElement('span')
    pill.className = `beck-status-inline ${CLS.statusInline}`
    if (node.status) pill.textContent = node.status
    else pill.style.display = 'none'
    card.appendChild(pill)
  } else {
    const icon = document.createElement('div')
    icon.className = `beck-icon ${CLS.iconMain}`
    setIcon(icon, node)
    if (icon.childElementCount > 0) card.appendChild(icon)

    const text = document.createElement('div')
    text.className = `beck-node-text ${CLS.text}`

    const title = document.createElement('div')
    title.className = `beck-node-title ${CLS.title}`
    title.textContent = node.title
    text.appendChild(title)

    if (node.subtitle) {
      const sub = document.createElement('div')
      sub.className = `beck-node-subtitle ${CLS.subtitle}`
      sub.textContent = node.subtitle
      text.appendChild(sub)
    }

    pill = document.createElement('span')
    pill.className = `beck-status ${CLS.status}`
    if (node.status) pill.textContent = node.status
    else pill.style.display = 'none'
    text.appendChild(pill)

    card.appendChild(text)
  }

  wrap.appendChild(card)
  return { wrap, card, pill }
}
