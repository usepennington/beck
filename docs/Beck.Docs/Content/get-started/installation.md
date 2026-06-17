---
title: Installation
description: Add the Beck package and render your first diagram in about three minutes.
order: 2
sectionLabel: Get started
uid: start.installation
---

Beck ships as a single NuGet package — a Razor Class Library that embeds the prebuilt engine
as a static web asset **and** contains `Beck.Authoring`, a C# API for emitting Beck YAML from
code. There's no npm package and no CDN to wire up.

> [!NOTE]
> Beck targets .NET 8 and later, and works in any ASP.NET Core host — including a
> Pennington docs site like this one. Check your SDK with `dotnet --version`.

## 1. Add the package

```bash
dotnet add package Beck
```

## 2. Include the engine once

Reference the static web asset in your host's `<head>`. It auto-registers the
`<beck-diagram>` element and hydrates every ` ```beck ` code block on the page.

```html
<script src="/_content/Beck/beck.global.js" defer></script>
```

In a Pennington site, that one line goes in your `App.razor` head — exactly how this site
does it.

## 3. Write a diagram

Drop a fenced ` ```beck ` block into any Markdown page, or use the `<beck-diagram>` element
directly:

```yaml
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

Beck lays it out, routes the edges, and animates the flow:

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
