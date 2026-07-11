---
title: Style system
description: The BeckStyle record and its sub-records, the built-in registry, style resolution and precedence, the metrics-font and artwork vocabularies, and the custom-style sanitizer.
order: 46
sectionLabel: Reference
uid: docs.reference.styles
---

A **style** is a complete visual identity for a diagram — shapes, strokes, typography, colour bias,
and motion character — selected by a single token. Every built-in is an instance of the public
`BeckStyle` record (namespace `Beck`); custom styles are derived from one with a `with` expression.
A style never emits resolved colour into shape CSS: it redefines the *defaults* of the `--beck-*`
tokens, so a themed diagram still adopts the host palette and still flips with light and dark. For a
visual tour see [Pick a built-in style](/docs/guides/styles); to build your own, [Author a custom
style](/docs/guides/custom-styles).

## Setting a style

| where | how | scope |
|---|---|---|
| YAML | `meta: { style: <name> }` | one document |
| C# | `new SvgRenderOptions { Style = <BeckStyle> }` | site-wide default |
| authoring builder | `.Style("<name>")` or `.Style(BeckStyle)` | emits `meta.style` |

## Precedence

```
resolved style = meta.style (YAML)  ??  SvgRenderOptions.Style (C# default)  ??  BeckStyle.Classic
```

The C# option is a site-wide default that an individual document's `meta.style` opts back out of.
This is **the opposite** of `SvgRenderOptions.Theme`, where the option overrides `meta.theme`. Light
and dark are an orthogonal axis: every style defines both token sets and [`Theme`](/docs/guides/theme)
works unchanged on top.

## Resolving a name

`meta.style` (and a custom style's `BeckStyle.Name`) is a string token that must match `[a-z0-9-]+`
— non-empty, lowercase ASCII letters, digits, and hyphens (`BeckStyles.IsValidName`). A malformed
token is rejected at validation with a warning and the resolved default is used instead.

A well-formed token is looked up in this order:

1. `BeckStyles.ByName` — the built-in registry (case-sensitive). Built-in names always win.
2. `SvgRenderOptions.Styles` — the per-render custom registry.
3. On no match: a validation warning, then fall back to `SvgRenderOptions.Style` (or `Classic`).

Because built-ins are consulted first, a custom style cannot shadow a built-in name.

## Built-in styles

`BeckStyles.All` lists all nine in declaration order; `BeckStyles.ByName` keys them by
`BeckStyle.Name`. `classic` is the default and is byte-identical when no style is set.

| name | artwork | identity |
|---|---|---|
| `classic` | `Plain` | The default card look — soft shadows, rounded corners, Inter + IBM Plex Mono. |
| `minimal` | `Plain` | Flat: no shadows, hairline borders, a single travelling dot; glow and rings off. |
| `terminal` | `Plain` | Monospace everything, `[bracketed]` titles, square packets, green-ramp accent, hard-step trails. |
| `blueprint` | `Blueprint` | Drafting surface — faint grid, dashed edges, dimension ticks on groups, mono uppercase labels. |
| `glow` | `Plain` | Gradient edges, soft packet bloom, breathing pulse on active nodes. |
| `brutalist` | `Brutalist` | Thick strokes, a solid blur-free offset shadow, uppercase Archivo, `steps()` flow motion. |
| `sketch` | `Sketch` | Hand-drawn — Shantell Sans, deterministically wobbled outlines. |
| `extrude` | `Extruded` | 2.5D slabs with static depth faces; highlight presses the node down toward its face. |
| `circuit` | `Circuit` | Chip nodes with pin stubs and a via dot at every route bend. |

## The `BeckStyle` record

`sealed record BeckStyle` (namespace `Beck`). The public surface is data only. Every member is
`required` except `Artwork`.

| member | type | description |
|---|---|---|
| `Name` | `string` | The YAML token (`[a-z0-9-]+`). |
| `LightTokens` | `StyleTokens` | The light `--beck-*` table. |
| `DarkTokens` | `StyleTokens` | The dark overrides (a partial set layered over the light block). |
| `Geometry` | `StyleGeometry` | Corner radii, stroke widths, shadows, artwork offsets, and the card box-model. |
| `Typography` | `StyleTypography` | Family stacks, the per-role table, metrics-font key, and title decoration. |
| `Mix` | `StyleMix` | The fixed `color-mix` tint ratios. |
| `Strokes` | `StyleStrokes` | Dash patterns by role, plus the dashed/gradient-edge toggles. |
| `Motion` | `StyleMotion` | Effect durations, sequence-dim ratios, packet glyph, and effect gates. |
| `Artwork` | `StyleArtwork` | Node-chrome shape family. Defaults to `Plain`. |
| `Classic` | `static BeckStyle` | The reference style, built from the engine's historical literals. |

### StyleTokens

`sealed record StyleTokens(IReadOnlyList<(string Name, string Value)> Entries)`. An ordered
`--beck-*` table emitted verbatim into the SVG `<style>` block. Values are three-tier
`var(--color-…, literal)` chains — a `--beck-*` role reading a host `--color-*` ramp with a literal
fallback. See [colours and theme tokens](/docs/reference/yaml#colours-and-theme-tokens) for the
token list. The dark block need only contain the entries that differ from the light block, which is
always emitted first.

### StyleTypography

| member | type | default | description |
|---|---|---|---|
| `SansFamily` | `string` | — | Default sans family stack (used when no font override is supplied). |
| `MonoFamily` | `string` | — | Default mono family stack. |
| `Roles` | `FontRoleTable` | — | Per-role weight/size/letter-spacing/case table; feeds both measurement and rendering. |
| `MetricsFont` | `MetricsFont` | `Inter` | Embedded metrics table the fallback measurer sizes against. |
| `PacketLabel` | `FontRoleSpec` | — | The `.beck-packet-label` CSS typography (separate from the measurement role of the same name). |
| `NarrationFigureCaption` | `bool` | `false` | When `true`, the narration bar renders as a numbered `Fig. N —` serif-italic caption. |
| `TitlePrefix` | `string` | `""` | Prepended to every primary node title (terminal's `[`). |
| `TitleSuffix` | `string` | `""` | Appended to every primary node title (terminal's `]`). |

`DecorateTitle(string)` applies the prefix/suffix at both the measurement and render boundary, so
the decoration widens the card without desyncing the `textLength` guard.

### StyleMix

Fixed `color-mix(in srgb, …)` percentages, all whole numbers. Members: `GroupBorder`, `NodeStroke`,
`IconChip`, `StatusPill`, `ClassHead`, `ClassHeadBorder`, `ActivationGlow`, `ChipStroke`, `MsgText`,
`BandFill`, `BandStroke`, `BandLabel`, `NarrationFill`, `NarrationBorder` — each `required int`.

### StyleStrokes

| member | type | default | description |
|---|---|---|---|
| `NodeDash` | `string` | — | External + ghost node outline dash. |
| `GroupDash` | `string` | — | Group box + band box dash. |
| `LifelineDash` | `string` | — | Sequence lifeline dash. |
| `EdgeDash` | `string` | — | Author-dashed edge dash. |
| `StreamDash` | `string` | — | Animated stream-overlay marching dash. |
| `DashedEdges` | `bool` | `false` | When `true`, every edge carries `EdgeDash` by default (blueprint). |
| `GradientEdges` | `bool` | `false` | When `true`, default-coloured edges use a token-built `<linearGradient>` (glow). |

### StyleMotion

| member | type | default | description |
|---|---|---|---|
| `PulseDur` / `HighlightDur` / `FailDur` | `double` | — | Effect sub-timeline lengths (seconds). |
| `DimLine` / `DimLabel` / `DimAct` / `DimBand` | `double` | — | Dimmed opacities during sequence storytelling. |
| `OverlayStroke` | `double` | — | Stroke width of pulse/highlight/fail/activate/trail overlays. |
| `RingStroke` | `double` | — | Stroke width of impact + stream rings. |
| `PacketRingMin` / `PacketRingFactor` | `double` | — | Ring-packet minimum stroke and size fraction. |
| `GlowEnabled` | `bool` | — | Whether a packet's `glow: true` applies the bloom filter. |
| `PacketGlowBlur` | `double` | `3.0` | `stdDeviation` of the packet-bloom blur. |
| `RingsEnabled` | `bool` | `true` | Hard gate on the impact/working ring overlays (minimal sets `false`). |
| `EffectAmplitude` | `double` | — | Uniform multiplier on effect peak opacity/stroke. `1.0` = classic peaks. |
| `PacketGlyph` | `PacketGlyph?` | `null` | Style default packet glyph, under the author's explicit `packet.shape`. |
| `TrailSteps` | `int?` | `null` | `steps(n)` timing on the trail reveal (terminal's hard-step trails). |
| `PacketSteps` | `int?` | `null` | `steps(n)` timing on the packet's own advance (brutalist's stepped flow). |
| `SequenceRevealScale` | `double` | `1.0` | Multiplier on the sequence-reveal ramp windows (a slow, soft scenery draw-on). |
| `PressDown` | `bool` | `false` | Active-effect transform presses down toward the depth faces (extrude). |
| `Pulse` | `PulseEffect` | `Ripple` | The node-pulse arrival character — see [PulseEffect](#pulseeffect). |
| `PulseColor` | `string?` | `null` | Colour override for the pulse overlay, as a `var(--beck-*)` token (circuit's `--beck-gold` LED). `null` keeps the arriving packet's colour. |
| `LiftEnabled` | `bool` | `true` | Whether pulse/highlight run the card transform (classic's lift / extrude's press). `false` (minimal, terminal, glow, brutalist, circuit) pins the card — the overlay cue alone carries the arrival, no zoom. The fail shake is unaffected. |

### StyleGeometry

`sealed record StyleGeometry`. Groups:

- **Corner radii** — `CardRadius`, `ClassRadius`, `GhostRadius`, `GroupRadius`, `IconChipRadius`,
  `GroupLabelBgRadius`, `NarrationRadius`, `BandRadius`.
- **Stroke widths** — `NodeStroke`, `EdgeStroke`, `GroupStroke`, `BandBoxStroke`, `LifelineStroke`,
  `EndNodeStroke`, `HairlineStroke`, `MessageStroke`, `EdgeLabelHalo` (a CSS length string).
- **Shadows** — `NodeShadow`, `NodeShadowDark`, `NarrationShadow` (CSS `filter` values; use `none`
  to turn one off).
- **Insets** (derived, read-only) — `NodeBorderInset` = `NodeStroke / 2`; `MeasureBorder` =
  `2·round(NodeStroke / 2)`. A style that thickens the stroke moves both with it.
- **Card box-model** — the `CardSizer` constants both the sizer and the renderer consume: `CardPadX`,
  `CardPadY`, `CardMinW`, `CardMaxW`, `IconW`, `IconGap`, line-height and gap fields for card/pill/
  ghost/class nodes, and `StartEndSize`. Change these only alongside the card-sizing tests. See the
  source for the full field list.
- **Artwork offsets** (all default `0`/unset; consumed only under the matching `Artwork`):

  | member | default | artwork | meaning |
  |---|---|---|---|
  | `NarrationBulletGap` | `9.6` | any | Gap between the caption bullet and its text. |
  | `SurfaceBackground` | `""` | any | Extra root-`<svg>` CSS (blueprint's grid `background-image`). |
  | `ShadowOffset` | `0` | `Brutalist` | Down-right offset of the hard shadow rect. |
  | `DepthOffset` | `0` | `Extruded` | Down-right offset of the two depth faces. |
  | `PinLength` / `PinThickness` / `PinPitch` / `ViaRadius` | `0` / `2.4` / `24` / `2.6` | `Circuit` | Chip-pin geometry and via-dot radius. |
  | `StationRadius` / `StationRing` | `0` / `2` | `Metro` | Station-dot radius and ring stroke. |
  | `DimensionTick` | `0` | `Blueprint` | Group-box dimension-line offset and witness-tick length. |

### StyleArtwork

`enum StyleArtwork` — the node-chrome shape family a style draws. A closed vocabulary, not an
extension interface: a style selects one value and the shape emitters branch on it, so a custom
style composes existing artwork rather than injecting markup. Every non-`Plain` value is gated by a
`StyleGeometry` offset (a `0` offset emits nothing — byte-identical to `Plain`).

| value | draws |
|---|---|
| `Plain` | Straight rounded rects and true circles (classic and every CSS/token-only style). |
| `Brutalist` | A solid, blur-free, token-coloured offset shadow rect behind each card. |
| `Sketch` | Node/group/pseudo-state outlines as deterministically wobbled `<path>`s (edges stay exact). |
| `Extruded` | Two solid depth faces (right + bottom) behind each card. |
| `Circuit` | Chip pin stubs on cards + a via dot at each edge-route bend. |
| `Metro` | A white-fill/coloured-ring station dot at each edge's two anchor endpoints. |
| `Blueprint` | A dimension line with witness ticks along each group box's top edge. |

### PacketGlyph

`enum PacketGlyph` — the style-default packet glyph (`StyleMotion.PacketGlyph`), distinct from the
per-packet author-facing `PacketShape`: `Dot`, `Ring`, `Square`, `Train`.

### PulseEffect

`enum PulseEffect` — the visual character of the node *pulse*, the arrival cue every packet hop
fires on its destination card (and the flow `pulse` step). Like `StyleArtwork` it is a closed
vocabulary: each member maps to one compiled overlay/keyframe recipe, so every effect stays
deterministic, shared-cycle, and inside the reduced-motion guard.

| value | style | arrival reads as |
|---|---|---|
| `Ripple` | classic | A soft border ripple expands off the card and fades. |
| `SurveyRing` | blueprint | An offset rectangular ring scales linearly outward — a surveyor's ping. |
| `MarkerPop` | sketch | No overlay: the card itself jolts a beat larger (`scale(1.08)`). |
| `Flash` | minimal, extrude | The card face tints in the pulse colour and eases away. |
| `Slam` | brutalist | The outline snaps thick for two frames and snaps back — hard cuts. |
| `Flicker` | terminal | The face invert-flickers twice, CRT-style. |
| `GlowRing` | glow | The expanding ring, carried on a drop-shadow bloom halo. |
| `Led` | circuit | A small dot in the card's top-right corner blinks once. |

## MetricsFont

`enum MetricsFont` (namespace `Beck.Rendering.Text`) selects the embedded metrics table the built-in
fallback measurer sizes against — pick the one matching `StyleTypography.SansFamily` so layout stays
correct with no font dependency. An explicit `SvgRenderOptions.Measurer` (e.g. Skia) overrides it.
Mono roles always resolve against the shared IBM Plex Mono coverage.

| value | sans slot |
|---|---|
| `Inter` | Inter (classic default). |
| `SourceSerif` | Source Serif 4 (for serif custom styles). |
| `Archivo` | Archivo, covers weight 800 (brutalist). |
| `ShantellSans` | Shantell Sans (sketch). |

## SvgRenderOptions

Two members carry styles:

| member | type | description |
|---|---|---|
| `Style` | `BeckStyle?` | Site-wide default. Lowest precedence — a document's `meta.style` overrides it. Null keeps `Classic`. |
| `Styles` | `IReadOnlyDictionary<string, BeckStyle>?` | Custom registry a `meta.style` token can name, consulted after `BeckStyles.ByName`. |

## Custom-style sanitization

Every string value on a **custom** (non-built-in) style is scrubbed before it reaches the `<style>`
block, because custom styles may be fed from less-trusted config. Built-in styles (matched by
reference against `BeckStyles.All`) are trusted and bypass the scan entirely — `Classic` pays no cost
and its output is untouched.

- **Scrubbed strings** — every `StyleTokens` name/value, `SansFamily`/`MonoFamily`, all five dash
  patterns, `EdgeLabelHalo`, `NodeShadow`/`NodeShadowDark`, and `SurfaceBackground`.
- **Forbidden substrings** (stripped, case-insensitive, to a fixed point) — `</`, `<!`, `@import`,
  `url(`, `{`, `}`.
- **Never scrubbed** — numeric geometry/mix fields, and `Name` (validated `[a-z0-9-]+` at the seams
  and never emitted into CSS).

A custom token value must therefore not rely on any forbidden character. There is no seam for
injecting raw markup or arbitrary CSS rules — this is what keeps output deterministic and safe.
