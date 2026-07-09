---
title: Pick a built-in style
description: The eleven built-in styles — classic, minimal, terminal, blueprint, glow, editorial, brutalist, sketch, extrude, circuit, and metro — each shown across an architecture, a sequence, and a class diagram.
order: 32
sectionLabel: Cross-cutting
uid: docs.guide.styles
---

A **style** is a complete visual identity for a diagram — its shapes, strokes, typography, colour bias, and motion character — chosen with a single token. Beck ships eleven: `classic` (the default, unchanged when you set nothing) plus ten designed looks. Set one per diagram with `meta.style`, or site-wide from C# with `SvgRenderOptions.Style` (a diagram's own `meta.style` opts back out). A style only ever redefines the *defaults* of the `--beck-*` tokens, so a themed diagram still adopts your host palette and still flips with light and dark — the [theme guide](/docs/guides/theme) covers that axis, which every style honours. To build your own by deriving from a built-in with a `with` expression, see [Author a custom style](/docs/guides/custom-styles).

## The three diagrams

Every style below renders the same three sources — an architecture graph with a flow, a sequence, and a class diagram — so you can read one look against another. Only the style changes between sections; the YAML is identical. The **Read path** architecture diagram plays its flow live, so you can watch each style's arrival pulse — blueprint's surveyor ring, terminal's CRT flicker, circuit's LED blink — land on the receiving card; the sequence and class renders stay static.

```yaml:symbol
wwwroot/examples/styles/architecture.beck.yaml
```

```yaml:symbol
wwwroot/examples/styles/sequence.beck.yaml
```

```yaml:symbol
wwwroot/examples/styles/class.beck.yaml
```

Each gallery is rendered at build time into its own fragment page (`fragments/styles/<name>.html`) by the same C# engine that renders `` ```beck `` fences, and fetched in as you scroll near its section — so this page stays light while every diagram is still a finished, self-animating SVG with no client rendering.

## classic

The default. A balanced, modern card look — soft shadows, rounded corners, Inter and IBM Plex Mono. Every other style is a deliberate departure from this baseline, and any diagram that sets no style renders exactly here, byte-for-byte.

<div class="beck-lazy"><a href="/fragments/styles/classic.html">View the classic gallery</a></div>

## minimal

Sober and flat: no shadows, hairline borders, and a single travelling dot for the packet. Glow and rings are off; an arriving dot tints the receiving card for a beat instead of ringing it. The closest thing to a "no design" look — reach for it when the diagram should recede and the surrounding prose should lead.

<div class="beck-lazy"><a href="/fragments/styles/minimal.html">View the minimal gallery</a></div>

## terminal

Monospace everything, `[bracketed]` label affordances, square block packets, and a default accent biased toward the green success ramp. The identity is carried by type and hard-stepped motion — a card receiving a packet invert-flickers twice, CRT-style — no scanlines, no blinking cursor. Suits CLI, DevOps, and infra diagrams.

<div class="beck-lazy"><a href="/fragments/styles/terminal.html">View the terminal gallery</a></div>

## blueprint

A drafting surface: a faint grid behind the diagram, dashed flowing edges, dimension ticks on group boxes, and mono uppercase labels. An arrival pings a rectangular surveyor's ring off the receiving card. Reads like an engineering plan — good for architecture and infrastructure where the "schematic" framing fits the content.

<div class="beck-lazy"><a href="/fragments/styles/blueprint.html">View the blueprint gallery</a></div>

## glow

The "designed by Claude" option: gradient strokes, a soft bloom around nodes, and a breathing pulse on active ones — arrivals ripple a glowing halo ring off the card. Gradients are defined once and reference tokens through `color-mix`, so the bloom still tracks your palette. Opt into it for a hero diagram; it is deliberately not the default.

<div class="beck-lazy"><a href="/fragments/styles/glow.html">View the glow gallery</a></div>

## editorial

A serif textbook figure: hairline rules, no fills, `Fig. N —` caption styling on the narration bar, and a slow draw-on reveal. When the flow reaches a node, the editor slowly inks a thin red frame around it, holds, and lifts it. Source Serif for the body. Best when a diagram sits inside long-form writing and should read as a printed figure rather than a UI.

<div class="beck-lazy"><a href="/fragments/styles/editorial.html">View the editorial gallery</a></div>

## brutalist

Thick strokes, a hard solid offset shadow (no blur), uppercase Archivo, and `steps()` easing on the flow — a hit card slams its border thick for two frames and snaps back. Loud and structural. The shadow is a static offset — nothing snaps or pops at rest; the stepped motion appears only in the choreography.

<div class="beck-lazy"><a href="/fragments/styles/brutalist.html">View the brutalist gallery</a></div>

## sketch

Hand-drawn: Shantell Sans, wobbly outlines, and arrows that draw themselves on. A box jolts a beat larger — a marker pop — the moment an arrow lands on it. The jitter is baked deterministically into the outline geometry — the same content always wobbles the same way — so there is no restless continuous motion, just a lively, informal line.

<div class="beck-lazy"><a href="/fragments/styles/sketch.html">View the sketch gallery</a></div>

## extrude

Nodes as 2.5D slabs with static depth faces. The depth never floats or bobs; a highlight presses the node *down* toward its face instead of lifting it, and an arriving pulse flashes the struck slab face magenta. Gives an architecture a physical, stacked-blocks feel without any resting animation.

<div class="beck-lazy"><a href="/fragments/styles/extrude.html">View the extrude gallery</a></div>

## circuit

Nodes as chips with pin stubs, right-angle traces with a via dot at every bend, and pulse packets riding the traces — an amber LED on the chip blinks when a signal lands. The vias are emitted by the SVG layer at the router's existing bend points — the routing itself is untouched. A natural fit for hardware and data-path diagrams.

<div class="beck-lazy"><a href="/fragments/styles/circuit.html">View the circuit gallery</a></div>

## metro

Edges as thick transit lines with white station dots at their ends and train-capsule packets sliding along them — a station ripples in its line's colour when the train arrives. Per-edge accents colour each line, so a multi-line diagram reads like a transit map. Line weight comes from the style's geometry, not the edge data.

<div class="beck-lazy"><a href="/fragments/styles/metro.html">View the metro gallery</a></div>

---

Want a look that is not here? Every built-in is an instance of the public `BeckStyle` record, and you compose your own by deriving from the closest one with a `with` expression, then registering it. [Author a custom style](/docs/guides/custom-styles) walks through it.
