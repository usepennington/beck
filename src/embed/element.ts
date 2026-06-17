import { STYLES } from '../styles'
import { mountModel, type DiagramHandle, type RenderOptions } from '../core'
import { loadDiagram } from '../model'
import type { ThemeMode } from '../model/schema'
import { BeckPlaybackElement } from './playback'

const FONT_HREF = 'https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700&display=swap'

/**
 * `<beck-diagram>` renders a YAML diagram inside Shadow DOM. The YAML may be the
 * element's text, a child `<script type="application/yaml">`, or fetched from a
 * `src` attribute. CSS custom properties pierce the shadow boundary, so the
 * diagram still adopts the host page's `--color-*` palette.
 *
 * Attributes: `mode` (light|dark|auto), `src`, `animate` (false to disable).
 */
export class BeckDiagramElement extends HTMLElement {
  static observedAttributes = ['mode', 'src']

  /** The live diagram handle (read by `<beck-playback>`). */
  diagram: DiagramHandle | null = null

  connectedCallback(): void {
    void this.render()
  }

  disconnectedCallback(): void {
    this.diagram?.destroy()
    this.diagram = null
  }

  attributeChangedCallback(name: string, oldValue: string | null, value: string | null): void {
    if (oldValue === value) return
    if (name === 'mode' && this.diagram) this.diagram.setTheme(((value as ThemeMode) || 'auto'))
    else if (name === 'src' && this.isConnected) void this.render()
  }

  private async render(): Promise<void> {
    const shadow = this.shadowRoot ?? this.attachShadow({ mode: 'open' })
    shadow.replaceChildren()

    const style = document.createElement('style')
    style.textContent = STYLES
    shadow.appendChild(style)

    const font = document.createElement('link')
    font.rel = 'stylesheet'
    font.href = FONT_HREF
    shadow.appendChild(font)

    const root = document.createElement('div')
    shadow.appendChild(root)

    const yaml = await this.readSource()
    if (yaml == null) return

    try {
      const model = loadDiagram(yaml)
      const opts: RenderOptions = {}
      const mode = this.getAttribute('mode') as ThemeMode | null
      if (mode) opts.theme = mode
      if (this.getAttribute('animate') === 'false') opts.animate = false
      this.diagram?.destroy()
      this.diagram = mountModel(root, model, opts)
    } catch (err) {
      root.textContent = err instanceof Error ? err.message : String(err)
      root.style.cssText = 'color:#e11d48;font-family:ui-monospace,monospace;font-size:0.8rem;white-space:pre-wrap;padding:12px;'
    }
  }

  private async readSource(): Promise<string | null> {
    const src = this.getAttribute('src')
    if (src) {
      try {
        const res = await fetch(src)
        return await res.text()
      } catch {
        return null
      }
    }
    const script = this.querySelector('script[type="application/yaml"], script[type="text/yaml"]')
    if (script?.textContent) return script.textContent
    const text = this.textContent?.trim()
    return text ? text : null
  }
}

/** Register `<beck-diagram>` and `<beck-playback>` (idempotent). */
export function defineBeckElements(): void {
  if (typeof customElements === 'undefined') return
  if (!customElements.get('beck-diagram')) customElements.define('beck-diagram', BeckDiagramElement)
  if (!customElements.get('beck-playback')) customElements.define('beck-playback', BeckPlaybackElement)
}
