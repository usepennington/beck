---
title: Generating diagrams in CI
description: Author diagrams from C# so they regenerate from your real model.
order: 5
sectionLabel: Get started
uid: start.ci
---

Because `Beck.Authoring` is a plain C# API, you can generate diagram YAML from your *actual*
model — an Aspire app graph, an EF model, a service registry — as part of a build or test, so
the picture can never drift from the code.

```csharp
using Beck;

string yaml = new DiagramBuilder("Web Platform")
    .Direction(Direction.TB)
    .Node("web", n => n.Title("Web App").Kind(NodeKind.User))
    .Node("api", "API Server")
    .Edge("web", "api")
    .ToYaml();

File.WriteAllText("docs/system.beck.yaml", yaml);
```

Commit the generated `.beck.yaml` (or a ` ```beck ` fence via `.ToFence()`) and your docs site
renders it live. Wire the generator into a test or a build target and a stale diagram becomes
a failing check.

See [Authoring from C#](/docs/authoring-from-csharp) for the full builder — including groups
and scripted animation flows — with the code pulled straight from the compiled sample.
