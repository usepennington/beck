---
title: YAML schema
description: Every key, value, and default in a Beck document — meta, nodes, groups, and edges.
order: 40
sectionLabel: Reference
uid: docs.reference.yaml
---

A Beck document is a YAML mapping with up to four top-level keys — `meta`, `nodes`, `groups`, and
`edges` — plus an optional `flow`. Only node `id`s are required; every other field has a default,
filled in before the diagram is laid out.

The `flow` block has its own page: [Flow & animation](/docs/reference/flow). For a visual tour of
these constructs, see the [syntax cheatsheet](/syntax).

```yaml
meta:    { ... }   # optional — title, direction, theme, spacing
nodes:   [ ... ]   # required — the boxes
groups:  [ ... ]   # optional — labelled boundaries
edges:   [ ... ]   # optional — the connections
flow:    { ... }   # optional — scripted animation (see the flow reference)
```

## meta

All keys are optional.

| key | type | default | description |
|---|---|---|---|
| `title` | string | — | Title drawn above the diagram. |
| `subtitle` | string | — | Muted line under the title. |
| `direction` | `TB` `BT` `LR` `RL` | `TB` | Primary layout axis: top-to-bottom, bottom-to-top, left-to-right, right-to-left. |
| `theme` | `auto` `light` `dark` | `auto` | `auto` follows the host page. |
| `animate` | bool | `true` | `false` renders a static frame and never loads the motion runtime. |
| `loop` | bool | `true` | `false` plays the flow once (forces `flow.repeat: 0`). |
| `spacing` | mapping | see below | Layout gaps and corner radius. |

`spacing` keys:

| key | type | default | description |
|---|---|---|---|
| `rank` | number (px) | `96` | Gap between ranks, along the flow direction. |
| `node` | number (px) | `32` | Gap between nodes within a rank, across the flow. |
| `cornerRadius` | number (px) | `16` | Corner radius on cards and edge bends. |

## nodes

A list of nodes. Each needs a unique `id`; everything else is optional.

| key | type | default | description |
|---|---|---|---|
| `id` | string | — | **Required.** Unique identifier, referenced by edges and groups. |
| `title` | string | = `id` | Display title. |
| `subtitle` | string | — | Muted second line. |
| `kind` | enum | `service` | Archetype; sets the default icon, accent, and variant. See [node kinds](#node-kinds). |
| `variant` | `solid` `subtle` `ghost` | per kind | Visual weight: `subtle` is dimmed; `ghost` is dashed and transparent. |
| `icon` | icon key or inline `<svg>` | per kind | A [named icon](#icons) or raw SVG markup. An unknown key falls back to the kind's icon. |
| `status` | string | — | Status-pill text. |
| `accent` | token or CSS colour | per kind | A [colour token](#colours-and-theme-tokens) or a raw colour. |
| `href` | string | — | Renders the card as a link (`<a href>`). |
| `target` | string | — | Anchor target (e.g. `_blank`); only meaningful with `href`. |
| `surface` | CSS colour | theme surface | Override the card background. |
| `textColor` | CSS colour | theme text | Override the card text colour. |
| `width` | number (px) | auto | Fix the card width (prevents reflow when the status changes). |
| `rank` | number | auto | Force the node onto a specific layout rank. |
| `order` | number | auto | Tie-break order within a rank. |
| `group` | string | — | Inline group membership (alternative to listing the node in a group's `members`). |

### Node kinds

`kind` is a shorthand that sets three defaults at once. Each is independently overridable.

| kind | accent | icon | variant |
|---|---|---|---|
| `service` | `primary` | `service` | `solid` |
| `db` | `info` | `db` | `solid` |
| `queue` | `warn` | `queue` | `solid` |
| `cache` | `warn` | `cache` | `solid` |
| `gateway` | `primary` | `gateway` | `solid` |
| `external` | `neutral` | `external` | `solid` |
| `user` | `success` | `user` | `solid` |
| `ghost` | `neutral` | `service` | `ghost` |

## groups

A list of labelled boxes drawn around member nodes.

| key | type | default | description |
|---|---|---|---|
| `id` | string | — | **Required.** Unique identifier. |
| `label` | string | = `id` | Box label. |
| `members` | list of node or group ids | — | A group id member nests that group inside this one. |
| `accent` | token or CSS colour | `neutral` | Box accent. |

Membership is a tree: every node or group belongs to at most one parent, and a group cannot nest
inside itself. A node can also join a group inline with its own `group` key. An edge's `from`/`to`
may target a group id.

## edges

A list of connections. `from` and `to` are required and must resolve to a declared node or group.

| key | type | default | description |
|---|---|---|---|
| `from` | node or group id | — | **Required.** Source endpoint. |
| `to` | node or group id | — | **Required.** Target endpoint. |
| `label` | string | — | Drawn on the line. |
| `kind` | enum | `data` | Semantic kind; sets default style, colour, and packet motion. See [edge kinds](#edge-kinds). |
| `style` | `solid` `dashed` | per kind | Line style. |
| `curve` | `step-round` `straight` `s` | `step-round` | Routing shape: orthogonal with rounded corners, a straight line, or a smooth S-curve. |
| `color` | token or CSS colour | per kind | Stroke colour. |
| `arrow` | `none` `end` `start` `both` | `end` | Which ends carry an arrowhead. The bool `true`/`false` maps to `end`/`none`. |
| `fromSide` | `top` `bottom` `left` `right` | auto | Pin the side the line leaves the source. |
| `toSide` | `top` `bottom` `left` `right` | auto | Pin the side the line enters the target. |

### Edge kinds

| kind | style | colour | packet motion |
|---|---|---|---|
| `data` | `solid` | edge | medium dot, steady |
| `control` | `solid` | edge | small dot, fast, accelerating |
| `async` | `dashed` | edge | large dot, slow, eased |
| `dependency` | `dashed` | neutral | small dot, no glow |

The packet-motion column describes the default animation along an edge of that kind; see [Flow &
animation](/docs/reference/flow) for the exact values and how to override them.

## Icons

Set a node's `icon` to one of these named keys. Many keys are aliases that share a glyph. An unknown
key falls back to the node kind's default icon, so a typo never drops the glyph.

| category | keys |
|---|---|
| Compute & services | `service` `server` · `function` · `container` `pod` · `kubernetes` `k8s` · `lambda` `serverless` · `agent` |
| Data & storage | `db` `database` · `cache` `redis` `memory` · `bucket` `storage` · `warehouse` · `file` · `vector` `embeddings` |
| Networking & edge | `gateway` `shield` · `loadbalancer` `lb` · `cdn` · `ingress` · `firewall` · `external` `globe` · `cloud` |
| Messaging & events | `queue` · `stream` `kafka` `topic` · `event` · `webhook` · `mail` `email` · `bell` `notification` |
| Security | `lock` · `key` · `vault` `secret` |
| AI & ML | `brain` `model` `llm` `ai` |
| Clients & tools | `user` · `mobile` · `browser` · `terminal` · `code` · `api` · `git` · `repo` |
| Observability & time | `chart` `metrics` `analytics` · `monitor` · `search` · `clock` `scheduler` `cron` · `bolt` |

For anything outside the set, pass raw inline `<svg>…</svg>` markup as the `icon` value. Use
`fill="currentColor"` or `stroke="currentColor"` and a `0 0 24 24` viewBox so the glyph inherits the
node's accent and theme.

## Colours and theme tokens

Anywhere a colour is accepted (`accent`, edge `color`, flow-step `color`), you can give either a
**token** or a **raw CSS colour**. Tokens follow the theme and recolour in light and dark mode; raw
colours are frozen.

| token | default |
|---|---|
| `primary` | your site's `--color-primary-600` |
| `success` | emerald |
| `warn` | amber |
| `danger` | red |
| `info` | violet |
| `neutral` | grey |

Under the hood every colour is a `--beck-*` CSS custom property that defaults to your site's
`--color-*` palette, with a literal fallback. That is why a diagram adopts the host page's colours
and dark mode with no configuration. Override a token by defining the host ramp (preferred) or by
setting the `--beck-*` variable on an ancestor — see [Match your theme and
colours](/docs/guides/theme).

| variable | defaults to | used for |
|---|---|---|
| `--beck-surface` | `--color-base-50` | diagram background |
| `--beck-node-bg` | `--color-base-50` | card background |
| `--beck-node-border` | `--color-base-200` | card border |
| `--beck-text` | `--color-base-800` | card title text |
| `--beck-text-muted` | `--color-base-500` | subtitles, edge labels |
| `--beck-text-faint` | `--color-base-400` | faint detail |
| `--beck-primary` | `--color-primary-600` | the `primary` token |
| `--beck-success` | emerald | the `success` token |
| `--beck-warn` | amber | the `warn` token |
| `--beck-danger` | red | the `danger` token |
| `--beck-info` | violet | the `info` token |
| `--beck-neutral` | `--color-base-400` | the `neutral` token |
| `--beck-edge` | `--color-base-300` | edge stroke |
| `--beck-packet` | `--beck-primary` | default travelling packet |
| `--beck-icon-bg` | `--color-base-100` | icon chip background |

Light and dark are handled purely by redefining the surface, text, and edge variables under
`[data-theme="dark"]`; the accent tokens ride your host ramps in both modes. There is no per-theme
JavaScript and no hardcoded hex in the renderer.
