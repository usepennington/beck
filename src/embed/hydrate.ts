import { renderDiagram } from '../core'
import type { DiagramHandle } from '../core'

// Scans for fenced ```beck code blocks (rendered by a markdown engine as
// `<code class="language-beck">`) and replaces each with a rendered diagram —
// the Mermaid-style integration. The diagram follows the host page's dark mode
// (an `html.dark` class or `data-theme="dark"`) and re-scans on SPA navigation.

const SELECTOR = 'code.language-beck'

interface Hydrated {
  handle: DiagramHandle
  /** The mounted host element, kept so detached diagrams can be torn down. */
  host: HTMLElement
}

function hostIsDark(): boolean {
  const el = document.documentElement
  return el.classList.contains('dark') || el.dataset.theme === 'dark'
}

/**
 * Destroy and drop any registry entries whose host has left the document. A SPA
 * (e.g. Pennington) swaps page content without a reload, detaching old diagrams;
 * without this their ResizeObserver / IntersectionObserver / GSAP timeline would
 * leak and the registry would grow unbounded. The `<beck-diagram>` element does
 * its own teardown in `disconnectedCallback`, so this only covers the fence path.
 */
function pruneDetached(registry: Hydrated[]): void {
  for (let i = registry.length - 1; i >= 0; i--) {
    if (!document.contains(registry[i].host)) {
      registry[i].handle.destroy()
      registry.splice(i, 1)
    }
  }
}

function hydrateAll(registry: Hydrated[]): void {
  pruneDetached(registry)
  const blocks = document.querySelectorAll<HTMLElement>(SELECTOR)
  blocks.forEach((code) => {
    if (code.dataset.beckHydrated) return
    code.dataset.beckHydrated = '1'
    const source = code.textContent ?? ''
    const host = document.createElement('div')
    host.className = 'beck-embed'
    // Replace the whole code-block presentation wrapper, not just the <pre>, so the
    // diagram doesn't render trapped inside the host's code-card chrome (border,
    // background, and a "beck" language-label bar). Markdown engines wrap a fenced
    // block in a styled container — Pennington's is `.code-highlight-wrapper`
    // (it also carries `data-language`). Fall back to the <pre>, then the <code>
    // itself, for plain or unknown hosts.
    const replaced =
      code.closest('.code-highlight-wrapper, [data-language]') ?? code.closest('pre') ?? code
    replaced.replaceWith(host)
    try {
      const handle = renderDiagram(host, source, { theme: hostIsDark() ? 'dark' : 'light' })
      registry.push({ handle, host })
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
    pruneDetached(registry)
    const mode = hostIsDark() ? 'dark' : 'light'
    for (const h of registry) h.handle.setTheme(mode)
  }).observe(document.documentElement, { attributes: true, attributeFilter: ['class', 'data-theme'] })

  // Reclaim diagrams promptly after a SPA content swap (Pennington fires `spa:commit`
  // on document once the new DOM is in place). Harmless on hosts that never fire it —
  // the rescan path prunes too — but this avoids leaking until the next diagram page.
  document.addEventListener('spa:commit', () => pruneDetached(registry))

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
