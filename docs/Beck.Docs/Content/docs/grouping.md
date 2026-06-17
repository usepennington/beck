---
title: Grouping
description: Cluster nodes into labelled boxes — and nest them to any depth.
order: 2
sectionLabel: Guide
uid: docs.grouping
---

A **group** is a labelled box drawn around a set of nodes. Add a `groups` list; each group
needs an `id` and a `members` list of node ids.

```yaml
groups:
  - { id: services, label: Services, members: [auth, orders], accent: primary }
```

```beck
meta: { title: Grouped services, direction: TB }
nodes:
  - { id: web, title: Web App, kind: user }
  - { id: gw, title: API Gateway, kind: gateway }
  - { id: auth, title: Auth }
  - { id: orders, title: Orders }
  - { id: db, title: Postgres, kind: db }
groups:
  - { id: services, label: Services, members: [auth, orders], accent: primary }
edges:
  - { from: web, to: gw }
  - { from: gw, to: auth }
  - { from: gw, to: orders }
  - { from: orders, to: db }
```

## Nesting

A member can be a **group id** instead of a node id — that nests the group. Groups compose to
arbitrary depth and may span ranks, because each group is laid out on its own and then fed to
its parent as a single sized super-node.

```beck
meta: { title: Nested boundaries, direction: TB, animate: false }
nodes:
  - { id: cf, title: CloudFront, kind: external, icon: cdn }
  - { id: alb, title: ALB, icon: loadbalancer }
  - { id: api, title: api, icon: container }
  - { id: pg, title: Postgres, kind: db }
  - { id: redis, title: Redis, icon: redis }
groups:
  - { id: vpc, label: VPC, members: [web, data], accent: info }
  - { id: web, label: Web Tier, members: [alb, api], accent: primary }
  - { id: data, label: Data Subnet, members: [pg, redis], accent: success }
edges:
  - { from: cf, to: alb }
  - { from: alb, to: api }
  - { from: api, to: pg, label: query }
  - { from: api, to: redis, label: cache }
```

Edges can target a group id as well as a node id, which is handy for drawing a connection to a
whole boundary rather than one box inside it.
