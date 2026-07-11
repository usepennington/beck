---
title: Draw a mind map
description: A nested topic tree drawn as a two-sided butterfly, with depth roles, accent cycling, status pills, and ghost branches.
order: 30
sectionLabel: Other diagram types
uid: docs.guide.mindmap
---

A `type: mindmap` document draws a nested topic tree as a two-sided "butterfly": a central `root`,
first-level branches split left and right, and every deeper subtree fanning outward from its parent.
Depth reads as **size and shape** — a big root card, medium rank-1 branch cards with icon chips, and
light leaf pills further out — while each top branch carries its own accent down its whole subtree.
The layout is fixed (there's no `direction` to choose), and a mind map renders **static**: it's a map
to read, so there are no packets or narration.

## The shape: root and topics

Every mind map needs a `root` and a `topics:` list of first-level branches. `root` can be a plain
string (shorthand for its title) or a mapping for a subtitle, accent, icon, or content. Each
first-level branch takes the next colour from the cycle **info → primary → success → warn → danger**
(wrapping), so a wide map still reads as distinct threads without you naming a colour for every
branch — and every descendant inherits its branch's colour:

```yaml:symbol
wwwroot/examples/guides/mindmap-01.beck.yaml
```

```beck:symbol
wwwroot/examples/guides/mindmap-01.beck.yaml
```

## Nesting, items, body, and accent overrides

Nest with `children:` to any depth. Shape follows depth: the **root** and every **rank-1** branch are
cards (a rank-1 card shows an icon chip when you set `icon:`); from **rank 2 outward** a heading is a
light pill. Give any topic real content with `items:` (a bulleted list) or `body:` (a wrapped
paragraph) and it stays a card at any depth. Every descendant inherits its parent's resolved accent —
set `accent:` explicitly on a topic to start a new colour that then flows to *its* children:

```yaml:symbol
wwwroot/examples/guides/mindmap-02.beck.yaml
```

```beck:symbol
wwwroot/examples/guides/mindmap-02.beck.yaml
```

> [!NOTE]
> `items:`/`body:` aren't unique to mind maps — architecture cards accept the same two fields. See
> [Add items or a body](/docs/guides/nodes#add-items-or-a-body) in the nodes guide.

## Status pills and ghost branches

Give a rank-1 branch a `status:` and it renders a small semantic pill under the title — the colour
comes from the word, not the branch accent: `complete`/`done` read success, `in progress` reads warn,
`blocked` reads danger, `review` reads info, `planned` reads neutral.

Mark a branch that isn't real yet with `variant: ghost` (or `ghost: true`). The whole subtree turns
neutral, dashed, and shadowless with a faint `planned` label — a clean way to sketch future work
without it competing with the committed branches.

```yaml
topics:
  - title: Research
    icon: search
    status: complete
  - title: Launch
    icon: cloud
    ghost: true
    children: [{ title: Beta program }, { title: Marketing site }]
```

## Layout

The butterfly layout is fixed left/right — `meta.direction` is accepted but ignored, since a mind map
only reads one way. Top branches alternate right/left in authoring order, balancing leaf rows per
side; each branch sits at the mean height of its leaves. Edges run parent → child as smooth un-arrowed
curves in a muted branch colour (a mind map is read, not followed), fanning from a single point on
each parent so its children read as one set.

A mind map renders static — no packets, no narration — identical to the reduced-motion frame. (A
`flow:` is still accepted for forward-compatibility, but a mind map doesn't animate it.)

## Generate it from your C#

```csharp
using Beck.Authoring;

string fence = new MindMapDiagramBuilder("Beck")
    .Root("Beck")
    .Topic("Rendering", t => t
        .Accent(AccentToken.Info)
        .Status("complete")
        .Topic("Pipeline", p => p.Items("Model", "Text", "Layout"))
        .Topic("Determinism", d => d.Body("Same YAML, same SVG.")))
    .Topic("Packages", t => t
        .Topic("Beck")
        .Topic("Beck.Skia"))
    .Topic("Roadmap", t => t.Ghost()   // a not-yet-real branch: neutral, dashed, "planned"
        .Topic("Plugins")
        .Topic("Themes"))
    .ToFence();   // ```beck … ``` — drop it into any Markdown page
```

---

Full field tables: [root and topics in the YAML
schema](/docs/reference/yaml#root-and-topics-type-mindmap). Generating one from C#:
[`MindMapDiagramBuilder`](/docs/guides/generate).
