---
title: YAML schema
description: Every key, value, and default in a Beck document — all four diagram types.
order: 40
sectionLabel: Reference
uid: docs.reference.yaml
---

A Beck document is a YAML mapping that opens with a root `type:` declaring what kind of diagram it
is. The type picks the layout engine and the top-level keys; everything else — theming, animation,
fenced-block rendering — is shared. Only ids are required; every other field has a default, filled
in before the diagram is laid out.

| `type` | what it draws | top-level keys |
|---|---|---|
| `architecture` | the layered boxes-and-lines system diagram | `meta` `nodes` `groups` `edges` `flow` |
| `sequence` | participants, lifelines, and ordered messages | `meta` `participants` `messages` `flow` |
| `state` | a state machine of pills and transitions | `meta` `states` `transitions` `flow` |
| `class` | UML class cards and relations | `meta` `classes` `relations` `groups` `flow` |

The `flow` block has its own page: [Flow & animation](/docs/reference/flow). For a visual tour of
these constructs, see the [syntax cheatsheet](/syntax).

```yaml
type: architecture   # architecture | sequence | state | class
meta:    { ... }     # optional — title, direction, theme, fit, spacing
nodes:   [ ... ]     # the boxes
groups:  [ ... ]     # optional — labelled boundaries
edges:   [ ... ]     # optional — the connections
flow:    { ... }     # optional — scripted animation (see the flow reference)
```

> [!NOTE]
> A document without a `type:` still renders as an architecture diagram, but that form is
> deprecated — the engine logs a console warning. Always declare the type.

## meta

Shared by every diagram type; all keys are optional. (`direction` and `spacing.rank` only affect
the layered types — architecture, state, class.)

| key | type | default | description |
|---|---|---|---|
| `title` | string | — | Title drawn above the diagram. |
| `subtitle` | string | — | Muted line under the title. |
| `direction` | `TB` `BT` `LR` `RL` | `TB` | Primary layout axis: top-to-bottom, bottom-to-top, left-to-right, right-to-left. |
| `theme` | `auto` `light` `dark` | `auto` | `auto` follows the host page. |
| `style` | string | `classic` | Visual style token — one of the nine built-ins (`classic`, `minimal`, `terminal`, `blueprint`, `glow`, `brutalist`, `sketch`, `extrude`, `circuit`) or a registered custom style. See [Pick a built-in style](/docs/guides/styles) and the [style system reference](/docs/reference/styles). |
| `animate` | bool | `true` | `false` renders a static frame and never loads the motion runtime. |
| `loop` | bool | `true` | `false` plays the flow once (forces `flow.repeat: 0`). |
| `fit` | `shrink` `scroll` | `shrink` | What a diagram wider than its container does: `shrink` scales it down to fit; `scroll` keeps it at natural size and scrolls horizontally. Vertical size is never constrained. |
| `spacing` | mapping | see below | Layout gaps and corner radius. |
| `narrate` | bool or mapping | `true` | Narration caption bar: `false` suppresses it; a mapping tunes the reading-time pace. See below. |

`spacing` keys:

| key | type | default | description |
|---|---|---|---|
| `rank` | number (px) | `96` (state/class `130`) | Gap between ranks, along the flow direction. |
| `node` | number (px) | `32` (state/class `72`) | Gap between nodes within a rank, across the flow. |
| `cornerRadius` | number (px) | `16` | Corner radius on cards and edge bends. |

`narrate` keys (a mapping value; a bare boolean just toggles `enabled`):

| key | type | default | description |
|---|---|---|---|
| `enabled` | bool | `true` | `false` suppresses the caption bar entirely. |
| `wpm` | number | `170` | Reading pace, words per minute — drives each caption's auto hold. |
| `min` | number (s) | `1.4` | Floor on a caption's on-screen time. |
| `pad` | number (s) | `0.5` | Extra seconds on top of the reading time. |

Captions are supplied by a [`narrate` flow step](/docs/reference/flow#narration) or a connector
`note:` (see `edges`, `messages`, `transitions` below); `meta.narrate` only paces and toggles them.

## nodes (`type: architecture`)

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

## groups (`architecture` and `class`)

A list of labelled boxes drawn around member nodes (namespace boxes, in a class diagram).

| key | type | default | description |
|---|---|---|---|
| `id` | string | — | **Required.** Unique identifier. |
| `label` | string | = `id` | Box label. |
| `members` | list of node or group ids | — | A group id member nests that group inside this one. |
| `accent` | token or CSS colour | `neutral` | Box accent. |

Membership is a tree: every node or group belongs to at most one parent, and a group cannot nest
inside itself. A node can also join a group inline with its own `group` key. An edge's `from`/`to`
may target a group id.

## edges (`type: architecture`)

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
| `note` | string | — | Narration caption for this hop, shown just before its packet in a [derived flow](/docs/reference/flow#derived-flow). Ignored when a `flow:` is authored. |
| `fromSide` | `top` `bottom` `left` `right` | auto | Pin the side the line leaves the source. |
| `toSide` | `top` `bottom` `left` `right` | auto | Pin the side the line enters the target. |

A **feedback edge** — one that runs back against the flow and would have to jump over the nodes
between its endpoints — automatically loops out on a clear face (over the top for `LR`/`RL`, out the
left for `TB`/`BT`) instead of jogging through the forward chain, so the forward edges stay straight.
An adjacent back-and-forth pair (two nodes wired both ways, with nothing between them) is left inline
as two parallel lines. Pin `fromSide`/`toSide` to force a specific loop face — e.g. both `bottom` to
route the return under the row — or set `curve: straight` for a direct line.

### Edge kinds

| kind | style | colour | packet motion |
|---|---|---|---|
| `data` | `solid` | edge | medium dot, steady |
| `control` | `solid` | edge | small dot, fast, accelerating |
| `async` | `dashed` | edge | large dot, slow, eased |
| `dependency` | `dashed` | neutral | small dot, no glow |

The packet-motion column describes the default animation along an edge of that kind; see [Flow &
animation](/docs/reference/flow) for the exact values and how to override them.

## participants and messages (`type: sequence`)

A sequence diagram lays `participants` out as columns (in declared order) and draws `messages` as
rows, in authored order. Without a `flow:`, the message order **is** the animation — one packet per
message. See the [sequence diagrams guide](/docs/guides/sequence).

`participants` entries take the same fields as architecture [nodes](#nodes-type-architecture)
(`id`, `title`, `subtitle`, `kind`, `icon`, `accent`, …); layout keys (`rank`, `order`, `group`)
are ignored.

`messages` entries:

| key | type | default | description |
|---|---|---|---|
| `from`, `to` | participant id | — | **Required.** Equal ids draw a self-message loop. |
| `label` | string | — | Drawn above the arrow. |
| `reply` | bool | `false` | A return message: dashed, open arrowhead, closes the receiver's activation bar. |
| `kind` | `data` `control` `async` `dependency` | `data` (`control` for replies) | Semantic kind; `async` renders dashed with an open arrowhead. |
| `style` | `solid` `dashed` | per kind | Line style override. |
| `color` | token or CSS colour | worker's accent | Stroke colour. Defaults to the accent of the participant doing the work — the receiver of a call, the sender of a reply — so request/reply pairs share a hue. |
| `note` | string | — | Narration caption for this message, shown just before it fires in the derived flow. See [narration](/docs/reference/flow#narration). |
| `activate` | bool | auto | Force (`true`) or suppress (`false`) an activation bar on the receiver. |

A list entry of the form `- section: <label>` (instead of a message) opens a tinted, dashed band
around every message until the next section (or the end), and becomes a `phase` seek point in the
derived animation. It takes an optional `accent` (token or CSS colour, default `neutral`) that
colours the band's border, fill, and floating label.

**Activation bars** are automatic: a non-reply message starts a bar on its receiver when a later
`reply: true` from that receiver back to the sender closes it. Nested request/reply pairs nest the
bars.

## states and transitions (`type: state`)

A state machine on the layered engine — states are pills, transitions are labelled edges. States
referenced only by transitions are auto-created, so a terse machine needs nothing but
`transitions:`. The token `"[*]"` (quote it — YAML) is the UML entry/exit pseudo-state: use it as a
`from` for the initial dot, as a `to` for the final bullseye. See the [state diagrams
guide](/docs/guides/state).

`states` entries (all optional refinements):

| key | type | default | description |
|---|---|---|---|
| `id` | string | — | **Required.** |
| `title` | string | = `id` | Pill text. |
| `subtitle` | string | — | Muted second line. |
| `accent` | token or CSS colour | `neutral` | Pill accent. |
| `width`, `rank`, `order` | number | auto | Same as architecture nodes. |

`transitions` entries:

| key | type | default | description |
|---|---|---|---|
| `from`, `to` | state id or `"[*]"` | — | **Required.** Equal ids draw a self-loop. |
| `label` | string | — | Drawn on the line. |
| `style` | `solid` `dashed` | `solid` | Line style. |
| `color` | token or CSS colour | edge | Stroke colour. |
| `note` | string | — | Narration caption for this transition, shown just before its packet in the derived flow. See [narration](/docs/reference/flow#narration). |

## classes and relations (`type: class`)

UML class cards — a «stereotype» + name header and field/method compartments — joined by relations
with the classic end markers. `groups` (namespace boxes) work exactly as in architecture diagrams.
See the [class diagrams guide](/docs/guides/class).

`classes` entries:

| key | type | default | description |
|---|---|---|---|
| `id` | string | — | **Required.** |
| `name` | string | = `id` | Class name (alias: `title`). |
| `stereotype` | string | — | Rendered as `«stereotype»` above the name (e.g. `interface`, `abstract`). |
| `fields` | list of strings | — | Field compartment lines, e.g. `"Id: Guid"`. |
| `methods` | list of strings | — | Method compartment lines, e.g. `"Submit()"`. |
| `accent` | token or CSS colour | `primary` | Header tint. |
| `href`, `target` | string | — | Link the card (e.g. to API docs). |
| `group` | string | — | Namespace-box membership. |
| `width`, `rank`, `order` | number | auto | Same as architecture nodes. |

`relations` entries — note the direction conventions:

| key | type | default | description |
|---|---|---|---|
| `from`, `to` | class id | — | **Required.** |
| `kind` | see below | `association` | Relation kind; picks the markers, style, and layout direction. |
| `label` | string | — | Drawn on the line. |
| `fromCard`, `toCard` | string | — | Multiplicities near the ends, e.g. `"1"`, `"*"`. |
| `color` | token or CSS colour | per kind | Stroke colour. |

| `kind` | authored direction | rendering |
|---|---|---|
| `inherits` | child → parent | solid, hollow triangle at the parent |
| `implements` | class → interface | dashed, hollow triangle at the interface |
| `association` | source → target | solid, arrowhead at the target |
| `aggregation` | whole → part | solid, hollow diamond at the whole |
| `composition` | whole → part | solid, filled diamond at the whole |
| `dependency` | source → target | dashed, open arrowhead at the target |

Parents rank above children automatically (`inherits`/`implements` are flipped internally so the
hierarchy reads top-down). Class diagrams are structural, so they don't animate by default: without
a `flow:` the diagram renders a still frame. Script a `flow:` if you want a guided tour.

## Icons

Set a node's `icon` to one of these named keys. Many keys are aliases that share a glyph. An unknown
key falls back to the node kind's default icon, so a typo never drops the glyph. See every glyph
rendered live in the [icon reference](/docs/reference/icons).

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
