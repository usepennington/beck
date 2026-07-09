---
title: Author a custom style
description: Derive a BeckStyle from a built-in with a with-expression, register it, and drive it from meta.style — retinting tokens, choosing a metrics font, and composing an artwork.
order: 33
sectionLabel: Cross-cutting
uid: docs.guide.custom-styles
---

This guide shows you how to build your own style when none of the [eleven
built-ins](/docs/guides/styles) is quite right. A style is the public `BeckStyle` record, so you
compose one by deriving from the closest built-in with a `with` expression, then registering it so a
document's `meta.style` can name it. You do not build a `BeckStyle` from scratch — every field is
`required`, and a built-in already fills them all.

## Derive from the closest built-in

Start from the built-in that is nearest to what you want and change only the fields that differ.
Reach into `BeckStyles.ByName` for the base, and give your style a name that matches `[a-z0-9-]+`
(non-empty, lowercase letters, digits, hyphens):

```csharp
using Beck;

BeckStyle ocean = BeckStyles.ByName["minimal"] with
{
    Name = "ocean",
    Mix = BeckStyles.ByName["minimal"].Mix with { NodeStroke = 48 },
};
```

Sub-records are records too, so nest `with` expressions to change one field without restating the
rest. Pick the base by what you are keeping: `minimal` for a flat token-only look, `glow` or
`editorial` for their motion character, or an artwork style (`sketch`, `metro`, …) when you want its
chrome. `BeckStyle.Classic` is the safe baseline if you want to change one thing about the default.

## Register it and render

A style reaches a diagram one of two ways. To make it the **site-wide default**, pass it as
`SvgRenderOptions.Style` — every document then renders in it unless its own `meta.style` says
otherwise:

```csharp
using Beck.Rendering;

string svg = BeckSvg.Render(yaml, new SvgRenderOptions { Style = ocean });
```

To let **individual documents opt in by name**, put it in the `Styles` registry and select it from
YAML with `meta.style`:

```csharp
var options = new SvgRenderOptions
{
    Styles = new Dictionary<string, BeckStyle> { ["ocean"] = ocean },
};
```

```yaml
meta: { style: ocean }
```

The registry key and the token in `meta.style` must match. Built-in names are resolved first, so a
custom style cannot reuse a built-in name — if you name yours `metro`, the built-in wins and yours is
never reached. An unknown token warns and falls back to `Style` (or `classic`). For the exact
resolution order and precedence, see the [style-system reference](/docs/reference/styles#resolving-a-name).

## Retint the colours

Colours live in `LightTokens` and `DarkTokens` as an ordered `--beck-*` table. Keep every value a
three-tier `var(--color-…, literal)` chain: point the role at a host `--color-*` ramp first, with a
literal only as the last-resort fallback. This is what lets your style still adopt the host palette
and flip with light and dark — never write a resolved hex into a token.

```csharp
LightTokens = new StyleTokens(new (string, string)[]
{
    // …carry the other entries from the base style…
    ("--beck-accent", "var(--color-sky-500, #0ea5c4)"),
    ("--beck-packet", "var(--beck-accent)"),
}),
```

Change only the entries you mean to; the easiest path is to copy the base style's array and edit the
handful of roles you care about. The dark table need only hold the entries that differ. For the
token list and what each paints, see the [theme tokens](/docs/reference/yaml#colours-and-theme-tokens).

## Choose a metrics font

If you change `Typography.SansFamily`, set `Typography.MetricsFont` to the embedded table that
matches it, so the built-in measurer sizes cards correctly with no font dependency. The choices are
`Inter`, `SourceSerif`, `Archivo`, and `ShantellSans`; mono roles always use IBM Plex Mono. If you
run an exact `SvgRenderOptions.Measurer` (Skia over your real font files), it overrides this key and
the metrics table is irrelevant.

```csharp
Typography = BeckStyles.ByName["editorial"].Typography with
{
    SansFamily = "'Source Serif 4', Georgia, serif",
    MetricsFont = MetricsFont.SourceSerif,
},
```

Either way the SVG only names fonts through `--beck-font`/`--beck-font-mono`; the host page loads
the actual webfonts, and a missing font degrades to a `textLength` squeeze rather than a broken
layout.

## Compose an artwork

Chrome that cannot be expressed in CSS — the brutalist shadow, sketch wobble, extrude faces, circuit
pins, metro stations, blueprint dimension lines — comes from the `Artwork` field plus its
`StyleGeometry` offset. You **compose** an existing one; you cannot supply new geometry. Set both the
enum value and the offset that gates it (a `0` offset draws nothing):

```csharp
BeckStyle flatSlab = BeckStyles.ByName["minimal"] with
{
    Name = "flat-slab",
    Artwork = StyleArtwork.Extruded,
    Geometry = BeckStyles.ByName["minimal"].Geometry with { DepthOffset = 7 },
};
```

Each artwork reads its own offset — `Brutalist`/`ShadowOffset`, `Extruded`/`DepthOffset`,
`Circuit`/`PinLength`, `Metro`/`StationRadius`, `Blueprint`/`DimensionTick`. `Sketch` needs only the
enum. The [reference](/docs/reference/styles#styleartwork) lists every pairing.

## What you cannot do

There is no seam for injecting raw markup or arbitrary CSS. A style is data: token strings, numbers,
dash patterns, and a choice from a fixed artwork vocabulary. Because a custom style may come from
less-trusted config, every CSS-bound string on it is scrubbed of `</`, `<!`, `@import`, `url(`, `{`,
and `}` before it reaches the `<style>` block, so a token value must not rely on those characters.
This is deliberate — it is what keeps output deterministic and free of injection. If you need chrome
the vocabulary does not cover, the answer is a new built-in in the engine, not a token hack. For the
full sanitizer rules see the [reference](/docs/reference/styles#custom-style-sanitization).

---

For the complete field list of `BeckStyle` and its sub-records, the built-in registry, and the
resolution rules, see the [style-system reference](/docs/reference/styles). For a visual tour of the
built-ins, see [Pick a built-in style](/docs/guides/styles).
