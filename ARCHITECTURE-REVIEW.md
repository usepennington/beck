# Beck — Pre-1.0 Architecture & Simplification Review

_A structured review of the engine (`src/`), the .NET package (`Beck`), the docs
site, and the build/maintenance surface. Produced by a multi-agent pass (14 review
dimensions → adversarial verification of every finding against the code **and** the
load-bearing invariants in `CLAUDE.md` → completeness critics). 86 findings survived
verification; 4 were rejected as factually wrong (see the appendix). Breaking changes are
treated as **free** here — there are no users yet, and this is the window to take them._

---

## TL;DR — the verdict

Beck is in genuinely good shape. The pipeline is a clean sequence of near-pure stages, the
invariants are real and mostly honored, the CSS-var theming is elegant, and the
Node-free `dotnet pack` packaging is sound. The reviewers found **no architectural rot** —
what they found is a small number of high-leverage structural issues plus a long tail of
pre-1.0 polish.

Two findings dominate everything else, and they're linked:

1. **One schema, five hand-maintained copies.** The closed token vocabularies (node kinds,
   edge kinds, curves, accents, packet eases/shapes, flow-step grammar) are spelled out by
   hand in the TS union, a parallel TS runtime array, the C# enums, the docs Monaco schema,
   and the GSAP ease map — with *nothing* enforcing they agree. This is the #1 maintenance
   hazard and it already caused real drift.
2. **Zero automated tests on a pipeline that is unusually cheap to test.** Every stage
   before `render/` is a pure function with an explicit data contract. Nothing guards them.

These two are the spine of the recommended plan: **build the safety net first, then spend
the pre-1.0 breaking-change budget freely.** A vocab single-source plus a handful of pure
tests is what makes every other refactor below safe to do.

---

## Priority map

| # | Theme | Severity | Breaking | Effort |
|---|-------|----------|----------|--------|
| **P0** | Single-source the schema vocabulary (5 copies → 1 owner + 1 CI guard) | High | No | M |
| **P0** | Add the test safety net (pure model/layout/route + C# golden + cross-lang round-trip) | High | No | M |
| **P0** | CI gate: committed `beck.global.js` must match `src/` | High | No | Trivial |
| **P1** | Pick one public API surface (`window.Beck` vs `index.ts`) and freeze it | High | Yes | S |
| **P1** | Split `Beck.Authoring` out of the Razor RCL (drop ASP.NET dep for console consumers) | High | Yes | M |
| **P1** | Fix `effects.ts` module-global reset registries (multi-diagram pages clobber each other) | High | No | M |
| **P1** | `<beck-playback>` → light DOM (kill the last shadow root + full-stylesheet dup) | High | No | S |
| **P1** | `relayout()` throws if called before GSAP loads | High | No | Trivial |
| **P2** | Aggressive simplification sweep (drop unused curves, `ghost` kind, trim packet vocab, merge flow steps) | Med | Yes | M |
| **P2** | Route correctness: `straight`/`s` bypass obstacle avoidance; groups aren't obstacles | High/Med | No | M |
| **P2** | Split `layered.ts` (flat engine vs recursive driver) and decompose the 290-line function | High | No | S/M |
| **P3** | Lint/format (Biome + `.editorconfig` + `dotnet format`), C# analyzer gate, CI dedup | Med | No | S |
| **P3** | Missing infra: a11y, GSAP CDN pin/SRI, unknown-key validation, CONTRIBUTING/ARCHITECTURE | Med/High | Mixed | S–M |
| **P3** | Repo hygiene: gitignore is a scratch *allowlist*, not a rule | Med | No | S |

---

## Theme 1 — One schema, many copies (the headline)

The single most valuable structural change. Every closed token set
(`NodeKind`, `EdgeKind`, `EdgeCurve`, `NodeVariant`, `EdgeStyle`, `PacketEase`,
`PacketShape`, `Direction`, `ThemeMode`, `AccentToken`, `Side`, `ArrowEnds`) is currently
declared in **up to five** disconnected places:

- the TS string-literal **union** in `src/model/schema.ts`;
- a parallel **runtime array** for validation (`KIND_LIST`/`EDGE_KIND_LIST`/`SIDES`/`ARROW_ENDS`
  in `validate.ts`, inline `as const` arrays at the `oneOf` call sites, `ACCENT_TOKENS` in
  `util/color.ts`, `PACKET_EASES`/`PACKET_SHAPES` in `defaults.ts`);
- the **C# enums** + `Tokens.Of` casing rules in `Beck/Authoring/Enums.cs`;
- the **docs Monaco** `SCHEMA` autocomplete in `docs/Beck.Docs/wwwroot/js/site.js` (a verbatim
  hand-copy of ~70 icon names + every key list);
- the **GSAP ease map** `PACKET_EASE` in `animate/timeline.ts` (whose comment literally says
  "kept in lockstep with the C# `PacketEase` enum").

Nothing fails if you miss one. Adding a single `NodeKind` is a five-file, two-language edit;
miss the runtime array and valid YAML is silently rejected, miss the C# enum and a future
multi-word member (`NodeKind.LoadBalancer`) emits `"loadbalancer"` which the parser rejects
at runtime with no compile-time signal.

**Plan (phased, low risk first):**

1. **Collapse the TS copies (near-zero risk, no test infra needed).** Make one `as const`
   tuple per vocabulary the source of truth (in `schema.ts` or a new `src/model/vocab.ts`),
   derive the union via `type NodeKind = typeof KINDS[number]`, and delete the inline arrays
   + the `as NodeKind`/`as Side` casts in `validate.ts` (those casts are *what defeats checking
   today*). Re-derive `AccentToken` from the shared array — `ACCENT_TOKENS` is currently typed
   `readonly string[]` with **no** relationship to the union, so this is a real type-safety gain.
   For arrays you keep hand-written, add `as const satisfies readonly PacketEase[]` so a
   mismatch in either direction is a `tsc` error.
2. **Generate the docs schema** from the TS vocab (emit a `vocab.json` at `build:lib` time
   that `site.js` consumes) instead of the hand-typed copy — or, as an interim, a CI assertion
   that `Object.keys(ICONS)` matches the docs icon list.
3. **Close the cross-language gap** (the only part a compiler can't): a checked-in `tokens.json`
   (or small generator) that emits both the TS tuples and the C# `enum` + `Tokens.Of` casing
   rules, so the two exceptions (`Direction` uppercase, `EdgeCurve` → `step-round`) live in one
   table. If you keep `Tokens.Of` hand-written, at minimum **drop the `_ =>` catch-all** so C#
   switch-exhaustiveness flags any new member, and annotate the exceptions with a
   `[WireToken("step-round")]`-style attribute. (Keep the per-type switches — don't rewrite all
   11 overloads into one reflection method; that loses the overload safety the dependency-free
   lib relies on.)

Related: the `parseStep` 115-line if-chain (`validate.ts`) and the C# `FlowBuilder` are two
hand-written encodings of the same 14-step grammar. Replace the if-chain with a
`STEP_BUILDERS: Record<key, builder>` table so the grammar is *enumerable* (and the
"must have one of…" error derives from `Object.keys`), giving a single machine-readable anchor
to diff against the C# step set.

---

## Theme 2 — The test safety net (what makes everything else safe)

There are **zero** automated tests, yet the pipeline is almost ideal for cheap, high-signal
testing: `loadDiagram(yaml) → DiagramModel`, `layeredLayout(model, sizes)`, and
`routeEdge(req)` are all pure, DOM-free, deterministic functions. Add **Vitest** (Vite is
already the build tool) + one `Beck.Tests` (xUnit) and cover, in ROI order:

1. **`validate.ts` + `defaults.ts` snapshots** — the single most load-bearing file. Snapshot
   `loadDiagram()` over the three `playground/samples` files plus inline fixtures: bare
   nodes-only (asserts derived flow + filled defaults), one-of-every-node-kind, one-of-every
   flow-step, legacy `arrow: true/false`, accent-token mapping, `meta.loop:false` → `repeat 0`,
   burst-count clamp boundaries. Plus a `toThrow(BeckError)` block for each error path
   (duplicate id, unknown endpoint, two-group membership, self-nesting, empty nodes). A dropped
   default becomes a loud diff.
2. **Cross-language round-trip** — the test that justifies the two-codebase design. Emit YAML
   from C# exercising *every* enum token and *every* flow step (the current sample omits
   burst/highlight/pulse/activate/stream/fail/phase/reset), feed each through the real
   `loadDiagram` under Node, assert no `BeckError`. Add adversarial scalars for the `YamlWriter`
   quoting rules (`0xFF`, `no`/`off`/`yes`/`~`, trailing space, control chars) and assert
   identity survives (title `0xFF` stays the string, not `255`). Wire into `ci.yml`.
3. **`routeEdge` property tests** — assert the two invariants `CLAUDE.md` flags as
   breakage-prone: exactly one `M` (single continuous path, non-empty), and — with obstacles
   forced so `laneDetour`/`clampLane` actually fire — coords stay on-canvas. Unit-test
   `roundedPath` collinear-skip / radius-clamp / `<2`-point cases.
4. **`layeredLayout` snapshot** with a synthetic `SizeMap` (every node 180×64) — determinism
   (twice → identical), snapshot of rounded geometry, and the structural invariant that every
   group rect strictly contains its members. (Skip "no negative coords" here — it's a tautology
   post-normalization; that guard belongs in the route test.)
5. **C# golden suite** (`Beck.Tests`) — `YamlWriter.Scalar` edge cases, builder field
   ordering, `ValidateEdgeEndpoints` throwing, `Parallel` nesting, empty-flow emission. Prefer
   plain string goldens (keeps the project dependency-light). **Add `dotnet test` to *both*
   `ci.yml` and `publish.yml`** — `publish.yml` is tag-triggered and packs with no gate today.
6. **One Playwright smoke test per sample** *last* — lowest signal-per-failure; the backstop
   for the `measure→layout→render→embed` seam and the render↔animate class vocabulary the pure
   tests can't reach. Static-frame only (GSAP-free), against the committed bundle.

A cheap mechanical guard worth calling out: change `PACKET_EASES`/`PACKET_SHAPES` to
`... as const satisfies readonly PacketEase[]` — combined with the existing
`Record<PacketEase, string>` GSAP map, that makes any TS-side ease drift a *compile* failure
with no test infra at all.

---

## Theme 3 — One public API surface, frozen before 1.0

There are **two** API definitions that disagree and drift independently:

- `src/index.ts` (ESM) exports `renderDiagram, mountModel, loadDiagram, defineBeckElements,
  BeckDiagramElement, BeckPlaybackElement, setGsapUrl, STYLES`, and **every** schema type — but
  this build is explicitly "not used by the package" (`vite.config.lib.ts`), so no real consumer
  can reach it.
- `src/global.ts` (IIFE — the *only* shipped artifact) exposes
  `window.Beck = { renderDiagram, setGsapUrl, defineBeckElements, hydrate }`.

They're not subset/superset: `hydrate` ships but isn't in `index.ts`; `mountModel`/`loadDiagram`/
`STYLES`/the element classes/all schema types are in `index.ts` but unreachable. Concrete fixes:

- **Decide the canonical surface.** Either drop `src/index.ts` + the ESM format entirely (the
  package has exactly one face: `window.Beck`), or ship the ESM build as a real library face and
  document it. Don't let the vite target silently decide.
- **Type `window.Beck`.** Add `declare global { interface Window { Beck?: BeckGlobal;
  BeckConfig?: BeckConfig } }` and an exported `BeckGlobal` interface; replace the
  `as unknown as { Beck }` self-cast in `global.ts`. (This is internal type hygiene — it does
  *not* give the plain-JS docs `site.js` a typed API, so don't oversell it.)
- **Rename `hydrate` → `rescan`.** `window.Beck.hydrate()` actually calls the internal `rescan`
  closure (re-scan the DOM for new ` ```beck ` blocks), not "start hydration". Misleading name on
  the one method with no docs.
- **Unify the theme word.** It's spelled `mode` (element attribute), `theme` (`RenderOptions`/
  `meta`/`setTheme`), and `ThemeMode` (type). Rename the element attribute to `theme`
  everywhere — **including** `styles.css`'s `:host([mode='dark'])` selector (which is also dead,
  since diagrams render in light DOM — likely just delete it).
- **Replace `export type * from './model/schema'`** with an explicit named list, so adding a
  future internal-only field to a schema interface doesn't silently become a public, frozen-at-1.0
  type. Also tighten `model/index.ts`'s `export *` to a named re-export to close a latent leak of
  `parseYaml`/`buildModel`/`topoOrder`/etc.
- **Drop `STYLES` from the public surface** (internal CSS-injection plumbing; also the only
  SCREAMING_CASE export). **Remove `timeline: Timeline | null` from `DiagramHandle`** — it leaks
  the GSAP type into the public API and lets callers desync the snapshot — and narrow
  `seek(label: string | number)` to `seek(label: string)` (no consumer uses numeric seek).

---

## Theme 4 — The packaging boundary

- **Split `Beck.Authoring` into its own dependency-free classlib.** `Beck.csproj` uses
  `Microsoft.NET.Sdk.Razor`, which pulls in `Microsoft.AspNetCore.App`. But `Beck.Authoring`'s
  whole pitch is "walk an Aspire graph / EF model / service registry into a `DiagramBuilder`" —
  exactly the console/tooling/source-generator scenarios that should **not** drag the web runtime.
  Create `Beck.Authoring` (`Microsoft.NET.Sdk`, multi-target `netstandard2.0;net8.0` — the
  `YamlWriter` already has the `NET7_0_OR_GREATER` guard), keep the namespace `Beck` (source
  non-breaking), have the RCL `ProjectReference` it. Ship two packages. `CLAUDE.md` already flags
  this as deferred — pre-1.0 with zero users is when it's free. Move `BeckMarkdown.cs` (pure
  string helper, used by `ToFence()`) into the authoring assembly; leave web-only `BeckAssets` in
  the RCL. Consider giving them a real `Beck.Authoring` namespace now so the split is mechanical.
- **CI bundle-freshness gate** (appears under build/packaging/hygiene — it's one fix). All three
  workflows run `npm run build:lib` (overwriting the committed bundle) before building .NET, but
  none diffs it, so a PR that changes `src/` and forgets to rebuild+commit passes green and ships
  stale JS to anyone who `dotnet pack`s locally. Add, after `build:lib`:
  `git diff --exit-code -- Beck/wwwroot/beck.global.js`. Use `git diff` (not byte-compare)
  for LF/CRLF normalization, and add a `.gitattributes` pinning the file `text eol=lf`. The build
  is byte-reproducible, so the gate is zero-flake.
- **Package metadata polish:** no `PackageIcon` (blank avatar on nuget.org for a *visual* tool —
  add a 128×128 brand PNG), and the packed `README` ends with "See the project README for the
  full YAML schema" with no link (dead pointer on NuGet). Pin `<StaticWebAssetBasePath>Beck</…>`
  so a future RCL rename can't 404 the hardcoded `BeckAssets.ScriptPath`.
- **Publish flow:** add a GitHub Release step (with `contents: write` + `generate_release_notes`)
  and a guard that the `v*` tag commit is an ancestor of `main` before packing.

---

## Theme 5 — The embed surface (where core-embed leverage lives)

- **`<beck-playback>` still uses shadow DOM** and injects the *entire* ~300-line diagram
  stylesheet to style two buttons — architecturally inconsistent with the deliberate light-DOM
  decision for diagrams, wasteful, and the shadow boundary blocks the host palette. Render it in
  light DOM like `<beck-diagram>`; export `ensureStyles` from `core.ts` and call it in
  `connectedCallback` (don't rely on render order). Removes the last shadow root and the
  per-element stylesheet dup.
- **`relayout()` throws before GSAP loads** — it calls `wireAnimation()` → `buildTimeline()` →
  `gsap()` which throws `"GSAP is not loaded yet"`. Mount guards this behind `await loadGsap()`;
  `relayout()` doesn't. Guard it with `gsapLoaded()` (already exported), and flip an
  `animationAvailable` flag off on permanent CDN failure so it stops re-attempting.
- **`build()` tears down and recreates *all* DOM** (title, subtitle, every card, re-measure) on
  every `relayout()` — the comment even says "rebuilt each time for simplicity." Split into
  `createDom()` (once, caches the `SizeMap`) and `applyLayout()` (re-run layout from cached sizes,
  reposition existing wraps). Note `relayout()` has **no in-repo callers** today, so also weigh
  whether it should exist at all. (The resize path is `fit()`, which only rescales — no flash.)
- **Theme detection is reimplemented in three places** with different rules (`core.ts`
  prefers-color-scheme; `hydrate.ts` `hostIsDark()` reads `html.dark`; `element.ts` passes the
  attribute through). Keep `core.ts` as the single applier, extract `hostIsDark()` into a shared
  util, and teach `core`'s `auto` mode to honor the host's `html.dark`/`[data-theme]` so
  `hydrate` can drop its bespoke `MutationObserver`. Delete the dead `:host([mode='dark'])` CSS.
- **`renderDiagram` throws on bad YAML but only the two embed callers guard it** (duplicated
  `.beck-error` rendering). Fold a shared `renderError(host, err)` into `renderDiagram` itself so
  the documented public entry point is safe-by-default and returns a no-op handle; delete the two
  copies. (See also Theme 8: preserve the author's source in the error view, and theme the box
  via `--beck-danger` instead of the hardcoded `#e11d48`.)
- Minor: dead `.beck-playback-btn.is-active` CSS (toggle never applied); the two hydrate
  `MutationObserver`s are actually correctly scoped (a verifier *rejected* merging them) — the
  only real nicety there is an optional disposer to tear hydration down.

---

## Theme 6 — Aggressive simplification (pre-1.0 only; all breaking)

A usage scan across every shipped sample/doc shows large parts of the vocabulary are barely or
never exercised. Each token removed deletes an entry from *all* the synchronized tables in
Theme 1 — so these compound with the single-source work.

- **Delete the `straight` and `s` edge curves.** No shipped YAML sets `curve` at all; everything
  uses the `step-round` default. They also bypass obstacle avoidance and bounds clamping (Theme
  7). Removing them deletes `sCurve`, two early-return branches, two schema/enum members, **and**
  the single bespoke multi-word `Tokens.Of` exception (`EdgeCurve.StepRound`) — which makes the
  C# token mapping uniform with no special case left.
- **Drop `ghost` as a `NodeKind`** (keep it only as a `NodeVariant`). `render/node.ts` already
  collapses them: `isGhost = variant === 'ghost' || kind === 'ghost'`. Two closed vocabularies,
  each duplicated into C#, for one boolean.
- **Trim the packet motion vocabulary.** 8 eases × 3 shapes, each maintained in four places, for
  a feature samples use twice. Cut eases to the 3–4 that read distinctly on a sub-second dot
  (`linear`/`smooth`/`accelerate`/`decelerate`; drop `expo`/`sine`/`steps`/`bounce`); collapse
  `PacketShape` to `dot|ring` (`circle` is just `dot` with `size:12`), which also deletes
  `PACKET_SHAPE_SIZE` and a fallback layer from `hopOptions`.
- **Merge near-duplicate flow steps.** `highlight`+`pulse` share one save/scale/restore skeleton;
  `activate`+`stream` are both "make this edge look busy until reset." Fold to one node-emphasis
  and one edge-emphasis step (with an optional knob if the variant matters). Removes union
  members, C# methods, parse branches, and `execStep` cases — and shrinks the surface the
  `effects.ts` reset refactor (Theme 7) must cover.

These are judgment calls about product scope, not just code — but pre-1.0 is precisely when to
make them, and the maintenance dividend is multiplied by the duplication count.

---

## Theme 7 — Correctness risks worth fixing

- **`effects.ts` keeps reset registries as module globals** (`lineOrig`, `markerOrig`,
  `workingCards`) shared across *all* diagrams on a page. `resetLinesNow`/`resetWorkingNow` run on
  every `Snapshot.restoreNow()`, so when one looping diagram resets it clobbers the
  `activate`-recolored edges and `working` nodes of **every other diagram** — exactly the
  multi-block Pennington docs page. Move this state into per-instance state owned by the
  `Snapshot` (mirror how `TrailState` is threaded). The clean version is data-driven reset:
  producers push a `reset: () => void` closure into per-instance state, and `restoreNow` just runs
  them — which also collapses the four dispatch styles (captured styles, SVG `data-*` querySelector,
  trail iteration, the two globals) into one list and removes the cross-module marker-string
  contract.
- **`straight`/`s` curves bypass obstacle avoidance AND bounds** (if not deleted per Theme 6):
  route `straight` through `orthogonalPolyline` + `roundedPath(poly, 0)` so it avoids nodes;
  keep `s` cosmetic but clamp its cubic control points to `bounds`.
- **Group boxes are never obstacles** — edges between unrelated nodes route straight through a
  third group's box and label. Add group rects (minus the endpoints' own ancestor/descendant
  chain) to the obstacle set with a larger inset. Decide intent explicitly and document it.
- **`measureNodes` silently drops unmeasurable nodes** and three stages independently tolerate a
  missing id by fallback/skip — robust, but a real bug surfaces as a quietly mis-sized node.
  Add a dev-only guard that every `model.nodes` id measured. (A verifier corrected the original
  claim: layout already iterates `model.nodes` as the authority, so only the silent *mis-sizing*
  path is real.)

---

## Theme 8 — Maintenance, tooling & missing infrastructure

- **No lint/format anywhere** — `tsc` is the only gate. Add Biome (lint+format, one binary) for
  `src/` + `playground/` wired into CI, a root `.editorconfig` covering TS *and* C#, and
  `dotnet format --verify-no-changes` in the .NET step. (Biome won't flag unused *exports* — add
  `knip` if dead-export pruning matters.)
- **No C# analyzer/warning gate.** The TS side is strict; the C# side runs a bare `dotnet build`
  that passes with warnings. Add `<TreatWarningsAsErrors>true</…>` + `<AnalysisLevel>latest-
  recommended</…>` to `Directory.Build.props` (keep the `NoWarn CS1591` exception).
- **CI duplication & gaps:** the three workflows repeat the Node + `build:lib` + dual-SDK setup
  verbatim — extract a composite action. CI never runs `npm run build` (the playground/Tailwind
  pipeline) — add it. Delete the never-consumed ESM branch in `vite.config.lib.ts` (and remove
  the `BECK_FORMAT=esm` mention from `CLAUDE.md` and `README.md`).
- **GSAP CDN supply-chain:** `loadGsap` imports a *floating* `gsap@3/+esm` with no integrity pin.
  Every consuming docs site runs whatever that URL returns. Pin the exact version
  (`gsap@3.12.7`), document a self-host recipe + CSP `connect-src` guidance, and a
  `data-gsap-version` knob. (Keep it a dynamic import — don't bundle.)
- **Accessibility is entirely absent** — the diagram DOM exposes nothing to assistive tech: no
  `role`/`aria-label` on the root (despite `meta.title` being right there), nodes are
  class-only `div`/`span`, the SVG overlay has no `role="img"`/`<title>`/`<desc>`, edges are
  anonymous paths. For a library embedded in docs sites this is close to a shipping blocker.
  Start with the pure-additive markup: figure role + node `aria-label` from data the model
  already fills, then an axe-core CI gate over the samples.
- **Unknown-key validation:** `asObject` accepts any mapping and validators read only known keys,
  so `tilte:`/`noeds:`/`acent:` silently produce a defaulted diagram with no signal — the worst
  authoring loop for a declarative format. Reject unknown **top-level** keys first (zero
  false-positive risk), then per-entity with a "did you mean?" suggestion.
- **`BeckError.line` is half-built:** only parse errors carry a line; all ~17 semantic throws in
  `validate.ts` are line-less because `yaml.load` discards positions. Either commit to it (parse
  with a position-preserving representation and thread `line` through the coercion helpers for
  the high-frequency throws — unknown endpoint, duplicate id, bad enum) or stop implying the
  capability. The current half-state is the worst option. (`README.md:37` overstates this.)
- **No CONTRIBUTING / ARCHITECTURE / CHANGELOG / SECURITY**, and `README.md` claims "complete and
  verified end-to-end" while there's no test suite. Add a human-facing `ARCHITECTURE.md` (the
  pipeline + invariants currently live only in the AI-facing `CLAUDE.md`), a `CONTRIBUTING.md`
  documenting the two-implementations rule and the `build:lib`+commit dance, a `CHANGELOG.md`
  tied to the MinVer tag flow, and soften the README status claim.
- **Repo hygiene is about *rules*, not files** — confirmed firsthand: only 111 files are tracked
  and *nothing* generated is committed (`_site/`, `output/`, root PNGs, `gallery.html`,
  `local-feed/` are all ignored). The real issue is `.gitignore` pins exact personal scratch
  filenames (`/gallery.html`, `/sample-flow.yaml`, `/*.png`) — the next person's `verify.html`
  leaks, and `/*.png` would silently ignore a future tracked logo. Replace with a `/scratch/`
  convention; keep `.playwright-mcp/` and `/local-feed/` as their own entries; delete the
  redundant nested `docs/Beck.Docs/.gitignore` (folding `_site/` into the root). Document that
  `Beck/wwwroot/beck.global.js` is the *one* intentionally-committed generated file so no
  cleanup deletes it.

---

## Per-subsystem notes

**`model/`** — Well-organized; the "fill every default" contract mostly holds. Beyond Theme 1:
`surface`/`textColor` skip the `accentToCss` resolution that `accent` gets (route them through
the existing `optColor` so tokens work uniformly); document the *intentional* exceptions to the
fill-every-default rule (`PacketKnobs` deferred to the animator per edge-kind; `fromSide`/
`toSide` deferred to the rank-aware router); promote the scattered scalar defaults (burst
`count`/`stagger`, `wait`, `repeat`) into named exports in `defaults.ts`, and reconcile the
pre-existing divergence where `deriveFlow` hardcodes `repeatDelay 1.2`/`wait 1` vs the authored
`1.5`/`0.5`.

**`layout/`** — `layered.ts` (457 lines, the biggest file) holds two unrelated algorithms:
split the flat `layoutLayer` engine into `layout/layer.ts` (exporting `LayItem`/`LayerResult`/
`FALLBACK`) and keep the recursive `layeredLayout` driver in `layered.ts`. Then decompose the
290-line `layoutLayer` into named per-stage functions (cycle-break → rank → virtuals →
barycenter → secondary → primary → direction), being explicit about which mutable maps each
reads/writes. Export `GROUP_PAD` (or, better, return a precomputed label anchor in
`LayoutResult.groups`) so `core.ts` stops re-deriving `gr.x + 14 / gr.y - 9` by eye. Hoist the
two magic `6`-sweep loops to `ORDER_SWEEPS`/`COORD_SWEEPS`; replace the virtual-id-substring
tie-break with an explicit `Map<id, seq>`; tighten `FALLBACK`'s loose typing and stop borrowing
`FALLBACK.h` as the rank-depth floor.

**`route/`** — Well-factored; `roundedPath` correctly preserves the single-path invariant and
the rank-aware `autoSides`/`clampLane` show the off-canvas hazard was understood. Real items are
in Theme 7. Delete the dead `route/index.ts` barrel and the `export { dist }` (both real
consumers import `./route/svg` directly). `autoSides` re-derives rank direction from pixel
geometry + a 6px epsilon — *don't* try to thread true ranks (they're per-container local, so
cross-boundary edges have no single delta); just document `SAME_RANK_EPS` as a layout-coupling
assumption, optionally expressed relative to `meta.spacing.rank`.

**`render/`** — Small and deliberate (the Tailwind-vs-CSS-var split is well-documented — a
verifier *rejected* the claim it's ad-hoc). Real wins: `createNode` has two parallel
solid-vs-ghost build branches — extract shared `buildIcon`/`buildStatusPill`/`createIconChip`
helpers and move the ghost color overrides into the var system (so `.beck-node--ghost` reads
`--beck-node-bg`/`--beck-text`) rather than inline styles; keep the two genuinely-different
container shapes. The icon registry stores ~18 aliases as full duplicate SVG strings — define
each glyph once + an `ALIASES` lookup table resolved at lookup time. A tiny `el(tag, hook, util,
text?)` helper removes ~5–8 repeated `createElement/className/textContent` triplets.

**`animate/`** — The largest, most carefully-commented subsystem; the GSAP-types-only/dynamic-
import discipline is clean. Beyond the Theme 7 reset-state fix: `packet.ts` ships a full
entry/exit-ramp feature that's **dead** (every caller passes `noEntry/noExit` true) — delete
~25 lines and two options. Split the stateful pieces out of `effects.ts` (`colorLine`+registry →
`recolor.ts`; `working` state machine → `working-state.ts`) leaving a pure card-beat module.
Narrow `colorLine` to a single path (the array form has one caller). Fix the stale `Snapshot`
doc comment claiming it drives the reduced-motion frame (it doesn't — that path never builds a
timeline).

**`Beck/Authoring/`** — Clean and well-documented; the hand-rolled `YamlWriter` is
**justified** (don't adopt YamlDotNet — it'd add a transitive dep to a dependency-free lib) but
should be pinned with the scalar round-trip test. Real drift already exists: **C# `Burst` can't
author `via`** though the engine supports it — add it and factor the shared from/to/via/color/
knobs tail so Packet and Burst can't diverge again. Make flow colors typed (a `Color` value type
with `Color.Token`/`Color.Css`) to match `NodeBuilder.Accent`'s typed overload and kill the
magic-string typo path. Group the 11-param packet-knob signature into a `PacketKnobs` carrier
(mirroring the TS interface). Make C# `count`/`stagger` nullable+omit-when-unset so the TS parser
is the sole owner of those defaults. Give `FlowBuilder` a structured step model instead of
pre-serialized YAML strings — it unblocks flow-endpoint validation and makes `Parallel` naturally
recursive.

---

## Appendix — claims the verifiers REJECTED (checked and dismissed)

The adversarial pass killed 4 plausible-sounding findings, which is worth recording so they
aren't re-raised:

1. **"`clampLane` only clamps the cross axis; channel offsets and the simple route can escape the
   canvas."** Geometrically false. Channel offsets move *inward* toward the partner anchor; the
   simple/elbow routes use only anchor coords and their midpoints; every anchor is ≥16px inside
   the canvas (`CANVAS_PAD`). The lane is the *only* escape vector and it's already clamped. The
   proposed defensive `clampPoint` pass would guard provably-in-bounds coordinates and risks
   breaking the right-angle shape.
2. **"The Tailwind-vs-CSS-var split is ad-hoc with one leak (group label)."** False — `node.ts`
   has an authoritative contract comment (with the MonorailCSS-scan rationale), `group.ts` and
   `styles.css` cross-reference it, and the group-accent-inline pattern is the *same* deliberate
   one `node.ts` uses. Hoisting a single-use class string adds indirection.
3. **"Stale `0.1.0` nuspec proves metadata can regress; add `EnablePackageValidation`."** False —
   the stale nuspec predates the metadata file's existence (git history confirms), lives in
   gitignored `obj/`, and `EnablePackageValidation` validates *API surface*, not nuspec metadata.
4. **"C# builder can't express nested `parallel` / burst-to-groups / phase-wait-reset."** False —
   traced the code: nested `parallel` emits correctly (every step is single-key by construction),
   `Burst(from, IEnumerable<string>)` handles group fan-out, and `Phase`/`Wait`/`Reset` all exist.
   All 14 flow steps have a C# method; the surface is *not* a subset.

---

## Recommended sequence

1. **Safety net + freshness gate (P0).** Vitest snapshots over `validate`/`defaults`, the
   cross-language round-trip, the `git diff` bundle gate. This is what lets you take every
   breaking change below without fear.
2. **Single-source the vocabulary (P0).** TS collapse first (`satisfies` + derived unions, delete
   casts), then the docs generation and the C#↔TS token guard.
3. **Freeze the public surface + split the package (P1).** Pick one API face, type `window.Beck`,
   rename `hydrate`/`mode`, prune internals; split `Beck.Authoring` out of the RCL.
4. **Correctness fixes (P1).** Per-instance reset state, `relayout` GSAP guard, light-DOM playback.
5. **Simplification sweep (P2).** Delete `straight`/`s`, `ghost`-as-kind, trim packet vocab, merge
   flow steps — each multiplied by the now-single-sourced tables. Split/decompose `layered.ts`.
6. **Infra & hygiene (P3).** Lint/format, C# analyzer gate, a11y, GSAP pin, unknown-key
   validation, CONTRIBUTING/ARCHITECTURE/CHANGELOG, gitignore scratch convention.
