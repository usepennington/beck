---
title: Author a diagram in C#
description: Generate a diagram from code with the fluent DiagramBuilder, then render the YAML it emits.
order: 11
sectionLabel: Tutorials
uid: docs.tutorial.csharp
---

In this tutorial we'll build the same kind of diagram as [Your first
diagram](/docs/tutorials/first-diagram) — but from C# instead of hand-written YAML. We'll use
`Beck.Authoring`, the fluent builder in the Beck package, to generate the YAML and then render it.

This is the foundation for generating diagrams from a real model later. You'll need the .NET SDK
(8.0 or newer).

## Step 1 — Set up a project

Create a console app and add the Beck package:

```bash
dotnet new console -o BeckDemo
cd BeckDemo
dotnet add package Beck
```

`Beck.Authoring` is dependency-free and lives in the `Beck` namespace, so a plain console app is all
we need.

## Step 2 — Build a diagram and print it

Replace the contents of `Program.cs` with this. Each `.Node(...)` and `.Edge(...)` call mirrors a
line of YAML; `.ToYaml()` renders the whole thing to a string:

```csharp
using Beck;

string yaml = new DiagramBuilder("My first system")
    .Direction(Direction.LR)
    .Node("web", "Web App", NodeKind.User)
    .Node("api", "API")
    .Node("cache", n => n.Title("Redis").Kind(NodeKind.Cache))
    .Node("db", n => n.Title("Database").Kind(NodeKind.Db))
    .Group("data", g => g.Label("Data").Members("cache", "db").Accent(AccentToken.Info))
    .Edge("web", "api")
    .Edge("api", "cache")
    .Edge("api", "db", e => e.Label("queries"))
    .ToYaml();

Console.WriteLine(yaml);
```

Run it:

```bash
dotnet run
```

You should see Beck YAML printed to the console — the same shape you wrote by hand in the first
tutorial:

```yaml
meta:
  title: My first system
  direction: LR
nodes:
  - { id: web, title: Web App, kind: user }
  - { id: api, title: API }
  - { id: cache, title: Redis, kind: cache }
  - { id: db, title: Database, kind: db }
groups:
  - { id: data, label: Data, members: [cache, db], accent: info }
edges:
  - { from: web, to: api }
  - { from: api, to: cache }
  - { from: api, to: db, label: queries }
```

Notice the C# enums became schema tokens: `NodeKind.Cache` is `cache`, `AccentToken.Info` is `info`.

## Step 3 — Render what it emits

That YAML is a complete diagram. Paste it into a ` ```beck ` fence on any page with the engine
included and it renders live:

```beck
meta:
  title: My first system
  direction: LR
nodes:
  - { id: web, title: Web App, kind: user }
  - { id: api, title: API }
  - { id: cache, title: Redis, kind: cache }
  - { id: db, title: Database, kind: db }
groups:
  - { id: data, label: Data, members: [cache, db], accent: info }
edges:
  - { from: web, to: api }
  - { from: api, to: cache }
  - { from: api, to: db, label: queries }
```

To skip the copy-paste, swap `.ToYaml()` for `.ToFence()` — it returns the YAML already wrapped in a
` ```beck ` block, ready to write straight into a Markdown file.

## Step 4 — Script the motion

So far Beck derives the animation from the edges. Add a `.Flow(...)` to choreograph it yourself —
send a request to the API, mark the database busy while it works, then return a result:

```csharp
    .Flow(f => f
        .Packet("web", "api", label: "GET /")
        .Working("db")
        .Packet("api", "db", color: "info")
        .Idle("db")
        .Packet("db", "api", color: "success")
        .Wait(1))
```

Drop that in just before `.ToYaml()`. The diagram now tells a story:

```beck
meta:
  title: My first system
  direction: LR
nodes:
  - { id: web, title: Web App, kind: user }
  - { id: api, title: API }
  - { id: cache, title: Redis, kind: cache }
  - { id: db, title: Database, kind: db }
groups:
  - { id: data, label: Data, members: [cache, db], accent: info }
edges:
  - { from: web, to: api }
  - { from: api, to: cache }
  - { from: api, to: db, label: queries }
flow:
  steps:
    - packet: { from: web, to: api, label: GET / }
    - working: { node: db }
    - packet: { from: api, to: db, color: info }
    - idle: { node: db }
    - packet: { from: db, to: api, color: success }
    - wait: 1
```

## What you built

You generated a complete diagram from C#, rendered the YAML it emits, and scripted its animation —
all with the fluent builder. Because the builder is just code, you can drive it from anything you
already have.

From here:

- Walk your real model into a builder and keep diagrams in sync in CI with [Generate diagrams from
  your code](/docs/guides/generate).
- Reach for the full builder surface in the [API reference](/api).
- Script richer animations with [Animate the flow](/docs/guides/flow) and the [flow
  reference](/docs/reference/flow).
