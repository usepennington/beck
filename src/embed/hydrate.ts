import { renderDiagram } from '../core'
import type { DiagramHandle } from '../core'

// Scans for fenced ```beck code blocks (rendered by a markdown engine as
// `<code class="language-beck">`) and replaces each with a rendered diagram —
// the Mermaid-style integration. The diagram follows the host page's dark mode
// (an `html.dark` class or `data-theme="dark"`) and re-scans on SPA navigation.

const SELECTOR = 'code.language-beck'

interface Hydrated {
  handle: DiagramHandle
}

function hostIsDark(): boolean {
  const el = document.documentElement
  return el.classList.contains('dark') || el.dataset.theme === 'dark'
}

function hydrateAll(registry: Hydrated[]): void {
  const blocks = document.querySelectorAll<HTMLElement>(SELECTOR)
  blocks.forEach((code) => {
    if (code.dataset.beckHydrated) return
    code.dataset.beckHydrated = '1'
    const source = code.textContent ?? ''
    const host = document.createElement('div')
    host.className = 'beck-embed'
    const replaced = code.closest('pre') ?? code
    replaced.replaceWith(host)
    try {
      const handle = renderDiagram(host, source, { theme: hostIsDark() ? 'dark' : 'light' })
      registry.push({ handle })
    } catch (err) {
      host.className = 'beck-error'
      host.textContent = err instanceof Error ? err.message : String(err)
    }
  })
}

/**
 * Begin hydrating fenced Beck diagrams. Returns a `rescan` function for hosts
 * that inject new content after load (exposed as `window.Beck.hydrate()`).
 */
export function startHydration(): () => void {
  if (typeof document === 'undefined') return () => {}
  const registry: Hydrated[] = []
  const rescan = () => hydrateAll(registry)

  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', rescan, { once: true })
  else rescan()

  // Re-theme every diagram when the host toggles dark mode.
  new MutationObserver(() => {
    const mode = hostIsDark() ? 'dark' : 'light'
    for (const h of registry) h.handle.setTheme(mode)
  }).observe(document.documentElement, { attributes: true, attributeFilter: ['class', 'data-theme'] })

  // Catch diagrams injected by SPA navigation (debounced).
  let pending = 0
  const observer = new MutationObserver(() => {
    if (pending) return
    pending = requestAnimationFrame(() => {
      pending = 0
      if (document.querySelector(`${SELECTOR}:not([data-beck-hydrated])`)) rescan()
    })
  })
  if (document.body) observer.observe(document.body, { childList: true, subtree: true })
  else document.addEventListener('DOMContentLoaded', () => observer.observe(document.body, { childList: true, subtree: true }), { once: true })

  return rescan
}
