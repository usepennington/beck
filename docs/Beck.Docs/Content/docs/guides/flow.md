---
title: Animate the flow
description: Script packets, bursts, status pills, and effects to tell a story with motion.
order: 33
sectionLabel: Cross-cutting
uid: docs.guide.flow
---

This guide shows you how to choreograph a diagram's animation — packets, bursts, status pills, and persistent effects — so the motion tells a story rather than just decorating the page.

Flow works the same across every diagram type. A `packet` rides a connector; a step like `status` or `fail` marks a box. Wherever a step below takes a `node`, that means any box — an architecture node, a state, a sequence participant, or a class card — and wherever a step takes a connector's `from`/`to`, that connector may be an edge, a message, a transition, or a relation. Only ids matter, so a state id or participant id works anywhere a node id does. The examples here happen to be architecture diagrams; the vocabulary carries over unchanged.

Each recipe below is a small live diagram; for the exhaustive list of steps and knobs, see the [flow & animation reference](/docs/reference/flow).

## Let Beck derive a flow

If you write no `flow` block at all, Beck animates anyway — and what it derives fits the diagram type: architecture and state diagrams get a topological packet-walk from roots to leaves; a sequence plays its messages in authored order. Class diagrams are the exception: they're structural reference material with no sequence of events to play, so they render a still frame unless you script a `flow:` yourself. Declaring the boxes and connectors is enough to get motion — see [derived flow](/docs/reference/flow#derived-flow) for the exact behaviour per type.

```yaml:symbol
wwwroot/examples/guides/flow-01.beck.yaml
```

```beck:symbol
wwwroot/examples/guides/flow-01.beck.yaml
```

Reach for an explicit `flow` when topological order does not match the story you want to tell.

## Add a flow block

A `flow` block has `steps` — an ordered list where each step is a single-key map — plus `repeat` and `repeatDelay`. Set `repeat: -1` to loop forever (the default), or `repeat: 0` to play once. `repeatDelay` is the pause in seconds before the loop restarts.

The workhorse step is `packet`, which sends one dot along an edge. Give it a `from`, a `to`, and optionally a `label` and `color`.

```yaml:symbol
wwwroot/examples/guides/flow-02.beck.yaml
```

```beck:symbol
wwwroot/examples/guides/flow-02.beck.yaml
```

> [!NOTE]
> `meta.loop: false` forces `flow.repeat: 0`, so the sequence plays once and stops. `meta.animate: false` skips the runtime entirely and renders a static frame.

## Shape a packet

Every packet inherits motion from its edge's `kind` — size, speed, glow, and ease — but you can override any of it. Set `shape` to `dot` (the default, keeping the edge-kind size), `circle`, or `ring`; tune `size`, `speed`, `glow`, `ease`, and `impact` (an expanding ring on arrival). The example below sends a slow, decelerating `ring` that lands with impact.

```yaml:symbol
wwwroot/examples/guides/flow-03.beck.yaml
```

```beck:symbol
wwwroot/examples/guides/flow-03.beck.yaml
```

See the [flow reference](/docs/reference/flow) for the full list of knobs and ease names.

## Send a burst

A `burst` fires several packets in waves. Set `count` for the number of waves and `stagger` for the gap between them. The clever part: `to` can be a **list**, so one source fans out to many targets at once.

```yaml:symbol
wwwroot/examples/guides/flow-04.beck.yaml
```

```beck:symbol
wwwroot/examples/guides/flow-04.beck.yaml
```

Each wave broadcasts a dot to every target in the list, so `count: 4` over three workers sends twelve dots in total.

## Mark nodes

State changes carry as much story as movement. Several steps annotate a box directly — a node, state, participant, or class card:

- `status` sets a persisting pill on the node.
- `working` leaves a node breathing (busy) until you clear it with `idle` or `reset`.
- `highlight` and `pulse` draw a brief eye to a node.
- `fail` shakes the node red and flashes, with optional status `text`.

Here is a tiny build-then-fail story: the build node goes to work, the tests run, and the deploy fails.

```yaml:symbol
wwwroot/examples/guides/flow-05.beck.yaml
```

```beck:symbol
wwwroot/examples/guides/flow-05.beck.yaml
```

## Persistent edge effects

Two steps change a connector — an edge, message, transition, or relation — until you `reset`, which is ideal for showing an established connection rather than a one-off message:

- `activate` recolours a path and keeps it lit.
- `stream` runs continuous flowing dashes along a path for ongoing traffic.

```yaml:symbol
wwwroot/examples/guides/flow-06.beck.yaml
```

```beck:symbol
wwwroot/examples/guides/flow-06.beck.yaml
```

## Narrate the story

Motion shows *what* moves; a caption says *why*. A `narrate` step writes a line to a caption bar
under the diagram and holds it long enough to read before the flow moves on — a teleprompter that
walks the viewer through the animation in words. Drop a `narrate` between the steps it explains:

```yaml:symbol
wwwroot/examples/guides/flow-08.beck.yaml
```

```beck:symbol
wwwroot/examples/guides/flow-08.beck.yaml
```

The shorthand `narrate: <text>` is all you usually need — the caption's hold time is computed from
its length, so a longer line lingers longer. When you want more control, use the full form and set
`hold` (seconds, overriding the auto pace) or `color` (an accent token or CSS colour that tints the
line):

```yaml
- narrate: The worker pulls last night's numbers from the warehouse.
- narrate: { text: The rollout failed — reverting., hold: 3, color: danger }
```

> [!NOTE]
> Captions are prose, so they often contain commas. The bare `narrate: <text>` shorthand handles
> them fine, but inside the braced `{ }` form — or any inline `note:` — you must quote text with a
> comma (`text: "Approved, and live."`), or YAML reads the comma as the end of the value.

You don't have to write a `flow` at all to get narration. When Beck [derives a
flow](/docs/reference/flow#derived-flow), a `note:` on the connector becomes the caption for that
hop — a `note` on an architecture edge, a [sequence message](/docs/guides/sequence#narrate-the-exchange),
or a state transition. It's the quickest way to caption a diagram that already animates itself:

```yaml
edges:
  - { from: worker, to: db, note: The worker pulls last night's numbers. }
```

Tune the reading pace once, for the whole diagram, with `meta.narrate` — a mapping of `wpm`
(reading speed), `min` (a floor on each caption's time), and `pad` (extra lead-in/out). Set
`meta.narrate: false` to drop the caption bar entirely. See the [narration
reference](/docs/reference/flow#narration) for the exact knobs and defaults.

## Control the sequence

A handful of steps shape timing rather than visuals:

- `parallel` runs a list of steps at the same instant.
- `wait` pauses for a number of seconds (default `0.5`).
- `phase` drops a named `seek()` label you can jump to from script.
- `reset` restores the diagram to its initial state.

```yaml:symbol
wwwroot/examples/guides/flow-07.beck.yaml
```

```beck:symbol
wwwroot/examples/guides/flow-07.beck.yaml
```

## Respect reduced motion

When a visitor has `prefers-reduced-motion` set, or you author `meta.animate: false`, Beck renders the static frame instead. Write your flow for the animated case — the static fallback is automatic, so you never script two versions.

## Next steps

- The [flow & animation reference](/docs/reference/flow) is the complete vocabulary: every step type, every packet knob, and the per-edge-kind motion defaults.
- Each diagram type's guide has a short note on flow for that type — [sequence](/docs/guides/sequence#scripting-the-animation), [state](/docs/guides/state#scripting-the-animation), and [class](/docs/guides/class#animation).
- To script a flow from C# instead of YAML, see [generate diagrams from your code](/docs/guides/generate) and the [Beck.Authoring API](/api).
- Try a sequence live in the [playground](/playground).

