---
title: Layout & direction
description: Direction, spacing, and the auto-layout engine.
order: 4
sectionLabel: Guide
uid: docs.layout
---

Beck lays diagrams out automatically with a Sugiyama-style layered engine: it ranks the nodes
along the flow, orders them to minimise crossings, assigns coordinates, then routes the edges
as orthogonal step-round paths with obstacle avoidance. You never place a box by hand.

## Direction

`meta.direction` sets the primary axis: `TB` (top-to-bottom, the default), `BT`, `LR`, or `RL`.

```beck
meta: { title: Left to right, direction: LR }
nodes:
  - { id: a, title: Ingest }
  - { id: b, title: Transform }
  - { id: c, title: Load, kind: db }
edges:
  - { from: a, to: b }
  - { from: b, to: c }
```

## Spacing

Tune the gaps with `meta.spacing`: `rank` (gap along the flow), `node` (gap across), and
`cornerRadius` (px on edge corners).

```yaml
meta:
  direction: TB
  spacing: { rank: 120, node: 48, cornerRadius: 4 }
```

## Pinning when you need to

The auto-layout is usually what you want, but a node can take a `rank` (force it onto a layer)
and an `order` (tie-break within a rank), and an edge can pin its `fromSide` / `toSide`
(`top`/`bottom`/`left`/`right`) when you want to nudge routing.
