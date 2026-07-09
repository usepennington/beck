# Beck

Declarative, animated diagrams from YAML — "Mermaid, but sexy." Rendered **entirely in C#**.

Write a diagram as structured YAML and Beck auto-lays it out, auto-routes the edges, themes it
from your site's CSS variables, and animates packets flowing through it. Four diagram types share
one document format and one engine — **architecture** (layered boxes and lines), **sequence**
(lifelines and messages that play the conversation), **state** (a machine that walks its own
transitions), and **class** (UML cards, generatable from real C# types).

The output is a single self-contained, self-animating inline `<svg>`: layout, routing, and the full
flow choreography are baked into CSS animations inside the SVG. **No client JavaScript, no GSAP, no
runtime, nothing to hydrate** — diagrams play even with scripts disabled, respect
`prefers-reduced-motion`, and adopt the host page's palette and dark mode through CSS variables.

```csharp
using Beck.Rendering;

string svg = BeckSvg.Render("""
    type: architecture
    meta: { title: Web Platform, direction: LR }
    nodes:
      - { id: web, title: Web App, kind: user }
      - { id: gw, title: API Gateway, kind: gateway }
      - { id: db, title: Postgres, kind: db }
    edges:
      - { from: web, to: gw }
      - { from: gw, to: db, label: reads }
    """);
```

## A diagram in YAML

Every document opens with a root `type:` — `architecture`, `sequence`, `state`, or `class`.
(An untyped document still renders as an architecture diagram, with a deprecation warning.)

```yaml
type: architecture
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

The other types swap the middle keys for their own vocabulary:

```yaml
type: sequence                 # participants: + messages: (reply/section/activation bars)
type: state                    # states: + transitions: ("[*]" = entry/exit pseudo-state)
type: class                    # classes: + relations: (inherits/implements/composition/…)
```

### Schema reference

- **`meta`** — `title`, `subtitle`, `direction` (TB/BT/LR/RL), `theme` (auto/light/dark),
  `animate` (bool), `loop` (bool), `spacing: { rank, node, cornerRadius }`, `narrate` (bool to
  toggle the caption bar, or `{ wpm, min, pad }` to tune reading-time pacing — see `narrate` step).
- **`nodes[]`** — `id` (required), `title`, `subtitle`, `icon` (named key or raw `<svg>`),
  `kind` (`service` `db` `queue` `cache` `gateway` `external` `user` `ghost`), `variant`
  (`solid` `subtle` `ghost`), `status`, `accent` (token `primary|success|warn|danger|info|neutral`
  or a raw color), `href`/`target` (renders the card as a link), `surface`/`textColor` (per-node
  color overrides), `width`, `rank`, `order`, `group`.
- **`groups[]`** — `id` (required), `label`, `members: [nodeId | groupId…]` (a member that is a
  group id nests it — groups compose to arbitrary depth and may span ranks), `accent`.
- **`edges[]`** — `from`, `to` (node or group id), `label`, `style` (`solid`/`dashed`),
  `curve` (`step-round`/`straight`/`s`), `kind` (`data`/`control`/`async`/`dependency`),
  `color`, `arrow` (`true`/`false`, or `end`/`start`/`both`/`none`), `note` (prose that narrates
  the hop in an auto-derived flow — see `narrate`), `fromSide`/`toSide`.
- **`flow`** — `repeat`, `repeatDelay`, `steps[]`, where each step is one of: `packet`
  (`{from, to, via?, color?, label?}` plus look/motion knobs `shape` (`dot`/`circle`/`ring`),
  `size`, `speed`, `glow`, `impact` (burst-on-arrival), `ease`), `burst` (`{from, to|[to…], count,
  stagger}` + the same knobs), `status` (`{node, text, color?}`), `highlight`/`pulse`
  (`{node, color?}`), `activate`/`stream` (`{from, to, color?}` — persistently recolor an edge /
  flow continuous dashes along it), `working` (`{node, color?}` — leave a node breathing until
  `idle`/`reset`), `idle` (`{node}`), `fail` (`{node, text?, color?}` — red shake + flash),
  `narrate` (`"text"`, or `{text, hold?, color?}` — set the caption line under the diagram and hold
  long enough to read it; the hold scales with the text length, tuned by `meta.narrate`),
  `phase` (label, for seeking), `wait` (seconds), `parallel` (`[steps…]`), `reset`. Set
  `meta.loop: false` to play the flow once instead of looping.
- **`type: sequence`** — `participants[]` (same fields as nodes) and `messages[]` (`from`, `to`,
  `label`, `reply`, `kind`, `style`, `color`, `note` (narrates the message while the choreography
  plays), `activate`; `- section: <label>` inserts a band).
  Request/reply pairs grow activation bars; the derived flow plays the messages in order.
- **`type: state`** — optional `states[]` (`id`, `title`, `subtitle`, `accent`, …) and
  `transitions[]` (`from`, `to`, `label`, `style`, `color`, `note`); `"[*]"` is the entry/exit
  pseudo-state and undeclared ids auto-create pill states.
- **`type: class`** — `classes[]` (`id`, `name`, `stereotype`, `fields[]`, `methods[]`, `accent`,
  …) and `relations[]` (`from`, `to`, `kind` = `inherits` `implements` `association` `aggregation`
  `composition` `dependency`, `label`, `fromCard`/`toCard`), plus `groups[]` as namespaces. Being
  structural reference material, a class diagram renders a still frame — it only animates if you
  script a `flow:`.

## Usage

Beck ships as two NuGet packages:

```bash
dotnet add package Beck        # the engine + the authoring API
dotnet add package Beck.Skia   # optional: exact text measurement with your own font files
```

**Render YAML to SVG.** `BeckSvg.Render(yaml)` returns the `<svg>` string; write it into any HTML
you produce — a Razor view, a minimal-API endpoint, a static-site build step:

```csharp
using Beck.Rendering;

string svg = BeckSvg.Render(yaml, new SvgRenderOptions
{
    Animation = AnimationMode.Full,   // Full (default) | Static | Scrub (scroll-driven)
});
```

**Fenced ` ```beck ` blocks (the docs-site path).** In a Pennington site, a code-block preprocessor
renders every Markdown ` ```beck ` fence to inline SVG at build time — the Mermaid-style authoring
experience with zero client cost. See the docs site's `BeckSvgPreprocessor` for the pattern.

**Author YAML from code.** The `Beck` package includes a dependency-free authoring API — one
builder per diagram type (`DiagramBuilder`, `SequenceDiagramBuilder`, `StateDiagramBuilder`,
`ClassDiagramBuilder`), including `ClassDiagramBuilder.FromTypes(...)`, which reflects real CLR
types into an always-current class diagram:

```csharp
using Beck;
using Beck.Rendering;

string yaml = new DiagramBuilder("Web Platform")
    .Node("web", n => n.Title("Web App").Kind(NodeKind.User))
    .Node("api", "API Server")
    .Edge("web", "api")
    .ToYaml();

string svg = BeckSvg.Render(yaml);
```

**Measure with your real fonts.** The built-in measurer carries embedded Inter + IBM Plex Mono
metrics, so it works with zero configuration. For pixel-exact card sizing in your own font, add
`Beck.Skia` and point a `SkiaTextMeasurer` at the same font files your CSS serves:

```csharp
using Beck.Rendering;
using Beck.Skia;
using Beck.Rendering.Text;

var font = new BeckFontSpec
{
    Family = "IBM Plex Sans",
    Files = new Dictionary<int, string> { [400] = "fonts/IBMPlexSans-Regular.ttf", /* … */ },
};
using var measurer = new SkiaTextMeasurer(font);
string svg = BeckSvg.Render(yaml, new SvgRenderOptions { Measurer = measurer, Font = font });
```

## Theming

Every themeable value is a `--beck-*` CSS custom property that **defaults to the host site's
palette** (`--color-primary-600`, `--color-base-*`, …) with a literal fallback. The SVG is inline,
so it participates in the page's cascade and matches it automatically, including light/dark —
which is just a matter of `[data-theme="dark"]` (or `prefers-color-scheme`) redefining the
variables. There is no per-theme rendering and no hardcoded colors.

## Styles

A **style** is a complete visual identity — shapes, strokes, typography, colour bias, and motion
character — chosen with a single token. Beck ships eleven. A style only ever redefines the *defaults*
of the `--beck-*` tokens, so a themed diagram still adopts your host palette and still flips with
light and dark; the theme axis above is orthogonal and works on top of every style.

| name | look |
|---|---|
| `classic` | the default card look — soft shadows, rounded corners, Inter + IBM Plex Mono |
| `minimal` | flat: no shadows, hairline borders, a single travelling dot; glow and rings off |
| `terminal` | monospace, `[bracketed]` titles, square packets, green-ramp accent, hard-step trails |
| `blueprint` | drafting surface — faint grid, dashed edges, dimension ticks on groups, mono uppercase labels |
| `glow` | gradient edges, soft packet bloom, breathing pulse on active nodes |
| `editorial` | serif textbook figure — hairlines, no fills, `Fig. N —` captions, slow draw-on reveal |
| `brutalist` | thick strokes, a solid blur-free offset shadow, uppercase Archivo, `steps()` flow motion |
| `sketch` | hand-drawn — Shantell Sans, deterministically wobbled outlines |
| `extrude` | 2.5D slabs with static depth faces; highlight presses the node down toward its face |
| `circuit` | chip nodes with pin stubs and a via dot at every route bend |
| `metro` | thick transit lines, white station dots at edge ends, train-capsule packets |

Set one per document in YAML, or site-wide from C#:

```yaml
meta: { style: metro }
```

```csharp
using Beck;
using Beck.Rendering;

// Site-wide default (a document's own meta.style overrides it):
string svg = BeckSvg.Render(yaml, new SvgRenderOptions { Style = BeckStyles.ByName["glow"] });
```

Precedence runs `meta.style` (YAML) → `SvgRenderOptions.Style` (C# default) → `classic` — the C#
option is a site-wide default a document opts back out of, deliberately the opposite of `Theme`.
Every built-in is an instance of the public `BeckStyle` record; derive your own from the closest one
with a `with` expression and register it in `SvgRenderOptions.Styles`. See the [style
guide](https://usepennington.github.io/beck/docs/guides/styles) and the [custom-style
how-to](https://usepennington.github.io/beck/docs/guides/custom-styles).

## How it works

```
YAML → parse (YamlDotNet) → validate(+defaults, per diagram type) → model
     → measure (ITextMeasurer: embedded Inter metrics, or Skia+HarfBuzz shaping)
     → layout  (per type: Sugiyama-lite layered engine, or the fixed sequence grid)
     → route   (orthogonal step-round edges with obstacle avoidance / sequence rows)
     → svg     (node shapes, group boxes, markers, labels, theming <style>)
     → animate (simulate the flow into an absolute schedule → compile to CSS keyframes)
```

State and class diagrams compile onto the same layered engine (pills, pseudo-states, and
compartment cards are node shapes; UML markers are edge decorations). Sequence diagrams get their
own fixed-grid layout and router, but every message is still one continuous SVG path, so the
shared packet animation rides it unchanged.

The animation compiler simulates the flow script into an absolute-time schedule, then emits one
shared-duration CSS animation per element with percentage-window keyframes — overshoot and elastic
eases are sampled analytically into CSS `linear()` timing functions. `prefers-reduced-motion` (or
`animate: false`) gets the fully-revealed static frame: all motion CSS lives inside a
`@media (prefers-reduced-motion: no-preference)` block.

## Repository layout

- `dotnet/Beck` — the engine + authoring API (`BeckSvg.Render`, `DiagramBuilder` family). Only
  dependency: YamlDotNet.
- `dotnet/Beck.Skia` — optional exact text measurement (SkiaSharp + HarfBuzzSharp shaping).
- `dotnet/Beck.Tests` — xunit suite: model/layout/route golden parity tests, card-sizing gates,
  render smoke tests.
- `dotnet/Beck.Sample` — console sample emitting authored YAML.
- `docs/Beck.Docs` — the Pennington docs site; every content diagram renders at build time, and
  the interactive playground runs the same engine compiled to WebAssembly (`docs/Beck.Docs.Client`).

## Acknowledgements

Beck grew out of [`abergs/animations`](https://github.com/abergs/animations) by
[Anders Åberg](https://andersaberg.com/) — an experiment in animated architecture diagrams with GSAP
and Tailwind CSS. That project was the inspiration and the jumping-off point for this codebase, and
Beck would not exist without it. Thank you, Anders. Beck's first engine was TypeScript + GSAP; the
current engine is a pure-C# port of it, and the CSS choreography is the compiled form of those
original GSAP timelines.
