---
title: Draw a state diagram
description: States, transitions, the [*] entry and exit pseudo-states, and a machine that animates its own lifecycle.
order: 27
sectionLabel: Other diagram types
uid: docs.guide.state
---

A `type: state` document draws a state machine — states as pills, transitions as labelled arrows,
with the UML entry dot and exit bullseye. The layered engine lays it out (so `direction: LR` reads
like a pipeline and `TB` like a lifecycle), and the derived animation walks a packet through the
transitions on loop.

## The tersest machine

You don't have to declare states at all — any id a transition mentions is created as a pill.
`"[*]"` is the entry/exit pseudo-state (quote it — the brackets are YAML syntax otherwise): a
transition *from* `"[*]"` draws the entry dot, one *to* `"[*]"` draws the exit bullseye:

```yaml:symbol
wwwroot/examples/guides/state-01.beck.yaml
```

<beck-diagram src="/examples/guides/state-01.beck.yaml" mode="auto"></beck-diagram>

## Refining states

Add a `states:` list to give a state a proper title, an accent, or a subtitle — anything not listed
keeps its auto-created pill. Back-transitions (reject, retract) route side by side with their
forward counterparts automatically:

```yaml:symbol
wwwroot/examples/guides/state-02.beck.yaml
```

<beck-diagram src="/examples/guides/state-02.beck.yaml" mode="auto"></beck-diagram>

## Self-transitions

A transition from a state to itself draws a compact loop — retries, heartbeats, re-entrant states:

```yaml:symbol
wwwroot/examples/guides/state-03.beck.yaml
```

<beck-diagram src="/examples/guides/state-03.beck.yaml" mode="auto"></beck-diagram>

## Scripting the animation

The derived flow traces each transition in machine order. For a guided story — highlight the happy
path, `fail` a state, park a `status` pill — add a `flow:` block; state ids work anywhere node ids
do. See [Animate the flow](/docs/guides/flow).

To caption the walk without scripting a flow, add a `note:` to a transition — it becomes a
narration line under the diagram as that transition fires. See [Narrate the
story](/docs/guides/flow#narrate-the-story).

---

Full field tables: [states and transitions in the YAML
schema](/docs/reference/yaml#states-and-transitions-type-state). Generating one from C#:
[`StateDiagramBuilder`](/docs/guides/generate).
