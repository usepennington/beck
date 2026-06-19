---
title: Connect and route edges
description: Labels, semantic kinds, line style, curves, arrowheads, colour, and pinned sides.
order: 22
sectionLabel: How-to guides
uid: docs.guide.edges
---

This guide shows you how to connect nodes and control exactly how the lines between them look and route — their meaning, style, curve, arrowheads, colour, and where they attach.

## Connect two nodes

To draw a line, add an edge with a `from` and a `to`. Both must reference a declared node `id`. Add an optional `label` to say what travels along it.

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

## Give an edge meaning

Set `kind` to convey what the connection *is*. Each kind carries a default line style and packet motion, so a diagram reads consistently without you styling every edge by hand.

- `data` (default) — a solid request/response line.
- `control` — a solid command/orchestration line.
- `async` — rendered **dashed**, for fire-and-forget messaging.
- `dependency` — rendered **dashed** and **neutral-coloured**, for "relies on" links that aren't live traffic.

```beck
meta: { direction: LR, animate: false }
nodes:
  - { id: api, title: API, kind: gateway }
  - { id: queue, title: Events, kind: queue }
  - { id: db, title: Postgres, kind: db }
edges:
  - { from: api, to: queue, kind: async, label: publish }
  - { from: api, to: db, kind: dependency }
```

## Force a line style

If you want a solid or dashed line regardless of kind, set `style` directly. This overrides the kind's default — useful when you want a `data` edge dashed, or an `async` edge solid.

```beck
meta: { direction: LR, animate: false }
nodes:
  - { id: a, title: Service A }
  - { id: b, title: Service B }
edges:
  - { from: a, to: b, style: dashed, label: optional }
```

## Choose a curve

The `curve` controls the routing shape:

- `step-round` (default) — orthogonal segments with rounded corners; the best default for layered diagrams.
- `straight` — a direct line; reads well for short, adjacent hops.
- `s` — a smooth S-bend; good for a single edge that crosses ranks and would otherwise look stiff.

```beck
meta: { direction: LR, animate: false }
nodes:
  - { id: lb, title: Load Balancer, icon: lb }
  - { id: a, title: Node A }
  - { id: b, title: Node B }
edges:
  - { from: lb, to: a, curve: straight }
  - { from: lb, to: b, curve: s }
```

## Arrowheads

Set `arrow` to control which ends are tipped: `end` (default), `start`, `both`, or `none`. The boolean shorthand `true` means `end` and `false` means `none` — handy when authoring from code.

```beck
meta: { direction: LR, animate: false }
nodes:
  - { id: a, title: Primary, kind: db }
  - { id: b, title: Replica, kind: db }
  - { id: c, title: Peer }
edges:
  - { from: a, to: b, label: replicate }
  - { from: a, to: c, arrow: both }
```

## Colour an edge

Use `color` to override the edge's tint. Pass an accent token (`primary`, `success`, `warn`, `danger`, `info`, `neutral`) to follow the theme in light and dark, or a raw CSS colour to fix it regardless of theme.

```beck
meta: { direction: LR, animate: false }
nodes:
  - { id: api, title: API, kind: gateway }
  - { id: ok, title: Success }
  - { id: err, title: Error }
edges:
  - { from: api, to: ok, color: success }
  - { from: api, to: err, color: danger }
```

> [!TIP]
> Prefer tokens over raw colours so your edges adapt when the diagram switches theme. See [Match your theme and colours](/docs/guides/theme).

## Nudge the routing

Beck auto-routes edges and the default is usually right, so reach for this only when a line leaves or enters somewhere awkward. Pin the anchor with `fromSide` and `toSide`, each one of `top`, `bottom`, `left`, or `right`.

```beck
meta: { direction: LR, animate: false }
nodes:
  - { id: api, title: API, kind: gateway }
  - { id: cache, title: Redis, kind: cache }
  - { id: db, title: Postgres, kind: db }
edges:
  - { from: api, to: cache, toSide: top }
  - { from: api, to: db, toSide: bottom }
```

## Draw to a whole group

An edge's `from` or `to` may be a **group id** as well as a node id. Beck routes the line to the group's box, which is the clean way to show "this talks to everything in here" without a line per member.

```beck
meta: { direction: TB, animate: false }
nodes:
  - { id: gw, title: API Gateway, kind: gateway }
  - { id: auth, title: Auth }
  - { id: orders, title: Orders }
groups:
  - { id: services, label: Services, members: [auth, orders], accent: primary }
edges:
  - { from: gw, to: services, label: routes }
```

See [Group related nodes](/docs/guides/groups) for how membership and nesting work.

---

For the complete list of edge fields, defaults, and accepted values, see the [YAML schema reference](/docs/reference/yaml). To try variations live, open the [playground](/playground).
