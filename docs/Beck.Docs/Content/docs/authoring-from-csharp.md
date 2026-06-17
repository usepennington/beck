---
title: Authoring from C#
description: Generate diagrams from code with Beck.Authoring's fluent builder.
order: 5
sectionLabel: Reference
uid: docs.authoring-csharp
---

`Beck.Authoring` is a dependency-free, fluent C# API for emitting Beck YAML from code. Walk any
model you already have into a `DiagramBuilder` and call `.ToYaml()` or `.ToFence()`.

The C# below is pulled **straight from the compiled sample** in this very project via
Pennington's `:symbol` source embed — so the code on this page is exactly what builds, and can
never drift from reality. Underneath it is the diagram that code produces, **rendered live by
Beck**.

## A grouped microservices diagram

```csharp:symbol
Samples/AuthoringSamples.cs > AuthoringSamples.Microservices
```

<beck-diagram src="/examples/microservices.beck.yaml" mode="auto"></beck-diagram>

`.Node(id, configure)` takes a builder callback for the full set of options (`Kind`, `Icon`,
`Accent`, `Subtitle`, `Link`, …); `.Group(...)` clusters nodes; `.Edge(...)` connects them. The
enums map straight to schema tokens — `NodeKind.Db` → `db`, `EdgeKind.Async` → a dashed async
edge.

## Scripting an animation flow

Add a `.Flow(...)` to choreograph the packet animation — send packets, set status pills, run
steps in parallel, leave a node "working" until it goes idle:

```csharp:symbol
Samples/AuthoringSamples.cs > AuthoringSamples.ReadPath
```

```beck
meta: { title: Read Path, direction: LR }
nodes:
  - { id: client, title: Client, kind: user }
  - { id: api, title: API }
  - { id: cache, title: Redis, kind: cache }
  - { id: db, title: Postgres, kind: db }
edges:
  - { from: client, to: api }
  - { from: api, to: cache }
  - { from: api, to: db }
flow:
  repeat: -1
  repeatDelay: 1.5
  steps:
    - packet: { from: client, to: api, label: GET /item }
    - parallel: [ { packet: { from: api, to: cache, color: warn } }, { working: { node: db } } ]
    - status: { node: cache, text: miss, color: warn }
    - packet: { from: api, to: db, label: SELECT }
    - idle: { node: db }
    - packet: { from: db, to: api, color: success }
    - wait: 1
```

Without a `.Flow(...)`, Beck auto-derives a sensible flow from the edges — so you only script
one when you want a specific story.
