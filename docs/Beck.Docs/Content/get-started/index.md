---
title: Introduction
description: What Beck is and why you'd draw diagrams from YAML.
order: 1
sectionLabel: Get started
uid: start.intro
---

Beck turns a declarative **YAML** description of your system into a clean, animated
architecture diagram — *Mermaid, but sexy*. You write the nodes and edges; Beck handles
layout, edge routing, grouping, theming, and a packet animation that traces how data flows.

Diagrams are **text**, so they live in your repo, diff in pull requests, and adopt your
site's colours and dark mode automatically. Here's one — rendered live, right now, by the
same engine this whole site ships:

```beck
meta:
  title: Web Platform
  direction: TB
nodes:
  - { id: web, title: Web App, kind: user }
  - { id: gw, title: API Gateway, kind: gateway }
  - { id: auth, title: Auth Service }
  - { id: orders, title: Orders Service }
  - { id: authdb, title: Auth DB, kind: db }
  - { id: events, title: Events, kind: queue, subtitle: Message bus }
groups:
  - { id: services, label: Services, members: [auth, orders], accent: primary }
edges:
  - { from: web, to: gw }
  - { from: gw, to: auth }
  - { from: gw, to: orders }
  - { from: auth, to: authdb }
  - { from: orders, to: events, kind: async }
```

That diagram is a fenced ` ```beck ` block in this page's Markdown. Nothing was exported or
pre-rendered — the browser parsed the YAML and drew it.

## Where to go next

- **[Installation](/get-started/installation)** — add the package and render your first diagram.
- **[Nodes & edges](/docs/)** — the core of the language.
- **[Playground](/playground)** — edit YAML and watch it render live.
