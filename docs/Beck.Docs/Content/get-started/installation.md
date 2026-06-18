---
title: Installation
description: Add the Beck package and render your first diagram in a Pennington or Markdig site.
order: 2
sectionLabel: Get started
uid: start.installation
---

Beck ships as a single NuGet package — a Razor Class Library that embeds the prebuilt engine
as a static web asset **and** contains `Beck.Authoring`, a C# API for emitting Beck YAML from
code. There's no npm package and no CDN to wire up.

> [!NOTE]
> Beck targets .NET 8 and later and works in any ASP.NET Core host. The fenced-block
> integration works with **Pennington** and **any Markdig-based site** — anywhere your Markdown
> is rendered to HTML in the browser. Check your SDK with `dotnet --version`.

## 1. Add the package

```bash
dotnet add package Beck
```

## 2. Include the engine once

Add the engine script to your site's `<head>`. Once it loads, it hydrates every fenced
` ```beck ` block on the page into a live diagram — the Mermaid-style integration, and the only
thing a Markdown-driven site needs:

```html
<script src="/_content/Beck/beck.global.js" defer></script>
```

That path is a static web asset the package serves; there's nothing to copy into your project.
Rather than hand-write the tag, inject `Beck.BeckAssets.ScriptTag` — it emits exactly the line
above, with a **root-relative** path that resolves from any route depth and survives a sub-path
deploy (GitHub Pages, a `/docs` prefix) where a bare relative path would break.

**Pennington.** On a `DocSite` host, add it to your head content:

```csharp
options.AdditionalHtmlHeadContent = Beck.BeckAssets.ScriptTag;
```

On a bare Pennington host with your own `App.razor` (like this site), drop the tag straight into
the `<head>`.

**Any other Markdig-based site.** Markdig renders a ` ```beck ` fence as
`<code class="language-beck">`, which is exactly what the engine looks for. Put the same script
tag in your layout's `<head>` and every fence on every page becomes a diagram — no Markdig
extension, no server-side rendering step.

## 3. Write a diagram

Put a fenced ` ```beck ` block in any Markdown page:

````markdown
```beck
meta:
  title: Hello
  direction: LR
nodes:
  - { id: web, title: Web App, kind: user }
  - { id: api, title: API }
  - { id: db, title: Database, kind: db }
edges:
  - { from: web, to: api }
  - { from: api, to: db, label: queries }
```
````

The engine lays it out, routes the edges, and animates the flow:

```beck
meta:
  title: Hello
  direction: LR
nodes:
  - { id: web, title: Web App, kind: user }
  - { id: api, title: API }
  - { id: db, title: Database, kind: db }
edges:
  - { from: web, to: api }
  - { from: api, to: db, label: queries }
```

That's it — no build step, no canvas wrangling. Next, learn the language in
[Nodes & edges](/docs/), or generate diagrams from C# in
[Authoring from C#](/docs/authoring-from-csharp).

> [!NOTE]
> Need a diagram outside a Markdown fence — straight from a `.yaml` file, or driven by your own
> JavaScript? There's a `<beck-diagram>` element and a small imperative API for those cases. See
> [Embedding diagrams](/docs/exporting).
