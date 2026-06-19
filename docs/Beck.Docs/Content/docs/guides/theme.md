---
title: Match your theme and colours
description: How diagrams adopt your palette, choosing a theme mode, and overriding tokens.
order: 25
sectionLabel: How-to guides
uid: docs.guide.theme
---

This guide shows you how to make a diagram match the surrounding site — adopting your brand colour, flipping with light and dark, and overriding individual tokens when you need to.

## How adoption works

Every colour Beck paints is a `--beck-*` custom property that defaults to your site's `--color-*` palette, with a literal fallback only for when no palette is present. A diagram therefore renders in *your* brand colour with no configuration, and it flips with your theme automatically — light and dark are nothing more than `[data-theme="dark"]` redefining the surface, text, and edge tokens, while accent tokens ride the host ramps in both modes. There is no per-theme JavaScript and no hardcoded hex in the renderer, so once your page sets its palette the diagram simply belongs.

## Use accent tokens, not raw hex

To stay on-theme, give nodes and edges an **accent token** rather than a raw colour. The tokens — `primary`, `success`, `warn`, `danger`, `info`, `neutral` — resolve through `--beck-*`, so they follow your palette and adapt to dark mode. A raw colour (a hex, `rgb()`, or CSS named colour) is used verbatim and stays frozen in both themes.

```beck
meta: { direction: LR, animate: false }
nodes:
  - { id: themed, title: Themed, subtitle: accent primary, accent: primary }
  - { id: frozen, title: Frozen, subtitle: raw hex, accent: "#7c3aed" }
edges:
  - { from: themed, to: frozen }
```

The left card tracks your theme; the right one will not budge when the page switches to dark. Reach for a raw colour only when you genuinely want a fixed, off-palette swatch.

## Choose a theme mode

By default `meta.theme` is `auto`, which follows the host page. Set it to `light` or `dark` to pin a single diagram regardless of the surrounding theme.

```beck
meta: { title: Always dark, theme: dark, direction: LR, animate: false }
nodes:
  - { id: app, title: App, kind: user }
  - { id: api, title: API, kind: gateway }
edges:
  - { from: app, to: api }
```

You can also set the mode imperatively when you mount the engine yourself: `<beck-diagram mode="light|dark|auto">`, or `window.Beck.renderDiagram(host, yaml, { theme })`. A live handle exposes `setTheme(mode)` if you want to flip it after render.

## Override a token site-wide

To recolour every diagram, **define the host ramp** — this is preferred because the change adopts everywhere, in Beck and in the rest of your UI at once:

```css
:root {
  --color-primary-600: #2563eb;
}
```

If you would rather scope it to Beck, set a `--beck-*` variable on an ancestor and give it a dark variant under your dark selector:

```css
.beck-root {
  --beck-primary: #2563eb;
}
[data-theme="dark"] .beck-root {
  --beck-primary: #60a5fa;
}
```

For the full list of token names and what each one paints, see the [theme tokens in the YAML schema](/docs/reference/yaml#colours-and-theme-tokens).

## Override one node

To change just one card rather than a token, set `surface` and `textColor` on that node — both take a raw CSS colour and bypass the theme for that card alone. See [Style your nodes](/docs/guides/nodes) for the details.

```beck
meta: { direction: LR, animate: false }
nodes:
  - { id: edge, title: Edge, kind: external }
  - { id: hero, title: Hero, surface: "#0f172a", textColor: "#f8fafc" }
edges:
  - { from: edge, to: hero }
```

## Pennington and MonorailCSS

On a Pennington site, MonorailCSS's `ColorScheme` maps `primary` and `base` for you, so `--beck-primary` and the surface and text tokens come through with no extra work. The catch is the other accents: a token expects a specific ramp — `success` reads green/emerald, `info` reads sky/violet — and if your scheme does not emit that ramp the token has nothing to resolve to. Remap those tokens to a ramp you *do* emit:

```css
body .beck-root {
  --beck-success: var(--color-green-500);
  --beck-info: var(--color-sky-500);
}
```

That single rule keeps every `success`/`info` accent on-brand without touching your YAML.

---

For the complete set of theme tokens and their defaults, see the [YAML schema reference](/docs/reference/yaml#colours-and-theme-tokens).
