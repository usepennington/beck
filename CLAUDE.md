# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

Beck turns a declarative **YAML** description into a beautiful, animated diagram ("Mermaid, but
sexy"). Every document declares a root `type:` — `architecture` (the layered node/edge graph),
`sequence`, `state`, or `class` — sharing one meta/flow/theming system (untyped documents render
as architecture with a console deprecation warning; docs never show the untyped form). One repo
produces two artifacts joined by the YAML schema contract:

- **A TypeScript engine** (`src/`) — built with Vite, runs in the browser.
- **A single .NET NuGet package** (`dotnet/Beck`) — a Razor Class Library that *embeds the prebuilt
  engine* as a static web asset **and** contains `Beck.Authoring`, a C# API that emits Beck YAML
  from code. There is no npm package and no CDN distribution; the package is the only shipping unit.

The intended consumer is a Pennington (`b:\penn`) docs site: include one script, write a ` ```beck `
fenced block, get a diagram. The packaging pattern is modeled on `b:\dewey`'s `DeweySearch.Web`.

## Commands

```bash
npm install
npm run dev          # Vite playground: sample picker, live YAML editor, light/dark toggle
npm run typecheck    # tsc --noEmit (run this after any src/ change — strict, noUnusedLocals)
npm run build        # typecheck + build the playground to dist/
npm run build:lib    # build the IIFE engine bundle INTO dotnet/Beck/wwwroot/beck.global.js
BECK_FORMAT=esm npm run build:lib   # optional ESM build to dist-lib/ (not used by the package)
```

```bash
# .NET (SDK 9/10/11 present; package targets net8.0)
dotnet run --project dotnet/Beck.Sample -c Release   # emit a sample diagram's YAML to stdout
dotnet build dotnet/Beck/Beck.csproj -c Release
dotnet pack  dotnet/Beck/Beck.csproj -c Release -o <out>   # Node-free; ships the committed wwwroot asset
```

There is **no automated test suite**. Verify visually by running `npm run dev`, or headlessly via the
Playwright MCP against `vite preview` / a static server (see "Verifying" below).

## The rendering pipeline (the core of `src/`)

Each stage is a near-pure function with an explicit contract; `core.ts` orchestrates them:

```
YAML → model/ (parse → validate+defaults; buildModel dispatches on root `type:` to the
       architecture / sequence / state / class builders — sequence.ts, state.ts, classes.ts)
     → measure (render cards off-flow, read getBoundingClientRect)
     → layout/ (per type: layered.ts — Sugiyama-lite: rank → order(+virtual nodes) → coords;
       groups = recursive compound sub-layout — each group is laid out then fed to its parent as one
       sized super-node, so groups nest and span ranks; `layoutLayer` is the group-free engine,
       `layeredLayout` the recursive driver. sequence.ts — fixed grid: participant columns, message
       rows, request/reply activation-bar pairing)
     → route/ (auto orthogonal step-round edges + obstacle avoidance; route/sequence.ts draws
       lifelines/activation bars/section bands + message paths) → SVG overlay
     → render/ (position DOM via transform, group boxes; node shapes: card, state pill, start/end
       pseudo-states, class compartment card)
     → animate/ (compile flow → GSAP timeline; play on scroll-into-view)
```

State and class diagrams compile onto the layered engine in their model builders (states→pill
nodes, transitions→edges; classes→compartment-card nodes, relations→edges with UML end markers —
`inherits`/`implements` are FLIPPED to parent→child so parents rank above children). Sequence
diagrams have their own layout+router but reuse everything else; their derived flow pins each
packet to its row via the flow step's `edge` id (many messages share one from/to pair).

`mountModel(root, model, opts)` in `src/core.ts` runs all of this and returns a `DiagramHandle`
(`play/pause/reset/seek/setTheme/relayout/destroy/ready`). `renderDiagram(host, yaml, opts)` is the
light-DOM convenience; `<beck-diagram>` (`src/embed/element.ts`) is the custom element, which also
renders in light DOM (via `renderDiagram`) so host CSS reaches it; `src/embed/hydrate.ts` scans
`code.language-beck` blocks and is the Pennington/Markdig integration.

## Load-bearing invariants (violating these breaks things subtly)

- **The model fills every default.** `src/model/validate.ts` turns raw YAML into a `DiagramModel`
  where nothing is optional and accents/colors are already CSS values. Downstream stages never see
  raw input — add new fields by giving them a default here, not by handling `undefined` later.
- **Colors are CSS variables, never hardcoded hex.** Components consume only `--beck-*` tokens
  (defined in `src/embed/styles.css`), which default to the host site's `--color-*` palette with a
  literal fallback. Light/dark is *only* `[data-theme="dark"]` redefining those vars — there is no
  per-theme JS and no `.dark { …hex… }`. This is why a diagram adopts the host page's colors.
  Animations that need a concrete color call `resolveColor()` (`src/util/color.ts`), which probes the
  computed value of a var inside the root.
- **GSAP is loaded from a CDN at runtime, never bundled.** `src/animate/runtime.ts` dynamic-imports
  it. Everything else imports GSAP **types only** (`typeof import('gsap')`), so it stays out of the
  bundle. `prefers-reduced-motion` (or `animate: false`) renders the static frame and never loads it.
  Do not add a static `import ... from 'gsap'`.
- **Every edge exposes one continuous `SVGPathElement`.** The animation layer (`packet`/`trail`)
  samples it with `getPointAtLength`/`getTotalLength`. The router must keep producing a single path
  per edge.
- **Shared DOM class vocabulary.** `render/` writes `.beck-node` / `.beck-status` /
  `.beck-status-inline` and `animate/` queries them. Renaming one side silently breaks the other.
- **Router anchoring is rank-aware.** In a layered layout, cross-rank edges must travel along the
  primary axis even when a wide fan-out makes them more horizontal (`primaryHorizontal` →
  `autoSides` in `src/route/orthogonal.ts`). Picking sides by raw dx/dy alone sends fan-out edges
  detouring around nodes.
- **CSS ships inside the JS.** `src/styles.ts` does `import css from './embed/styles.css?inline'`;
  the element injects that string into its shadow root (and `renderDiagram` into the document head).
  No separate stylesheet, no build-time CSS placeholder.

## The single-package model (`dotnet/Beck`)

- `npm run build:lib` writes the **committed** `dotnet/Beck/wwwroot/beck.global.js`. The Razor SDK
  packs `wwwroot/**` as a static web asset served at `_content/Beck/beck.global.js`, so `dotnet pack`
  needs no Node. **After changing the engine, run `npm run build:lib` and commit the regenerated
  bundle**, or the package ships stale JS. (`dist/` and `dist-lib/` are gitignored; the wwwroot copy
  is intentionally committed.)
- `Beck.Authoring` (`dotnet/Beck/Authoring/`) is a dependency-free fluent builder family — one per
  diagram type (`DiagramBuilder`, `SequenceDiagramBuilder`, `StateDiagramBuilder`,
  `ClassDiagramBuilder` + its reflection `FromTypes`), sharing `MetaOptions` — that emits YAML via
  a tiny hand-rolled `YamlWriter` (no YamlDotNet). The C# enums map to schema tokens in
  `Tokens.Of(...)` (note `EdgeCurve.StepRound` → `"step-round"`, `Direction` stays uppercase). When
  you add a YAML field, update **both** `src/model/` (the parser) and the matching C# builder.
- The RCL pulls in a `Microsoft.AspNetCore.App` framework reference (Razor SDK). That's fine for web
  hosts; it's the only reason a pure-console consumer of `Beck.Authoring` rides the ASP.NET shared
  framework. Split the authoring lib into its own classlib only if that becomes a real constraint.

## Verifying

The fastest real check is headless render via the Playwright MCP: serve the playground
(`npx vite preview --port <p>` after `npm run build`) or a static page that includes
`dotnet/Beck/wwwroot/beck.global.js` with a `code.language-beck` block, then screenshot and inspect
the DOM (`.beck-node-wrap` transforms, `.beck-overlay path` route data). Define `--color-*` vars on
the test page to confirm host-palette adoption. Always check for off-canvas edge routes (negative
path coordinates) — that's the signature of a routing regression.
