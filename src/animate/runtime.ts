// GSAP is loaded from a CDN at runtime (dynamic import) rather than bundled, so
// the engine ships GSAP-free and the static frame needs zero network. Types come
// from the `gsap` dev dependency via `typeof import(...)`, which is erased at
// build time — nothing from gsap ends up in the bundle.

type GsapModule = typeof import('gsap')
export type Gsap = GsapModule['gsap']
export type Timeline = ReturnType<Gsap['timeline']>

const DEFAULT_CDN = 'https://cdn.jsdelivr.net/npm/gsap@3/+esm'

let _gsap: Gsap | null = null
let _loading: Promise<Gsap> | null = null
let _cdn = DEFAULT_CDN

/** Override the CDN URL GSAP is loaded from (call before the first render). */
export function setGsapUrl(url: string): void {
  _cdn = url
}

export function gsapLoaded(): boolean {
  return _gsap != null
}

/** The loaded GSAP instance. Throws if called before `loadGsap()` resolves. */
export function gsap(): Gsap {
  if (!_gsap) throw new Error('Beck: GSAP is not loaded yet')
  return _gsap
}

/** Load GSAP from the CDN once; subsequent calls return the cached instance. */
export function loadGsap(): Promise<Gsap> {
  if (_gsap) return Promise.resolve(_gsap)
  if (_loading) return _loading
  _loading = import(/* @vite-ignore */ _cdn).then((mod: Record<string, unknown>) => {
    _gsap = (mod.gsap ?? mod.default) as Gsap
    return _gsap
  })
  return _loading
}

export function prefersReducedMotion(): boolean {
  return typeof matchMedia !== 'undefined' && matchMedia('(prefers-reduced-motion: reduce)').matches
}
