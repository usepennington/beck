---
title: Rendering output
description: How Beck renders — live in the browser, themed by your site.
order: 4
sectionLabel: Get started
uid: start.rendering
---

Beck renders **in the browser**. When the engine script loads it scans the page for
` ```beck ` blocks (and `<beck-diagram>` elements), parses the YAML, lays the graph out, routes
the edges as orthogonal step-round paths, and mounts the result as styled DOM with an SVG edge
overlay.

## It adopts your theme

Every colour is a CSS custom property that falls back to your site's palette, so a diagram
matches the surrounding page — including light and dark. Toggle the theme with the ☾/☀ button
in the header and watch every diagram on this page follow along. There's no per-theme
JavaScript and no hardcoded colours.

```beck
meta:
  title: Themed automatically
  direction: LR
nodes:
  - { id: a, title: Service, accent: primary }
  - { id: b, title: Cache, kind: cache, accent: info }
  - { id: c, title: DB, kind: db, accent: success }
edges:
  - { from: a, to: b }
  - { from: a, to: c }
```

## Motion is optional

Diagrams animate by default — packets trace the edges. Readers with
`prefers-reduced-motion` (or a diagram with `meta.animate: false`) get a crisp static frame
instead, and the animation runtime is never even loaded.

## Imperative rendering

Need a diagram outside Markdown? The engine exposes a small global API:

```js
const handle = window.Beck.renderDiagram(hostEl, yamlString, { theme: 'auto' });
// handle: play(), pause(), reset(), seek(label), setTheme(mode), relayout(), destroy()
```

That's exactly what powers the [Playground](/playground).
