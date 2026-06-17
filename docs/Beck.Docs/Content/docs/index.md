---
title: Nodes & edges
description: The two ideas every Beck diagram is built from.
order: 1
sectionLabel: Guide
uid: docs.nodes-edges
---

Every Beck diagram is built from two ideas: **nodes** — the boxes in your system — and
**edges** — the connections between them. Everything else is decoration on top of those two.

## Defining nodes

`nodes` is a list. Each node needs an `id`; the `title` defaults to the id. Ids are how you
reference a node elsewhere (in edges and groups).

```yaml
nodes:
  - { id: api, title: API Gateway }
  - { id: db, title: Database }
```

## Node kinds

Set a `kind` and Beck picks a fitting icon and accent. The kinds are `service` (default),
`db`, `queue`, `cache`, `gateway`, `external`, `user`, and `ghost`.

```beck
meta: { direction: LR, animate: false }
nodes:
  - { id: user, title: User, kind: user }
  - { id: gw, title: Gateway, kind: gateway }
  - { id: svc, title: Service, kind: service }
  - { id: cache, title: Cache, kind: cache }
  - { id: db, title: Database, kind: db }
  - { id: q, title: Queue, kind: queue }
edges:
  - { from: user, to: gw }
  - { from: gw, to: svc }
  - { from: svc, to: cache }
  - { from: svc, to: db }
  - { from: svc, to: q }
```

## Connecting with edges

An edge is a `from`/`to` pair of ids. Beck routes the line and places the arrowhead for you.
Add a `label` to name the relationship, and a `kind` (`data`, `control`, `async`,
`dependency`) to give it semantic styling — `async` and `dependency` render dashed.

```beck
meta: { direction: TB }
nodes:
  - { id: api, title: API Gateway, kind: gateway }
  - { id: worker, title: Worker }
  - { id: bus, title: Events, kind: queue }
  - { id: db, title: Postgres, kind: db }
edges:
  - { from: api, to: db, label: reads }
  - { from: api, to: bus, label: publish, kind: async }
  - { from: bus, to: worker, kind: async }
```

## Accents and per-node colour

Give a node an `accent` — a token (`primary`, `success`, `warn`, `danger`, `info`, `neutral`)
or a raw colour — to make it stand out. Tokens follow the theme; raw colours don't.

```beck
meta: { direction: LR, animate: false }
nodes:
  - { id: a, title: Normal }
  - { id: b, title: Primary, accent: primary }
  - { id: c, title: Warning, accent: warn }
  - { id: d, title: Danger, accent: danger }
edges:
  - { from: a, to: b }
  - { from: b, to: c }
  - { from: c, to: d }
```

Next: bundle related nodes into [groups](/docs/grouping).
