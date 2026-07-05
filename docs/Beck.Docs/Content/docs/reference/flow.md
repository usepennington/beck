---
title: Flow & animation
description: The flow block, every step type, and the knobs that shape a travelling packet.
order: 41
sectionLabel: Reference
uid: docs.reference.flow
---

A diagram's animation is a `flow` — an ordered list of steps the engine compiles into a timeline and
plays when the diagram scrolls into view. When no `flow` is authored, Beck derives one from the
edges. For a task-oriented walkthrough, see [Animate the flow](/docs/guides/flow).

Animation is skipped entirely — and the motion runtime never loads — when `meta.animate` is `false`
or the reader prefers reduced motion. See [reduced motion](#reduced-motion).

## The flow block

| key | type | default | description |
|---|---|---|---|
| `repeat` | number | `-1` | `-1` loops forever; `0` plays once. `meta.loop: false` forces `0`. |
| `repeatDelay` | number (s) | `1.5` | Pause between repeats. |
| `steps` | list | — | Ordered steps; each is a single-key mapping (see below). |

```yaml
flow:
  repeat: -1
  repeatDelay: 1.5
  steps:
    - packet: { from: client, to: api, label: GET /item }
    - working: { node: db }
    - packet: { from: api, to: db, color: info }
    - idle: { node: db }
    - packet: { from: db, to: api, color: success }
    - wait: 1
```

### Derived flow

With no `flow` block, Beck derives one — a bare document already animates. What it derives depends
on the diagram type:

- **architecture** and **state** — a topological walk from roots to leaves: a `phase` label the
  first time each node (or state) sends, a `packet` along every edge (or transition) in order, then
  a wait and a `reset`, looping forever.
- **sequence** — the authored message order *is* the story: one `packet` per message, in order,
  with each `- section:` band emitted as a seekable `phase`. Packets ride their message's colour
  (replies with a decelerating ease), and the scenery starts dimmed — each row, bar, and band
  lights up as the story reaches it, then everything fades back down before the loop.
- **class** — a quiet structural cascade rather than a packet story: each inheritance level lights
  up in turn, top to bottom, with the relations into that level recolouring as it does.

## Steps

Each step is a mapping with a single key naming its type. `from`/`to`/`node` values must reference a
declared node id (or, for edge endpoints, a group id).

| step | fields | effect |
|---|---|---|
| `packet` | `from`, `to`, `via?`, `color?`, `label?`, + [knobs](#packet-knobs) | One dot travels the edge (or multi-hop chain). |
| `burst` | `from`, `to` (id or list), `via?`, `count`, `stagger`, `color?`, `label?`, + [knobs](#packet-knobs) | `count` waves; each broadcasts a dot to every target. |
| `status` | `node`, `text`, `color?` | Set the node's status pill (persists until changed or reset). |
| `highlight` | `node`, `color?` | A brief scale-and-glow flourish. |
| `pulse` | `node`, `color?` | A scale bump with an expanding ripple ring. |
| `activate` | `from`, `to`, `color?` | Persistently recolour an edge and its arrowhead until `reset`. |
| `stream` | `from`, `to`, `color?` | Continuous flowing dashes along an edge until `reset`. |
| `working` | `node`, `color?` | Leave a node visibly busy (breathing glow) until `idle` or `reset`. |
| `idle` | `node` | Clear a node's `working` state. |
| `fail` | `node`, `text?`, `color?` | A red shake and flash, with optional status text. |
| `phase` | `<label>` (string) | A named label the handle's `seek(label)` can jump to. |
| `wait` | `<seconds>` (number) | Pause. Default `0.5`. |
| `reset` | — | Restore the initial state (clears trails, streams, working, recolours, pills). |
| `parallel` | `[ <steps> ]` | Run the listed steps simultaneously. |

`burst` clamps `count` to 1–24 (default `3`) and defaults `stagger` to `0.12` seconds. `status`,
`working`, `stream`, and `activate` persist until a later step or `reset` clears them; everything
else is a one-shot beat.

```yaml
- status: { node: scan, text: FAILED, color: danger }
- fail: { node: scan, text: vulnerable }
- parallel:
    - burst: { from: alert, to: [oncall, slack, email], count: 4 }
    - pulse: { node: alert }
- phase: notified
- wait: 1
- reset:
```

## Packet knobs

`packet` and `burst` share these. Each unset knob falls back to the traversed edge kind's default
(see [per-edge-kind motion](#per-edge-kind-motion)), then to an engine constant.

| knob | type | default | description |
|---|---|---|---|
| `shape` | `dot` `circle` `ring` | `dot` | Packet form. See [shapes](#packet-shapes). |
| `size` | number (px) | edge kind | Dot radius. |
| `speed` | number (px/s) | edge kind | Travel speed. |
| `glow` | bool | edge kind | Soft glow around the dot. |
| `impact` | bool | `false` | Emit an expanding ring at the destination on arrival. |
| `ease` | enum | edge kind | Easing of the travel. See [eases](#eases). |
| `via` | list of ids | — | Waypoints — the packet chains through each in turn. A group waypoint pulses its members on arrival. |
| `color` | token or colour | `--beck-packet` | Dot and trail colour. |
| `label` | string | — | Text that rides above the dot (only the first dot of a `burst` is labelled). |

A travelling packet draws a colour trail in lockstep with its easing and pulses the target node on
arrival.

### Packet shapes

Each shape travels below so you can read it in motion. An explicit `size` always overrides a shape's
baseline radius.

<BeckGallery Of="packet-shapes" />

### Eases

An ease only reveals itself in motion, so each one runs live — same edge, same speed, different
curve.

<BeckGallery Of="eases" />

## Per-edge-kind motion

When a packet's knobs are unset, it inherits the motion of the edge kind it travels — so `data`,
`control`, `async`, and `dependency` packets read differently with zero authoring. Explicit knobs
always win. Each card below sends a default packet along an edge of that kind, so the difference in
size, speed, glow, and easing is something you watch rather than read off a table.

<BeckGallery Of="edge-kinds" />

## Reduced motion

If `meta.animate` is `false`, a `<beck-diagram>` carries `animate="false"`, or the reader's system
requests reduced motion, Beck renders the static frame and never loads its animation runtime (GSAP,
fetched from a CDN at runtime). The persistent CSS-driven effects (`working`, `stream`, status and
icon tints) consume theme variables directly, so they survive a theme change and keep running even
while the timeline is paused.

To drive playback yourself — play, pause, seek to a `phase` label — render with
`window.Beck.renderDiagram` and use the returned handle; see the [API reference](/api).
