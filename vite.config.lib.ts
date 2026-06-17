import { defineConfig } from 'vite'
import { resolve } from 'path'

// Library build. CSS is imported with `?inline` inside the engine, so it is
// bundled into the JS as a string (works the same in dev and build) and the
// custom element injects it into its shadow root — no separate stylesheet, no
// CSS-inlining plugin. GSAP is never statically imported, so it stays out of
// the bundle and is loaded from a CDN at runtime.
//
// Two formats, selected via BECK_FORMAT:
//   global → IIFE, registers <beck-diagram> and exposes window.Beck
//   esm    → ES module exports (renderDiagram, defineBeckElements, …)
const format = (process.env.BECK_FORMAT as 'global' | 'esm') || 'global'
const isGlobal = format === 'global'

// The IIFE bundle is written straight into the Beck RCL's wwwroot so it ships
// inside the NuGet package (served at _content/Beck/beck.global.js). The committed
// asset lets `dotnet pack` run without Node — regenerate it with `npm run build:lib`.
export default defineConfig({
  build: {
    outDir: isGlobal ? 'dotnet/Beck/wwwroot' : 'dist-lib',
    emptyOutDir: false,
    cssCodeSplit: false,
    minify: true,
    lib: {
      entry: resolve(__dirname, isGlobal ? 'src/global.ts' : 'src/index.ts'),
      formats: [isGlobal ? 'iife' : 'es'],
      name: 'Beck',
      fileName: () => (isGlobal ? 'beck.global.js' : 'beck.js'),
    },
  },
})
