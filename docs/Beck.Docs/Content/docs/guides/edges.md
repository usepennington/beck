---
title: Connect and route edges
description: Labels, semantic kinds, line style, curves, arrowheads, colour, and pinned sides.
order: 23
sectionLabel: Architecture diagrams
uid: docs.guide.edges
---

This guide shows you how to connect nodes and control exactly how the lines between them look and route — their meaning, style, curve, arrowheads, colour, and where they attach.

> [!NOTE]
> This guide covers **architecture** edges. The other diagram types connect their boxes with their own constructs — [sequence messages](/docs/guides/sequence), [state transitions](/docs/guides/state), and [class relations](/docs/guides/class) — and edge kinds, curves, arrowheads, and pinned sides are architecture-only.

## Connect two nodes

To draw a line, add an edge with a `from` and a `to`. Both must reference a declared node `id`. Add an optional `label` to say what travels along it.

```yaml:symbol
wwwroot/examples/guides/edges-01.beck.yaml
```

<beck-diagram src="/examples/guides/edges-01.beck.yaml" mode="auto" animate="false"></beck-diagram>

## Give an edge meaning

Set `kind` to convey what the connection *is*. Each kind carries a default line style and packet motion, so a diagram reads consistently without you styling every edge by hand.

- `data` (default) — a solid request/response line.
- `control` — a solid command/orchestration line.
- `async` — rendered **dashed**, for fire-and-forget messaging.
- `dependency` — rendered **dashed** and **neutral-coloured**, for "relies on" links that aren't live traffic.

```yaml:symbol
wwwroot/examples/guides/edges-02.beck.yaml
```

<beck-diagram src="/examples/guides/edges-02.beck.yaml" mode="auto" animate="false"></beck-diagram>

## Force a line style

If you want a solid or dashed line regardless of kind, set `style` directly. This overrides the kind's default — useful when you want a `data` edge dashed, or an `async` edge solid.

```yaml:symbol
wwwroot/examples/guides/edges-03.beck.yaml
```

<beck-diagram src="/examples/guides/edges-03.beck.yaml" mode="auto" animate="false"></beck-diagram>

## Choose a curve

The `curve` controls the routing shape:

- `step-round` (default) — orthogonal segments with rounded corners; the best default for layered diagrams.
- `straight` — a direct line; reads well for short, adjacent hops.
- `s` — a smooth S-bend; good for a single edge that crosses ranks and would otherwise look stiff.

```yaml:symbol
wwwroot/examples/guides/edges-04.beck.yaml
```

<beck-diagram src="/examples/guides/edges-04.beck.yaml" mode="auto" animate="false"></beck-diagram>

## Arrowheads

Set `arrow` to control which ends are tipped: `end` (default), `start`, `both`, or `none`. The boolean shorthand `true` means `end` and `false` means `none` — handy when authoring from code.

```yaml:symbol
wwwroot/examples/guides/edges-05.beck.yaml
```

<beck-diagram src="/examples/guides/edges-05.beck.yaml" mode="auto" animate="false"></beck-diagram>

## Colour an edge

Use `color` to override the edge's tint. Pass an accent token (`primary`, `success`, `warn`, `danger`, `info`, `neutral`) to follow the theme in light and dark, or a raw CSS colour to fix it regardless of theme.

```yaml:symbol
wwwroot/examples/guides/edges-06.beck.yaml
```

<beck-diagram src="/examples/guides/edges-06.beck.yaml" mode="auto" animate="false"></beck-diagram>

> [!TIP]
> Prefer tokens over raw colours so your edges adapt when the diagram switches theme. See [Match your theme and colours](/docs/guides/theme).

## Narrate a hop

Give an edge a `note:` and Beck captions the animation with it — the text appears in a bar under the diagram just before the edge's packet travels, when the flow is [derived](/docs/reference/flow#derived-flow) rather than scripted. It's the one-line way to explain *why* a connection fires. See [Narrate the story](/docs/guides/flow#narrate-the-story) for pacing and the scripted `narrate` step.

## Nudge the routing

Beck auto-routes edges and the default is usually right, so reach for this only when a line leaves or enters somewhere awkward. Pin the anchor with `fromSide` and `toSide`, each one of `top`, `bottom`, `left`, or `right`.

```yaml:symbol
wwwroot/examples/guides/edges-07.beck.yaml
```

<beck-diagram src="/examples/guides/edges-07.beck.yaml" mode="auto" animate="false"></beck-diagram>

## Draw to a whole group

An edge's `from` or `to` may be a **group id** as well as a node id. Beck routes the line to the group's box, which is the clean way to show "this talks to everything in here" without a line per member.

```yaml:symbol
wwwroot/examples/guides/edges-08.beck.yaml
```

<beck-diagram src="/examples/guides/edges-08.beck.yaml" mode="auto" animate="false"></beck-diagram>

See [Group related nodes](/docs/guides/groups) for how membership and nesting work.

---

For the complete list of edge fields, defaults, and accepted values, see the [YAML schema reference](/docs/reference/yaml). To try variations live, open the [playground](/playground).

