---
title: Embedding diagrams
description: The fenced block is the main path; an element and a JS API cover the rest.
order: 6
sectionLabel: Reference
uid: docs.exporting
---

Almost every diagram you publish should be a fenced ` ```beck ` block in Markdown. The other two
paths exist only for the cases a fence can't reach.

## The fenced ```beck block (use this)

In any Markdown rendered by a host that includes the engine script — **Pennington**, or **any
Markdig-based site** — a ` ```beck ` fence is hydrated into a live diagram. Markdig emits the
fence as `<code class="language-beck">`; the engine finds it, parses the YAML, and replaces it
with a themed, animated diagram. Every diagram in these docs is authored this way.

````markdown
```beck
nodes:
  - { id: web, title: Web App, kind: user }
  - { id: api, title: API }
edges:
  - { from: web, to: api }
```
````

It needs nothing server-side: no Markdig extension, no build step, no pre-rendering. Generating
the YAML from C#? `DiagramBuilder.ToFence()` emits exactly this block — see
[Authoring from C#](/docs/authoring-from-csharp).

## The `<beck-diagram>` element

When the YAML lives in its own file and you'd rather not inline it, the `<beck-diagram>` element
takes a `src` URL (or inline YAML, or a child `<script type="application/yaml">`). It renders in
light DOM, so it picks up the host page's styles and palette exactly like a fence does:

```html
<beck-diagram src="/diagrams/architecture.beck.yaml"></beck-diagram>
```

This is the one path that renders straight from a file — handy when the same `.beck.yaml` is both
shown as source and rendered, with no duplication. Reach for it sparingly; a fence is simpler.

## Imperatively from JavaScript

To drive a diagram from your own UI — a live editor, a playground — render it by hand and keep
the handle:

```js
const handle = window.Beck.renderDiagram(host, yaml, { theme: 'auto' });
handle.setTheme('dark');
handle.relayout();
```

The handle exposes `play`, `pause`, `reset`, `seek`, `setTheme`, `relayout`, and `destroy` — enough
to drive the [Playground](/playground) or sync a diagram to your own UI.

> [!TIP]
> Generating the YAML itself? Use [Beck.Authoring](/docs/authoring-from-csharp) to build it from
> your real model in C#, then drop the `.ToFence()` output into any Markdown page.
