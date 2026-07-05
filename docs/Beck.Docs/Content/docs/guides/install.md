---
title: Add Beck to your site
description: Install the package, include the engine, and wire your palette so diagrams match your site.
order: 20
sectionLabel: Setup
uid: docs.guide.install
---

This guide shows you how to render Beck diagrams in your own ASP.NET Core site and make them adopt
your colours and dark mode. It assumes a host with a Tailwind-style CSS palette (colours exposed as
`--color-*` custom properties). On a Pennington site, see [Add Beck to a Pennington
site](/docs/guides/pennington) instead — the wiring is a little different.

## Add the package

Beck ships as a single NuGet package — the prebuilt engine (a static web asset) plus `Beck.Authoring`
(the C# builder). Add it to the project that serves your site:

```bash
dotnet add package Beck
```

There is no npm package and no CDN to wire up. The package targets net8.0 and later.

## Include the engine

The engine is one script. Add it to your layout's `<head>`, once:

```html
<script src="/_content/Beck/beck.global.js" defer></script>
```

That path is a static web asset the package serves. Make sure your host serves RCL static web assets
— most do; if yours doesn't, add `app.MapStaticAssets()` (or `app.UseStaticFiles()`) in
`Program.cs`. The path is root-relative, so it resolves from any route depth and survives a sub-path
deploy (a `/docs` prefix, GitHub Pages). Rather than hand-write the tag, you can inject
`Beck.BeckAssets.ScriptTag`, which emits exactly the line above.

## Write a diagram

In any HTML the browser renders — a Razor view, a Markdown page processed by Markdig, a plain
`.html` file — write a fenced ` ```beck ` block. A Markdown engine renders it as
`<code class="language-beck">`, and the script replaces it with a live diagram. Here is the source
you write:

````markdown
```beck
type: architecture
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

And here it is rendered live:

```beck
type: architecture
meta: { direction: LR }
nodes:
  - { id: web, title: Web App, kind: user }
  - { id: api, title: API, kind: gateway }
  - { id: db, title: Postgres, kind: db }
edges:
  - { from: web, to: api }
  - { from: api, to: db, label: queries }
```

That's all the engine needs — no Markdig extension, no server-side render step.

## Make diagrams match your site

Out of the box a diagram renders in Beck's built-in palette. To make it *yours*, you give it your
colours — and the mechanism is plain CSS variables, so it works with any Tailwind-style host.

Every colour Beck paints is a `--beck-*` custom property whose default reads a `--color-*` variable
from your page:

```text
--beck-primary  →  var(--color-primary-600)
--beck-success  →  var(--color-emerald-500)
--beck-surface  →  var(--color-base-50)     (and darker base shades in dark mode)
--beck-text     →  var(--color-base-800)
…
```

So Beck adopts your palette the moment those `--color-*` variables exist on the page. Tailwind
already publishes its built-in colours as `--color-*` (so `--color-emerald-500`, `--color-amber-500`,
`--color-red-500`, and `--color-violet-500` — the `success`, `warn`, `danger`, and `info` accents —
resolve for free). You only need to supply the two ramps Beck names that Tailwind doesn't: your brand
`primary` and a neutral `base`.

```css
:root {
  /* Brand accent — Beck reads --color-primary-600 */
  --color-primary-600: #4f46e5;

  /* Neutral ramp — Beck reads --color-base-50 … --color-base-950 */
  --color-base-50:  #f8fafc;
  --color-base-100: #f1f5f9;
  --color-base-200: #e2e8f0;
  --color-base-300: #cbd5e1;
  --color-base-400: #94a3b8;
  --color-base-500: #64748b;
  --color-base-700: #334155;
  --color-base-800: #1e293b;
  --color-base-900: #0f172a;
  --color-base-950: #020617;
}
```

> [!TIP]
> If you define your Tailwind theme with `@theme`, declare the same variables there instead — they
> land on `:root` just the same, and a `primary` colour in your theme already publishes
> `--color-primary-600`.

### Dark mode comes for free

Beck reads dark mode from a `.dark` class (or `data-theme="dark"`) on your `<html>` element — the
exact convention Tailwind uses. When that class is present it switches to the darker shades of your
`base` ramp and re-themes every diagram on the page automatically. So if your dark toggle already
flips `.dark` on `<html>`, you have nothing more to do.

For finer control — pinning one diagram's theme, or overriding a single token — see [Match your
theme and colours](/docs/guides/theme).

## Beyond the fence

To keep YAML in its own file, point the custom element at it; it fetches the file and renders in
light DOM, so your CSS reaches it:

```html
<beck-diagram src="/diagrams/architecture.beck.yaml"></beck-diagram>
```

To drive a diagram from your own JavaScript, render it by hand and keep the handle:

```html
<script>
  const handle = window.Beck.renderDiagram(host, yaml, { theme: "auto" });
  handle.setTheme("dark");
</script>
```

The handle exposes `play()`, `pause()`, `reset()`, `seek()`, `setTheme()`, `relayout()`, and
`destroy()` — see the [API reference](/api).

> [!NOTE]
> Motion is automatic and respects `prefers-reduced-motion` and `meta.animate: false`, rendering a
> static frame and never loading the motion runtime when either applies.

Next, learn the language in [Your first diagram](/docs/tutorials/first-diagram), and keep the [YAML
schema](/docs/reference/yaml) handy for every field.
