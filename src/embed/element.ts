import { renderDiagram, type DiagramHandle, type RenderOptions } from '../core'
import type { ThemeMode } from '../model/schema'
import { BeckPlaybackElement } from './playback'

/**
 * `<beck-diagram>` renders a YAML diagram in **light DOM** (via `renderDiagram`), so the
 * host page's Tailwind/MonorailCSS utility classes reach it — the same path the fenced
 * ```beck integration uses. The YAML may be the element's text, a child
 * `<script type="application/yaml">`, or fetched from a `src` attribute. Colour/theme
 * ride the `--beck-*`/`--color-*` custom properties.
 *
 * Attributes: `mode` (light|dark|auto), `src`, `animate` (false to disable).
 */
export class BeckDiagramElement extends HTMLElement {
  static observedAttributes = ['mode', 'src', 'animate']

  /** The live diagram handle (read by `<beck-playback>`). */
  diagram: DiagramHandle | null = null

  /**
   * Inline YAML captured once, before the first render replaces the element's children.
   * Re-renders (mode/animate/src changes) must not read back the rendered diagram's own
   * text; `src` sources are re-fetched live instead.
   */
  private inlineSource: string | null = null

  connectedCallback(): void {
    if (this.inlineSource == null && !this.getAttribute('src')) {
      const script = this.querySelector('script[type="application/yaml"], script[type="text/yaml"]')
      this.inlineSource = (script?.textContent ?? this.textContent ?? '').trim()
    }
    void this.render()
  }

  disconnectedCallback(): void {
    this.diagram?.destroy()
    this.diagram = null
  }

  attributeChangedCallback(name: string, oldValue: string | null, value: string | null): void {
    if (oldValue === value) return
    if (name === 'mode' && this.diagram) this.diagram.setTheme(((value as ThemeMode) || 'auto'))
    else if ((name === 'src' || name === 'animate') && this.isConnected) void this.render()
  }

  private async render(): Promise<void> {
    let yaml: string | null
    try {
      yaml = await this.readSource()
    } catch (err) {
      this.showError(err)
      return
    }
    if (yaml == null) return // no source present — nothing to render (not an error)

    try {
      const opts: RenderOptions = {}
      const mode = this.getAttribute('mode') as ThemeMode | null
      if (mode) opts.theme = mode
      if (this.getAttribute('animate') === 'false') opts.animate = false
      this.diagram?.destroy()
      // renderDiagram clears this element's children and mounts into it (light DOM),
      // injecting the stylesheet into the document once (id-guarded).
      this.diagram = renderDiagram(this, yaml, opts)
    } catch (err) {
      this.showError(err)
    }
  }

  private showError(err: unknown): void {
    this.diagram?.destroy()
    this.diagram = null
    const box = document.createElement('div')
    box.className = 'beck-error'
    box.textContent = err instanceof Error ? err.message : String(err)
    this.replaceChildren(box)
  }

  /** Returns the YAML source, or null when none is present. Throws if a `src` fails to load. */
  private async readSource(): Promise<string | null> {
    const src = this.getAttribute('src')
    if (src) {
      const res = await fetch(src)
      if (!res.ok) throw new Error(`Beck: failed to load src "${src}" (HTTP ${res.status})`)
      return await res.text()
    }
    return this.inlineSource && this.inlineSource.length > 0 ? this.inlineSource : null
  }
}

/** Register `<beck-diagram>` and `<beck-playback>` (idempotent). */
export function defineBeckElements(): void {
  if (typeof customElements === 'undefined') return
  if (!customElements.get('beck-diagram')) customElements.define('beck-diagram', BeckDiagramElement)
  if (!customElements.get('beck-playback')) customElements.define('beck-playback', BeckPlaybackElement)
}
