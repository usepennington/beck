# Beck

Declarative, animated architecture diagrams from YAML — for .NET docs and apps.

This single package ships two things:

1. **The Beck client** as a static web asset, served at `_content/Beck/beck.global.js`. It hydrates
   fenced ` ```beck ` code blocks into animated, themed diagrams (the Mermaid-style integration) and
   also registers a `<beck-diagram>` custom element. GSAP is loaded from a CDN at runtime, so nothing
   animation-related is in your build.
2. **`Beck.Authoring`** — a dependency-free C# API for emitting Beck YAML from code, so any program
   can turn its own model into a diagram.

## Client setup

Reference the package and add the script to your page head once:

```html
<script src="_content/Beck/beck.global.js" defer></script>
```

`Beck.BeckAssets.ScriptTag` returns exactly that snippet (handy for a Pennington
`DocSiteOptions.AdditionalHtmlHeadContent`). Then any ` ```beck ` fenced block in your Markdown
renders as a diagram, following your site's `--color-*` palette and dark mode automatically.

````markdown
```beck
meta: { title: Request Path }
nodes:
  - { id: client, title: Browser, kind: user }
  - { id: api, title: API Server }
  - { id: db, title: Postgres, kind: db }
edges:
  - { from: client, to: api }
  - { from: api, to: db, label: query }
```
````

## Authoring from code

```csharp
using Beck;

string fence = new DiagramBuilder("Web Platform")
    .Direction(Direction.TB)
    .Node("web", n => n.Title("Web App").Kind(NodeKind.User))
    .Node("gw", n => n.Title("API Gateway").Kind(NodeKind.Gateway))
    .Node("auth", "Auth Service")
    .Node("authdb", n => n.Title("Auth DB").Kind(NodeKind.Db))
    .Group("services", g => g.Label("Services").Members("auth").Accent(AccentToken.Primary))
    .Edge("web", "gw")
    .Edge("gw", "auth")
    .Edge("auth", "authdb", e => e.Label("reads"))
    .ToFence(); // ```beck … ``` ready to drop into Markdown
```

Use `.ToYaml()` for the raw YAML, or `.ToFence()` for a Markdown code block. Because the emitter is
free of any framework or diagram dependency, the natural pattern is to walk *any* source — an Aspire
app graph, an EF model, a service registry — into a `DiagramBuilder` and let the client render it.

See the project README for the full YAML schema.
