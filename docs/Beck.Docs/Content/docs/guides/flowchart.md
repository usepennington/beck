---
title: Draw a flowchart
description: Process cards, decision diamonds, I/O parallelograms, and start/end terminators, joined by labelled links.
order: 29
sectionLabel: Other diagram types
uid: docs.guide.flowchart
---

A `type: flowchart` document draws a classic process/decision graph — steps as process cards,
decision diamonds, I/O parallelograms, and pill-shaped terminators, joined by labelled `links:`. It
runs on the same layered engine as architecture diagrams (so `direction: LR` reads like a pipeline
and `TB` like a procedure), and the derived animation walks a packet through the links in declared
order.

## The tersest flowchart

You don't have to declare steps at all — any id a link mentions is auto-created as a plain
`process` card. `"[*]"` is the start/end pseudo-step (quote it — the brackets are YAML syntax
otherwise): a link *from* `"[*]"` draws the start terminator, one *to* `"[*]"` draws the end
terminator, exactly like the `"[*]"` entry/exit pseudo-state in [state diagrams](/docs/guides/state):

```yaml:symbol
wwwroot/examples/guides/flowchart-01.beck.yaml
```

```beck:symbol
wwwroot/examples/guides/flowchart-01.beck.yaml
```

## Decisions, I/O, and a loop back

Add a `steps:` list to give a step a real `kind`. `decision` renders a diamond — pair it with two
links carrying `yes`/`no` labels for the branches. `io` renders a parallelogram for
read/write-shaped steps. A link back to an earlier step (here, `retry` routing back to `check`)
draws as a loop instead of crossing the forward flow:

```yaml:symbol
wwwroot/examples/guides/flowchart-02.beck.yaml
```

```beck:symbol
wwwroot/examples/guides/flowchart-02.beck.yaml
```

> [!NOTE]
> Every `kind` — `process`, `decision`, `terminator`, `io`, `start`, `end` — defaults to a neutral
> accent. Set `accent` per step (a [colour token](/docs/reference/yaml#colours-and-theme-tokens) or
> raw CSS colour) to make a branch or an outcome read as success, danger, or a brand hue.

## Labels, accents, and narration

`links:` fields work like architecture `edges:` — `label`, `style` (`solid`/`dashed`), `color`, and
a `note:` that narrates the hop in a [derived flow](/docs/reference/flow#derived-flow):

```yaml:symbol
wwwroot/examples/guides/flowchart-03.beck.yaml
```

```beck:symbol
wwwroot/examples/guides/flowchart-03.beck.yaml
```

## Scripting the animation

Without a `flow:`, the engine walks `links:` in declared order — one packet per link. For a guided
story — highlight a branch, `fail` a step, park a `status` pill — add a `flow:` block; step ids
work anywhere node ids do. See [Animate the flow](/docs/guides/flow).

## Generate it from your C#

```csharp
using Beck.Authoring;

string fence = new FlowchartDiagramBuilder("Checkout")
    .Direction(Direction.Tb)
    .Decision("valid", "Payment valid?")
    .Link(FlowchartDiagramBuilder.Pseudo, "valid")
    .Link("valid", "charge", "yes")
    .Link("valid", "retry", "no")
    .Link("charge", FlowchartDiagramBuilder.Pseudo)
    .Link("retry", "valid")
    .ToFence();   // ```beck … ``` — drop it into any Markdown page
```

---

Full field tables: [steps and links in the YAML
schema](/docs/reference/yaml#steps-and-links-type-flowchart). Generating one from C#:
[`FlowchartDiagramBuilder`](/docs/guides/generate).
