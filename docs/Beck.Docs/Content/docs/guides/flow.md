---
title: Animate the flow
description: Script packets, bursts, status pills, and effects to tell a story with motion.
order: 26
sectionLabel: How-to guides
uid: docs.guide.flow
---

This guide shows you how to choreograph a diagram's animation — packets, bursts, status pills, and persistent effects — so the motion tells a story rather than just decorating the page.

You already know how to declare nodes and edges. Here you learn to drive what moves along them and when. Each recipe below is a small live diagram; for the exhaustive list of steps and knobs, see the [flow & animation reference](/docs/reference/flow).

## Let Beck derive a flow

If you write no `flow` block at all, Beck animates anyway: a packet traverses each edge in topological order — roots to leaves — then waits, resets, and loops. Nodes and edges alone are enough to get motion.

```beck
meta: { title: Auto flow, direction: LR }
nodes:
  - { id: client, title: Client, kind: user }
  - { id: api, title: API, kind: gateway }
  - { id: db, title: Postgres, kind: db }
edges:
  - { from: client, to: api }
  - { from: api, to: db, label: queries }
```

Reach for an explicit `flow` when topological order does not match the story you want to tell.

## Add a flow block

A `flow` block has `steps` — an ordered list where each step is a single-key map — plus `repeat` and `repeatDelay`. Set `repeat: -1` to loop forever (the default), or `repeat: 0` to play once. `repeatDelay` is the pause in seconds before the loop restarts.

The workhorse step is `packet`, which sends one dot along an edge. Give it a `from`, a `to`, and optionally a `label` and `color`.

```beck
meta: { title: Request, direction: LR }
nodes:
  - { id: client, title: Client, kind: user }
  - { id: api, title: API, kind: gateway }
  - { id: db, title: Postgres, kind: db }
edges:
  - { from: client, to: api }
  - { from: api, to: db }
flow:
  repeat: -1
  repeatDelay: 1.5
  steps:
    - packet: { from: client, to: api, label: GET /item }
    - packet: { from: api, to: db, label: SELECT, color: info }
    - packet: { from: db, to: api, color: success }
```

> [!NOTE]
> `meta.loop: false` forces `flow.repeat: 0`, so the sequence plays once and stops. `meta.animate: false` skips the runtime entirely and renders a static frame.

## Shape a packet

Every packet inherits motion from its edge's `kind` — size, speed, glow, and ease — but you can override any of it. Set `shape` to `dot` (the default, keeping the edge-kind size), `circle`, or `ring`; tune `size`, `speed`, `glow`, `ease`, and `impact` (an expanding ring on arrival). The example below sends a slow, decelerating `ring` that lands with impact.

```beck
meta: { title: Shaped packet, direction: LR }
nodes:
  - { id: ingest, title: Ingest, kind: gateway }
  - { id: store, title: Warehouse, icon: warehouse, kind: db }
edges:
  - { from: ingest, to: store }
flow:
  steps:
    - packet: { from: ingest, to: store, shape: ring, ease: decelerate, size: 14, speed: 220, glow: true, impact: true, color: info }
```

See the [flow reference](/docs/reference/flow) for the full list of knobs and ease names.

## Send a burst

A `burst` fires several packets in waves. Set `count` for the number of waves and `stagger` for the gap between them. The clever part: `to` can be a **list**, so one source fans out to many targets at once.

```beck
meta: { title: Fan-out, direction: TB }
nodes:
  - { id: broker, title: Broker, kind: queue }
  - { id: a, title: Worker A }
  - { id: b, title: Worker B }
  - { id: c, title: Worker C }
edges:
  - { from: broker, to: a }
  - { from: broker, to: b }
  - { from: broker, to: c }
flow:
  steps:
    - burst: { from: broker, to: [a, b, c], count: 4, stagger: 0.1, color: warn }
```

Each wave broadcasts a dot to every target in the list, so `count: 4` over three workers sends twelve dots in total.

## Mark nodes

State changes carry as much story as movement. Several steps annotate a node directly:

- `status` sets a persisting pill on the node.
- `working` leaves a node breathing (busy) until you clear it with `idle` or `reset`.
- `highlight` and `pulse` draw a brief eye to a node.
- `fail` shakes the node red and flashes, with optional status `text`.

Here is a tiny build-then-fail story: the build node goes to work, the tests run, and the deploy fails.

```beck
meta: { title: CI run, direction: LR }
nodes:
  - { id: build, title: Build, icon: code }
  - { id: test, title: Tests, kind: service }
  - { id: deploy, title: Deploy, kind: gateway }
edges:
  - { from: build, to: test }
  - { from: test, to: deploy }
flow:
  steps:
    - working: { node: build }
    - packet: { from: build, to: test, label: artifact }
    - idle: { node: build }
    - status: { node: build, text: built, color: success }
    - pulse: { node: test }
    - packet: { from: test, to: deploy }
    - fail: { node: deploy, text: rollout failed }
```

## Persistent edge effects

Two steps change an edge until you `reset`, which is ideal for showing an established connection rather than a one-off message:

- `activate` recolours a path and keeps it lit.
- `stream` runs continuous flowing dashes along a path for ongoing traffic.

```beck
meta: { title: Live link, direction: LR }
nodes:
  - { id: producer, title: Producer, kind: service }
  - { id: topic, title: Topic, icon: kafka, kind: queue }
  - { id: consumer, title: Consumer, kind: service }
edges:
  - { from: producer, to: topic }
  - { from: topic, to: consumer }
flow:
  steps:
    - activate: { from: producer, to: topic, color: success }
    - stream: { from: topic, to: consumer, color: info }
    - wait: 2
    - reset:
```

## Control the sequence

A handful of steps shape timing rather than visuals:

- `parallel` runs a list of steps at the same instant.
- `wait` pauses for a number of seconds (default `0.5`).
- `phase` drops a named `seek()` label you can jump to from script.
- `reset` restores the diagram to its initial state.

```beck
meta: { title: Read path, direction: LR }
nodes:
  - { id: client, title: Client, kind: user }
  - { id: api, title: API, kind: gateway }
  - { id: cache, title: Redis, kind: cache }
  - { id: db, title: Postgres, kind: db }
edges:
  - { from: client, to: api }
  - { from: api, to: cache }
  - { from: api, to: db }
flow:
  steps:
    - phase: lookup
    - packet: { from: client, to: api, label: GET /item }
    - parallel: [ { packet: { from: api, to: cache, color: warn } }, { working: { node: db } } ]
    - status: { node: cache, text: miss, color: warn }
    - packet: { from: api, to: db, label: SELECT }
    - idle: { node: db }
    - packet: { from: db, to: api, color: success }
    - wait: 1
    - reset:
```

## Respect reduced motion

When a visitor has `prefers-reduced-motion` set, or you author `meta.animate: false` (or `<beck-diagram animate="false">`), Beck renders the static frame and never loads the motion runtime. Write your flow for the animated case — the static fallback is automatic, so you never script two versions.

## Next steps

- The [flow & animation reference](/docs/reference/flow) is the complete vocabulary: every step type, every packet knob, and the per-edge-kind motion defaults.
- To script a flow from C# instead of YAML, see [generate diagrams from your code](/docs/guides/generate) and the [Beck.Authoring API](/api).
- Try a sequence live in the [playground](/playground).
