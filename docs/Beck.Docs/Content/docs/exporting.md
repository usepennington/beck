---
title: Exporting & embedding
description: Ways to get a Beck diagram onto a page.
order: 6
sectionLabel: Reference
uid: docs.exporting
---

There are three ways to put a Beck diagram in front of a reader; pick whichever fits your
source of truth.

## 1. A fenced ```beck block

The Mermaid-style path. In any Markdown rendered by a host that includes the engine, a
` ```beck ` fence is hydrated into a live diagram. This is what every diagram in these docs
uses.

## 2. The `<beck-diagram>` element

A custom element that reads inline YAML, a child `<script type="application/yaml">`, or a
`src` URL. It renders in light DOM, so it adopts the host page's styles and palette:

```html
<beck-diagram src="/examples/microservices.beck.yaml" mode="auto"></beck-diagram>
```

This is handy when the YAML lives in its own file — the same file can be shown as source (via a
`:symbol` embed) and rendered, with no duplication.

## 3. Imperatively from JavaScript

```js
const handle = window.Beck.renderDiagram(host, yaml, { theme: 'auto' });
handle.setTheme('dark');
handle.relayout();
```

The returned handle exposes `play`, `pause`, `reset`, `seek`, `setTheme`, `relayout`, and
`destroy` — enough to drive a [playground](/playground) or sync a diagram to your own UI.

> [!TIP]
> Generating the YAML itself? Use [Beck.Authoring](/docs/authoring-from-csharp) to build it
> from your real model in C#, then feed the result to any of the three paths above.
