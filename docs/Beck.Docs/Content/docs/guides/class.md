---
title: Draw a class diagram
description: UML class cards with stereotypes, fields, and methods; inheritance, composition, and dependencies — or generate the whole thing from your C# types.
order: 28
sectionLabel: Other diagram types
uid: docs.guide.class
---

A `type: class` document draws UML class cards — a «stereotype» and name header over field and
method compartments — joined by relations with the classic end markers: hollow triangles for
inheritance, diamonds for composition, dashed open arrows for dependencies. And because Beck ships
with a C# authoring API, you can [generate the entire diagram from your real
types](#generate-it-from-your-c-types) and never let it drift.

## Classes

Each class needs an `id`; `name`, `stereotype`, `fields`, and `methods` fill the card. Compartment
lines are plain strings, so write them the way your team reads them:

```yaml:symbol
wwwroot/examples/guides/class-01.beck.yaml
```

<beck-diagram src="/examples/guides/class-01.beck.yaml" mode="auto"></beck-diagram>

That relation is a `composition` — filled diamond at the whole (`from`), with `fromCard`/`toCard`
multiplicities at the ends.

## Relations

Six kinds, each with its UML rendering. Directions follow the way you'd say it aloud: *Order
inherits Entity*, *Order depends on IOrderNotifier*, *Order is composed of OrderLines*:

```yaml:symbol
wwwroot/examples/guides/class-02.beck.yaml
```

<beck-diagram src="/examples/guides/class-02.beck.yaml" mode="auto"></beck-diagram>

| kind | write it as | draws |
|---|---|---|
| `inherits` | child → parent | solid line, hollow triangle at the parent |
| `implements` | class → interface | dashed line, hollow triangle at the interface |
| `association` | source → target | solid line, arrowhead at the target |
| `aggregation` | whole → part | hollow diamond at the whole |
| `composition` | whole → part | filled diamond at the whole |
| `dependency` | source → target | dashed line, open arrowhead |

Parents always rank above children — `inherits` and `implements` are flipped internally so the
hierarchy reads top-down without you thinking about layout. `groups` work here too, as namespace
boxes.

## Generate it from your C# types

Hand-written class diagrams rot. The `ClassDiagramBuilder` in `Beck.Authoring` reflects real CLR
types into cards and infers the relations among them — base types become `inherits`, interfaces
become `implements`, property types become labelled associations (collections get a `*`
multiplicity), and enums become «enum» cards:

```csharp
string fence = ClassDiagramBuilder
    .FromTypes(typeof(Entity), typeof(Order), typeof(OrderLine), typeof(Customer), typeof(OrderStatus))
    .Title("Order Model")
    .ToFence();   // ```beck … ``` — drop it into any Markdown page
```

Run that in your docs build (or a source generator, or a unit test that snapshots it) and the
diagram is always exactly what the code says. Types *not* passed in are ignored, so the diagram
stays scoped to what you choose to show. See [Generate diagrams from your
code](/docs/guides/generate) for the pattern.

## Animation

Class diagrams are structural, so the derived animation is deliberately quiet: each inheritance
level lights up in turn, top to bottom. Script your own `flow:` (highlight one aggregate, pulse the
interfaces) if you want a guided tour — class ids work anywhere node ids do.

---

Full field tables: [classes and relations in the YAML
schema](/docs/reference/yaml#classes-and-relations-type-class).
