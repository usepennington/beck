---
title: Add Beck to your site
description: Install the package, include the engine once, and render your first fenced diagram.
order: 20
sectionLabel: How-to guides
uid: docs.guide.install
---

This guide shows you how to get a Beck diagram rendering on a page in your own .NET site — from installing the package to writing your first fenced block.

## Add the package

Beck ships as a single NuGet package. Add it to the project that builds your site:

```bash
dotnet add package Beck
```

The package targets net8.0+ and contains both the prebuilt browser engine (a static web asset) and `Beck.Authoring`, the C# fluent API for emitting Beck YAML from code. There is no npm package and no CDN to wire up.

## Include the engine once

The engine is one script. Add it to your `<head>` exactly once and it hydrates every Beck diagram on the page:

```html
<script src="/_content/Beck/beck.global.js" defer></script>
```

`BeckAssets.ScriptPath` is root-relative (`/_content/Beck/beck.global.js`), so the tag survives sub-path and GitHub Pages deploys without rewriting. Rather than hand-write the tag, you can inject `Beck.BeckAssets.ScriptTag`, which yields exactly this markup.

Pick the recipe that matches your host:

**Pennington DocSite** — set the head content in your options:

```csharp
options.AdditionalHtmlHeadContent = Beck.BeckAssets.ScriptTag;
```

**Bare Pennington host** — drop the tag in `<head>` yourself, and call `app.MapStaticAssets()` so the RCL's static asset is served:

```csharp
app.MapStaticAssets();
```

**Any Markdig-based site** — there is nothing else to do. Markdig renders a ` ```beck ` fence as `<code class="language-beck">`, and the script hydrates it on load. No Markdig extension, no server step.

## Write a diagram

With the engine included, write a ` ```beck ` fenced block in any Markdown page. Here is the literal source you type:

````markdown
```beck
meta: { direction: LR }
nodes:
  - { id: web, title: Web App, kind: user }
  - { id: api, title: API, kind: gateway }
  - { id: db, title: Postgres, kind: db }
edges:
  - { from: web, to: api }
  - { from: api, to: db, label: queries }
```
````

And here is the same block rendered live — themed and animated — by the engine:

```beck
meta: { direction: LR }
nodes:
  - { id: web, title: Web App, kind: user }
  - { id: api, title: API, kind: gateway }
  - { id: db, title: Postgres, kind: db }
edges:
  - { from: web, to: api }
  - { from: api, to: db, label: queries }
```

## Beyond the fence

If you want to keep the YAML in its own file, point the custom element at it. It fetches the source and renders in light DOM so your host CSS reaches it:

```html
<beck-diagram src="/diagrams/x.beck.yaml"></beck-diagram>
```

To drive a diagram from your own JavaScript, call `renderDiagram`. It returns a handle you can control:

```html
<script>
  const handle = window.Beck.renderDiagram(host, yaml, { theme: "dark" });
  handle.pause();
</script>
```

The handle exposes `play()`, `pause()`, `reset()`, `seek()`, `setTheme()`, `relayout()`, and `destroy()`. See the [API reference](/api) for the full handle.

> [!NOTE]
> Motion is automatic — Beck derives a flow from your edges and plays it. It respects `prefers-reduced-motion` and `meta.animate: false`, rendering a static frame and never loading the motion runtime when either applies.

Next, work through [Your first diagram](/docs/tutorials/first-diagram) to learn the language, and keep [the YAML schema](/docs/reference/yaml) handy for every field.
