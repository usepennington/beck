# Beck

Declarative, animated architecture diagrams from YAML — "Mermaid, but sexy."

Write a diagram as structured YAML and Beck auto-lays it out, auto-routes the edges, themes it
from your site's CSS variables, and animates packets flowing through it. The engine ships as a
prebuilt bundle with **no build step for authors** and loads GSAP from a CDN at runtime, so nothing
animation-related is bundled.

```bash
npm install
npm run dev          # open the playground (samples, live editor, light/dark toggle)
npm run build:lib    # build dist-lib/beck.global.js (IIFE) — set BECK_FORMAT=esm for the ESM build
```

## A diagram in YAML

```yaml
meta:
  title: Web Platform
  direction: TB        # TB (default) | BT | LR | RL
nodes:
  - { id: web, title: Web App, kind: user }
  - { id: gw, title: API Gateway, kind: gateway }
  - { id: auth, title: Auth Service }
  - { id: authdb, title: Auth DB, kind: db }
groups:
  - { id: services, label: Services, members: [auth], accent: primary }
edges:
  - { from: web, to: gw }
  - { from: gw, to: auth }
  - { from: auth, to: authdb, label: reads }
```

Only node `id`s are required. With no `flow:` block, a packet animation is auto-derived from the
edges. Validation errors are reported with friendly messages (and a line number for YAML syntax).

### Schema reference

- **`meta`** — `title`, `subtitle`, `direction` (TB/BT/LR/RL), `theme` (auto/light/dark),
  `animate` (bool), `loop` (bool), `spacing: { rank, node, cornerRadius }`.
- **`nodes[]`** — `id` (required), `title`, `subtitle`, `icon` (named key or raw `<svg>`),
  `kind` (`service` `db` `queue` `cache` `gateway` `external` `user` `ghost`), `variant`
  (`solid` `subtle` `ghost`), `status`, `accent` (token `primary|success|warn|danger|info|neutral`
  or a raw color), `width`, `rank`, `order`, `group`.
- **`groups[]`** — `id` (required), `label`, `members: [nodeId…]`, `accent`.
- **`edges[]`** — `from`, `to` (node or group id), `label`, `style` (`solid`/`dashed`),
  `curve` (`step-round`/`straight`/`s`), `kind` (`data`/`control`/`async`/`dependency`),
  `color`, `arrow`, `fromSide`/`toSide`.
- **`flow`** — `repeat`, `repeatDelay`, `steps[]`, where each step is one of: `packet`
  (`{from, to, via?, color?, label?}`), `status` (`{node, text, color?}`), `highlight`/`pulse`
  (`{node, color?}`), `activate`/`stream` (`{from, to, color?}` — persistently recolor an edge /
  flow continuous dashes along it), `working` (`{node, color?}` — leave a node breathing until
  `idle`/`reset`), `idle` (`{node}`), `fail` (`{node, text?, color?}` — red shake + flash),
  `phase` (label, for seeking), `wait` (seconds), `parallel` (`[steps…]`), `reset`. Set
  `meta.loop: false` to play the flow once instead of looping.

## Usage

**Custom element** (Shadow-DOM isolated; reads inline YAML, a child `<script type="application/yaml">`,
or a `src` URL):

```html
<script type="module" src="/path/to/beck.global.js"></script>
<beck-diagram mode="auto">
  <script type="application/yaml">
    meta: { title: Hello }
    nodes: [{ id: a, title: A }, { id: b, title: B }]
    edges: [{ from: a, to: b }]
  </script>
</beck-diagram>
```

**Imperatively:**

```ts
import { renderDiagram } from 'beck'
const handle = renderDiagram(document.querySelector('#chart'), yamlString, { theme: 'auto' })
// handle: play(), pause(), reset(), seek(label), setTheme(mode), relayout(), destroy(), ready
```

## Theming

Every themeable value is a `--beck-*` CSS custom property that **defaults to the host site's
palette** (`--color-primary-600`, `--color-base-*`, …) with a literal fallback. Custom properties
pierce the Shadow DOM boundary, so a diagram automatically matches the surrounding page, including
light/dark — which is just a matter of `[data-theme="dark"]` redefining the variables. There is no
per-theme JavaScript and no hardcoded colors in the renderer.

## How it works

```
YAML → parse → validate(+defaults) → model
     → measure (render cards off-flow, read sizes)
     → layout (Sugiyama-lite: rank → order → coords; groups as constraints)
     → route  (auto orthogonal step-round edges with obstacle avoidance)
     → render (position DOM, group boxes, SVG overlay)
     → animate (compile flow → GSAP timeline; play on scroll-into-view)
```

`prefers-reduced-motion` (or `animate: false`) renders the static frame and never loads GSAP.

## Distribution — the `Beck` NuGet package

Beck ships as a single NuGet package (`dotnet/Beck`, a Razor Class Library) — there is no npm
package and no CDN to configure. The package contains both halves:

- **The client**, as a static web asset served at `_content/Beck/beck.global.js`. It auto-registers
  `<beck-diagram>` and **hydrates fenced `` ```beck `` code blocks** (the Mermaid-style integration),
  so in a Pennington (or any ASP.NET Core) site you just include the script once and write fences.
- **`Beck.Authoring`**, a dependency-free C# API (`DiagramBuilder`) for emitting Beck YAML from code,
  so any program can turn its own model into a diagram.

The TypeScript engine is built with `npm run build:lib`, which writes the committed
`dotnet/Beck/wwwroot/beck.global.js`. That committed asset is what the package ships, so
`dotnet pack` never needs Node — regenerate and commit it when the engine source changes.

```csharp
using Beck;
string fence = new DiagramBuilder("Web Platform")
    .Node("web", n => n.Title("Web App").Kind(NodeKind.User))
    .Node("api", "API Server")
    .Edge("web", "api")
    .ToFence();   // ```beck … ``` — drop into Markdown; the client renders it
```

See `dotnet/Beck/README.md` for the package usage.

## Status

The engine + playground, the fenced-block hydrator, and the `Beck` NuGet package (client static web
asset + `Beck.Authoring`) are complete and verified end-to-end (C# → YAML → fenced block → rendered,
host-themed diagram).
