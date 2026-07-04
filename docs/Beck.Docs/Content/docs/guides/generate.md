---
title: Generate diagrams from your code
description: Build diagrams with the C# DiagramBuilder so they regenerate from your real model.
order: 31
sectionLabel: How-to guides
uid: docs.guide.generate
---

This guide shows you how to generate Beck diagrams from your C# code so they stay in sync with the real system instead of drifting away from it.

## Why generate

A hand-drawn diagram is a snapshot. The moment you add a service, rename a queue, or move a database, the picture lies — and nobody notices until it misleads someone. A diagram **walked from your real model** can't drift: you build it from the same source of truth the system runs on — an Aspire app graph, an EF model, a service registry — and regenerate it whenever that source changes.

`Beck.Authoring` is the C# half of the package. It is dependency-free, lives in the `Beck` namespace, and emits Beck YAML from a fluent `DiagramBuilder`. Walk your model into the builder once, wire the regeneration into your build, and the diagram updates itself.

> [!NOTE]
> This is the how-to for **wiring generation into a build**. If you're learning the builder for the first time, start with the [C# tutorial](/docs/tutorials/csharp).

## Build it with DiagramBuilder

To produce a diagram from code, chain `DiagramBuilder`: set the direction, add nodes and edges, group what belongs together. Drive the calls from a loop over your real model rather than typing each node by hand.

```csharp
using Beck;

var builder = new DiagramBuilder("Web Platform")
    .Direction(Direction.TB)
    .Node("web", n => n.Title("Web App").Kind(NodeKind.User))
    .Node("gw", n => n.Title("API Gateway").Kind(NodeKind.Gateway))
    .Node("orders", "Orders Service")
    .Node("db", n => n.Title("Postgres").Kind(NodeKind.Db))
    .Group("services", g => g.Label("Services").Members("gw", "orders").Accent(AccentToken.Primary))
    .Edge("web", "gw")
    .Edge("gw", "orders")
    .Edge("orders", "db", e => e.Label("query"));
```

`Node`, `Group`, and `Edge` all take an id plus an optional configuration callback, so `foreach (var service in registry) builder.Node(service.Id, ...)` is the whole trick. The full builder surface — every `NodeBuilder`, `GroupBuilder`, `EdgeBuilder`, and `FlowBuilder` method — is in the [API reference](/api).

> [!TIP]
> `ToYaml()` throws if an edge references an id you never declared, so a malformed walk fails loudly in C# rather than blanking the diagram in the browser.

## Emit it

The builder has two terminal methods. Pick by where the diagram is going.

To write a standalone file your site can fetch, call `.ToYaml()` and save it:

```csharp
File.WriteAllText("wwwroot/diagrams/generated.beck.yaml", builder.ToYaml());
```

To get a ready-to-paste Markdown block, call `.ToFence()` — it returns the YAML already wrapped in a ` ```beck ` fence:

```csharp
File.WriteAllText("docs/architecture.md", builder.ToFence());
```

## Render the result

A `.beck.yaml` file renders by pointing a `<beck-diagram>` at it. The engine fetches the file and hydrates it in light DOM:

```html
<beck-diagram src="/diagrams/generated.beck.yaml"></beck-diagram>
```

A fence from `.ToFence()` renders by dropping it straight into any Markdown page — the engine script hydrates ` ```beck ` blocks automatically, no Markdig extension required. Either way you need the engine included once in `<head>`; see [Add Beck to your site](/docs/guides/install) if you haven't already.

Here is the kind of diagram the snippet above produces:

```yaml:symbol
wwwroot/examples/guides/generate-01.beck.yaml
```

<beck-diagram src="/examples/guides/generate-01.beck.yaml" mode="auto" animate="false"></beck-diagram>

## The other diagram types

Every diagram type has a builder. `SequenceDiagramBuilder` scripts an interaction
(`Participant` / `Message` / `Reply` / `Section`), `StateDiagramBuilder` a machine
(`State` / `Transition` / `Initial` / `Final`), and `ClassDiagramBuilder` a UML model:

```csharp
string fence = new SequenceDiagramBuilder("Checkout")
    .Participant("web", "Web App", NodeKind.User)
    .Participant("api", "Orders API")
    .Message("web", "api", "POST /orders")
    .Reply("api", "web", "201 Created")
    .ToFence();
```

The class builder has the strongest generation story of all: **`ClassDiagramBuilder.FromTypes`**
reflects real CLR types into cards and infers the relations among them — base types become
`inherits`, interfaces `implements`, property types labelled associations (collections get a `*`
multiplicity), enums «enum» cards. A domain-model diagram that literally cannot drift:

```csharp
string fence = ClassDiagramBuilder
    .FromTypes(typeof(Entity), typeof(Order), typeof(OrderLine), typeof(Customer), typeof(OrderStatus))
    .Title("Order Model")
    .ToFence();
```

Only the types you pass are drawn — references to anything outside the set are ignored, so the
diagram stays scoped on purpose. See [Draw a class diagram](/docs/guides/class) for the output.

## Regenerate in CI

To keep the committed diagram honest, regenerate it on every build and fail when it drifts. Put the generation in a small console target (or a test) that writes the file, then guard with `git diff --exit-code` — a non-empty diff means someone changed the model without committing the new diagram.

```bash
# Walk the model and rewrite the diagram
dotnet run --project tools/GenerateDiagram -c Release

# Fail the build if the committed file is now stale
git diff --exit-code wwwroot/diagrams/generated.beck.yaml
```

If you'd rather assert inside the test suite, write the file in a test and run `dotnet test`, then keep the same `git diff --exit-code` guard as the final CI step. The contributor's fix is the same in both cases: rerun the generator and commit the regenerated YAML.

> [!TIP]
> `Beck.Sample` is a runnable end-to-end example of generating diagrams from code. Run `dotnet run --project dotnet/Beck.Sample -c Release` to print one to stdout — pass `sequence`, `state`, `class`, or `reflection` to see the other builders in use.

## Next steps

- The [API reference](/api) — the complete `Beck.Authoring` builder API.
- [Author a diagram in C#](/docs/tutorials/csharp) — the hands-on introduction to the builder.
- [Flow & animation reference](/docs/reference/flow) — the vocabulary for scripting `.Flow(...)` once your generated diagram should move.

