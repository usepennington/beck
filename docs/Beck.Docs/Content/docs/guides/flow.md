---
title: Animate the flow
description: Script packets, bursts, status pills, and effects to tell a story with motion.
order: 30
sectionLabel: How-to guides
uid: docs.guide.flow
---

This guide shows you how to choreograph a diagram's animation — packets, bursts, status pills, and persistent effects — so the motion tells a story rather than just decorating the page.

You already know how to declare nodes and edges. Here you learn to drive what moves along them and when. Each recipe below is a small live diagram; for the exhaustive list of steps and knobs, see the [flow & animation reference](/docs/reference/flow).

## Let Beck derive a flow

If you write no `flow` block at all, Beck animates anyway: a packet traverses each edge in topological order — roots to leaves — then waits, resets, and loops. Nodes and edges alone are enough to get motion.

```yaml:symbol
wwwroot/examples/guides/flow-01.beck.yaml
```

<beck-diagram src="/examples/guides/flow-01.beck.yaml" mode="auto"></beck-diagram>

Reach for an explicit `flow` when topological order does not match the story you want to tell.

## Add a flow block

A `flow` block has `steps` — an ordered list where each step is a single-key map — plus `repeat` and `repeatDelay`. Set `repeat: -1` to loop forever (the default), or `repeat: 0` to play once. `repeatDelay` is the pause in seconds before the loop restarts.

The workhorse step is `packet`, which sends one dot along an edge. Give it a `from`, a `to`, and optionally a `label` and `color`.

```yaml:symbol
wwwroot/examples/guides/flow-02.beck.yaml
```

<beck-diagram src="/examples/guides/flow-02.beck.yaml" mode="auto"></beck-diagram>

> [!NOTE]
> `meta.loop: false` forces `flow.repeat: 0`, so the sequence plays once and stops. `meta.animate: false` skips the runtime entirely and renders a static frame.

## Shape a packet

Every packet inherits motion from its edge's `kind` — size, speed, glow, and ease — but you can override any of it. Set `shape` to `dot` (the default, keeping the edge-kind size), `circle`, or `ring`; tune `size`, `speed`, `glow`, `ease`, and `impact` (an expanding ring on arrival). The example below sends a slow, decelerating `ring` that lands with impact.

```yaml:symbol
wwwroot/examples/guides/flow-03.beck.yaml
```

<beck-diagram src="/examples/guides/flow-03.beck.yaml" mode="auto"></beck-diagram>

See the [flow reference](/docs/reference/flow) for the full list of knobs and ease names.

## Send a burst

A `burst` fires several packets in waves. Set `count` for the number of waves and `stagger` for the gap between them. The clever part: `to` can be a **list**, so one source fans out to many targets at once.

```yaml:symbol
wwwroot/examples/guides/flow-04.beck.yaml
```

<beck-diagram src="/examples/guides/flow-04.beck.yaml" mode="auto"></beck-diagram>

Each wave broadcasts a dot to every target in the list, so `count: 4` over three workers sends twelve dots in total.

## Mark nodes

State changes carry as much story as movement. Several steps annotate a node directly:

- `status` sets a persisting pill on the node.
- `working` leaves a node breathing (busy) until you clear it with `idle` or `reset`.
- `highlight` and `pulse` draw a brief eye to a node.
- `fail` shakes the node red and flashes, with optional status `text`.

Here is a tiny build-then-fail story: the build node goes to work, the tests run, and the deploy fails.

```yaml:symbol
wwwroot/examples/guides/flow-05.beck.yaml
```

<beck-diagram src="/examples/guides/flow-05.beck.yaml" mode="auto"></beck-diagram>

## Persistent edge effects

Two steps change an edge until you `reset`, which is ideal for showing an established connection rather than a one-off message:

- `activate` recolours a path and keeps it lit.
- `stream` runs continuous flowing dashes along a path for ongoing traffic.

```yaml:symbol
wwwroot/examples/guides/flow-06.beck.yaml
```

<beck-diagram src="/examples/guides/flow-06.beck.yaml" mode="auto"></beck-diagram>

## Control the sequence

A handful of steps shape timing rather than visuals:

- `parallel` runs a list of steps at the same instant.
- `wait` pauses for a number of seconds (default `0.5`).
- `phase` drops a named `seek()` label you can jump to from script.
- `reset` restores the diagram to its initial state.

```yaml:symbol
wwwroot/examples/guides/flow-07.beck.yaml
```

<beck-diagram src="/examples/guides/flow-07.beck.yaml" mode="auto"></beck-diagram>

## Respect reduced motion

When a visitor has `prefers-reduced-motion` set, or you author `meta.animate: false` (or `<beck-diagram animate="false">`), Beck renders the static frame and never loads the motion runtime. Write your flow for the animated case — the static fallback is automatic, so you never script two versions.

## Next steps

- The [flow & animation reference](/docs/reference/flow) is the complete vocabulary: every step type, every packet knob, and the per-edge-kind motion defaults.
- To script a flow from C# instead of YAML, see [generate diagrams from your code](/docs/guides/generate) and the [Beck.Authoring API](/api).
- Try a sequence live in the [playground](/playground).

