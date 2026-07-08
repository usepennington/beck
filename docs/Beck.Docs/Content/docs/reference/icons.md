---
title: Icons
description: Every built-in icon key, rendered live from the engine's own registry.
order: 43
sectionLabel: Reference
uid: docs.reference.icons
---

Set a node's `icon` to one of the keys below. Every card is rendered from the engine's own icon
registry, so this page always matches the version you're running — nothing is hand-listed. A key
falls back to the node kind's default icon when it isn't recognised, so a typo never drops the glyph.

Many keys are aliases that share a glyph; each card shows its primary key, with the aliases listed
beneath. The glyph is drawn exactly as it appears in a node's accent-tinted icon chip.

<IconGallery />

## Custom SVG

For anything outside this set, pass raw inline `<svg>…</svg>` markup as the `icon` value instead of a
key. Use `fill="currentColor"` or `stroke="currentColor"` and a `0 0 24 24` viewBox so the glyph
inherits the node's accent and theme.

```yaml:symbol
wwwroot/examples/reference/icon-custom.beck.yaml
```

```beck:symbol
wwwroot/examples/reference/icon-custom.beck.yaml
```

See [node `icon`](/docs/reference/yaml#icons) in the schema for where the key slots in, and the
[node guide](/docs/guides/nodes) for choosing icons in context.
