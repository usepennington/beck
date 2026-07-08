---
title: Add Beck to your site
description: Render diagrams to SVG server-side with the Beck NuGet package, measure with your own fonts, and wire your palette so diagrams match your site.
order: 20
sectionLabel: Setup
uid: docs.guide.install
---

This guide shows you how to render Beck diagrams **server-side** in your own ASP.NET Core site and
make them adopt your colours and dark mode. Beck renders to a self-contained inline `<svg>` — there
is no client-side engine and no JavaScript to include. It assumes a host with a Tailwind-style CSS
palette (colours exposed as `--color-*` custom properties). On a Pennington site, see [Add Beck to a
Pennington site](/docs/guides/pennington) — you can render fenced ` ```beck ` blocks automatically
there.

## Add the package

Beck renders diagrams with **`Beck`**, a pure-C# package — no Node, no browser, no CDN:

```bash
dotnet add package Beck
```

For card sizing that exactly matches the fonts your site renders in, also add
**`Beck.Skia`** — strongly recommended, and covered in [Measure with your own
fonts](#measure-with-your-own-fonts) below:

```bash
dotnet add package Beck.Skia
```

Both target net8.0 and later.

## Render a diagram

`BeckSvg.Render(yaml)` returns a self-contained, self-animating `<svg>` string. Write it into any
page you produce — a Razor view, a minimal-API endpoint, a build step:

```csharp
using Beck.Rendering;

string svg = BeckSvg.Render("""
    type: architecture
    meta: { direction: LR }
    nodes:
      - { id: web, title: Web App, kind: user }
      - { id: api, title: API, kind: gateway }
      - { id: db, title: Postgres, kind: db }
    edges:
      - { from: web, to: api }
      - { from: api, to: db, label: queries }
    """);
```

In a Razor view, drop the result straight in with `@((MarkupString)svg)` (or `@Html.Raw(svg)`). That
YAML produces:

```beck
type: architecture
meta: { direction: LR }
nodes:
  - { id: web, title: Web App, kind: user }
  - { id: api, title: API, kind: gateway }
  - { id: db, title: Postgres, kind: db }
edges:
  - { from: web, to: api }
  - { from: api, to: db, label: queries }
```

It works out of the box: the package ships a built-in text measurer, so you need no font files to get
started. Motion is baked into the SVG and respects `prefers-reduced-motion` and `meta.animate: false`,
emitting a static frame instead when either applies.

## Measure with your own fonts

Beck sizes every card to its text, which means it has to **measure** that text. Getting the
measurement right is what makes cards fit snugly instead of clipping or floating in empty space.

The built-in default measures against **Inter + IBM Plex Mono** metrics. That is perfect if your site
draws diagrams in those faces (or leaves Beck's default font stack in place) — but if your diagrams
render in a *different* font, its glyphs are wider or narrower than Beck assumed, so cards come out a
little too tight or too roomy. A per-text `textLength` guard keeps text from ever overflowing, but the
fit won't be pixel-perfect.

> [!IMPORTANT]
> **For diagrams that match your site's design, measure with your site's actual fonts.** Add
> `Beck.Skia` and pass a `SkiaTextMeasurer` — it shapes text with HarfBuzz over the exact
> `.ttf` files the browser will draw, so card sizing is exact. This is how these docs render every
> diagram, and it's the recommended setup for anything you ship.

```csharp
using Beck.Rendering;
using Beck.Skia;
using Beck.Rendering.Text;

// Point the spec at the same font files your CSS serves to the browser.
var font = new BeckFontSpec
{
    Family = "IBM Plex Sans",
    MonoFamily = "IBM Plex Mono",
    Files = new Dictionary<int, string>
    {
        [400] = "wwwroot/fonts/IBMPlexSans-Regular.ttf",
        [500] = "wwwroot/fonts/IBMPlexSans-Medium.ttf",
        [600] = "wwwroot/fonts/IBMPlexSans-SemiBold.ttf",
        [700] = "wwwroot/fonts/IBMPlexSans-Bold.ttf",
    },
    MonoFiles = new Dictionary<int, string>
    {
        [400] = "wwwroot/fonts/IBMPlexMono-Regular.ttf",
    },
};
using var measurer = new SkiaTextMeasurer(font);

string svg = BeckSvg.Render(yaml, new SvgRenderOptions { Measurer = measurer, Font = font });
```

Passing `Font` also rewrites the SVG's `--beck-font` tokens to ask for those families, so what you
measure and what the browser draws are the same. A `SkiaTextMeasurer` caches its faces and is safe to
share, so build one and reuse it across every render.

## Make diagrams match your site

Out of the box a diagram renders in Beck's built-in palette. To make it *yours*, you give it your
colours — and the mechanism is plain CSS variables, so it works with any Tailwind-style host.

Every colour Beck paints is a `--beck-*` custom property whose default reads a `--color-*` variable
from your page:

```text
--beck-primary  →  var(--color-primary-600)
--beck-success  →  var(--color-emerald-500)
--beck-surface  →  var(--color-base-50)     (and darker base shades in dark mode)
--beck-text     →  var(--color-base-800)
…
```

So Beck adopts your palette the moment those `--color-*` variables exist on the page. Tailwind
already publishes its built-in colours as `--color-*` (so `--color-emerald-500`, `--color-amber-500`,
`--color-red-500`, and `--color-violet-500` — the `success`, `warn`, `danger`, and `info` accents —
resolve for free). You only need to supply the two ramps Beck names that Tailwind doesn't: your brand
`primary` and a neutral `base`.

```css
:root {
  /* Brand accent — Beck reads --color-primary-600 */
  --color-primary-600: #4f46e5;

  /* Neutral ramp — Beck reads --color-base-50 … --color-base-950 */
  --color-base-50:  #f8fafc;
  --color-base-100: #f1f5f9;
  --color-base-200: #e2e8f0;
  --color-base-300: #cbd5e1;
  --color-base-400: #94a3b8;
  --color-base-500: #64748b;
  --color-base-700: #334155;
  --color-base-800: #1e293b;
  --color-base-900: #0f172a;
  --color-base-950: #020617;
}
```

> [!TIP]
> If you define your Tailwind theme with `@theme`, declare the same variables there instead — they
> land on `:root` just the same, and a `primary` colour in your theme already publishes
> `--color-primary-600`.

### Dark mode comes for free

Beck reads dark mode from a `.dark` class (or `data-theme="dark"`) on your `<html>` element — the
exact convention Tailwind uses. When that class is present it switches to the darker shades of your
`base` ramp and re-themes every diagram on the page automatically. So if your dark toggle already
flips `.dark` on `<html>`, you have nothing more to do.

For finer control — pinning one diagram's theme, or overriding a single token — see [Match your
theme and colours](/docs/guides/theme).

Next, learn the language in [Your first diagram](/docs/tutorials/first-diagram), and keep the [YAML
schema](/docs/reference/yaml) handy for every field.
