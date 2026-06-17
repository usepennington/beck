---
title: Styling & themes
description: Accents, variants, icons, and how diagrams adopt your palette.
order: 3
sectionLabel: Guide
uid: docs.styling
---

Beck is designed to look right with zero styling — but a few fields let you direct attention.

## Variants

`variant` sets a node's visual weight: `solid` (default), `subtle`, or `ghost`.

```beck
meta: { direction: LR, animate: false }
nodes:
  - { id: a, title: Solid, variant: solid }
  - { id: b, title: Subtle, variant: subtle }
  - { id: c, title: Ghost, variant: ghost }
edges:
  - { from: a, to: b }
  - { from: b, to: c }
```

## Icons

Set `icon` to a named key (e.g. `cdn`, `bucket`, `container`, `vector`, `model`, `redis`) or
paste raw inline `<svg>`. An unknown name falls back to the node kind's default icon.

```beck
meta: { direction: LR, animate: false }
nodes:
  - { id: cdn, title: CDN, icon: cdn }
  - { id: box, title: Bucket, icon: bucket }
  - { id: pod, title: Pod, icon: container }
  - { id: vec, title: Vectors, icon: vector }
edges:
  - { from: cdn, to: box }
  - { from: box, to: pod }
  - { from: pod, to: vec }
```

## Theming

Every themeable value is a `--beck-*` CSS custom property that **defaults to your site's
palette** (`--color-primary-600`, `--color-base-*`, …). That's why every diagram on this site
is violet and flips with the theme toggle — the page sets those variables once, light and dark,
and the diagrams inherit them. There's no per-theme JavaScript and no hardcoded hex in the
renderer.

You can still override a single node with `surface` and `textColor` (raw CSS colours) when you
need to.
