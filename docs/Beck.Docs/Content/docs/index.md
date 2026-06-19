---
title: Introduction
description: Beck turns a few lines of YAML into a clean, animated architecture diagram — for .NET.
order: 1
sectionLabel: Start here
uid: docs.overview
---

Beck turns a declarative **YAML** description of your system into a clean, animated architecture
diagram. You write the boxes and the lines; Beck handles the layout, the routing, the theming, and
the motion — and renders it live in the browser, in your site's own colours.

It ships as a single .NET NuGet package: a prebuilt engine that hydrates a ` ```beck ` fenced code
block into a diagram, plus `Beck.Authoring`, a C# API for generating that YAML from your real model.

```beck
meta: { title: Web platform, direction: LR }
nodes:
  - { id: user, title: Client, kind: user }
  - { id: gw, title: API Gateway, kind: gateway }
  - { id: orders, title: Orders }
  - { id: db, title: Postgres, kind: db }
  - { id: bus, title: Events, kind: queue }
groups:
  - { id: svc, label: Services, members: [orders], accent: primary }
edges:
  - { from: user, to: gw }
  - { from: gw, to: orders }
  - { from: orders, to: db, label: query }
  - { from: orders, to: bus, label: publish, kind: async }
```

## The shape of a diagram

Every Beck document has up to four top-level keys. Only node `id`s are required — everything else
has a sensible default.

```yaml
meta:    # title, direction, theme, spacing  (all optional)
nodes:   # the boxes — each needs an id
groups:  # optional labelled boundaries around nodes
edges:   # the connections — each is a from/to pair
```

Add an optional `flow:` block to script the animation; leave it out and Beck derives a sensible one
from your edges.

## Where to go next

**Learn by doing.** Start with [Your first diagram](/docs/tutorials/first-diagram) to build one
block by block, then [Author a diagram in C#](/docs/tutorials/csharp) to generate one from code.

**Get something done.** The how-to guides are task-focused: add Beck to [your
site](/docs/guides/install) or a [Pennington site](/docs/guides/pennington), [style your
nodes](/docs/guides/nodes), [connect and route edges](/docs/guides/edges), [control the
layout](/docs/guides/layout), [group related nodes](/docs/guides/groups), [match your
theme](/docs/guides/theme), [animate the flow](/docs/guides/flow), or [generate diagrams from your
code](/docs/guides/generate).

**Look something up.** The [YAML schema](/docs/reference/yaml) and [flow &
animation](/docs/reference/flow) references list every field and option. For a visual tour of every
construct see the [syntax cheatsheet](/syntax); for the C# builder, the [API reference](/api). Or
just open the [playground](/playground) and start typing.
