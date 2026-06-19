---
title: Beck for LLMs — author diagrams from YAML
description: A getting-started cheat-sheet for AI agents writing Beck diagrams — the document shape, every valid token, a worked example, and where to look the details up.
---

# Beck for LLMs

This page is a cheat-sheet for an AI assistant authoring **Beck** diagrams. Beck turns a small
declarative YAML document into a clean, auto-laid-out, animated architecture diagram that renders in
the browser and adopts the host page's colours. You write the boxes and the lines; Beck does the
layout, routing, theming, and motion.

**The one rule:** only node `id`s are required. Every other field has a sensible default, so the
smallest useful diagram is a few nodes and edges. Add detail only where you want to override a
default.

The rest of this site is available to you as Markdown — every page below has a `/_llms/<path>.md`
sidecar. **This page is the map; follow the links for the full tables.** Don't reproduce the
reference here from memory — open it.

## Quickest start: a fenced block

The primary integration is a Markdown fenced code block tagged ` ```beck `. On any page that
includes the engine script, every such block is hydrated into a live diagram — nothing server-side:

````markdown
```beck
meta: { title: Web platform, direction: LR }
nodes:
  - { id: user, title: Client, kind: user }
  - { id: gw, title: API Gateway, kind: gateway }
  - { id: orders, title: Orders }
  - { id: db, title: Postgres, kind: db }
edges:
  - { from: user, to: gw }
  - { from: gw, to: orders }
  - { from: orders, to: db, label: query }
```
````

That is a complete diagram. With no `flow:` block, Beck derives a packet animation from the edges.

## The document shape

A Beck document is a YAML mapping with up to four top-level keys, plus an optional `flow`:

```yaml
meta:    { ... }   # optional — title, direction, theme, fit, spacing
nodes:   [ ... ]   # the boxes — each needs a unique id
groups:  [ ... ]   # optional — labelled boundaries around nodes
edges:   [ ... ]   # optional — connections (from/to)
flow:    { ... }   # optional — scripted animation; omit and one is derived
```

Inline (`{ }` / `[ ]`) and block YAML are both fine. Use whichever is clearer.

## Cheat-sheet: valid values

These are the **closed sets** an author must not invent. Pick from these exact tokens. For the full
field tables and defaults, see the [YAML schema reference](/docs/reference/yaml).

### meta (all optional)

| key | values / type | default |
|---|---|---|
| `title`, `subtitle` | string | — |
| `direction` | `TB` `BT` `LR` `RL` | `TB` |
| `theme` | `auto` `light` `dark` | `auto` (follows host page) |
| `animate` | bool | `true` |
| `loop` | bool | `true` (`false` plays once) |
| `fit` | `shrink` `scroll` | `shrink` |
| `spacing` | `{ rank, node, cornerRadius }` (px) | `96` / `32` / `16` |

### nodes — `id` required, rest optional

Key fields: `id`, `title` (= `id`), `subtitle`, `kind`, `variant`, `icon`, `status`, `accent`,
`href`/`target`, `surface`/`textColor`, `width`, `rank`, `order`, `group`.

`kind` is shorthand that sets the default **icon + accent + variant** at once; override any of them
individually.

| `kind` | accent | icon | variant |
|---|---|---|---|
| `service` (default) | primary | service | solid |
| `db` | info | db | solid |
| `queue` | warn | queue | solid |
| `cache` | warn | cache | solid |
| `gateway` | primary | gateway | solid |
| `external` | neutral | external | solid |
| `user` | success | user | solid |
| `ghost` | neutral | service | ghost |

`variant`: `solid` `subtle` `ghost`. Full field list and semantics: [nodes in the
schema](/docs/reference/yaml#nodes) and the [Style your nodes guide](/docs/guides/nodes).

### groups

`id` required; `label` (= `id`), `members` (list of node **or group** ids — a group id nests that
group), `accent`. Membership is a tree: every node/group has at most one parent, no cycles. A node
can also join inline with its own `group:` key. Edges may target a group id. See [Group related
nodes](/docs/guides/groups).

### edges — `from` and `to` required

Both must resolve to a declared node or group id.

| key | values | default |
|---|---|---|
| `from`, `to` | node/group id | — (required) |
| `label` | string | — |
| `kind` | `data` `control` `async` `dependency` | `data` |
| `style` | `solid` `dashed` | per kind |
| `curve` | `step-round` `straight` `s` | `step-round` |
| `arrow` | `none` `end` `start` `both` (or bool → `end`/`none`) | `end` |
| `color` | token or CSS colour | per kind |
| `fromSide` / `toSide` | `top` `bottom` `left` `right` | auto |

Edge `kind` sets a default style and packet motion: `data` (solid, steady), `control` (solid, small
fast), `async` (dashed, large slow), `dependency` (dashed neutral). See [Connect and route
edges](/docs/guides/edges).

### Colours and accents

Anywhere a colour is accepted (`accent`, edge `color`, flow-step `color`), give **either** a theme
token **or** a raw CSS colour. Tokens follow light/dark; raw colours are frozen.

The six tokens — and the only valid token names — are: `primary` `success` `warn` `danger` `info`
`neutral`. Don't invent others. How tokens map to your palette and the full `--beck-*` variable
list: [Match your theme and colours](/docs/guides/theme) and the [colour
tokens](/docs/reference/yaml#colours-and-theme-tokens).

### Icons

Set a node's `icon` to a named key; an **unknown key silently falls back** to the kind's icon, so a
typo never drops the glyph. Common keys: `service` `function` `container` `kubernetes` `lambda`
`agent` `db` `cache` `bucket` `warehouse` `vector` `gateway` `loadbalancer` `cdn` `firewall` `cloud`
`queue` `stream` `event` `webhook` `mail` `lock` `key` `vault` `brain` (alias `llm`/`model`/`ai`)
`user` `mobile` `browser` `terminal` `code` `api` `git` `chart` `monitor` `search` `clock` `bolt`.
For anything else, pass raw inline `<svg>…</svg>` using `fill="currentColor"`/`stroke="currentColor"`
and a `0 0 24 24` viewBox so it inherits the node's accent and theme. Full live catalogue:
[Icons reference](/docs/reference/icons).

## Flow (animation)

A `flow` is an ordered list of single-key step mappings the engine compiles into a timeline and
plays on scroll-into-view. Omit it and Beck derives one from the edges. `from`/`to`/`node` reference
declared ids.

```yaml
flow:
  repeat: -1          # -1 loops forever (default); 0 plays once
  repeatDelay: 1.5    # seconds between repeats
  steps:
    - packet: { from: client, to: api, label: GET /item }
    - working: { node: db }
    - packet: { from: api, to: db, color: info }
    - idle: { node: db }
    - packet: { from: db, to: api, color: success }
    - wait: 1
```

**Step types** (the complete set): `packet`, `burst`, `status`, `highlight`, `pulse`, `activate`,
`stream`, `working`, `idle`, `fail`, `phase`, `wait`, `reset`, `parallel`.

- `packet` `{ from, to, via?, color?, label? }` — one dot travels an edge (or a multi-hop chain via `via`).
- `burst` `{ from, to|[to…], count, stagger, … }` — `count` waves broadcast to every target.
- `status` `{ node, text, color? }`, `working` `{ node, color? }`, `stream`/`activate` `{ from, to, color? }` — **persist** until a later step or `reset`.
- `highlight` / `pulse` `{ node, color? }`, `fail` `{ node, text?, color? }` — one-shot beats.
- `idle` `{ node }` clears `working`; `reset` restores the initial state.
- `phase: <label>` marks a seek point; `wait: <seconds>` pauses; `parallel: [ …steps ]` runs steps together.

`packet`/`burst` share look-and-motion **knobs** (each defaults to the traversed edge kind): `shape`
(`dot` `circle` `ring`), `size`, `speed`, `glow`, `impact`, `ease`, `via`, `color`, `label`. Eases:
`linear` `smooth` `accelerate` `decelerate` `expo` `sine` `steps` `bounce`. Full semantics: [Flow &
animation reference](/docs/reference/flow) and the [Animate the flow guide](/docs/guides/flow).

## A complete worked example

```beck
meta:
  title: Order service
  direction: LR
nodes:
  - { id: client, title: Web App, kind: user }
  - { id: api, title: API, kind: gateway }
  - { id: orders, title: Orders }
  - { id: cache, title: Redis, kind: cache }
  - { id: db, title: Postgres, kind: db }
  - { id: bus, title: Events, kind: queue }
groups:
  - { id: backend, label: Backend, members: [orders, cache, db], accent: primary }
edges:
  - { from: client, to: api }
  - { from: api, to: orders }
  - { from: orders, to: cache, label: read-through }
  - { from: orders, to: db, label: query }
  - { from: orders, to: bus, label: publish, kind: async }
flow:
  steps:
    - packet: { from: client, to: api, label: POST /orders }
    - packet: { from: api, to: orders }
    - working: { node: db }
    - packet: { from: orders, to: db, color: info }
    - idle: { node: db }
    - packet: { from: orders, to: bus, color: warn }
    - status: { node: orders, text: created, color: success }
    - wait: 1
    - reset:
```

## Authoring from C# (`Beck.Authoring`)

To generate the YAML from code (walking a real model — an Aspire graph, an EF model, a service
registry), use the dependency-free fluent `DiagramBuilder` in the `Beck` namespace:

```csharp
using Beck;

string fence = new DiagramBuilder("Order service")
    .Direction(Direction.LR)
    .Node("client", "Web App", NodeKind.User)
    .Node("api", n => n.Title("API").Kind(NodeKind.Gateway))
    .Node("db", n => n.Title("Postgres").Kind(NodeKind.Db))
    .Group("backend", g => g.Label("Backend").Members("db").Accent(AccentToken.Primary))
    .Edge("client", "api")
    .Edge("api", "db", e => e.Label("query"))
    .Flow(f => f
        .Packet("client", "api", label: "POST /orders")
        .Working("db")
        .Packet("api", "db", color: "info")
        .Idle("db"))
    .ToFence();   // ```beck … ``` ready for Markdown; use .ToYaml() for the raw YAML
```

C# enums map to schema tokens (lowercased), with one special case: `EdgeCurve.StepRound` →
`step-round`. `Direction` stays uppercase. Enums available: `Direction`, `NodeKind`, `NodeVariant`,
`EdgeStyle`, `EdgeCurve`, `EdgeKind`, `PacketEase`, `PacketShape`, `ThemeMode`, `FitMode`,
`AccentToken`, `Side`, `ArrowEnds`. The builder throws if an edge references an undeclared id. Full
surface: [API reference](/api) · tutorial: [Author a diagram in C#](/docs/tutorials/csharp) ·
keeping diagrams in sync with your model: [Generate diagrams from your code](/docs/guides/generate).

## Embedding a diagram in a page

Beck ships as a single .NET NuGet package (`Beck`) — there is no npm package or CDN. Include its one
script once:

```html
<script src="/_content/Beck/beck.global.js" defer></script>
```

Then choose an integration:

- **Fenced block (main path):** write a ` ```beck ` block in any Markdown/HTML; it auto-hydrates.
- **Custom element:** `<beck-diagram>` renders in light DOM so host CSS reaches it. Source can be
  inline text, a child `<script type="application/yaml">`, or a `src` URL. Attributes: `mode`
  (`light`/`dark`/`auto`), `src`, `animate` (`false` to disable).
  ```html
  <beck-diagram src="/diagrams/architecture.beck.yaml" mode="auto"></beck-diagram>
  ```
- **Imperative:** after the script loads, the API is on `window.Beck`:
  ```js
  const handle = window.Beck.renderDiagram(host, yamlString, { theme: "auto" });
  // handle: play(), pause(), reset(), seek(label), setTheme(mode), relayout(), destroy(), ready
  ```

Setup and palette wiring: [Add Beck to your site](/docs/guides/install) (generic ASP.NET/Tailwind)
or [Add Beck to a Pennington site](/docs/guides/pennington).

## Rules and gotchas

- **Only node `id`s are required.** Lean on defaults; override deliberately.
- **Edge endpoints must exist.** `from`/`to` must name a declared node or group id (group ids are
  valid endpoints).
- **`kind` is a bundle** of icon + accent + variant defaults — set it first, then override pieces.
- **Colours are tokens or raw CSS.** The only tokens are `primary success warn danger info neutral`;
  a token follows the theme, a raw colour is frozen. Beck reads the host page's `--color-*` palette,
  so a diagram matches its site and dark mode with no extra config.
- **Icons fail soft** — an unknown `icon` key falls back to the kind's icon; use inline `<svg>` for
  anything off the list.
- **Groups nest** by putting a group id in another group's `members`; the membership graph is a
  tree (one parent, no self-nesting).
- **Animation is automatic** — no `flow:` means a derived one. `meta.loop: false` plays once;
  `meta.animate: false` (or the reader's reduced-motion setting) renders a static frame and never
  loads the motion runtime.
- **Flow steps are ordered**, single-key mappings; `parallel` runs its children simultaneously;
  `status`/`working`/`stream`/`activate` persist until cleared or `reset`.
- **It's plain YAML** — the parser reports friendly errors (with a line number for syntax issues),
  so prefer fixing the YAML over guessing.
