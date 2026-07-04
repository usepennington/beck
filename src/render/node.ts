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
  // state-diagram shapes
  pill: 'relative flex flex-col items-center gap-[1px] min-w-[96px] px-5 py-2.5 rounded-full',
  pillTitle: 'text-sm font-semibold leading-[1.3] whitespace-nowrap',
  pillSubtitle: 'text-[0.68rem] leading-[1.3] whitespace-nowrap',
  // class-diagram compartment card
  classCard: 'relative flex flex-col min-w-[170px] rounded-[12px] overflow-hidden text-left',
  classHead: 'flex flex-col items-center gap-0 px-4 py-2',
  classStereo: 'text-[0.65rem] leading-[1.3] tracking-[0.03em]',
  classTitle: 'text-sm font-semibold leading-[1.4]',
  classSection: 'flex flex-col gap-[2px] px-3.5 py-[7px]',
  classMember: 'font-mono text-[0.72rem] leading-[1.45] whitespace-nowrap',
}

/** Hidden-by-default status pill; present on every shape so `status`/`fail` flow
 *  steps always have a target. */
function makeStatus(node: NodeModel): HTMLElement {
  const pill = document.createElement('span')
  pill.className = `beck-status ${CLS.status}`
  if (node.status) pill.textContent = node.status
  else pill.style.display = 'none'
  return pill
}

function linkOrDiv(node: NodeModel, baseUrl: string): HTMLElement {
  const card = document.createElement(node.href ? 'a' : 'div')
  if (node.href) {
    const a = card as HTMLAnchorElement
    a.href = withBase(node.href, baseUrl)
    if (node.target) a.target = node.target
    if (node.target === '_blank') a.rel = 'noopener noreferrer'
  }
  return card
}

/** A state-diagram pill: centred title (+ optional subtitle), no icon chip. */
function createPill(node: NodeModel, baseUrl: string): { card: HTMLElement; pill: HTMLElement } {
  const card = linkOrDiv(node, baseUrl)
  card.className = `beck-node beck-node--pill ${CLS.pill}`
  const title = document.createElement('div')
  title.className = `beck-node-title ${CLS.pillTitle}`
  title.textContent = node.title
  card.appendChild(title)
  if (node.subtitle) {
    const sub = document.createElement('div')
    sub.className = `beck-node-subtitle ${CLS.pillSubtitle}`
    sub.textContent = node.subtitle
    card.appendChild(sub)
  }
  const pill = makeStatus(node)
  card.appendChild(pill)
  return { card, pill }
}

/** The UML entry (filled dot) / exit (bullseye) pseudo-state. */
function createPseudo(node: NodeModel): { card: HTMLElement; pill: HTMLElement } {
  const card = document.createElement('div')
  card.className = `beck-node beck-node--${node.shape === 'start' ? 'start' : 'end'}`
  const pill = makeStatus(node)
  pill.style.display = 'none'
  card.appendChild(pill)
  return { card, pill }
}

/** A UML class card: «stereotype» + name header, then field/method compartments. */
function createClassCard(node: NodeModel, baseUrl: string): { card: HTMLElement; pill: HTMLElement } {
  const card = linkOrDiv(node, baseUrl)
  card.className = `beck-node beck-node--class ${CLS.classCard}`

  const head = document.createElement('div')
  head.className = `beck-class-head ${CLS.classHead}`
  if (node.stereotype) {
    const st = document.createElement('div')
    st.className = `beck-class-stereo ${CLS.classStereo}`
    st.textContent = `«${node.stereotype}»`
    head.appendChild(st)
  }
  const title = document.createElement('div')
  title.className = `beck-node-title ${CLS.classTitle}`
  title.textContent = node.title
  head.appendChild(title)
  card.appendChild(head)

  for (const [cls, members] of [
    ['beck-class-fields', node.fields],
    ['beck-class-methods', node.methods],
  ] as const) {
    if (!members.length) continue
    const section = document.createElement('div')
    section.className = `beck-class-section ${cls} ${CLS.classSection}`
    for (const m of members) {
      const line = document.createElement('div')
      line.className = `beck-class-member ${CLS.classMember}`
      line.textContent = m
      section.appendChild(line)
    }
    card.appendChild(section)
  }

  const pill = makeStatus(node)
  pill.style.display = 'none'
  card.appendChild(pill)
  return { card, pill }
}

/** Build a node's DOM. Theming is entirely via the `--beck-accent` var + classes. */
export function createNode(node: NodeModel, baseUrl = ''): RenderedNode {
  const wrap = document.createElement('div')
  wrap.className = 'beck-node-wrap'
  wrap.dataset.node = node.id

  // Non-card shapes (state pills, pseudo-states, class cards) branch off before
  // the classic architecture card below.
  if (node.shape !== 'card') {
    const made =
      node.shape === 'pill'
        ? createPill(node, baseUrl)
        : node.shape === 'class'
          ? createClassCard(node, baseUrl)
          : createPseudo(node)
    made.card.style.setProperty('--beck-accent', node.accent)
    if (node.width) made.card.style.width = `${node.width}px`
    if (node.surface) made.card.style.setProperty('--beck-node-bg', node.surface)
    if (node.textColor) made.card.style.setProperty('--beck-text', node.textColor)
    wrap.appendChild(made.card)
    return { wrap, card: made.card, pill: made.pill }
  }

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
