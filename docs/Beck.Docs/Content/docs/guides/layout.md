---
title: Control the layout
description: Direction, spacing, and pinning a node's rank or order when you need to.
order: 24
sectionLabel: How-to guides
uid: docs.guide.layout
---

This guide shows you how to steer Beck's automatic layout when the default arrangement isn't quite what you want.

Beck lays diagrams out for you, Sugiyama-style: it ranks nodes along the flow, orders them within each rank to minimise edge crossings, then routes edges orthogonally around the cards. You rarely place anything by hand. The recipes below adjust that machinery — set the axis, loosen or tighten the spacing, and, when the auto-layout still puts a node in the wrong place, pin it.

## Set the direction

To change the primary axis, set `meta.direction`. The default is `TB` (top-to-bottom); the others are `BT`, `LR` (left-to-right), and `RL`. The direction decides which way ranks progress — everything else (ordering, routing) follows from it.

```yaml:symbol
wwwroot/examples/guides/layout-01.beck.yaml
```

<beck-diagram src="/examples/guides/layout-01.beck.yaml" mode="auto" animate="false"></beck-diagram>

## Tune the spacing

To loosen or tighten the diagram, set `meta.spacing`. It takes three keys: `rank` (the gap along the flow, default `96`), `node` (the gap across a rank, default `32`), and `cornerRadius` (edge and card corner radius in px, default `16`).

Give a busy diagram more room to breathe:

```yaml:symbol
wwwroot/examples/guides/layout-02.beck.yaml
```

<beck-diagram src="/examples/guides/layout-02.beck.yaml" mode="auto" animate="false"></beck-diagram>

Or pack a compact one tighter:

```yaml:symbol
wwwroot/examples/guides/layout-03.beck.yaml
```

<beck-diagram src="/examples/guides/layout-03.beck.yaml" mode="auto" animate="false"></beck-diagram>

## Fit a wide diagram to the page

By default a diagram that is wider than its container scales down to fit, so it never overflows the page. On a narrow screen a large diagram can become too small to read. Set `meta.fit: scroll` to keep it at full size and let the container scroll sideways instead:

```yaml
meta:
  fit: scroll   # `shrink` (the default) scales to fit; `scroll` keeps full size
```

In `scroll` mode the diagram stays at its natural width and a horizontal scrollbar appears only when the page is too narrow to show all of it; on a wide page it simply centres as usual. Vertical size is never constrained either way — only the horizontal axis differs.

## Pin a node to a rank

When the auto-layout isn't what you want, override it per node. `node.rank` forces a node onto a specific layer; `node.order` tie-breaks left-to-right (or top-to-bottom) within a rank.

Here a metrics sink has no outgoing edge, so Beck ranks it next to the service that feeds it. Pinning `metrics` to rank `2` pushes it out to its own layer alongside the database, where it reads as a downstream consumer rather than a sibling of the service:

```yaml:symbol
wwwroot/examples/guides/layout-04.beck.yaml
```

<beck-diagram src="/examples/guides/layout-04.beck.yaml" mode="auto" animate="false"></beck-diagram>

> [!TIP]
> Reach for `rank`/`order` only when the automatic placement is wrong — a handful of pins go a long way, and over-pinning fights the crossing-reduction pass that makes diagrams readable.

## Nudge edge routing

If an edge leaves or arrives on the wrong face of a card, pin its anchors with `fromSide`/`toSide` (`top` `bottom` `left` `right`) rather than moving the nodes. See [Connect and route edges](/docs/guides/edges) for the full routing recipes.

---

For the complete `meta` and `node` field lists, see the [YAML schema reference](/docs/reference/yaml). To draw a boundary around related nodes — which also influences how they rank together — see [Group related nodes](/docs/guides/groups).

