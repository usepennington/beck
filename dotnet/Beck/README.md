# Beck

Declarative, animated diagrams from YAML — architecture, sequence, state, and class — rendered
**entirely server-side in C#** for .NET docs and apps.

`BeckSvg.Render(yaml)` turns a small YAML document into a single self-contained, self-animating
inline `<svg>`. Layout, edge routing, theming, and the full flow choreography (packets, trails,
highlights, narration captions) are baked into CSS animations inside the SVG — **no client
JavaScript, no runtime, nothing to hydrate**. Diagrams respect `prefers-reduced-motion` and adopt
the host page's palette and dark mode through CSS variables.

This package ships two things:

1. **The rendering engine** (`Beck.Rendering` namespace) — `BeckSvg.Render(yaml, options)`.
2. **The authoring API** (`Beck` namespace) — a dependency-free fluent builder family for emitting
   Beck YAML from code, so any program can turn its own model into a diagram.

## Render a diagram

```csharp
using Beck.Rendering;

string svg = BeckSvg.Render("""
    type: architecture
    meta: { title: Request Path }
    nodes:
      - { id: client, title: Browser, kind: user }
      - { id: api, title: API Server }
      - { id: db, title: Postgres, kind: db }
    edges:
      - { from: client, to: api }
      - { from: api, to: db, label: query }
    """);
```

Write the string into any page you produce — a Razor view (`@((MarkupString)svg)`), a minimal-API
endpoint, or a static-site build step. The root `type:` picks the diagram kind — `architecture`,
`sequence` (participants + messages), `state` (states + transitions), or `class` (classes +
relations) — all sharing the same theming, animation, and options.

`SvgRenderOptions` controls the render: `Animation` (`Full`, `Static`, or `Scrub` — scroll-driven),
`Theme` (pin light/dark or emit both hooks), `Measurer`/`Font` (see below), `Style`/`Styles` (see
below), and `EmbedFonts`.

## Styles

A **style** is a complete visual identity — shapes, strokes, typography, colour bias, and motion —
chosen with a single token. Beck ships eleven (`classic` the default, plus `minimal`, `terminal`,
`blueprint`, `glow`, `editorial`, `brutalist`, `sketch`, `extrude`, `circuit`, `metro`). A style only
redefines the *defaults* of the `--beck-*` tokens, so a themed diagram still adopts the host palette
and flips with light and dark. Select one per document with `meta: { style: metro }`, or set a
site-wide default from C#:

```csharp
string svg = BeckSvg.Render(yaml, new SvgRenderOptions { Style = BeckStyles.ByName["glow"] });
```

Precedence runs `meta.style` → `SvgRenderOptions.Style` → `classic`. Every built-in is an instance of
the public `BeckStyle` record; derive your own with a `with` expression and register it in
`SvgRenderOptions.Styles` so a document's `meta.style` can name it.

## Text measurement

Beck sizes every card to its text. Out of the box it measures with an embedded Inter + IBM Plex
Mono metrics table — zero configuration, no native dependencies. For pixel-exact sizing in your
site's own fonts, add the **`Beck.Skia`** package and pass a `SkiaTextMeasurer` pointed at the same
font files your CSS serves:

```csharp
using Beck.Rendering;
using Beck.Rendering.Text;
using Beck.Skia;

var font = new BeckFontSpec
{
    Family = "IBM Plex Sans",
    Files = new Dictionary<int, string> { [400] = "fonts/IBMPlexSans-Regular.ttf" /* … */ },
};
using var measurer = new SkiaTextMeasurer(font);
string svg = BeckSvg.Render(yaml, new SvgRenderOptions { Measurer = measurer, Font = font });
```

## Authoring from code

```csharp
using Beck;

string yaml = new DiagramBuilder("Web Platform")
    .Direction(Direction.TB)
    .Node("web", n => n.Title("Web App").Kind(NodeKind.User))
    .Node("gw", n => n.Title("API Gateway").Kind(NodeKind.Gateway))
    .Node("auth", "Auth Service")
    .Node("authdb", n => n.Title("Auth DB").Kind(NodeKind.Db))
    .Group("services", g => g.Label("Services").Members("auth").Accent(AccentToken.Primary))
    .Edge("web", "gw")
    .Edge("gw", "auth")
    .Edge("auth", "authdb", e => e.Label("reads"))
    .ToYaml();

string svg = BeckSvg.Render(yaml);
```

Use `.ToYaml()` for the raw YAML, or `.ToFence()` for a Markdown ` ```beck ` code block. Because
the emitter is free of any framework or diagram dependency, the natural pattern is to walk *any*
source — an Aspire app graph, an EF model, a service registry — into a `DiagramBuilder` and render
it.

Each diagram type has its own builder — `SequenceDiagramBuilder`, `StateDiagramBuilder`, and
`ClassDiagramBuilder` — and the class builder can reflect an always-current domain model straight
from your types:

```csharp
string yaml = ClassDiagramBuilder
    .FromTypes(typeof(Order), typeof(OrderLine), typeof(Customer))
    .Title("Order Model")
    .ToYaml();
```

## Docs and schema

Full guides, the YAML schema reference, and a live playground:
https://usepennington.github.io/beck/
