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

Animation is skipped entirely — Beck emits the static frame — when `meta.animate` is `false`
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

With no `flow` block, Beck derives one for the three types that have a sequence of events to play — a
bare document already animates. What it derives depends on the diagram type:

- **architecture** and **state** — a topological walk from roots to leaves: a `phase` label the
  first time each node (or state) sends, a `packet` along every edge (or transition) in order, then
  a wait and a `reset`, looping forever.
- **sequence** — the authored message order *is* the story: one `packet` per message, in order,
  with each `- section:` band emitted as a seekable `phase`. Packets ride their message's colour
  (replies with a decelerating ease), and the scenery starts dimmed — each row, bar, and band
  lights up as the story reaches it, then everything fades back down before the loop.
- **class** — *nothing.* A class diagram is structural reference material, not a narrative, so it
  has no derived flow: with no `flow` block it renders a still frame and never loads the animation
  runtime. Script a `flow:` yourself if you want a guided tour.

A `note:` on a connector (an architecture edge, a sequence message, or a state transition) is
folded into the derived flow as a [`narrate`](#narration) caption, emitted just before that hop's
packet. Notes are ignored once you author an explicit `flow:` — narrate the story with `narrate`
steps there instead.

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
| `narrate` | `<text>` (string), or `text`, `hold?`, `color?` | Set the caption bar under the diagram and hold long enough to read it. See [narration](#narration). |
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

## Narration

A `narrate` step (or a connector `note:` folded into a [derived flow](#derived-flow)) writes a line
to a caption bar rendered under the diagram body and holds it long enough to read. Captions
cross-fade as the flow advances and clear on each loop. The bar only appears when the flow actually
carries caption text and narration is enabled; because it is part of the animation, a diagram
rendered as a static frame (see [reduced motion](#reduced-motion)) shows no caption bar.

### The `narrate` step

| field | type | default | description |
|---|---|---|---|
| `text` | string | — | The caption line. The shorthand `narrate: <text>` sets just this. |
| `hold` | number (s) | auto | Seconds to hold the caption, overriding the length-derived time below. |
| `color` | token or CSS colour | theme text | Tints the caption line (and its leading dot). |

With no `hold`, a caption's on-screen time is derived from its length so a longer line lingers
longer: `max(min, pad + (words / wpm) × 60)` seconds, using the `meta.narrate` knobs.

### `meta.narrate`

Tunes narration for the whole diagram. A bare boolean toggles the caption bar; a mapping tunes the
reading-time pace. Absent means enabled with the defaults below (captions still only appear once a
`narrate` step or a connector `note:` supplies text).

| key | type | default | description |
|---|---|---|---|
| `enabled` | bool | `true` | `false` (or `narrate: false`) suppresses the caption bar entirely. |
| `wpm` | number | `170` | Reading pace, words per minute — drives each caption's auto hold. |
| `min` | number (s) | `1.4` | Floor on a caption's on-screen time. |
| `pad` | number (s) | `0.5` | Extra seconds added on top of the reading time (lead-in/out). |

```yaml
meta:
  narrate: { wpm: 200, min: 2 }   # or `narrate: false` to turn captions off
flow:
  steps:
    - narrate: The request enters the gateway.
    - packet: { from: client, to: gw }
    - narrate: { text: Auth rejected the token., hold: 3, color: danger }
```

## Reduced motion

If `meta.animate` is `false`, or the reader's system requests reduced motion, Beck emits the static
frame instead. The persistent CSS-driven effects (`working`, `stream`, status and icon tints) consume
theme variables directly, so they survive a theme change and keep running even while the timeline is
paused.
