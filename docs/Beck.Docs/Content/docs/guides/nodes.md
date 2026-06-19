---
title: Style your nodes
description: Kinds, icons, accents, variants, links, and one-off colour overrides.
order: 22
sectionLabel: How-to guides
uid: docs.guide.nodes
---

This guide shows you how to make a node look exactly the way you want — picking its shape, icon, colour, weight, secondary text, link target, and, when you must, a hardened one-off palette.

Every recipe below is a small static illustration (`animate: false`). For the full node field table see [the YAML schema](/docs/reference/yaml); for site-wide colour see [Match your theme and colours](/docs/guides/theme).

## Pick a kind

`kind` is the fastest lever: it sets a node's default icon, accent, and variant in one word. If you want a database to read as a database, give it `kind: db` and stop there.

| kind | accent | icon | variant |
|---|---|---|---|
| `service` | primary | service | solid |
| `db` | info | db | solid |
| `queue` | warn | queue | solid |
| `cache` | warn | cache | solid |
| `gateway` | primary | gateway | solid |
| `external` | neutral | external | solid |
| `user` | success | user | solid |
| `ghost` | neutral | service | ghost |

```yaml:symbol
wwwroot/examples/guides/nodes-01.beck.yaml
```

<beck-diagram src="/examples/guides/nodes-01.beck.yaml" mode="auto" animate="false"></beck-diagram>

`kind` defaults to `service`, so a node with no `kind` is a primary-accented service card.

## Change the icon

To swap the glyph, set `icon` to a named key. The names cover the usual infrastructure vocabulary (`cdn`, `lock`, `kafka`, `lambda`, `brain`, and many more) — see the full list in [the YAML schema](/docs/reference/yaml#icons). An unknown key falls back to the kind's default icon, so a typo never breaks the render.

```yaml:symbol
wwwroot/examples/guides/nodes-02.beck.yaml
```

<beck-diagram src="/examples/guides/nodes-02.beck.yaml" mode="auto" animate="false"></beck-diagram>

For a glyph that isn't in the set, pass raw inline SVG. Beck drops the markup straight into the icon chip, so size your paths to a 24×24 viewBox:

```yaml:symbol
wwwroot/examples/guides/nodes-03.beck.yaml
```

<beck-diagram src="/examples/guides/nodes-03.beck.yaml" mode="auto" animate="false"></beck-diagram>

> [!TIP]
> Inline SVG uses `fill="currentColor"` so the glyph follows the node's accent and theme automatically.

## Set an accent

`accent` colours the icon chip, border tint, and status pill. Prefer a **token** — `primary`, `success`, `warn`, `danger`, `info`, or `neutral` — because tokens ride the theme and recolour cleanly in light and dark mode.

```yaml:symbol
wwwroot/examples/guides/nodes-04.beck.yaml
```

<beck-diagram src="/examples/guides/nodes-04.beck.yaml" mode="auto" animate="false"></beck-diagram>

Any value that isn't a token — a hex, `rgb()`, or CSS colour name — is used verbatim. A raw accent does **not** follow the theme, so reserve it for a deliberately fixed brand colour:

```yaml:symbol
wwwroot/examples/guides/nodes-05.beck.yaml
```

<beck-diagram src="/examples/guides/nodes-05.beck.yaml" mode="auto" animate="false"></beck-diagram>

## Change visual weight

`variant` controls how loud a node is, independent of its kind:

- `solid` — the default, fully filled card.
- `subtle` — dimmed, for supporting or background components.
- `ghost` — dashed, transparent, for a planned-but-absent placeholder.

```yaml:symbol
wwwroot/examples/guides/nodes-06.beck.yaml
```

<beck-diagram src="/examples/guides/nodes-06.beck.yaml" mode="auto" animate="false"></beck-diagram>

> [!NOTE]
> The `ghost` **kind** and the `ghost` **variant** are not the same thing. `kind: ghost` is a neutral placeholder node (which happens to default to the ghost variant); `variant: ghost` is purely the dashed, transparent look applied to any kind.

## Add a subtitle or status

`subtitle` adds a muted second line — good for a hostname, region, or technology note. `status` renders a small pill on the card.

```yaml:symbol
wwwroot/examples/guides/nodes-07.beck.yaml
```

<beck-diagram src="/examples/guides/nodes-07.beck.yaml" mode="auto" animate="false"></beck-diagram>

A `status` set here is the node's resting state. If you want a status that **changes** while the diagram plays — flipping from `idle` to `busy`, say — that's a flow concern; see [Animate the flow](/docs/guides/flow).

## Make a node a link

To turn a card into a hyperlink, add `href`. Beck renders the whole node as an `<a>`. Add `target: _blank` to open in a new tab.

```yaml:symbol
wwwroot/examples/guides/nodes-08.beck.yaml
```

<beck-diagram src="/examples/guides/nodes-08.beck.yaml" mode="auto" animate="false"></beck-diagram>

`target` is only meaningful alongside `href`.

## Override one node's colours

When a single card must match a fixed brand or a hardened reference design, override its surface and text directly. `surface` sets the card background and `textColor` sets the text — both take raw CSS colours and both ignore the theme, so use them only where you genuinely need a fixed look.

```yaml:symbol
wwwroot/examples/guides/nodes-09.beck.yaml
```

<beck-diagram src="/examples/guides/nodes-09.beck.yaml" mode="auto" animate="false"></beck-diagram>

> [!WARNING]
> `surface` and `textColor` opt a node out of light/dark theming. A card that looks right in dark mode may be unreadable in light mode — set both, and test in both themes.

---

For the complete node field table, see [the YAML schema](/docs/reference/yaml). To recolour every node at once by adopting your site's palette, see [Match your theme and colours](/docs/guides/theme).

