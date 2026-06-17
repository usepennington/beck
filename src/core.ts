import { loadDiagram } from './model'
import type { DiagramModel, ThemeMode } from './model/schema'
import { createNode, type RenderedNode } from './render/node'
import { createGroup } from './render/group'
import { measureNodes } from './layout/measure'
import { layeredLayout } from './layout/layered'
import type { LayoutResult } from './layout/types'
import { createOverlay, routeEdges, type RoutedEdge } from './route/svg'
import { loadGsap, prefersReducedMotion, setGsapUrl, type Timeline } from './animate/runtime'
import { buildTimeline, type CompiledFlow } from './animate/timeline'
import { STYLES } from './styles'

export interface RenderOptions {
  theme?: ThemeMode
  /** Force-disable animation (still respects prefers-reduced-motion when true). */
  animate?: boolean
  /** Override the GSAP CDN URL. */
  gsapUrl?: string
}

export interface DiagramHandle {
  readonly timeline: Timeline | null
  play(): void
  pause(): void
  reset(): void
  seek(label: string | number): void
  setTheme(mode: ThemeMode): void
  relayout(): void
  destroy(): void
  /** Resolves after the first layout (and GSAP load, if animating). */
  readonly ready: Promise<void>
}

interface BuiltState {
  viewport: HTMLElement
  canvas: HTMLElement
  svg: SVGSVGElement
  rendered: Map<string, RenderedNode>
  edges: RoutedEdge[]
  layout: LayoutResult
}

/** Mount a validated model into an already-scoped `.beck-root` container. */
export function mountModel(root: HTMLElement, model: DiagramModel, opts: RenderOptions = {}): DiagramHandle {
  root.classList.add('beck-root')
  if (opts.gsapUrl) setGsapUrl(opts.gsapUrl)

  let themeMode: ThemeMode = opts.theme ?? model.meta.theme
  let mql: MediaQueryList | null = null
  const onMedia = () => applyTheme()
  const applyTheme = () => {
    if (themeMode === 'auto') {
      const dark = typeof matchMedia !== 'undefined' && matchMedia('(prefers-color-scheme: dark)').matches
      root.dataset.theme = dark ? 'dark' : 'light'
    } else {
      root.dataset.theme = themeMode
    }
  }
  const watchMedia = () => {
    if (typeof matchMedia === 'undefined') return
    mql?.removeEventListener('change', onMedia)
    mql = null
    if (themeMode === 'auto') {
      mql = matchMedia('(prefers-color-scheme: dark)')
      mql.addEventListener('change', onMedia)
    }
  }
  applyTheme()
  watchMedia()

  const build = (): BuiltState => {
    // keep the title/subtitle? They are rebuilt each time for simplicity.
    root.replaceChildren()

    if (model.meta.title) {
      const h = document.createElement('h1')
      h.className = 'beck-title'
      h.textContent = model.meta.title
      root.appendChild(h)
    }
    if (model.meta.subtitle) {
      const p = document.createElement('p')
      p.className = 'beck-subtitle'
      p.textContent = model.meta.subtitle
      root.appendChild(p)
    }

    const viewport = document.createElement('div')
    viewport.className = 'beck-viewport'
    const canvas = document.createElement('div')
    canvas.className = 'beck-canvas'
    viewport.appendChild(canvas)
    root.appendChild(viewport)

    const rendered = new Map<string, RenderedNode>()
    for (const n of model.nodes) {
      const rn = createNode(n)
      rn.wrap.style.visibility = 'hidden'
      canvas.appendChild(rn.wrap)
      rendered.set(n.id, rn)
    }

    const sizes = measureNodes(rendered.values())
    const layout = layeredLayout(model, sizes)

    for (const [id, rn] of rendered) {
      const r = layout.nodes.get(id)
      if (!r) continue
      rn.wrap.style.transform = `translate(${r.x}px, ${r.y}px)`
      rn.wrap.style.visibility = ''
    }

    for (const g of model.groups) {
      const gr = layout.groups.get(g.id)
      if (!gr) continue
      const { box, label } = createGroup(g)
      box.style.left = `${gr.x}px`
      box.style.top = `${gr.y}px`
      box.style.width = `${gr.w}px`
      box.style.height = `${gr.h}px`
      canvas.appendChild(box)
      if (label) {
        label.style.left = `${gr.x + 14}px`
        label.style.top = `${gr.y - 9}px`
        canvas.appendChild(label)
      }
    }

    const svg = createOverlay(layout.width, layout.height)
    canvas.appendChild(svg)
    const edges = routeEdges(svg, model, layout)

    canvas.style.width = `${layout.width}px`
    canvas.style.height = `${layout.height}px`

    return { viewport, canvas, svg, rendered, edges, layout }
  }

  let state = build()

  const fit = () => {
    const avail = state.viewport.clientWidth
    if (!avail || !state.layout.width) return
    const s = Math.min(1, avail / state.layout.width)
    state.canvas.style.transform = s < 1 ? `scale(${s})` : ''
    state.viewport.style.height = `${state.layout.height * s}px`
  }

  const ro = typeof ResizeObserver !== 'undefined' ? new ResizeObserver(() => fit()) : null
  ro?.observe(root)
  fit()

  // ---- animation ----
  const shouldAnimate = opts.animate !== false && model.meta.animate && !prefersReducedMotion()
  let compiled: CompiledFlow | null = null
  let io: IntersectionObserver | null = null
  let resolveReady!: () => void
  const ready = new Promise<void>((r) => (resolveReady = r))

  const wireAnimation = () => {
    if (!shouldAnimate) return
    const wraps = new Map<string, HTMLElement>([...state.rendered].map(([id, rn]) => [id, rn.wrap]))
    compiled = buildTimeline({ root, canvas: state.canvas, svg: state.svg, nodes: wraps, edges: state.edges, model })
    if (typeof IntersectionObserver !== 'undefined') {
      io = new IntersectionObserver(
        (entries) => {
          for (const e of entries) {
            if (!compiled) continue
            if (e.isIntersecting) compiled.timeline.play()
            else compiled.timeline.pause()
          }
        },
        { threshold: 0.2 },
      )
      io.observe(root)
    } else {
      compiled.timeline.play()
    }
  }

  const setupAnimation = async () => {
    if (!shouldAnimate) {
      resolveReady()
      return
    }
    try {
      await loadGsap()
      wireAnimation()
    } catch (err) {
      console.warn('Beck: animation disabled —', err)
    } finally {
      resolveReady()
    }
  }
  void setupAnimation()

  const seekTo = (label: string | number) => {
    if (!compiled) return
    compiled.snapshot.restoreNow()
    compiled.timeline.pause()
    compiled.timeline.seek(0, true)
    compiled.timeline.seek(label, false)
  }

  return {
    get timeline() {
      return compiled?.timeline ?? null
    },
    play() {
      compiled?.timeline.play()
    },
    pause() {
      compiled?.timeline.pause()
    },
    reset() {
      if (!compiled) return
      compiled.timeline.pause()
      compiled.timeline.seek(0)
      compiled.snapshot.restoreNow()
    },
    seek: seekTo,
    setTheme(mode: ThemeMode) {
      themeMode = mode
      applyTheme()
      watchMedia()
    },
    relayout() {
      io?.disconnect()
      io = null
      compiled?.timeline.kill()
      compiled = null
      state = build()
      fit()
      if (shouldAnimate) wireAnimation()
    },
    destroy() {
      ro?.disconnect()
      io?.disconnect()
      compiled?.timeline.kill()
      mql?.removeEventListener('change', onMedia)
      root.replaceChildren()
    },
    ready,
  }
}

/** Inject the Beck stylesheet into a document once (id-guarded). */
function ensureStyles(doc: Document): void {
  if (doc.getElementById('beck-styles')) return
  const style = doc.createElement('style')
  style.id = 'beck-styles'
  style.textContent = STYLES
  doc.head.appendChild(style)
}

/**
 * Render a YAML diagram into a host element (light DOM). Ensures the stylesheet
 * is present in the document, then mounts. For shadow-DOM isolation use the
 * `<beck-diagram>` element instead.
 */
export function renderDiagram(host: HTMLElement, yaml: string, opts: RenderOptions = {}): DiagramHandle {
  ensureStyles(host.ownerDocument)
  const model = loadDiagram(yaml)
  const root = document.createElement('div')
  host.replaceChildren(root)
  return mountModel(root, model, opts)
}
