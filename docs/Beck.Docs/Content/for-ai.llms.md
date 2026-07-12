---
title: Beck for LLMs — author diagrams from YAML
description: A getting-started cheat-sheet for AI agents writing Beck diagrams — the document shape, every valid token, a worked example, and where to look the details up.
---

# Beck for LLMs

This page is a cheat-sheet for an AI assistant authoring **Beck** diagrams. Beck turns a small
declarative YAML document into a clean, auto-laid-out, animated architecture diagram — rendered
server-side in C# to a self-animating inline SVG that adopts the host page's colours. You write the
boxes and the lines; Beck does the layout, routing, theming, and motion.

**The one rule:** only node `id`s are required. Every other field has a sensible default, so the
smallest useful diagram is a few nodes and edges. Add detail only where you want to override a
default.

The rest of this site is available to you as Markdown — every page below has a `/_llms/<path>.md`
sidecar. **This page is the map; follow the links for the full tables.** Don't reproduce the
reference here from memory — open it.

## Quickest start: a fenced block

The primary integration is a Markdown fenced code block tagged ` ```beck `. A site built with the
Beck NuGet package renders every such block to a static, self-animating SVG at build time — no
client JavaScript at all:

````markdown
```beck
type: architecture
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

A Beck document is a YAML mapping that **always opens with a root `type:`** — one of
`architecture`, `sequence`, `state`, `class`, `flowchart`, `mindmap`, or `chart`. The type picks the
top-level keys; `meta` is shared by all, and `flow` by every type except `chart` (which is static).
For `type: architecture`:

```yaml
type: architecture # REQUIRED first — architecture | sequence | state | class
meta:    { ... }   # optional — title, direction, theme, fit, spacing
nodes:   [ ... ]   # the boxes — each needs a unique id
groups:  [ ... ]   # optional — labelled boundaries around nodes
edges:   [ ... ]   # optional — connections (from/to)
flow:    { ... }   # optional — scripted animation; omit and one is derived
```

The other types swap the middle keys: `type: sequence` uses `participants` + `messages`,
`type: state` uses `states` + `transitions`, `type: class` uses `classes` + `relations` (+
`groups`), `type: flowchart` uses `steps` + `links`, `type: mindmap` uses `root` + `topics`, and
`type: chart` uses `series`. Their
cheat-sheets are [below](#the-other-diagram-types-sequence-state-class-flowchart-mindmap-chart).

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
| `style` | `classic` `minimal` `terminal` `blueprint` `glow` `brutalist` `sketch` `extrude` `circuit` (or a registered custom name) | `classic` |
| `animate` | bool | `true` |
| `loop` | bool | `true` (`false` plays once) |
| `fit` | `shrink` `scroll` | `shrink` |
| `spacing` | `{ rank, node, cornerRadius }` (px) | `96` / `32` / `16` (state & class default roomier: `130` / `72` / `16`) |
| `narrate` | bool, or `{ enabled, wpm, min, pad }` | `true` (caption-bar pacing; `false` = off) |

### nodes — `id` required, rest optional

Key fields: `id`, `title` (= `id`), `subtitle`, `kind`, `variant`, `icon`, `status`, `items` (bulleted
list), `body` (wrapped paragraph), `accent`, `href`/`target`, `surface`/`textColor`, `width`,
`rank`, `order`, `group`.

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
| `note` | string (narrates the hop in a derived flow) | — |
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

## The other diagram types: sequence, state, class, flowchart, mindmap, chart

All share `meta`, colours, and theming with architecture diagrams (charts are static — no `flow`).
Only the middle keys differ. Full tables: [YAML schema](/docs/reference/yaml); guides:
[sequence](/docs/guides/sequence) · [state](/docs/guides/state) · [class](/docs/guides/class) ·
[flowchart](/docs/guides/flowchart) · [mindmap](/docs/guides/mindmap) · [chart](/docs/guides/chart).

### `type: sequence` — participants + messages

Participants are columns (declared order); messages are rows (authored order). Participant fields =
node fields (`id`, `title`, `kind`, `icon`, `accent`, …). Message fields: `from`, `to` (required;
equal ids = self-message loop), `label`, `reply` (bool — dashed return, closes the receiver's
activation bar), `kind` (same four edge kinds; `async` = dashed + open arrow), `style`, `color`,
`note` (narrates the message in the derived flow), `activate` (bool — force/suppress the receiver's
activation bar). Message `color` defaults to the
accent of the participant doing the work (call receiver / reply sender), so request/reply pairs
share a hue. A list entry `- section: <label>` (optional `accent`) opens a tinted band around every
message until the next section. Activation bars pair automatically: request in, `reply: true` back
out. With no `flow:`, one packet rides each message in order — the message order IS the story, and
the diagram dims then reveals each row as its packet fires.

```beck
type: sequence
participants:
  - { id: web, title: Web App, kind: user }
  - { id: api, title: API }
  - { id: db, title: Postgres, kind: db }
messages:
  - { from: web, to: api, label: POST /orders }
  - { from: api, to: api, label: validate }
  - { from: api, to: db, label: INSERT }
  - { from: db, to: api, label: ok, reply: true }
  - { from: api, to: web, label: 201, reply: true }
```

### `type: state` — states + transitions

States are pills; `states:` is optional refinement (`id`, `title`, `subtitle`, `accent`, `width`,
`rank`, `order`) — ids used only in `transitions` are auto-created. `"[*]"` (quoted!) is the UML
entry/exit pseudo-state: `from: "[*]"` draws the entry dot, `to: "[*]"` the exit bullseye.
Transition fields: `from`, `to`, `label`, `style`, `color`, `note` (narrates the transition in the
derived flow). Same-pair opposite transitions (submit/reject) route side by side automatically;
self-transitions draw a loop. `direction: LR` usually reads best.

```beck
type: state
meta: { direction: LR }
states:
  - { id: review, title: In Review, accent: warn }
transitions:
  - { from: "[*]", to: draft }
  - { from: draft, to: review, label: submit }
  - { from: review, to: draft, label: reject }
  - { from: review, to: published, label: approve }
  - { from: published, to: "[*]" }
```

### `type: class` — classes + relations (+ groups)

Class fields: `id` (required), `name` (= `id`; alias `title`), `stereotype` (rendered
`«stereotype»`), `fields` / `methods` (lists of plain strings — quote ones containing `:`),
`accent`, `href`, `group`, `width`, `rank`, `order`. Relation fields: `from`, `to`, `kind`,
`label`, `fromCard`/`toCard` (multiplicities), `color`. Relation kinds and their authored
directions — say it aloud and write it that way:

- `inherits` child→parent (hollow triangle at parent) · `implements` class→interface (dashed)
- `aggregation` / `composition` whole→part (hollow / filled diamond at the whole)
- `association` source→target (plain arrow) · `dependency` source→target (dashed open arrow)

Parents automatically rank above children. `groups` = namespace boxes.

```beck
type: class
classes:
  - { id: entity, name: Entity, stereotype: abstract, accent: neutral, fields: ["Id: Guid"] }
  - { id: order, name: Order, fields: ["Total: Money"], methods: ["Submit()"] }
  - { id: line, name: OrderLine, fields: ["Sku: string"] }
relations:
  - { from: order, to: entity, kind: inherits }
  - { from: order, to: line, kind: composition, fromCard: "1", toCard: "*" }
```

### `type: flowchart` — steps + links

A decision/process graph on the layered engine. `steps:` is optional refinement (`id`, `text`,
`kind`, `subtitle`, `accent`, `icon`, `href`/`target`, `surface`/`textColor`, `width`, `rank`,
`order`) — ids used only in `links` are auto-created as `process` cards. `kind` is one of `process`
(card, default), `decision` (diamond), `terminator` (pill), `io` (parallelogram), `start`/`end` (the
start/end pseudo-shape). `"[*]"` (quoted!) works as a `from`/`to` shorthand for the start/end
pseudo-step, exactly like state diagrams. Link fields: `from`, `to`, `label`, `style`, `color`,
`note` (narrates the link in the derived flow). With no `flow:`, one packet rides each link in
declared order.

```beck
type: flowchart
steps:
  - { id: check, text: Valid?, kind: decision }
links:
  - { from: "[*]", to: check }
  - { from: check, to: charge, label: "yes" }
  - { from: check, to: retry, label: "no" }
  - { from: retry, to: check }
  - { from: charge, to: "[*]" }
```

### `type: mindmap` — root + topics

A nested topic tree drawn as a two-sided butterfly (root centred, branches fanning left/right).
`root` is required — a plain string (its title) or a mapping (`title`, `id`, `subtitle`, `items`,
`body`, `accent`, `icon`, `href`/`target`, `surface`/`textColor`, `width`). `topics:` are the
first-level branches; each can nest further via its own `children:`, to any depth, with the same
fields as `root`. A heading-only leaf renders as a pill; anything with `items`/`body` or `children`
renders as a card. First-level branches cycle through `primary`, `info`, `success`, `warn`,
`danger`, `neutral`; deeper topics inherit their parent's resolved accent unless they set their own.
`meta.direction` is accepted but ignored — the layout is always the fixed butterfly. With no
`flow:`, packets broadcast from the root out to the leaves.

```beck
type: mindmap
root: Beck
topics:
  - title: Rendering
    children:
      - title: Pipeline
        items: [Model, Text, Layout]
      - title: Determinism
        body: Same YAML, same SVG.
  - title: Packages
    accent: success
    children: [Beck, Beck.Skia]
```

### `type: chart` — series

A small, static data chart: `chart:` is `bar`, `line`, `pie`, `donut`, or `scatter`. `series:` is
required — one entry per bar/slice (`value:`), line (`values: [...]`), or scatter cluster
(`points: [[x, y], ...]`); any series may set its own `color:`. Series colours are derived from
`--beck-primary` by `palette:` (`analogous` default, `monochromatic`, `complementary`, `sequential`)
— pure token expressions, so they re-tint with the host palette and flip light/dark. `legend:` is
`right` (default), `top`, `bottom`, or `none`; `legendValues: true` prints values in a right-hand
legend; `center` / `centerLabel` set a donut's centre. No `flow` — charts don't animate.

```beck
type: chart
chart: donut
palette: analogous
legend: right
legendValues: true
center: 134M
centerLabel: total
series:
  - { label: Gateway, value: 42 }
  - { label: Catalog, value: 33 }
  - { label: Checkout, value: 28 }
```

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
`stream`, `working`, `idle`, `fail`, `narrate`, `phase`, `wait`, `reset`, `parallel`.

- `packet` `{ from, to, via?, color?, label? }` — one dot travels an edge (or a multi-hop chain via `via`).
- `burst` `{ from, to|[to…], count, stagger, … }` — `count` waves broadcast to every target.
- `status` `{ node, text, color? }`, `working` `{ node, color? }`, `stream`/`activate` `{ from, to, color? }` — **persist** until a later step or `reset`.
- `highlight` / `pulse` `{ node, color? }`, `fail` `{ node, text?, color? }` — one-shot beats.
- `narrate: <text>` (or `{ text, hold?, color? }`) — set the caption line under the diagram and hold long enough to read it; hold auto-scales with length. A connector `note:` becomes a `narrate` in a *derived* flow.
- `idle` `{ node }` clears `working`; `reset` restores the initial state.
- `phase: <label>` marks a seek point; `wait: <seconds>` pauses; `parallel: [ …steps ]` runs steps together.

`packet`/`burst` share look-and-motion **knobs** (each defaults to the traversed edge kind): `shape`
(`dot` `circle` `ring`), `size`, `speed`, `glow`, `impact`, `ease`, `via`, `color`, `label`. Eases:
`linear` `smooth` `accelerate` `decelerate` `expo` `sine` `steps` `bounce`. Full semantics: [Flow &
animation reference](/docs/reference/flow) and the [Animate the flow guide](/docs/guides/flow).

## A complete worked example

```beck
type: architecture
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
        .Narrate("The client places an order.")
        .Packet("client", "api", label: "POST /orders")
        .Working("db")
        .Packet("api", "db", color: "info")
        .Idle("db"))
    .ToFence();   // ```beck … ``` ready for Markdown; use .ToYaml() for the raw YAML
```

Narration has a builder on every type: `.Narrate(enabled, wpm, min, pad)` on the diagram (meta
toggle + pacing), `.Narrate(text, hold?, color?)` as a flow step, and `.Note(text)` on an edge,
message, or transition to caption a derived flow.

Each diagram type has its own builder: `DiagramBuilder` (architecture),
`SequenceDiagramBuilder` (`Participant`/`Message`/`Reply`/`Section`), `StateDiagramBuilder`
(`State`/`Transition`/`Initial`/`Final`), `ClassDiagramBuilder`
(`Class`/`Inherits`/`Composition`/…, plus **`ClassDiagramBuilder.FromTypes(typeof(…), …)`**, which
reflects real CLR types into an always-current class diagram — base types become `inherits`,
interfaces `implements`, property types labelled associations), `FlowchartDiagramBuilder`
(`Step`/`Process`/`Decision`/`Terminator`/`Io`/`Start`/`End`/`Link`), `MindMapDiagramBuilder`
(`Root`/`Topic`, with `Topic` nestable to any depth for `children`), and `ChartDiagramBuilder`
(`Palette`/`Legend`/`Center`/`Series`, one `Series` per bar/line/point-cluster).

C# enums map to schema tokens (lowercased), with one special case: `EdgeCurve.StepRound` →
`step-round`. `Direction` stays uppercase. Enums available: `Direction`, `NodeKind`, `NodeVariant`,
`EdgeStyle`, `EdgeCurve`, `EdgeKind`, `RelationKind`, `PacketEase`, `PacketShape`, `ThemeMode`,
`FitMode`, `AccentToken`, `Side`, `ArrowEnds`, `ChartKind`, `ChartPalette`, `LegendPlacement`. The
builders throw if an edge/message/relation references an undeclared id. Full surface: [API reference](/api) · tutorial: [Author a diagram in
C#](/docs/tutorials/csharp) · keeping diagrams in sync with your model: [Generate diagrams from
your code](/docs/guides/generate).

## Embedding a diagram in a page

Beck renders diagrams with the pure-C# engine — there is no client JS, no npm package, and no CDN.
Two rendering paths:

- **Fenced block (main path):** write a ` ```beck ` block (inline YAML) or a ` ```beck:symbol ` block
  (pointing at a `.beck.yaml` file) in any Markdown page. The `Pennington.Beck` package's
  preprocessor runs the engine at build time and inlines a static, self-animating `<svg>` — no
  script tag, no client runtime. Comma flags tune one fence: `,static` forces the still frame,
  `,scrub` drives playback from scroll, and `,style=<name>` overrides the document's `meta.style`
  (they combine, e.g. ` ```beck:symbol,style=sketch,static `).
- **C# / ASP.NET:** `BeckSvg.Render(yaml)` from the `Beck` package returns a self-contained
  `<svg>` string you write into a page (server-side or at build time).

Setup and palette wiring: [Add Beck to your site](/docs/guides/install) (generic ASP.NET/Tailwind)
or [Add Beck to a Pennington site](/docs/guides/pennington).

## Rules and gotchas

- **Always declare the root `type:`** (`architecture` | `sequence` | `state` | `class`). A typeless
  document still renders as architecture but is deprecated and logs a console warning.
- **Only ids are required.** Lean on defaults; override deliberately.
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
- **Animation is automatic** — no `flow:` means a derived one, *except for `type: class`*, which is
  structural reference material and renders a still frame unless you script a `flow:` yourself.
  `meta.loop: false` plays once; `meta.animate: false` (or the reader's reduced-motion setting)
  renders a static frame.
- **`type: mindmap` ignores `meta.direction`** — the butterfly layout is always fixed left/right.
- **Flowchart `"[*]"`** works as a `links` `from`/`to` shorthand for the start/end pseudo-step,
  exactly like state diagrams' entry/exit pseudo-state — quote it, since `[*]` is YAML syntax.
- **Flow steps are ordered**, single-key mappings; `parallel` runs its children simultaneously;
  `status`/`working`/`stream`/`activate` persist until cleared or `reset`.
- **It's plain YAML** — the parser reports friendly errors (with a line number for syntax issues),
  so prefer fixing the YAML over guessing.
