---
title: Draw a data chart
description: Bar, line, pie, donut, and scatter charts whose whole colour set is derived from one primary token — re-tinting with your palette and flipping light and dark.
order: 31
sectionLabel: Other diagram types
uid: docs.guide.chart
---

A `type: chart` document draws a small data chart — a **bar**, **line**, **pie**, **donut**, or
**scatter** — to round out a diagram with the numbers behind it. Charts are deliberately simple: no
axes to configure, no data toolkit, and no animation. What they *do* carry is Beck's colour idea taken
to its conclusion — **every series colour is derived from `--beck-primary`** by a pure
`color-mix`/relative-colour expression, so the whole set re-tints with your palette and flips light↔dark
on the same switch as the rest of the page. Swap the primary and every bar, line, slice, and dot
follows.

## The shape: chart kind and series

Set the `chart` kind, then list a `series`. What each series carries depends on the kind: a single
`value` for a bar or a pie/donut slice, a list of `values` for a line, or a list of `[x, y]` `points`
for a scatter. That's the whole schema — colours, spacing, and the legend are derived for you.

```yaml:symbol
wwwroot/examples/guides/chart-01.beck.yaml
```

```beck:symbol,static
wwwroot/examples/guides/chart-01.beck.yaml
```

A bar chart colours each bar from the palette and prints its value above it; the legend maps the
colours back to labels. Any series can pin its own colour with `color:` (a token like `info` or a raw
CSS colour) to break out of the derived set.

## Colour palettes

`palette:` picks how the colours beyond the first are generated from `--beck-primary`. Each is a pure
function of that one token, so a chart needs no colour list of its own:

| palette | how it derives | best for |
|---|---|---|
| `analogous` *(default)* | small hue steps either side of the primary | categorical series — distinct yet harmonious |
| `monochromatic` | tints of the primary, mixed toward the surface | an ordered magnitude, single-hue |
| `complementary` | the primary alternating with its opposite, lightening per pair | a two-way comparison |
| `sequential` | the primary fading toward neutral | one continuous scale — density or heat |

Because the colours are expressions over the tokens rather than baked hex, they re-tint with the host
palette and adapt to dark mode automatically — see [Match your theme and
colours](/docs/guides/theme). Here `complementary` sets two lines against each other:

```yaml:symbol
wwwroot/examples/guides/chart-02.beck.yaml
```

```beck:symbol,static
wwwroot/examples/guides/chart-02.beck.yaml
```

## Lines, scatters, and centred donuts

A **line** series is a list of `values`, one per x-step; lines share a light gridline backdrop and a
dot on the latest point. A **scatter** series is a list of `[x, y]` `points`, one colour per series
(cluster). A **pie** is a filled wedge per slice; a **donut** is the same with a hole, and it can carry
a `center` headline and a `centerLabel` sub-caption:

```yaml:symbol
wwwroot/examples/guides/chart-04.beck.yaml
```

```beck:symbol,static
wwwroot/examples/guides/chart-04.beck.yaml
```

```yaml:symbol
wwwroot/examples/guides/chart-03.beck.yaml
```

```beck:symbol,static
wwwroot/examples/guides/chart-03.beck.yaml
```

## The legend

`legend:` places the key `right` (the default), `top`, `bottom`, or `none`. A right-hand legend is a
column; top and bottom are centered rows that wrap. For a single-magnitude chart (bar, pie, donut) add
`legendValues: true` to print each value alongside its label in a right-hand column — as in the donut
above. A single-series chart, or one whose bars already label themselves, reads fine with `legend:
none`.

## Generate it from your C#

`ChartDiagramBuilder` emits the same schema from code — fix the kind at construction, then add one
`Series` per bar, line, or cluster:

```csharp
using Beck.Authoring;

string fence = new ChartDiagramBuilder(ChartKind.Donut, "Cloud spend by service")
    .Palette(ChartPalette.Analogous)
    .Legend(LegendPlacement.Right, values: true)
    .Center("$128k", "total")
    .Series("Compute", 52)
    .Series("Storage", 34)
    .Series("Network", 22)
    .ToFence();   // ```beck … ``` — drop it into any Markdown page
```

The `Series` overloads follow the data shapes — a single value for bar/pie/donut, several for a line,
`(x, y)` tuples for a scatter:

```csharp
new ChartDiagramBuilder(ChartKind.Line)
    .Series("This quarter", 2.4, 2.7, 3.1, 3.6)      // a value per x-step
    .Series("Last quarter", 1.9, 2.1, 2.3, 2.8);

new ChartDiagramBuilder(ChartKind.Scatter)
    .Series("v3.4", (74, 88), (80, 95), (77, 84));   // (x, y) points
```

---

Full field tables: [chart series in the YAML schema](/docs/reference/yaml#chart-series-type-chart).
Generating one from C#: [`ChartDiagramBuilder`](/docs/guides/generate).
