# The Bad Idea

**Beck.Rendering: a pure-C# renderer that turns Beck YAML into one self-animating inline `<svg>`. No JavaScript. No GSAP. Full sexy.**

Status: **implemented — and no longer a bad idea; it's the only engine.** The repo has since moved
entirely to C#: the TypeScript engine this spec cites as its oracle has been deleted (it lives in
git history before the .NET-only restructure), the renderer ships as the `Beck` NuGet package
(project paths below say `Beck.Rendering`/`Beck.Rendering.Skia`; today they are `dotnet/Beck` and
`dotnet/Beck.Skia`, with namespaces unchanged), and the extracted goldens in `dotnet/Beck.Tests`
are the frozen parity reference. This document remains the design spec for the engine's algorithms,
constants, and CSS compilation model.

---

## 1. What and why

Today Beck ships a TS engine that renders in the browser: measure cards with `getBoundingClientRect`, lay out, route, then animate with a CDN-loaded GSAP timeline. The intended consumer is a Pennington docs site that builds **static HTML**. This project makes the diagram itself static too: a C# library takes the same YAML and emits a single self-contained `<svg>` element in which the entire flow choreography — packets, trails, elastic highlights, status pills, narration captions, sequence dimming — runs as **CSS animations baked into a `<style>` block inside the SVG**.

What makes this feasible (established by codebase survey):

- **Layout and routing are pure math.** The Sugiyama-lite layered engine, the recursive group scheme, the sequence grid, and the orthogonal router touch no DOM. They port mechanically.
- **Theming already survives.** Every color is a `--beck-*` CSS variable; inline SVG participates in the host page's cascade, so host-palette adoption and `[data-theme="dark"]` flipping work identically with zero per-theme rendering.
- **Half the animation layer is already declarative.** `stream` and `working` are CSS animations in the current engine. Trails are cloned-path `stroke-dashoffset` reveals. Packets are SVG circles sampled along a path — exactly what CSS `offset-path` does natively. Flow packets always run in trail mode (`noEntry`/`noExit`), so the packet radius never animates during flow.
- **Nothing in the choreography needs runtime input.** Narration pacing is word-count math over YAML text (`narrate.ts:11`). Every duration derives from path lengths C# computes itself. The GSAP timeline is a deterministic function of the model — which means it can be *compiled* instead of *executed*.

The two genuinely hard problems, and their answers here:

1. **Text measurement without a browser.** An `ITextMeasurer` abstraction. Users who name their font get exact SkiaSharp + HarfBuzz shaping (`Beck.Rendering.Skia`). Everyone else gets an embedded metrics table for Inter (Beck's default `--beck-font`). Every emitted text run carries a `textLength` guard so a mispredicted width squeezes typography instead of breaking layout.
2. **GSAP's imperative timeline → declarative keyframes.** A two-pass compiler: simulate the flow exactly as `timeline.ts` does to produce an absolute schedule, then emit one shared-duration CSS animation per element with percentage-window keyframes. Overshoot/oscillating eases (`back.out(2)`, `elastic.out(1, 0.4)`, `bounce.out`) are sampled analytically into CSS `linear()` timing functions — visually indistinguishable from GSAP.

**Browser floor:** the design leans on `color-mix()` (already required by the existing engine), `linear()` easing, CSS-animatable SVG geometry (`r`, `cx`, `cy`), and `offset-path: path()` on SVG elements. That is evergreen browsers from roughly mid-2024. Acceptable: the current engine already requires `color-mix`.

---

## 2. Goals and non-goals

### Goals

- **G1 — Static-frame parity.** For every sample in the repo, the C# SVG's static frame is visually indistinguishable from the JS engine's rendering (±1px layout tolerance) when both use the same font.
- **G2 — Full animation fidelity.** Every flow step kind (all 15: `packet`, `burst`, `status`, `highlight`, `pulse`, `activate`, `stream`, `working`, `idle`, `fail`, `narrate`, `phase`, `wait`, `reset`, `parallel`) compiles to CSS. Derived flows, sequence choreography (dim/reveal/finale), and narration included.
- **G3 — Host-palette adoption preserved.** Inline embedding keeps the exact `--beck-*` → `--color-*` → literal fallback cascade and `[data-theme="dark"]` behavior.
- **G4 — Zero runtime dependencies for the consumer.** No script tag, no CDN, no hydration. The SVG is the artifact.
- **G5 — User-specified fonts measured exactly.** SkiaSharp/HarfBuzz shaping when the user provides font files; graceful metrics-table fallback when they don't.
- **G6 — Deterministic output.** Same YAML + same options → byte-identical SVG. (Ids and keyframe names derive from a content hash, not randomness.)

### Non-goals (v1)

- **No programmatic `DiagramHandle`** (`play`/`pause`/`seek`/`relayout`/`setTheme` API). `setTheme` is free anyway (flip `data-theme` on any ancestor). Frame-accurate `seek()` has no CSS equivalent; the optional scroll-scrub mode (§10.8) is the consolation prize.
- **No play-on-scroll-into-view.** The JS engine gates playback on an IntersectionObserver (`core.ts:230`, threshold 0.2). Pure CSS cannot express "start once when visible." Default is play-on-load; `animation-trigger` can be adopted later when it ships broadly (§14).
- **No responsive re-layout.** The SVG scales via `viewBox` (which natively reproduces `fit: shrink`); it never re-wraps.
- **No changes to `src/` or to the shipped `Beck` RCL package.** The TS engine remains the playground/live-editing experience and the parity oracle.
- **No SMIL.** All-CSS was chosen deliberately: one clock strategy, `prefers-reduced-motion` works as a media query, and scroll-scrub composes later. Do not mix in SMIL.
- **No shared object model with `Beck.Authoring`** in v1. The builders eagerly flatten sequence/state/class/flow records to YAML strings and keep leaf fields private (`SequenceBuilder.cs`, `FlowBuilder.cs:33`), so YAML text is the bridge: `builder.ToYaml()` → `BeckSvg.Render(...)`. This preserves the repo's "one schema contract" philosophy. A shared model is future work (§14).

---

## 3. Load-bearing design decisions

| # | Decision | Rationale |
|---|---|---|
| D1 | **Input is YAML text**, parsed with YamlDotNet into an untyped node tree, with a C# port of `model/coerce.ts` on top | Matches js-yaml tolerances (numeric strings, bool strings, silent drops). Untyped tree = all scalars are strings; the coercion layer already handles string-or-typed inputs, so semantics match. No Authoring refactor needed. |
| D2 | **Two new projects**: `Beck.Rendering` (plain classlib, net8.0, only dep YamlDotNet) and `Beck.Rendering.Skia` (SkiaSharp + HarfBuzzSharp measurer) | The existing `Beck` package is a Razor RCL riding the ASP.NET shared framework — wrong home for a console-usable renderer. Skia's native assets must never be mandatory; the measurer is a plug-in. |
| D3 | **All-CSS animation, shared-cycle model.** Every animated element gets animation(s) whose duration is the whole cycle `T`; its action occupies a percentage window; everything starts together and loops in lockstep forever | This is the only robust way to keep dozens of elements synchronized across infinite iterations without JS. Per-element `animation-delay` chains drift on iteration and can't loop as a unit. |
| D4 | **Eases compile to `linear()`** (sampled from analytic Penner-equivalent functions), except `linear`→`linear` and `steps`→`steps(12)` | One uniform mechanism handles monotonic, overshoot (`back`), oscillating (`elastic`), and `bounce` eases. Deterministic sampling → deterministic output (G6). |
| D5 | **`ITextMeasurer` with two implementations + `textLength` guard on every text run** | See §7. |
| D6 | **Node cards become pure SVG primitives** — no `<foreignObject>` | `foreignObject` dies in `<img>` contexts and sanitizers. There is no text wrapping anywhere in Beck's cards (all roles are single-line, cards grow to content — `measure` survey confirmed no max-widths), so SVG `<text>` suffices. |
| D7 | **Initial "animation states" live in motion-gated CSS, not markup attributes** | Under `prefers-reduced-motion`, the JS engine never builds the timeline, so nothing is dimmed/hidden — the static frame is the fully-revealed diagram. Baking sequence dimming into attributes would break that. Dim rules go inside `@media (prefers-reduced-motion: no-preference)`; the reduced-motion frame falls out for free. |
| D8 | **All ids, class-scoping, and keyframe names are suffixed with an 8-char content hash** of (YAML + options) | Keyframe names and `url(#...)` ids are document-global; multiple diagrams per page must not collide. Content hash, not a counter or timestamp (G6). |

---

## 4. Project layout

```
dotnet/
  Beck/                      # existing RCL — UNTOUCHED in v1
  Beck.Rendering/            # NEW: the engine
    Beck.Rendering.csproj    # Microsoft.NET.Sdk, net8.0, <PackageReference: YamlDotNet>
    Model/                   # port of src/model (schema, coerce, defaults, validate,
                             #   sequence, state, classes) + Yaml/ (YamlDotNet → node tree)
    Text/                    # ITextMeasurer, FontRole, InterMetricsMeasurer + embedded
                             #   metrics resource, CardSizer (box-model math)
    Layout/                  # port of src/layout (layered, sequence, types) + SharedConstants
    Route/                   # port of src/route (orthogonal, step-round, svg geometry,
                             #   sequence, label placement)
    Svg/                     # SvgWriter, node shape templates, markers, icons, theming <style>
    Animate/                 # ScheduleBuilder (timeline simulation), Easing, CssCompiler
    BeckSvg.cs               # public entry: Render(yaml, options)
    SvgRenderOptions.cs
  Beck.Rendering.Skia/       # NEW: exact measurement
    SkiaTextMeasurer.cs      # SkiaSharp + HarfBuzzSharp shaping
    BeckFontSpec.cs
  Beck.Rendering.Tests/      # NEW: xunit + golden files (first automated tests in the repo)
```

Conventions (match the existing C# side): file-scoped namespaces, `Nullable`+`ImplicitUsings` enable, `sealed` types, private `_camelCase` fields, XML docs on public members, **`CultureInfo.InvariantCulture` on every number-to-string** (SVG coordinates especially), `InvalidOperationException`/`BeckYamlException` with actionable messages at render time. Coordinates round to 2 decimals exactly like `step-round.ts:53` (`Math.Round(n * 100) / 100`).

Central package versions go in `Directory.Packages.props`. The new projects join `Beck.slnx`.

---

## 5. The pipeline and porting map

```
YAML ──YamlDotNet──▶ node tree ──Model/──▶ DiagramModel (all defaults filled)
     ──Text/──▶ SizeMap (card sizes from font metrics + box model)
     ──Layout/──▶ LayoutResult (node/group rects, canvas w/h)
     ──Route/──▶ RoutedEdge[] (path data + geometry) + labels + sequence scenery
     ──Animate/ScheduleBuilder──▶ Schedule (absolute-time tracks)   [skipped if static]
     ──Svg/ + Animate/CssCompiler──▶ one <svg> string
```

Porting map (TS oracle → C# home). Fidelity means: same algorithm, same constants, same iteration counts, same tie-breaks.

| TS source | C# target | Notes |
|---|---|---|
| `src/model/coerce.ts` | `Model/Coerce.cs` | The whole tolerance model. Port first; everything sits on it. See §6. |
| `src/util/color.ts` (`accentToCss`, `withAlpha`) | `Model/Colors.cs` | `withAlpha(c,p)` = `color-mix(in srgb, {c} {p}%, transparent)`. `resolveColor` is NOT ported — no DOM probing exists; effects use `color-mix` output directly. |
| `src/model/defaults.ts` | `Model/Defaults.cs` | Every table verbatim: `SPACING_BY_TYPE`, `KIND_DEFAULTS`, `EDGE_KIND_DEFAULTS`, `PACKET_KIND_STYLE`, `PACKET_SHAPE_SIZE`, `DEFAULT_NARRATION {wpm:170,min:1.4,pad:0.5}`, `deriveFlow`, `topoOrder`. |
| `src/model/validate.ts`, `sequence.ts`, `state.ts`, `classes.ts` | `Model/*.cs` builders | §6. |
| `src/layout/measure.ts` + card DOM/CSS | `Text/CardSizer.cs` | Replaced, not ported — §7.3. |
| `src/layout/types.ts` | `Layout/Geometry.cs` | `Rect`, `SizeMap`, `againstFlow`. |
| `src/layout/layered.ts` | `Layout/LayeredLayout.cs` | Sugiyama-lite: DFS cycle break (3-color, explicit stack), longest-path Kahn ranking, rank compression, virtual nodes (ids ``" v{n}"``), **exactly 6 barycenter sweeps** (even top-down / odd bottom-up, original-index tie-break), explicit `order` re-sort, 6 median-coordinate iterations with two-sweep separation resolution, recursive compound driver (`GROUP_PAD {28,16,16}`, `CANVAS_PAD 16`), back-edge gutter (`LANE_RESERVE 22`, `SELF_LOOP_RESERVE 30`, label extent `len*7+8`). |
| `src/layout/sequence.ts` | `Layout/SequenceLayout.cs` | Fixed grid; all constants (`HEAD_GAP 20`, `LABEL_ROOM 40`, `SELF_H 22`, `BAND_*`, `TAIL 40`, `BAR_HALF 5`, `LEVEL_STEP 4`, `SELF_LOOP 32`, gap floor 48). **Keep `labelEst = len*6.8+40` as-is** — it feeds layout parity; do not "improve" it with real measurement in v1. `computeActivations` request/reply pairing + `activationOffset`. |
| `src/route/step-round.ts` | `Route/StepRound.cs` | Quadratic corner rounding, dedupe 0.5px, collinear ε 0.01, 2-decimal rounding. |
| `src/route/orthogonal.ts` | `Route/OrthogonalRouter.cs` | `autoSides` (rank-aware, `SAME_RANK_EPS 6`), `sidesFor` feedback diversion (`againstFlow` + `spansObstacle` → same-face U-loop), `laneDetour` (`CHANNEL_OFFSET 18`, `LANE_PAD 22`, `LANE_MARGIN 6`), self-loops (extent 30, face = right for TB/BT, bottom for LR/RL), `segHitsRect` inset 3, `sCurve` 0.4-offset cubic. |
| `src/route/svg.ts` | `Route/EdgePainter.cs` + `Route/LabelPlacer.cs` | Marker geometry verbatim (§8.5). Label placement (`chooseLabelBox`, Liang-Barsky `segGap`, crossing penalty −4, `LABEL_END_INSET 14`) ports directly, with `getBBox` replaced by `ITextMeasurer` at the edge-label role. Anchor spreading (`anchorShifts`: bucket by face, sort by far endpoint, centered comb, ≤70% face fill, 20px pitch cap, `ALIGN_EPS 4`). |
| `src/route/sequence.ts` | `Route/SequencePainter.cs` | Bands, lifeline gradients, activation gradients, message paths, `chipBehind` (with measurer instead of `getBBox`; fallback `len*6.6 × 12`). |
| `src/render/node.ts` + `src/render/group.ts` + `src/embed/styles.css` | `Svg/NodeShapes.cs`, `Svg/GroupPainter.cs`, `Svg/Stylesheet.cs` | HTML cards → SVG templates, §8. |
| `src/render/icons.ts` | `Svg/Icons.cs` | Registry verbatim (~50 keys incl. aliases); 24×24 `viewBox`, `stroke-width 1.6`, round caps/joins, `stroke="currentColor"`. Inline `<svg>` strings in `node.icon` pass through after sanitization to the SVG element subset. |
| `src/animate/timeline.ts` (+ all of `src/animate/`) | `Animate/ScheduleBuilder.cs` | Simulation, not execution — §9. |
| — (new) | `Animate/Easing.cs`, `Animate/CssCompiler.cs` | §9.3, §10. |

Cross-file constant mirrors (`LANE_RESERVE`↔`LANE_PAD`, `SELF_LOOP_RESERVE`↔`SELF_LOOP_EXTENT`, self-loop faces, sequence bar constants) live in **one** `Layout/SharedConstants.cs` so they cannot drift.

---

## 6. Model layer

Port the model to `sealed record` types mirroring `schema.ts`: `DiagramModel { Meta, Nodes, Groups, Edges, Flow, Sections }`. The prime invariant carries over verbatim from CLAUDE.md: **the model fills every default** — downstream stages never see null where the TS type is non-optional, and every color is already a CSS value (`var(--beck-primary)` or passthrough literal) via the `accentToCss` rule (falsy→fallback token var; known token→its var; else verbatim).

Details that are easy to get wrong (all verified against source):

- **Coercion tolerances** (`coerce.ts`): `optString` silently drops non-scalars; `asString` stringifies numbers/bools; `optNumber` accepts numeric strings; `optBool` accepts `"true"`/`"false"`; `triBool` returns null to let heuristics decide (sequence `activate`). `oneOf` throws with the exact message shape `` `field` must be one of: a, b, c (got "x") ``.
- **Error type**: `BeckYamlException : Exception` with optional 1-based line; message suffix `" (line N)"`.
- **Untyped documents** render as architecture with a **one-time** deprecation warning (module-level flag in TS; a static flag or `ILogger`-style callback in C#).
- **Icon fallback**: unknown icon key silently reverts to the kind default; `<svg`-prefixed strings pass through (`isKnownIcon`, `icons.ts:101`).
- **Edge id formats differ per builder** and flow steps reference them: architecture/state `"{from}->{to}#{i}"`, sequence `"msg{i}"`, class uses possibly-flipped endpoints. `inherits`/`implements` relations are **flipped to parent→child** with swapped end labels and `markerStart:'triangle'`.
- **Sequence**: message color defaults to the *worker* participant's accent (`worker = reply ? from : to`); replies and `async` are dashed with `arrow-open` markers; `curve` is always `straight`; derived flow pins packets to edge ids and gives replies `ease:'decelerate'`; derived repeat/repeatDelay = `-1`/`2.0` (architecture derived: `-1`/`1.2`; authored default: `-1`/`1.5`).
- **State**: `[*]` maps to `#start`/`#end` pseudo-nodes by position (from→start, to→end); declared states push in **first-reference order**; reserved-id collisions throw.
- **Class**: no authored flow → `flow = empty, repeat 0` **and `meta.animate = false`** (class diagrams are static unless scripted). `name` beats `title`. Default accent `primary` (state pills default `neutral`).
- **`meta.loop: false` zeroes `flow.repeat` after any flow construction** — it always wins.
- **Burst clamps**: count `max(1, min(24, round(n ?? 3)))`, stagger `max(0, n ?? 0.12)`. Narration clamps: `wpm ≥ 30`, `min ≥ 0`, `pad ≥ 0`; bare `narrate: true|false` toggles `enabled`.

YAML ingestion: parse with `YamlStream` into `YamlMappingNode`/`YamlSequenceNode`/`YamlScalarNode`; the coercion layer consumes scalars **as strings always** (this is why the tolerances line up — no schema-typed deserialization anywhere).

---

## 7. Text measurement — the novel subsystem

### 7.1 The abstraction

```csharp
public enum FontRole
{
    // family, px size, weight — derived from the utility classes in render/node.ts
    // and the CSS in embed/styles.css. rem values resolve at 16px root.
    CardTitle,        // Inter 14px / 600, line-height 1.3
    CardSubtitle,     // Inter 12px / 400, lh 1.35
    Status,           // Inter 10.4px (0.65rem) / 500, lh 1.2
    GhostLabel,       // Inter 11.52px (0.72rem) / 500
    StatusInline,     // Inter  9.92px (0.62rem) / 500
    PillTitle,        // Inter 14px / 600, lh 1.3
    PillSubtitle,     // Inter 10.88px (0.68rem) / 400, lh 1.3
    ClassStereotype,  // Inter 10.4px / 400, lh 1.3, letter-spacing 0.03em
    ClassTitle,       // Inter 14px / 600, lh 1.4
    ClassMember,      // IBM Plex Mono 11.52px / 400, lh 1.45
    EdgeLabel,        // Inter 11.2px (0.7rem) / 500
    PacketLabel,      // Inter 10.56px (0.66rem) / 600
    GroupLabel,       // Inter 11.2px (0.7rem) / 600, ls 0.04em, uppercase
    MsgText,          // IBM Plex Mono 10.88px / 500
    BandLabel,        // IBM Plex Mono 9.92px / 700, ls 0.14em, uppercase
    DiagramTitle,     // Inter 24px / 700, ls -0.02em
    DiagramSubtitle,  // Inter 14.4px (0.9rem) / 400
    Narration,        // Inter 14.72px (0.92rem) / 400, lh 1.45
}

public readonly record struct TextMetrics(double Width, double Ascent, double Descent);

public interface ITextMeasurer
{
    TextMetrics Measure(string text, FontRole role);
}
```

Letter-spacing and uppercase transforms are applied by the measurer (width += ls·(n−1); uppercase before measuring), so callers stay dumb.

### 7.2 Implementations

**`InterMetricsMeasurer` (default, zero-dependency).** An embedded resource generated offline from Inter's and IBM Plex Mono's font files: per-glyph advance widths (units/em-normalized) for the Basic Latin + Latin-1 ranges, per weight (400/500/600/700), plus kerning is *ignored* (Inter's kerning is mild; the `textLength` guard absorbs the residue), plus ascent/descent ratios. Unknown glyphs use the font's average advance. A global safety factor of **1.02** pads widths. Generating the table is a one-time dev-tool script (can use SkiaSharp at dev time; the shipped library carries only the baked table).

**`SkiaTextMeasurer` (in `Beck.Rendering.Skia`).** Constructed from a `BeckFontSpec`:

```csharp
public sealed class BeckFontSpec
{
    public required string Family { get; init; }        // emitted into --beck-font
    public string? MonoFamily { get; init; }             // emitted into --beck-font-mono
    // Font files per weight; a single variable font may back multiple weights.
    public required IReadOnlyDictionary<int, string> Files { get; init; }     // weight → path
    public IReadOnlyDictionary<int, string>? MonoFiles { get; init; }
}
```

Measurement path: load `SKTypeface` per weight, shape each run with HarfBuzzSharp (`hb_shape` over a `Font` from the typeface blob) so ligatures/kerning contribute to the advance sum, scale to the role's px size. Missing weights fall back to the nearest available with a build-time warning (matching browsers' synthetic-weight behavior closely enough).

Selection: `SvgRenderOptions.Measurer` (an `ITextMeasurer`), defaulting to `InterMetricsMeasurer.Instance`. When a `BeckFontSpec` is supplied, the renderer also rewrites the emitted `--beck-font`/`--beck-font-mono` tokens so the SVG *asks for* the measured font, and can optionally embed it (`EmbedFonts = true` → `@font-face` with base64 `data:` URI inside the SVG `<style>` — makes the artifact correct standalone at the cost of size).

### 7.3 Card sizing (`Text/CardSizer.cs`)

The browser's `getBoundingClientRect` is replaced by explicit box-model math. There is **no text wrapping and no max-width anywhere** in the card shapes (survey-verified); width is intrinsic single-line content, height is stacked line boxes. Line box height = `round(fontSize × lineHeight)`. All constants decoded from the utility classes in `render/node.ts:39-62`:

| Shape | Width | Height |
|---|---|---|
| **card** | `max(180, 16·2 + [34 + 12 if icon] + max(title, subtitle, statusChip) + node.width? override)` — border 1.5px counts per side (border-box: browser sizes include border; add 3 total) | `max(14·2 + textStackH, 14·2 + 34 if icon)` where textStackH = title lh(1.3·14) + [3 + subtitle lh(1.35·12)] + [2 + statusChip h] |
| **status chip** | `8·2 + text(Status)` | `3·2 + lh(1.2 · 10.4)` — rounded-full |
| **ghost** | `14·2 + iconRow` where iconRow = `[16 + 7 if icon] + label(GhostLabel)`; statusInline may widen | `8·2 + rowH(16 vs label lh) + [3 + statusInline lh]` |
| **pill** | `max(96, 20·2 + max(title, subtitle) )` | `10·2 + title lh(1.3·14) + [1 + subtitle lh(1.3·10.88)]` |
| **start / end** | 16 | 16 (fixed, CSS-defined pseudo-states) |
| **class** | `max(170, max(head, widest member + 14·2))` where head = `16·2 + max(stereo, title)` | head(`8·2` + stereo lh + title lh) + per non-empty section (`7·2 + n·memberLh + (n−1)·2` + 1px border) |

Rounding matches `measure.ts:14`: `Math.Round` the final w/h. **Golden-test this table against the browser** (§13, M2) — it is the highest-risk parity surface. Expect one calibration pass (e.g. sub-pixel border/line-height details) against Playwright-extracted ground truth.

### 7.4 The `textLength` guard

Every emitted `<text>`/`<tspan>` whose width participated in layout gets `textLength="{measuredWidth}"` and `lengthAdjust="spacingAndGlyphs"`. When the viewer's actual font matches the measurement, this is a no-op; when it doesn't, glyphs compress/stretch a few percent instead of escaping their card. `SvgRenderOptions.TextLengthGuard = All | FallbackOnly | Off` (default `All`; `FallbackOnly` skips it when a Skia measurer was used).

---

## 8. Static SVG output

### 8.1 Document shape

One root element (namespace prefix `b-` for the scoping class; `{h}` is the 8-char content hash):

```xml
<svg class="beck-svg b-{h}" viewBox="0 0 {W} {H}" width="{W}" height="{H}"
     style="max-width:{W}px;height:auto" font-family="var(--beck-font)"
     role="img" aria-label="{meta.title ?? 'diagram'}">
  <style>/* tokens + shape CSS + animation CSS, all selectors scoped under .b-{h} */</style>
  <defs><!-- markers, gradients, glow filters --></defs>
  <g class="beck-title-block"><!-- meta.title / meta.subtitle as centered <text> --></g>
  <g class="beck-canvas" transform="translate(0,{titleBlockH})">
    <g class="beck-groups"><!-- z1: group boxes, largest-area-first --></g>
    <g class="beck-overlay"><!-- z2: edges, markers-consumers, scenery, labels --></g>
    <g class="beck-nodes"><!-- z3: node cards --></g>
    <g class="beck-group-labels"><!-- z4 --></g>
    <g class="beck-fx"><!-- packets, trails, ripple/glow overlays --></g>
  </g>
  <g class="beck-narration"><!-- caption bar group, only when narration active --></g>
</svg>
```

- `viewBox` + `max-width` natively reproduces `fit: shrink` (scales down, never up). `fit: scroll` is documented as "wrap the SVG in `overflow-x:auto`" (Pennington integration does this; the SVG itself can't scroll).
- Title block: `meta.title` at `DiagramTitle` role, centered at `W/2`, then subtitle; heights from line boxes + the 4px/8px margins (`core.ts:104,111`). Canvas Y-offset accounts for them.
- Z-order mirrors the DOM (`groups 1 < overlay 2 < nodes 3 < group labels 4`), expressed by paint order of the `<g>` layers.

### 8.2 The `<style>` block — theming

Emit three strata, all selectors prefixed `.b-{h}`:

1. **Token definitions** — the light-theme table from `styles.css:13-50` verbatim (three-tier: `--beck-node-border: var(--color-base-200, #e2e8f0)` etc.), declared on `.b-{h}` itself. Then the nine dark overrides from `styles.css:52-63` under **two** hooks so both host-controlled and standalone rendering work:
   ```css
   [data-theme='dark'] .b-XXXX { /* nine overrides */ }
   @media (prefers-color-scheme: dark) { :root:not([data-theme='light']) .b-XXXX { /* same */ } }
   ```
   `meta.theme`/`options.Theme` of `light`/`dark` instead pins the tokens (emit only that set, no media query). `auto` emits both hooks.
2. **Shape CSS** — the SVG translation of the visual rules: `.beck-node` fill `var(--beck-node-bg)`, stroke `color-mix(in srgb, var(--beck-accent) 32%, var(--beck-node-border))` width 1.5; shadows as `filter: drop-shadow(0 1px 3px rgb(0 0 0/.05)) drop-shadow(0 4px 12px rgb(0 0 0/.06))` (dark variant swaps the shadow pair); dashed variants via `stroke-dasharray`; all the derived-color formulas (icon chip 15%, class head 10%/28%, status chip 14%, group border 45%, band/chip/message formulas from `styles.css:246-303`). Per-node accent arrives exactly as today: `style="--beck-accent: {node.accent}"` on the node's `<g>`.
3. **Animation CSS** — §10, wrapped in `@media (prefers-reduced-motion: no-preference)`.

### 8.3 Node shape templates (`Svg/NodeShapes.cs`)

Each node renders as `<g class="beck-node-wrap" data-node="{id}" transform="translate(x,y)" style="--beck-accent:{accent}">`. Text baselines are computed (`y = boxTop + ascent`), not `dominant-baseline`-dependent, except where the TS already uses `dominant-baseline:central` (edge labels, chips) — keep those as-is.

- **card**: `<rect class="beck-node" rx="14">`; icon chip `<rect rx="9">` (34×34) + nested `<svg x y width="20" height="20" viewBox="0 0 24 24">` carrying the icon body with `stroke="currentColor"` and CSS `color: var(--beck-accent)`; title/subtitle `<text>`; status chip = rounded-full `<rect>` + `<text>` (hidden when no authored status). `href` → wrap contents in `<a href>` (SVG links work).
- **ghost**: dashed stroke, no fill, no shadow; 16×16 icon (nested svg 14×14); label + optional status-inline text.
- **pill**: `<rect rx="{h/2}">`, centered title/subtitle stack.
- **start/end**: 16×16 — filled circle r8 `fill:var(--beck-text-muted)`; end = ring (r7, stroke 2) + inner dot r3.5.
- **class**: outer `<rect rx="12">` clipped compartments — head band `<rect>` (accent 10% fill) with bottom border `<line>` (28% mix), `«stereotype»` + title centered; fields/methods sections separated by `<line stroke="var(--beck-node-border)">`, members left-aligned mono at role `ClassMember` (fields muted, methods full text).
- **modifiers**: `--external` dashed, `--subtle` opacity 0.72.

### 8.4 Groups

Box: `<rect class="beck-group" rx="18" fill="none" stroke-dasharray="6 6" stroke-width="1.5" stroke="color-mix(in srgb, {group.accent} 45%, transparent)">`. Label: chip pattern — a `<rect fill="var(--beck-surface)">` sized to the measured uppercase label (+ 5.6px x-padding) behind a `<text>` at `(gr.x + 14, gr.y − 9 + …)`, fill = group accent, role `GroupLabel`. Painted in the z4 layer.

### 8.5 Edges, markers, labels, sequence scenery

Nearly verbatim ports — this subsystem is already SVG:

- Edge: `<path fill="none" stroke="{edge.color}" stroke-width="1.6" stroke-linecap="round" stroke-linejoin="round" [stroke-dasharray="7 5"]>` with `data-edge` etc.
- Markers: the four bodies from `svg.ts:31-73` with exact geometry (`arrow` polygon `0,1 10,5 0,9`; `arrow-open` polyline sw 1.8; hollow `triangle` filled `var(--beck-surface)`; `diamond`/`diamond-open`), `orient="auto-start-reverse"`, cached per (shape, color), ids `beck-arrow-{n}-{h}`.
- Edge labels: `LabelPlacer` port with measurer-fed boxes; text halo via `paint-order:stroke; stroke:var(--beck-surface); stroke-width:3px`.
- Sequence: bands (`rx 14`, dashed 6 6, 5%/30% mixes, mono uppercase chip label), lifelines (`stroke-dasharray 6 7`, sw 2, per-column fade `linearGradient` stops 0→1, 0.8→1, 1→0 opacity `userSpaceOnUse`), activation bars (`rx 5`, per-accent vertical gradient 0.95→0.35, drop-shadow glow 45% mix), message chips (`chipBehind`), message paths (sw 2, straight or self-loop `roundedPath(…, 9)`).

### 8.6 Narration bar

A group under the canvas: `<rect rx="12">` filled `color-mix(in srgb, var(--beck-primary) 6%, var(--beck-surface))`, stroke 15% mix, drop-shadow; the leading 7×7 dot circle at 0.55 opacity; width `min(736, 0.92·W)`, centered; height reserves two lines (`2.75em` at `Narration` role) so nothing jumps. Caption text is wrapped by the measurer (greedy break) into ≤2 centered lines per beat. Each beat renders its own pre-built `<g class="beck-beat" data-beat="{i}" opacity="0">`; the compiler animates them (§10 table). Per-beat `color` tints text and dot via `fill:currentColor`-style structure (`color` set on the beat group).

---

## 9. The animation compiler — pass 1: the schedule

`Animate/ScheduleBuilder.cs` **re-implements `timeline.ts` as a simulation**. It walks `model.flow.steps` with the exact semantics of `execStep` and produces:

```csharp
sealed record Schedule(
    double Duration,          // = simulated tl.duration()
    double RepeatDelay,
    int Repeat,               // -1 infinite / 0 once / N
    IReadOnlyList<Track> Tracks,
    IReadOnlyList<FxElement> Elements);  // packets, trails, ripples… to pre-create in markup

sealed record Track(string TargetId, TrackProperty Property,
    IReadOnlyList<Segment> Segments);    // absolute-time, sorted, non-overlapping
sealed record Segment(double Start, double Duration, Ease Ease, string From, string To);
```

Simulation rules that must match `timeline.ts` exactly:

- **Position model**: each step lands at `position ?? currentDuration`. Zero-duration effects (`status`, `activate`, `stream` gate, `working`, `idle`, `colorLine`) register at a time but do **not** extend duration; tweens and sub-timelines do. `parallel` gives all children the same base (recursive). `wait` adds a dummy segment extending duration.
- **Edge lookup `pathOf`**: exact edge-id match (sequence, only when chain length is 2) → forward from/to → **reversed** to/from. Reversed hops flip the packet's travel direction on the same path.
- **Knob merge `hopOptions`**: explicit knobs over `PACKET_KIND_STYLE[kind]`; `PACKET_SHAPE_SIZE` beats kind size for circle/ring; ease token map (`smooth→power2.inOut` etc. — the C# `Ease` enum holds the *analytic* function, §9.3).
- **Packet+trail unit**: duration `max(0.3, pathLength / speed)`; C# computes `pathLength` itself — closed-form for line and quadratic segments (every `step-round` path is M/L/Q; `s`-curves are a single cubic → Gauss–Legendre quadrature, 16 points, deterministic). Trail reveal and dot travel share the window and ease. Arrival returns `pos + duration`; the **arrival pulse is pinned** at that absolute time (parallel packets flash targets simultaneously). Multi-hop chains sequence hop-by-hop; label rides only the final hop; a group target pulses every member instead.
- **Burst**: waves at `base + c·stagger`, all targets of a wave simultaneous, label only on the first dot.
- **Sequence choreography** (derived flows only): dim levels `line .15 / label .35 / act .25 / band .45` as *initial* states; `onPacket` brightens row parts (0.25s at departure) and bars (`start` bar → 0.3s ending at `max(at, arrival−0.15)`; `end` bar → fade to 0.25 over 0.35s at arrival); `onPhase` lights bands in order (0.4s); `finale` fades everything back over 0.6s at `duration − 0.75` when the flow ends in `reset`.
- **Loop-clean rule**: if `repeat ≠ 0` and the last step isn't `reset`, a restore-to-initial event is appended at the end; `reset` steps themselves are restore events at their position. In the schedule, "restore" = every touched track snaps to its initial value (steps-end semantics).
- **Narration**: per beat at `pos`: previous beat opacity 1→0 over 0.12 (power1.in); this beat 0→1 over 0.3 (power2.out) starting `pos+0.12`; dwell `max(0, hold)` extends duration; `hold = step.hold ?? readingTime(text)` with `max(min, pad + words/wpm·60)`.
- `phase` steps emit nothing (labels have no CSS meaning; `onPhase` still fires for sequence bands).
- **Elements**: the builder also inventories every fx element the markup must pre-create — one `<circle class="beck-packet">` (+ optional glow filter def + optional `<text>` label) per hop occurrence, one trail `<path>` clone per hop, one stream clone per stream step, one ripple rect per pulse, one glow/ring overlay rect per highlight/working/fail target, one colored-overlay path (+ colored marker def) per `activate` (colorLine) target, one pill-state group per distinct `(text, color)` a status target shows. Elements are reused across loop iterations exactly like the JS engine reuses its compile-time elements.

### 9.3 `Animate/Easing.cs`

Analytic implementations (pure functions `double → double`) of: `none/linear`, `power1/2/3.in/out/inOut`, `expo.inOut`, `sine.inOut`, `back.out(s)` (s = 1.70158·overshoot), `elastic.out(amplitude, period)`, `bounce.out`, `steps(n)`. These are the standard Penner forms GSAP implements — port from the published formulas, verify by sampling GSAP in the browser once and comparing tables (dev-time check, not runtime).

`ToCss(ease)`:
- `linear` → `linear`; `steps(12)` → `steps(12)`.
- Everything else → `linear(v0, v1 p1%, …, vn)` sampled at **adaptive points**: start from 16 uniform samples, insert midpoints where the chord error exceeds 0.5% until ≤ 48 points. Deterministic. (Monotonic power/sine/expo eases could be `cubic-bezier`, but one mechanism keeps output uniform and diff-stable; revisit only if CSS size becomes a problem.)

---

## 10. The animation compiler — pass 2: CSS emission

### 10.1 The shared-cycle model

`T = schedule.Duration + schedule.RepeatDelay`. Every animated element gets:

```css
.b-{h} .p3 { animation: b{h}-p3 {T}s linear {iter}; }
@keyframes b{h}-p3 { 0% {...} 12.3456% {...} ... 100% {...} }
```

- `iter`: `repeat −1` → `infinite`; `repeat 0` → `1` + `animation-fill-mode: forwards` (a non-looping flow ends on the revealed frame — matches the JS engine); `repeat N` → `N+1` (GSAP repeat semantics).
- Percentages formatted with 4 decimals, invariant culture.
- Per-segment easing rides the *from*-keyframe's `animation-timing-function` (that's how CSS applies timing between keyframe pairs) — this is where the `linear(...)` strings land.
- Outside its windows, an element's keyframes **hold** its idle value; the 100% keyframe equals the 0% keyframe for looping flows (lockstep loop, no pop). The repeatDelay is dead time: last real keyframe at `(Duration/T)·100%`, held to 100%. (For finite `repeat N` this holds the end state *within* each iteration rather than only between them — a hair different from GSAP's inter-iteration hold; accepted, invisible in practice.)
- Instant events (status swaps, colorLine, restores) use paired keyframes 0.01% apart (steps-end effect).
- Elements needing two independent clocks get comma-separated animations: stream = gate (cycle-locked opacity) + `b{h}-stream-march {len/220}s linear infinite`; working = gate + `b{h}-working 1.5s ease-in-out infinite` (the existing `beck-working` ring, rebuilt stroke-based).
- **All of stratum 3 lives inside `@media (prefers-reduced-motion: no-preference)`**, including the *initial-state rules* (sequence dimming, packet hiding is attribute-level but packets are invisible-idle anyway, narration bar starts empty). Reduced-motion users therefore get the fully-revealed static frame — same as the JS engine.

### 10.2 Per-primitive compilation table

| Flow primitive | Pre-created markup | CSS animation |
|---|---|---|
| **packet hop** | `<circle class="beck-packet" r="{size}" fill="{color}" opacity="0" style="offset-path: path('{d}'); offset-rotate: 0deg">` (+ glow filter def `feGaussianBlur stdDeviation=3` merged w/ source, region −200%/500%; `ring` shape = `fill:none; stroke:{color}; stroke-width:max(2.5, r·0.28)`); label `<text class="beck-packet-label">` offset-pathed along a parallel path raised by `r+6` | window: `offset-distance` 0%→100% (reversed hop: 100%→0%) with the hop's ease; `opacity` 0→1 at window start, 1→0 at end (instant pairs). Constant radius — flow packets never ramp (`noEntry/noExit` always, per `trail.ts:118`). |
| **impact** (knob) | ring `<circle fill="none" stroke="{color}" stroke-width="2.5">` at the arrival point | at arrival: `r {size}→{size·3.4}`, `opacity .9→0`, `stroke-width 2.5→0.5` over 0.55s power2.out |
| **trail** | edge path clone: `stroke="{color}" stroke-width="2" fill="none"`, no markers, `stroke-dasharray:{len}; stroke-dashoffset:{±len}` | window: `stroke-dashoffset ±len→0` with the hop's ease; holds 0 until the flow's restore point, then snaps back to ±len |
| **stream** | clone: sw 2.5, `stroke-dasharray:5 9`, opacity 0 | gate: opacity 0→1 at step time, hold until restore; march: separate infinite `to { stroke-dashoffset:-14 }` |
| **status** | one `<g class="beck-pill" data-state="{i}">` per distinct (text,color) incl. the authored resting state; chip bg `color-mix(… {color} 14%, transparent)`, text fill {color}; ghost targets swap the status-inline text instead | opacity steps between states at their times; restore returns to state 0. (Card width stays sized to the authored status — flow pills may overhang slightly; accepted, noted.) |
| **highlight** | overlay `<rect>` matching the card (same rx) with `stroke="{color}" stroke-width="2"` + soft glow (blur filter), opacity 0; transforms animate a per-node `<g class="beck-fx-node">` wrapper *inside* the positioned wrap (`transform-box: fill-box; transform-origin: center`) | 0.21s to `scale(1.04) translateY(-2px)` w/ `back.out(2)`→`linear()`; 0.49s back to identity w/ `elastic.out(1,.4)`→`linear()`; overlay opacity up then out; border tint via overlay stroke (base card untouched — the "save/restore closure" pattern evaporates) |
| **pulse** | ripple `<rect>` (card bounds, same rx, `stroke="{color}" stroke-width="2" fill="none"`) | ripple: scale 1→1.15, opacity .6→0 over 0.48s power2.out; card fx-group: 0.18s `back.out(3)` up (scale 1.04, y −2, soft shadow overlay), 0.42s `elastic.out(1,.5)` back |
| **working / idle** | ring overlay `<rect>` (card bounds+rx, fill none, `stroke: var(--beck-working, var(--beck-accent))`) | gate opacity between working/idle/restore times + infinite breathing: `stroke-width 0→18`, stroke fading `color-mix(… 55%→0%, transparent)` over 1.5s ease-in-out (stroke-based rebuild of the box-shadow ring `0→9px`); card border tint via a border overlay during the window |
| **fail** | reuses highlight overlay machinery | border/glow flash to danger over 0.12s; shake on fx-group: `translateX` −5/+5/−3/0 at 0.06/0.08/0.07/0.07s; hold ~0.6s then restore; optional statusPill state fires at the same time |
| **activate** (colorLine) | colored clone of each edge path (stroke {color}, same width/dash) + a colored marker def; opacity 0 | opacity 0→1 instant at step time; →0 at restore. (CSS can't animate `marker-*` refs or reliably cross-fade marker fills — the overlay clone sidesteps it.) |
| **narrate** | per-beat `<g>` in the narration bar, opacity 0 | beat i−1: 1→0 over 0.12 (power1.in) at `pos`; beat i: 0→1 over 0.3 (power2.out) at `pos+0.12`; restore empties the bar |
| **sequence choreo** | dim initial states as motion-gated CSS rules (`.b-{h} .beck-msg path { opacity:.15 }` etc.) | brighten/fade windows per §9; finale fades all groups back before loop end |
| **pulse-on-arrival** | (pulse machinery) | pinned at each hop's arrival time |
| **wait / reset / phase** | — | schedule-level only |

### 10.8 Play triggering

`SvgRenderOptions.Animation`:

- **`Full` (default)** — animations run on load, looping per the flow. (The IntersectionObserver gate is the one real loss vs. JS; documented.)
- **`Static`** — skip the schedule entirely; emit the revealed static frame (also what class diagrams without authored flow produce via `meta.animate=false`, and what `animate: false` in YAML produces).
- **`Scrub`** — instead of `animation: … {T}s infinite`, emit `animation: … linear both; animation-timeline: view(block 90% 10%)` so scrolling the diagram through the viewport scrubs the entire choreography. Progressive: browsers without scroll-timelines show the static first frame. This is the "seek bar made of scroll" — a genuinely-better-than-the-API party trick, and it's ~20 lines of alternate emission.

---

## 11. Public API

```csharp
// Beck.Rendering
public static class BeckSvg
{
    public static string Render(string yaml, SvgRenderOptions? options = null);
}

public sealed class SvgRenderOptions
{
    public ITextMeasurer Measurer { get; init; } = InterMetricsMeasurer.Instance;
    public BeckFontSpec? Font { get; init; }          // rewrites --beck-font tokens; Skia users pass the same spec to the measurer
    public ThemeMode? Theme { get; init; }            // overrides meta.theme (auto|light|dark)
    public AnimationMode Animation { get; init; } = AnimationMode.Full;   // Full|Static|Scrub
    public TextLengthGuard TextLengthGuard { get; init; } = TextLengthGuard.All;
    public bool EmbedFonts { get; init; }             // @font-face data: URIs (standalone artifacts)
    public string? IdSuffix { get; init; }            // override the content hash (testing)
}

// Beck.Rendering.Skia
public sealed class SkiaTextMeasurer : ITextMeasurer
{
    public SkiaTextMeasurer(BeckFontSpec spec);
}
```

Usage, the way a smart end user throws us the bone:

```csharp
var font = new BeckFontSpec {
    Family = "Inter",
    Files = new Dictionary<int, string> {
        [400] = "fonts/Inter-Regular.otf", [500] = "fonts/Inter-Medium.otf",
        [600] = "fonts/Inter-SemiBold.otf", [700] = "fonts/Inter-Bold.otf" },
    MonoFamily = "IBM Plex Mono",
    MonoFiles = new Dictionary<int, string> { [400] = "fonts/IBMPlexMono-Regular.otf",
        [500] = "fonts/IBMPlexMono-Medium.otf", [700] = "fonts/IBMPlexMono-Bold.otf" },
};
var svg = BeckSvg.Render(yaml, new SvgRenderOptions {
    Font = font,
    Measurer = new SkiaTextMeasurer(font),
});
```

Errors: YAML/model violations throw `BeckYamlException` (same messages as the TS `BeckError`, incl. line numbers); option misuse throws `InvalidOperationException` — consistent with `Beck.Authoring`'s validate-at-emit philosophy.

---

## 12. Pennington / docs-site integration

Today the docs site ships fences as `<code class="language-beck">` and the JS engine hydrates on load (no server-side step; `docs/.../pennington.md:47`). The renderer slots in at the static-build seam:

1. **v1 (docs-site local):** a post-render pass in `Beck.Docs` — during `dotnet run -- build`, transform each `code.language-beck` block's text through `BeckSvg.Render` and replace the `<pre>` with the SVG (keep the source in a `<details>` or data attribute for the "view source" affordance). The docs site already references the Beck project; it adds a reference to `Beck.Rendering` + `Beck.Rendering.Skia` and passes the site's font spec.
2. **Later:** a proper Markdig extension / Pennington option (`RenderBeckStatically = true`) once the shape is proven, and a `ToSvg(SvgRenderOptions)` convenience on the builders (via `ToYaml()` internally) if `Beck` takes a reference on `Beck.Rendering`.

The playground and JS engine remain untouched — they're the authoring/dev loop and the parity oracle.

---

## 13. Verification plan

The repo has no automated tests; this project introduces them (`Beck.Rendering.Tests`, xunit). The TS engine is the oracle throughout — extract ground truth from the live playground via the Playwright MCP **without modifying the engine**:

- **Corpus**: every playground sample + every ` ```beck ` fence in `docs/Beck.Docs/Content` + `Beck.Sample`'s five outputs. Check the corpus YAML + extracted goldens into `Beck.Rendering.Tests/goldens/`.
- **M-level parity gates** (each milestone lands with its gate green):
  - *Model*: JSON-dump the TS `buildModel` output per corpus file (evaluate `loadDiagram` in the playground page context), compare to C# model serialization. Exact match expected (modulo key order).
  - *Measure*: extract `SizeMap` (per-node `getBoundingClientRect` w/h) with the page fonts pinned to local Inter/Plex files; compare to `CardSizer` + `SkiaTextMeasurer` at the same files. Tolerance ±1px per dimension; calibrate `CardSizer` until green.
  - *Layout*: extract `.beck-node-wrap` transforms + canvas w/h; compare rects. Tolerance ±0.5px (algorithms are deterministic; disagreement means a porting bug, not noise).
  - *Route*: extract `.beck-overlay path` `d` strings; compare token-by-token with ±0.01 numeric tolerance. Also assert the standing invariant: **no negative path coordinates** (the off-canvas regression signature called out in CLAUDE.md).
  - *Static visual*: serve the C# SVGs on a bare page defining `--color-*` vars; Playwright screenshots vs. the JS playground at the same theme/fonts; pixel-diff with a small threshold. Run light + dark.
  - *Animation*: golden-file the emitted CSS (schedule determinism makes this stable); spot-check live by screenshotting the C# SVG at t = 0 / 25 / 50 / 75% of `T` (drive via `document.getAnimations()[…].currentTime` in the test page only — test scaffolding may use JS; the artifact may not) against the JS playground paused at the same fractions for 2–3 representative flows (architecture derived, sequence derived, authored flow with effects + narration).
- **Unit level**: coercion tolerances, ease sampling vs. reference tables, path-length math vs. brute-force flattening, YamlWriter-emitted YAML round-trips through the C# parser identically to hand-written YAML.

---

## 14. Milestones

Ordered so every stage lands against its parity gate before the next begins. M0–M5 produce visible value (static SVG) even if the animation compiler slips.

- **M0 — Scaffolding.** Projects, solution wiring, options types, content-hash id scheme, golden-extraction Playwright script.
- **M1 — Model.** YamlDotNet ingestion + `Coerce` + all four builders + defaults tables. Gate: model JSON parity on the corpus.
- **M2 — Measurement.** `ITextMeasurer`, `InterMetricsMeasurer` (+ offline table generator), `SkiaTextMeasurer`, `CardSizer`. Gate: SizeMap parity ±1px. *Highest-risk milestone; budget a calibration loop.*
- **M3 — Layered layout.** `LayeredLayout` + compound groups + gutter. Gate: rect parity ±0.5px.
- **M4 — Routing.** `StepRound`, `OrthogonalRouter`, `EdgePainter`, markers, `LabelPlacer`, `anchorShifts`. Gate: path-d parity + no-negative-coordinates.
- **M5 — Static SVG, architecture.** Node templates, groups, theming `<style>`, title block. Gate: visual diff, light+dark.
- **M6 — State / class / sequence static.** Pills, pseudo-states, class cards; `SequenceLayout` + `SequencePainter` (bands/lifelines/bars/messages). Gate: visual diff across all four types.
- **M7 — Schedule + easing.** `ScheduleBuilder` simulation, `Easing` + `ToCss`, path-length math. Gate: unit tests + schedule golden files.
- **M8 — CSS compiler, core motion.** Packets (offset-path), trails, streams, pulse-on-arrival, wait/reset/loop model, reduced-motion gating. Gate: animation spot-checks on derived flows.
- **M9 — Full sexy.** highlight/pulse/working/fail/status/activate/burst overlays, narration bar + beats, sequence choreography + finale, Scrub mode. Gate: the authored-flow + narrated samples side-by-side with JS.
- **M10 — Integration + polish.** Docs-site build transform, `EmbedFonts`, README/docs page ("Static SVG rendering"), CSS-size audit, NuGet packaging.

---

## 15. Risks and open questions

| Risk | Assessment / mitigation |
|---|---|
| **Card sizing never quite matches the browser** | The whole reason M2 has its own gate. `textLength` means a miss degrades typography, not layout. If a specific CSS detail proves unreproducible, adjust *both* by pinning the detail in a golden and documenting the delta. |
| **`offset-path` on SVG children** | Supported in evergreen browsers since ~2023–24, but the least-seasoned dependency here. Fallback (flag, or automatic): emit sampled `translate()` keyframes along the path (every ~2% of length) instead — heavier CSS, identical visuals. Decide during M8. |
| **CSS bloat on busy diagrams** | Estimate: a 12-message narrated sequence ≈ 60–80 animated elements × ~1–8 keyframes ≈ 30–80 KB of CSS. Compare: today's runtime is 100+ KB engine + GSAP from CDN. Audit in M10; dedupe identical keyframe bodies (many packets share shapes). |
| **`linear()` fidelity for elastic/bounce** | Non-issue at 48 samples; verify once against GSAP visually in M7. |
| **Two engines drifting** (the strategic cost) | Accepted for the experiment. Guards: parity suite in CI, and CLAUDE.md's "update both sides when adding a YAML field" rule gains a third entry. Long-term outs: make C# the only engine, or extract a shared layout spec. Decide only if the bad idea graduates. |
| **Sanitizers stripping `<style>` inside SVG** | Out of scope for Pennington (we own the pipeline). Document for third-party consumers. |
| **Status pills wider than the measured card** | Accepted cosmetic overhang (§10.2). Alternative (size card to max pill state) trades static-frame parity — revisit if it looks bad in practice. |
| **`animation-trigger` (start-when-visible)** | Track it; when it ships cross-browser, `Full` mode can adopt it and close the IntersectionObserver gap with zero API change. |
| **Fonts without provided files** (family name only, no paths) | Skia can resolve installed system fonts by family (`SKTypeface.FromFamilyName`) — allow `BeckFontSpec` without `Files` on machines where the font is installed; warn that CI must have the font. |

### Future work (explicitly not v1)

Shared object model between `Beck.Authoring` and `Beck.Rendering` (kills the YAML round-trip and the private-fields wall); `builder.ToSvg()`; zero-JS playback toggle (checkbox hack — needs an HTML wrapper, so it's a Pennington feature, not an SVG feature); PNG rasterization via the same Skia dependency; a Pennington-native Markdig extension.

---

*Appendix: the five reference surveys behind this spec (model schema/defaults, layout+routing algorithms, render/CSS anatomy, animation vocabulary, .NET surface) were extracted from source at commit `b268d41`. Line references throughout cite that state of the tree.*
