// IIFE entry shipped inside the Beck NuGet package (served at
// _content/Beck/beck.global.js). Auto-registers the custom elements, hydrates
// fenced ```beck code blocks, and exposes a `window.Beck` namespace.
import { defineBeckElements } from './embed/element'
import { startHydration } from './embed/hydrate'
import { renderDiagram } from './core'
import { setGsapUrl } from './animate/runtime'
import { ICONS, resolveIcon } from './render/icons'

// Let a host point GSAP at a self-hosted/pinned copy for CSP-locked or offline sites.
// This must happen before startHydration() — auto-hydration loads GSAP synchronously on
// the first render, so a `window.Beck.setGsapUrl()` call would arrive too late. Read it
// from the loading <script>'s `data-gsap-url` (e.g.
// `<script src="/_content/Beck/beck.global.js" defer data-gsap-url="/lib/gsap.js">`),
// or a `window.BeckConfig.gsapUrl` global set before the bundle loads.
const bootScript = typeof document !== 'undefined' ? (document.currentScript as HTMLScriptElement | null) : null
const bootGsapUrl =
  bootScript?.dataset.gsapUrl ??
  (globalThis as { BeckConfig?: { gsapUrl?: string } }).BeckConfig?.gsapUrl
if (bootGsapUrl) setGsapUrl(bootGsapUrl)

defineBeckElements()
const hydrate = startHydration()

// `icons`/`resolveIcon` expose the built-in icon registry so a host can introspect the set
// (the docs site's icon reference renders straight from this — one source of truth, no copy to
// drift). `icons` is read-only by convention.
const Beck = { renderDiagram, setGsapUrl, defineBeckElements, hydrate, icons: ICONS, resolveIcon }
;(globalThis as unknown as { Beck: typeof Beck }).Beck = Beck

export default Beck
