---
title: Your first diagram
description: Build a three-tier system from scratch, one block at a time.
order: 3
sectionLabel: Get started
uid: start.first-diagram
---

Let's build a small system from nothing. Every Beck diagram has up to four top-level keys:
`meta`, `nodes`, `groups`, and `edges`. Only node `id`s are required — everything else has a
sensible default.

## Start with nodes

A node needs an `id`; `title` defaults to the id. Give a node a `kind` and Beck picks an icon
and accent for you.

```yaml
nodes:
  - { id: web, title: Web App, kind: user }
  - { id: api, title: API }
  - { id: db, title: Database, kind: db }
```

## Connect them with edges

An edge is a `from`/`to` pair. Add a `label` to name the relationship.

```yaml
edges:
  - { from: web, to: api }
  - { from: api, to: db, label: queries }
```

Put it together and Beck draws it — with a packet animation derived automatically from the
edges:

```beck
meta:
  title: Three-tier app
  direction: TB
nodes:
  - { id: web, title: Web App, kind: user }
  - { id: api, title: API }
  - { id: db, title: Database, kind: db }
edges:
  - { from: web, to: api }
  - { from: api, to: db, label: queries }
```

## Try it yourself

Open this in the **[Playground](/playground)** and tweak it live — add a cache, change the
`direction` to `LR`, or wrap the services in a group. The diagram re-renders as you type.
