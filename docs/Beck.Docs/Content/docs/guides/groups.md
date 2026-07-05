---
title: Group related nodes
description: Draw labelled boundaries, assign membership, and nest groups to any depth.
order: 25
sectionLabel: Architecture diagrams
uid: docs.guide.groups
---

This guide shows you how to draw labelled boundaries around related nodes — assigning membership, nesting groups to any depth, and routing edges to a whole box.

> [!NOTE]
> Groups apply to **architecture** and **[class](/docs/guides/class)** diagrams — in a class diagram the box reads as a namespace (see [Namespace boxes](#namespace-boxes-class-diagrams) below). Sequence and state diagrams don't use groups.

## Add a group

To wrap a set of nodes in a labelled boundary, add an entry to the top-level `groups` list. Give it an `id`, a `label`, the `members` you want inside (node ids), and an `accent` to tint the box:

```yaml:symbol
wwwroot/examples/guides/groups-01.beck.yaml
```

<beck-diagram src="/examples/guides/groups-01.beck.yaml" mode="auto"></beck-diagram>

The group box sizes itself around its members and spans whatever ranks they land on. `accent` takes the same tokens as a node — `primary`, `info`, `neutral` and so on — and defaults to `neutral`.

## Assign membership inline

If you would rather declare membership on the node instead of listing every id in the group, set `group: <id>` on the node. This is equivalent to adding that node to the group's `members`, and it is far tidier when you generate diagrams from code and emit nodes one at a time:

```yaml:symbol
wwwroot/examples/guides/groups-02.beck.yaml
```

<beck-diagram src="/examples/guides/groups-02.beck.yaml" mode="auto"></beck-diagram>

Mix the two styles freely — list some members on the group and pin the rest with `node.group`.

## Nest groups

A member may itself be a **group** id, so groups compose to any depth. A nested group can span ranks, letting an outer boundary enclose several inner ones. Give every group a clear id — never reuse a node id such as `web` for a group, or the two collide:

```yaml:symbol
wwwroot/examples/guides/groups-03.beck.yaml
```

<beck-diagram src="/examples/guides/groups-03.beck.yaml" mode="auto"></beck-diagram>

Here `vpc` lists two group ids as its members, so `webtier` and `datasubnet` render as boxes inside the VPC boundary.

## Connect to a boundary

An edge's `from` or `to` may target a group id instead of a node id, drawing the connection to the whole box rather than to one card inside it:

```yaml:symbol
wwwroot/examples/guides/groups-04.beck.yaml
```

<beck-diagram src="/examples/guides/groups-04.beck.yaml" mode="auto"></beck-diagram>

> [!NOTE]
> Membership is a tree: each node or group has at most one parent. A node cannot belong to two groups, a group cannot contain itself, and there are no cycles.

## Namespace boxes (class diagrams)

Groups work in a [class diagram](/docs/guides/class) too, where the box reads as a namespace or module around its classes. Membership is identical — list `members` on the group, or set `group:` on each class:

```beck
type: class
meta: { animate: false }
classes:
  - { id: Order, name: Order, group: sales }
  - { id: OrderLine, name: OrderLine, group: sales }
  - { id: Customer, name: Customer, group: crm }
groups:
  - { id: sales, label: Sales }
  - { id: crm, label: CRM }
relations:
  - { from: Order, to: OrderLine, kind: composition, fromCard: "1", toCard: "*" }
  - { from: Order, to: Customer, kind: association }
```

For the full list of group fields, see the [YAML schema reference](/docs/reference/yaml).

