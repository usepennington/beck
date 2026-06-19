---
title: Control the layout
description: Direction, spacing, and pinning a node's rank or order when you need to.
order: 23
sectionLabel: How-to guides
uid: docs.guide.layout
---

This guide shows you how to steer Beck's automatic layout when the default arrangement isn't quite what you want.

Beck lays diagrams out for you, Sugiyama-style: it ranks nodes along the flow, orders them within each rank to minimise edge crossings, then routes edges orthogonally around the cards. You rarely place anything by hand. The recipes below adjust that machinery — set the axis, loosen or tighten the spacing, and, when the auto-layout still puts a node in the wrong place, pin it.

## Set the direction

To change the primary axis, set `meta.direction`. The default is `TB` (top-to-bottom); the others are `BT`, `LR` (left-to-right), and `RL`. The direction decides which way ranks progress — everything else (ordering, routing) follows from it.

```beck
meta: { direction: LR, animate: false }
nodes:
  - { id: web, title: Web App, kind: user }
  - { id: api, title: API, kind: gateway }
  - { id: db, title: Postgres, kind: db }
edges:
  - { from: web, to: api }
  - { from: api, to: db, label: queries }
```

## Tune the spacing

To loosen or tighten the diagram, set `meta.spacing`. It takes three keys: `rank` (the gap along the flow, default `96`), `node` (the gap across a rank, default `32`), and `cornerRadius` (edge and card corner radius in px, default `16`).

Give a busy diagram more room to breathe:

```beck
meta: { direction: LR, spacing: { rank: 140, node: 56, cornerRadius: 24 }, animate: false }
nodes:
  - { id: gw, title: Gateway, kind: gateway }
  - { id: svc, title: Service }
  - { id: db, title: Postgres, kind: db }
edges:
  - { from: gw, to: svc }
  - { from: svc, to: db }
```

Or pack a compact one tighter:

```beck
meta: { direction: LR, spacing: { rank: 64, node: 20, cornerRadius: 6 }, animate: false }
nodes:
  - { id: gw, title: Gateway, kind: gateway }
  - { id: svc, title: Service }
  - { id: db, title: Postgres, kind: db }
edges:
  - { from: gw, to: svc }
  - { from: svc, to: db }
```

## Pin a node to a rank

When the auto-layout isn't what you want, override it per node. `node.rank` forces a node onto a specific layer; `node.order` tie-breaks left-to-right (or top-to-bottom) within a rank.

Here a metrics sink has no outgoing edge, so Beck ranks it next to the service that feeds it. Pinning `metrics` to rank `2` pushes it out to its own layer alongside the database, where it reads as a downstream consumer rather than a sibling of the service:

```beck
meta: { direction: LR, animate: false }
nodes:
  - { id: gw, title: Gateway, kind: gateway }
  - { id: svc, title: Service }
  - { id: db, title: Postgres, kind: db, rank: 2 }
  - { id: metrics, title: Metrics, icon: metrics, rank: 2, order: 1 }
edges:
  - { from: gw, to: svc }
  - { from: svc, to: db }
  - { from: svc, to: metrics }
```

> [!TIP]
> Reach for `rank`/`order` only when the automatic placement is wrong — a handful of pins go a long way, and over-pinning fights the crossing-reduction pass that makes diagrams readable.

## Nudge edge routing

If an edge leaves or arrives on the wrong face of a card, pin its anchors with `fromSide`/`toSide` (`top` `bottom` `left` `right`) rather than moving the nodes. See [Connect and route edges](/docs/guides/edges) for the full routing recipes.

---

For the complete `meta` and `node` field lists, see the [YAML schema reference](/docs/reference/yaml). To draw a boundary around related nodes — which also influences how they rank together — see [Group related nodes](/docs/guides/groups).
