# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

Beck turns a declarative **YAML** description into a beautiful, animated diagram ("Mermaid, but
sexy") — **rendered entirely in C#, server-side**. `BeckSvg.Render(yaml)` emits a single
self-contained, self-animating inline `<svg>`: layout, routing, and the full flow choreography
(packets, trails, highlights, status pills, narration captions, sequence dimming) are baked into
CSS animations inside the SVG. There is no client JavaScript, no GSAP, no hydration, and no npm
toolchain anywhere in the repo.

Every document declares a root `type:` — `architecture` (the layered node/edge graph), `sequence`,
`state`, or `class` — sharing one meta/flow/theming system (untyped documents render as
architecture with a one-time deprecation warning). The shipping units are two NuGet packages:

- **`Beck`** (`Beck`) — the engine (`Beck.Rendering.*` namespaces: model → measure → layout
  → route → svg → animate) **plus** the authoring API (`Beck` namespace: `DiagramBuilder` family
  emitting Beck YAML from code). Only dependency: YamlDotNet.
- **`Beck.Skia`** (`Beck.Skia`) — optional exact text measurement (SkiaSharp + HarfBuzzSharp
  shaping over user-supplied font files). Never mandatory; the engine defaults to an embedded
  Inter/IBM Plex Mono metrics table.

The intended consumer is a Pennington (`b:\penn`) docs site: reference the package, render
` ```beck ` fences to SVG at build time (see `docs/Beck.Docs/BeckSvgPreprocessor.cs` for the
canonical integration). The docs site's interactive playground runs this same engine compiled to
WebAssembly (`docs/Beck.Docs.Client`).

## Commands

```bash
dotnet build Beck.slnx                                   # everything (engine, tests, docs site)
dotnet test Beck.Tests/Beck.Tests.csproj          # the full gate: model/layout/route golden
                                                         #   parity, card sizing, render smoke tests
dotnet run --project Beck.Sample -c Release       # emit a sample diagram's YAML to stdout
dotnet pack Beck/Beck.csproj -c Release -o <out>  # version comes from `v*` git tags (MinVer)

dotnet run --project docs/Beck.Docs                      # docs site dev server (diagrams render live)
dotnet run --project docs/Beck.Docs -- build             # static docs build to docs/Beck.Docs/output
```

The engine targets net8.0; the docs site runs net10.0 (SDK pinned by `global.json`).

## The rendering pipeline (the core of `Beck`)

Each stage is a near-pure function with an explicit contract; `BeckSvg.cs` orchestrates them:

```
YAML → Model/    (YamlDotNet node tree → coerce → validate+defaults; buildModel dispatches on the
                  root `type:` to the architecture / sequence / state / class builders)
     → Text/     (ITextMeasurer → CardSizer box-model math → SizeMap; InterMetricsMeasurer is the
                  embedded zero-dependency default, SkiaTextMeasurer the exact plug-in)
     → Layout/   (per type: LayeredLayout — Sugiyama-lite: rank → order(+virtual nodes) → coords;
                  groups = recursive compound sub-layout, each group laid out then fed to its parent
                  as one sized super-node. SequenceLayout — fixed grid: participant columns, message
                  rows, request/reply activation-bar pairing)
     → Route/    (OrthogonalRouter: auto step-round edges + obstacle avoidance; SequencePainter
                  draws lifelines/activation bars/section bands + message paths; LabelPlacer)
     → Svg/      (node shape templates, group boxes, markers, icons, the theming <style> block)
     → Animate/  (ScheduleBuilder simulates the flow script into absolute-time tracks; CssCompiler
                  emits shared-cycle percentage-window @keyframes; Easing samples analytic Penner
                  eases into CSS linear() functions)
```

State and class diagrams compile onto the layered engine in their model builders (states→pill
nodes, transitions→edges; classes→compartment-card nodes, relations→edges with UML end markers —
`inherits`/`implements` are FLIPPED to parent→child so parents rank above children). Sequence
diagrams have their own layout+router but reuse everything else; their derived flow pins each
packet to its row via the flow step's `edge` id (many messages share one from/to pair).

## Load-bearing invariants (violating these breaks things subtly)

- **The model fills every default.** `Model/` turns raw YAML into a `DiagramModel` where nothing is
  optional and accents/colors are already CSS values. Downstream stages never see raw input — add
  new fields by giving them a default there, not by handling null later.
- **Colors are CSS variables, never hardcoded hex.** The SVG consumes only `--beck-*` tokens, which
  default to the host site's `--color-*` palette with a literal fallback. Light/dark is *only*
  `[data-theme="dark"]` (plus the `prefers-color-scheme` hook) redefining those vars — no per-theme
  rendering. This is why a diagram adopts the host page's colors. Effects that need a derived color
  emit `color-mix(...)` expressions, never resolved literals.
- **All animation is compiled, never executed.** The schedule is a deterministic simulation of the
  flow script; every animated element gets a whole-cycle-duration CSS animation whose action
  occupies a percentage window, so dozens of elements loop in lockstep forever. Instant events are
  paired keyframes 0.01% apart. Don't introduce per-element `animation-delay` chains — they drift
  across iterations.
- **All motion CSS lives inside `@media (prefers-reduced-motion: no-preference)`** — including
  initial dim/hide states. Reduced-motion users get the fully-revealed static frame.
- **Deterministic output.** Same YAML + same options → byte-identical SVG. Ids, class scoping, and
  keyframe names derive from an 8-char content hash — never a counter, timestamp, or RNG. Every
  number-to-string uses `CultureInfo.InvariantCulture`; coordinates round to 2 decimals.
- **Every edge is one continuous `<path>`.** Trails and packets (CSS `offset-path`) ride the edge's
  single path; the router must keep producing one path per edge.
- **Router anchoring is rank-aware.** Cross-rank edges must travel along the primary axis even when
  a wide fan-out makes them more horizontal (`AutoSides` in `Route/OrthogonalRouter.cs`). Picking
  sides by raw dx/dy alone sends fan-out edges detouring around nodes. Off-canvas edge routes
  (negative path coordinates) are the signature of a routing regression.
- **Measured widths guard the typography.** Every `<text>` whose width fed layout carries
  `textLength` + `lengthAdjust`, so a font mismatch squeezes glyphs instead of breaking layout.
  Change card box-model math only alongside the card-sizing tests.
- **The frozen goldens are the reference.** `Beck.Tests/Goldens` + `Corpus` were extracted
  from the original TypeScript engine (deleted from the repo; it lives in git history before this
  restructure). They are regression anchors — regenerate them only from the C# engine itself, and
  only when a change is *intentionally* visual.

## Authoring API ↔ schema contract

`Beck/Authoring/` (namespace `Beck`) is a dependency-free fluent builder family — one per
diagram type (`DiagramBuilder`, `SequenceDiagramBuilder`, `StateDiagramBuilder`,
`ClassDiagramBuilder` + its reflection `FromTypes`), sharing `MetaOptions` — that emits YAML via a
tiny hand-rolled `YamlWriter` (no YamlDotNet). The C# enums map to schema tokens in `Tokens.Of(...)`
(note `EdgeCurve.StepRound` → `"step-round"`, `Direction` stays uppercase). **When you add a YAML
field, update both `Model/` (the parser) and the matching builder** — they are two encodings of one
schema.

## Docs site (`docs/Beck.Docs`)

Pennington site. ` ```beck ` fences render to SVG at build time through `BeckSvgPreprocessor`
(flags: ` ```beck,static ` and ` ```beck,scrub `; ` ```beck:symbol ` reads YAML from a file). Razor
pages use the `<BeckDiagram Yaml=...>` component — same shared `BeckDiagramRenderer` singleton
(one `SkiaTextMeasurer` over the site's IBM Plex files). The playground is a Blazor WASM island
(`Beck.Docs.Client`) running the engine with a canvas-based measurer. `wwwroot/js/site.js` is page
chrome only (theme toggle, copy buttons, nav) — it must never grow rendering responsibilities.

## Verifying

`dotnet test` is the primary gate. For visual checks, run the docs site (`dotnet run --project
docs/Beck.Docs`) and inspect pages, or render a YAML file to SVG in a scratch page and screenshot
it headlessly via the Playwright MCP. Define `--color-*` vars on a test page to confirm
host-palette adoption; toggle `data-theme="dark"` to check both themes. Always check for
off-canvas edge routes (negative path coordinates) — the signature of a routing regression.
