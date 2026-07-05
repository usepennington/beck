---
title: Draw a sequence diagram
description: Participants, messages, replies, activation bars, self-messages, and section bands — with the message order as the animation.
order: 26
sectionLabel: Other diagram types
uid: docs.guide.sequence
---

A `type: sequence` document draws the classic interaction diagram — participants across the top,
lifelines down, messages in call order — and then does what Mermaid can't: it **plays** the
conversation. Without a `flow:` block, the message order *is* the animation; a packet rides each
arrow, in order, on loop.

## Participants and messages

Declare `participants` (columns, in order) and `messages` (rows, in order). Participants take the
same fields as architecture nodes — `title`, `kind`, `icon`, `accent`, `subtitle` — so a database
column can look like a database:

```yaml:symbol
wwwroot/examples/guides/sequence-01.beck.yaml
```

<beck-diagram src="/examples/guides/sequence-01.beck.yaml" mode="auto"></beck-diagram>

A few things happened automatically there:

- **Message colors** came from the participants: each message tints itself with the accent of the
  participant doing the work — the receiver of a call, the sender of a reply — so every
  request/response pair shares one hue. An explicit `color:` on a message wins.
- **Replies** (`reply: true`) render dashed with an open arrowhead and a quieter label, so
  request/response pairs read at a glance.
- **Activation bars** grew on the receivers: a message starts a bar when a later reply from that
  receiver back to the sender closes it. Nested request/reply pairs nest their bars. Set
  `activate: false` on a message to suppress its bar, or `activate: true` to force one without a
  matching reply.
- **The story dims and reveals.** With the derived animation, everything starts faded; each message
  row lights up as its packet fires, activation bars brighten while their participant works, and
  the whole conversation fades back down before the loop restarts. A hand-written `flow:` (or
  `animate: false`) renders everything at full strength instead.

## Self-messages and sections

A message from a participant to itself draws a small loop. A list entry of `- section: <label>`
opens a tinted, dashed band around every message until the next section (or the end) — use it to
chapter a long interaction. Give it an `accent` to color the band and its floating label
(`- { section: Payment, accent: info }`); each section also becomes a `phase` the animation can
[seek to](/docs/reference/flow) and lights up as its chapter begins:

```yaml:symbol
wwwroot/examples/guides/sequence-02.beck.yaml
```

<beck-diagram src="/examples/guides/sequence-02.beck.yaml" mode="auto"></beck-diagram>

## Async messages

`kind: async` renders dashed with an open arrowhead and gives the packet the slow, eased async
motion — right for fire-and-forget events and queue deliveries:

```yaml:symbol
wwwroot/examples/guides/sequence-03.beck.yaml
```

<beck-diagram src="/examples/guides/sequence-03.beck.yaml" mode="auto"></beck-diagram>

## Scripting the animation

The derived flow (one packet per message, in order) is usually what you want. To take over, add a
`flow:` block exactly as in any other Beck diagram — `status`, `working`, `fail`, and friends all
work on participants. See [Animate the flow](/docs/guides/flow).

---

Full field tables: [participants and messages in the YAML
schema](/docs/reference/yaml#participants-and-messages-type-sequence). Generating one from C#:
[`SequenceDiagramBuilder`](/docs/guides/generate).
