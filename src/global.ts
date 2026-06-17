// IIFE entry shipped inside the Beck NuGet package (served at
// _content/Beck/beck.global.js). Auto-registers the custom elements, hydrates
// fenced ```beck code blocks, and exposes a `window.Beck` namespace.
import { defineBeckElements } from './embed/element'
import { startHydration } from './embed/hydrate'
import { renderDiagram } from './core'
import { setGsapUrl } from './animate/runtime'

defineBeckElements()
const hydrate = startHydration()

const Beck = { renderDiagram, setGsapUrl, defineBeckElements, hydrate }
;(globalThis as unknown as { Beck: typeof Beck }).Beck = Beck

export default Beck
