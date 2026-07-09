---
title: Pick a built-in style
description: The eleven built-in styles — classic, minimal, terminal, blueprint, glow, editorial, brutalist, sketch, extrude, circuit, and metro — each shown across an architecture, a sequence, and a class diagram.
order: 32
sectionLabel: Cross-cutting
uid: docs.guide.styles
---

A **style** is a complete visual identity for a diagram — its shapes, strokes, typography, colour bias, and motion character — chosen with a single token. Beck ships eleven: `classic` (the default, unchanged when you set nothing) plus ten designed looks. Set one per diagram with `meta.style`, or site-wide from C# with `SvgRenderOptions.Style` (a diagram's own `meta.style` opts back out). A style only ever redefines the *defaults* of the `--beck-*` tokens, so a themed diagram still adopts your host palette and still flips with light and dark — the [theme guide](/docs/guides/theme) covers that axis, which every style honours. To build your own by deriving from a built-in with a `with` expression, see [Author a custom style](/docs/guides/custom-styles).

## The three diagrams

Every style below renders the same three sources — an architecture graph with a flow, a sequence, and a class diagram — so you can read one look against another. Only the style changes between sections; the YAML is identical.

```yaml:symbol
wwwroot/examples/styles/architecture.beck.yaml
```

```yaml:symbol
wwwroot/examples/styles/sequence.beck.yaml
```

```yaml:symbol
wwwroot/examples/styles/class.beck.yaml
```

Each gallery render is a `` ```beck:symbol,style=<name>,static `` fence — the `style=` flag injects `meta.style` before rendering, so this page repeats one snippet per style instead of eleven hand-edited copies.

## classic

The default. A balanced, modern card look — soft shadows, rounded corners, Inter and IBM Plex Mono. Every other style is a deliberate departure from this baseline, and any diagram that sets no style renders exactly here, byte-for-byte.

```beck:symbol,style=classic,static
wwwroot/examples/styles/architecture.beck.yaml
```

```beck:symbol,style=classic,static
wwwroot/examples/styles/sequence.beck.yaml
```

```beck:symbol,style=classic,static
wwwroot/examples/styles/class.beck.yaml
```

## minimal

Sober and flat: no shadows, hairline borders, and a single travelling dot for the packet. Glow and rings are off. The closest thing to a "no design" look — reach for it when the diagram should recede and the surrounding prose should lead.

```beck:symbol,style=minimal,static
wwwroot/examples/styles/architecture.beck.yaml
```

```beck:symbol,style=minimal,static
wwwroot/examples/styles/sequence.beck.yaml
```

```beck:symbol,style=minimal,static
wwwroot/examples/styles/class.beck.yaml
```

## terminal

Monospace everything, `[bracketed]` label affordances, square block packets, and a default accent biased toward the green success ramp. The identity is carried by type and hard-stepped motion — no scanlines, no blinking cursor. Suits CLI, DevOps, and infra diagrams.

```beck:symbol,style=terminal,static
wwwroot/examples/styles/architecture.beck.yaml
```

```beck:symbol,style=terminal,static
wwwroot/examples/styles/sequence.beck.yaml
```

```beck:symbol,style=terminal,static
wwwroot/examples/styles/class.beck.yaml
```

## blueprint

A drafting surface: a faint grid behind the diagram, dashed flowing edges, dimension ticks on group boxes, and mono uppercase labels. Reads like an engineering plan — good for architecture and infrastructure where the "schematic" framing fits the content.

```beck:symbol,style=blueprint,static
wwwroot/examples/styles/architecture.beck.yaml
```

```beck:symbol,style=blueprint,static
wwwroot/examples/styles/sequence.beck.yaml
```

```beck:symbol,style=blueprint,static
wwwroot/examples/styles/class.beck.yaml
```

## glow

The "designed by Claude" option: gradient strokes, a soft bloom around nodes, and a breathing pulse on active ones. Gradients are defined once and reference tokens through `color-mix`, so the bloom still tracks your palette. Opt into it for a hero diagram; it is deliberately not the default.

```beck:symbol,style=glow,static
wwwroot/examples/styles/architecture.beck.yaml
```

```beck:symbol,style=glow,static
wwwroot/examples/styles/sequence.beck.yaml
```

```beck:symbol,style=glow,static
wwwroot/examples/styles/class.beck.yaml
```

## editorial

A serif textbook figure: hairline rules, no fills, `Fig. N —` caption styling on the narration bar, and a slow draw-on reveal. Source Serif for the body. Best when a diagram sits inside long-form writing and should read as a printed figure rather than a UI.

```beck:symbol,style=editorial,static
wwwroot/examples/styles/architecture.beck.yaml
```

```beck:symbol,style=editorial,static
wwwroot/examples/styles/sequence.beck.yaml
```

```beck:symbol,style=editorial,static
wwwroot/examples/styles/class.beck.yaml
```

## brutalist

Thick strokes, a hard solid offset shadow (no blur), uppercase Archivo, and `steps()` easing on the flow. Loud and structural. The shadow is a static offset — nothing snaps or pops at rest; the stepped motion appears only in the choreography.

```beck:symbol,style=brutalist,static
wwwroot/examples/styles/architecture.beck.yaml
```

```beck:symbol,style=brutalist,static
wwwroot/examples/styles/sequence.beck.yaml
```

```beck:symbol,style=brutalist,static
wwwroot/examples/styles/class.beck.yaml
```

## sketch

Hand-drawn: Shantell Sans, wobbly outlines, and arrows that draw themselves on. The jitter is baked deterministically into the outline geometry — the same content always wobbles the same way — so there is no restless continuous motion, just a lively, informal line.

```beck:symbol,style=sketch,static
wwwroot/examples/styles/architecture.beck.yaml
```

```beck:symbol,style=sketch,static
wwwroot/examples/styles/sequence.beck.yaml
```

```beck:symbol,style=sketch,static
wwwroot/examples/styles/class.beck.yaml
```

## extrude

Nodes as 2.5D slabs with static depth faces. The depth never floats or bobs; a highlight presses the node *down* toward its face instead of lifting it. Gives an architecture a physical, stacked-blocks feel without any resting animation.

```beck:symbol,style=extrude,static
wwwroot/examples/styles/architecture.beck.yaml
```

```beck:symbol,style=extrude,static
wwwroot/examples/styles/sequence.beck.yaml
```

```beck:symbol,style=extrude,static
wwwroot/examples/styles/class.beck.yaml
```

## circuit

Nodes as chips with pin stubs, right-angle traces with a via dot at every bend, and pulse packets riding the traces. The vias are emitted by the SVG layer at the router's existing bend points — the routing itself is untouched. A natural fit for hardware and data-path diagrams.

```beck:symbol,style=circuit,static
wwwroot/examples/styles/architecture.beck.yaml
```

```beck:symbol,style=circuit,static
wwwroot/examples/styles/sequence.beck.yaml
```

```beck:symbol,style=circuit,static
wwwroot/examples/styles/class.beck.yaml
```

## metro

Edges as thick transit lines with white station dots at their ends and train-capsule packets sliding along them. Per-edge accents colour each line, so a multi-line diagram reads like a transit map. Line weight comes from the style's geometry, not the edge data.

```beck:symbol,style=metro,static
wwwroot/examples/styles/architecture.beck.yaml
```

```beck:symbol,style=metro,static
wwwroot/examples/styles/sequence.beck.yaml
```

```beck:symbol,style=metro,static
wwwroot/examples/styles/class.beck.yaml
```

---

Want a look that is not here? Every built-in is an instance of the public `BeckStyle` record, and you compose your own by deriving from the closest one with a `with` expression, then registering it. [Author a custom style](/docs/guides/custom-styles) walks through it.
