---
title: Your first diagram
description: Build a small system from nothing, one block at a time, live in the playground.
order: 10
sectionLabel: Tutorials
uid: docs.tutorial.first-diagram
---

In this tutorial we'll build a small web system from nothing — a client, an API, a database, and a
cache — one block at a time. By the end you'll have a complete, animated diagram and know how the
four parts of a Beck document fit together.

You don't need to install anything. Open the **[playground](/playground)** in another tab and type
along; after each step, the diagram below shows what you should see.

## Step 1 — Add a node

A document starts by declaring what it is — `type: architecture` — followed by `nodes`. Each node
needs an `id`. Type this:

```yaml
type: architecture
nodes:
  - { id: web, title: Web App }
```

You should see a single box labelled **Web App**:

```beck
type: architecture
nodes:
  - { id: web, title: Web App }
```

The `title` is what's drawn; the `id` is the short name we'll use to refer to this node later.

## Step 2 — Add more nodes and connect them

Now add two more nodes, then an `edges` list to connect them. An edge is a `from`/`to` pair of ids:

```yaml
type: architecture
nodes:
  - { id: web, title: Web App }
  - { id: api, title: API }
  - { id: db, title: Database }
edges:
  - { from: web, to: api }
  - { from: api, to: db }
```

Beck routes the lines and places the arrowheads for you:

```beck
type: architecture
meta: { animate: false }
nodes:
  - { id: web, title: Web App }
  - { id: api, title: API }
  - { id: db, title: Database }
edges:
  - { from: web, to: api }
  - { from: api, to: db }
```

## Step 3 — Give the nodes a kind

Set a `kind` on a node and Beck picks a fitting icon and accent colour. Make the web app a `user`,
the database a `db`, and add a `cache`. Add a `label` to the edge into the database, too:

```yaml
nodes:
  - { id: web, title: Web App, kind: user }
  - { id: api, title: API }
  - { id: cache, title: Redis, kind: cache }
  - { id: db, title: Database, kind: db }
edges:
  - { from: web, to: api }
  - { from: api, to: cache }
  - { from: api, to: db, label: queries }
```

Notice the icons and the colours — you didn't set any of them by hand:

```beck
type: architecture
meta: { animate: false }
nodes:
  - { id: web, title: Web App, kind: user }
  - { id: api, title: API }
  - { id: cache, title: Redis, kind: cache }
  - { id: db, title: Database, kind: db }
edges:
  - { from: web, to: api }
  - { from: api, to: cache }
  - { from: api, to: db, label: queries }
```

## Step 4 — Draw a boundary

Group the data stores into a labelled box. Add a `groups` list; each group has an `id`, a `label`,
and a list of `members` (node ids):

```yaml
groups:
  - { id: data, label: Data, members: [cache, db], accent: info }
```

The box is drawn around its members automatically:

```beck
type: architecture
meta: { animate: false }
nodes:
  - { id: web, title: Web App, kind: user }
  - { id: api, title: API }
  - { id: cache, title: Redis, kind: cache }
  - { id: db, title: Database, kind: db }
groups:
  - { id: data, label: Data, members: [cache, db], accent: info }
edges:
  - { from: web, to: api }
  - { from: api, to: cache }
  - { from: api, to: db, label: queries }
```

## Step 5 — Set the title and direction

Finally, add a `meta` block to give the diagram a title and lay it out left-to-right instead of
top-to-bottom:

```yaml
meta:
  title: My first system
  direction: LR
```

That's the whole diagram. Drop the `animate: false` we've been using and Beck animates it for you —
a packet traces each edge in order, then the diagram resets and loops:

```beck
type: architecture
meta:
  title: My first system
  direction: LR
nodes:
  - { id: web, title: Web App, kind: user }
  - { id: api, title: API }
  - { id: cache, title: Redis, kind: cache }
  - { id: db, title: Database, kind: db }
groups:
  - { id: data, label: Data, members: [cache, db], accent: info }
edges:
  - { from: web, to: api }
  - { from: api, to: cache }
  - { from: api, to: db, label: queries }
```

## What you built

In five steps you wrote a complete diagram using all four parts of a Beck document — `meta`,
`nodes`, `groups`, and `edges` — and Beck handled the layout, routing, icons, colours, and
animation. You never positioned a box or drew a line.

From here:

- Generate a diagram like this one from code in [Author a diagram in C#](/docs/tutorials/csharp).
- Put a diagram in your own site with [Add Beck to your site](/docs/guides/install).
- Make it yours: [style your nodes](/docs/guides/nodes), [match your
  theme](/docs/guides/theme), or [script the animation](/docs/guides/flow).
- Try the other diagram types — the same document shape draws a
  [sequence](/docs/guides/sequence), a [state machine](/docs/guides/state), or a [class
  model](/docs/guides/class).
