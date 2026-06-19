---
title: Group related nodes
description: Draw labelled boundaries, assign membership, and nest groups to any depth.
order: 24
sectionLabel: How-to guides
uid: docs.guide.groups
---

This guide shows you how to draw labelled boundaries around related nodes — assigning membership, nesting groups to any depth, and routing edges to a whole box.

## Add a group

To wrap a set of nodes in a labelled boundary, add an entry to the top-level `groups` list. Give it an `id`, a `label`, the `members` you want inside (node ids), and an `accent` to tint the box:

```beck
meta: { direction: TB, animate: false }
nodes:
  - { id: gw, title: API Gateway, kind: gateway }
  - { id: auth, title: Auth }
  - { id: orders, title: Orders }
  - { id: db, title: Postgres, kind: db }
groups:
  - { id: services, label: Services, members: [auth, orders], accent: primary }
edges:
  - { from: gw, to: auth }
  - { from: gw, to: orders }
  - { from: orders, to: db }
```

The group box sizes itself around its members and spans whatever ranks they land on. `accent` takes the same tokens as a node — `primary`, `info`, `neutral` and so on — and defaults to `neutral`.

## Assign membership inline

If you would rather declare membership on the node instead of listing every id in the group, set `group: <id>` on the node. This is equivalent to adding that node to the group's `members`, and it is far tidier when you generate diagrams from code and emit nodes one at a time:

```beck
meta: { direction: TB, animate: false }
nodes:
  - { id: gw, title: API Gateway, kind: gateway }
  - { id: auth, title: Auth, group: services }
  - { id: orders, title: Orders, group: services }
  - { id: db, title: Postgres, kind: db }
groups:
  - { id: services, label: Services, accent: primary }
edges:
  - { from: gw, to: auth }
  - { from: gw, to: orders }
  - { from: orders, to: db }
```

Mix the two styles freely — list some members on the group and pin the rest with `node.group`.

## Nest groups

A member may itself be a **group** id, so groups compose to any depth. A nested group can span ranks, letting an outer boundary enclose several inner ones. Give every group a clear id — never reuse a node id such as `web` for a group, or the two collide:

```beck
meta: { direction: TB, animate: false }
nodes:
  - { id: web, title: Web, kind: user }
  - { id: api, title: API, kind: gateway, group: webtier }
  - { id: cache, title: Redis, kind: cache, group: datasubnet }
  - { id: db, title: Postgres, kind: db, group: datasubnet }
groups:
  - { id: webtier, label: Web Tier, accent: primary }
  - { id: datasubnet, label: Data Subnet, accent: info }
  - { id: vpc, label: VPC, members: [webtier, datasubnet] }
edges:
  - { from: web, to: api }
  - { from: api, to: cache }
  - { from: api, to: db }
```

Here `vpc` lists two group ids as its members, so `webtier` and `datasubnet` render as boxes inside the VPC boundary.

## Connect to a boundary

An edge's `from` or `to` may target a group id instead of a node id, drawing the connection to the whole box rather than to one card inside it:

```beck
meta: { direction: TB, animate: false }
nodes:
  - { id: client, title: Client, kind: user }
  - { id: auth, title: Auth, group: services }
  - { id: orders, title: Orders, group: services }
groups:
  - { id: services, label: Services, accent: primary }
edges:
  - { from: client, to: services, label: requests }
```

> [!NOTE]
> Membership is a tree: each node or group has at most one parent. A node cannot belong to two groups, a group cannot contain itself, and there are no cycles.

For the full list of group fields, see the [YAML schema reference](/docs/reference/yaml).
