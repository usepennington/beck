---
title: Add Beck to a Pennington site
description: Register the Pennington.Beck package so fenced beck diagrams render to static SVG at build time, pick styles per fence or site-wide, measure with your own fonts, and adopt your MonorailCSS palette.
order: 21
sectionLabel: Setup
uid: docs.guide.pennington
---

This guide shows you how to add Beck to a [Pennington](https://usepennington.github.io/pennington/)
site so a fenced ` ```beck ` block renders to a static, self-animating `<svg>` at **build time** — no
client JavaScript. Pennington ships a first-class integration package, **`Pennington.Beck`**, so the
whole wiring is one package reference and one service registration. For a generic (non-Pennington)
ASP.NET host, see [Add Beck to your site](/docs/guides/install) instead.

## Add the package

```bash
dotnet add package Pennington.Beck
```

Then register it in `Program.cs`:

```csharp
builder.Services.AddPenningtonBeck();
```

That is the whole integration. `Pennington.Beck` hooks the shared code-block pipeline (it works the
same on `AddPennington`, `AddDocSite`, and `AddBlogSite` hosts), so the next ` ```beck ` fence in any
Markdown page renders as an inline SVG — on the live dev server and in the static build. It brings
the `Beck` engine with it; a malformed diagram fails loud with a visible error box and a diagnostics
entry rather than silently vanishing.

## Tune a fence with flags

A comma-separated tail after the language adjusts one fence without touching its YAML, and flags
combine (`beck:symbol,static` works):

| flag | effect |
|---|---|
| `static` | Renders the fully-revealed final frame with no motion. |
| `scrub` | Drives the choreography from scroll position instead of a looping timeline. |
| `style=<name>` | Overrides the document's `meta.style` — render one YAML in any built-in look. |

## Pick a style

Beck ships [nine built-in styles](/docs/guides/styles) — `classic`, `minimal`, `terminal`,
`blueprint`, `glow`, `brutalist`, `sketch`, `extrude`, and `circuit`. There are three places to set
one, from narrowest to widest scope:

**Per document**, in the YAML itself:

```yaml
type: architecture
meta:
  style: sketch
```

**Per fence**, with the `style=` flag — a last-word override that beats the document's own
`meta.style`, handy when one shared `.beck.yaml` should appear in different looks:

````markdown
```beck,style=sketch
type: architecture
nodes: [ ... ]
```
````

**Site-wide**, as the default for every fence via `BeckOptions.RenderOptions` (an individual
document's `meta.style` still opts back out):

```csharp
builder.Services.AddPenningtonBeck(beck =>
{
    beck.RenderOptions = new SvgRenderOptions { Style = BeckStyles.ByName["sketch"] };
});
```

Custom styles you register in `SvgRenderOptions.Styles` are addressable the same three ways — see
[Author a custom style](/docs/guides/custom-styles) and the
[style system reference](/docs/reference/styles) for resolution and precedence.

## Measure with your site's own fonts

By default Beck measures text against embedded Inter/IBM Plex Mono metrics, and every label carries
a `textLength` guard so a font mismatch squeezes glyphs slightly instead of breaking layout. If your
site renders diagrams in different fonts, cards drift a little tight or roomy — for anything you
publish, add the optional **`Beck.Skia`** package and point a `SkiaTextMeasurer` at the `.ttf` files
your CSS actually serves:

```bash
dotnet add package Beck.Skia
```

```csharp
var font = new BeckFontSpec
{
    Family = "IBM Plex Sans",
    MonoFamily = "IBM Plex Mono",
    Files = new Dictionary<int, string>
    {
        [400] = "wwwroot/fonts/IBMPlexSans-Regular.ttf",
        [600] = "wwwroot/fonts/IBMPlexSans-SemiBold.ttf",
        [700] = "wwwroot/fonts/IBMPlexSans-Bold.ttf",
    },
    MonoFiles = new Dictionary<int, string> { [400] = "wwwroot/fonts/IBMPlexMono-Regular.ttf" },
};

builder.Services.AddPenningtonBeck(beck =>
{
    beck.RenderOptions = new SvgRenderOptions
    {
        Font = font,
        Measurer = new SkiaTextMeasurer(font),
    };
});
```

## Adopt your MonorailCSS palette

MonorailCSS emits your `ColorScheme` as `--color-*` custom properties — `--color-primary-*` from your
`PrimaryColorName` and `--color-base-*` from your `BaseColorName`. Beck reads exactly those, so
diagrams take on your brand colour and follow your dark mode with no extra work:

```csharp
builder.Services.AddMonorailCss(_ => new MonorailCssOptions
{
    ColorScheme = new NamedColorScheme
    {
        PrimaryColorName = ColorName.Emerald,
        BaseColorName = ColorName.Slate,
    },
});
```

The one gotcha is the other accents. Beck's `success`, `warn`, `danger`, and `info` tokens read
`--color-emerald-500`, `--color-amber-500`, `--color-red-500`, and `--color-violet-500`. MonorailCSS
only emits the ramps your utilities actually reference, so if your site never uses, say, a violet
utility, `info` has nothing to resolve to. Remap those tokens to a ramp you do emit by adding a rule
to your MonorailCSS `ExtraStyles`:

```csharp
ExtraStyles = """
    body .beck-svg {
        --beck-success: var(--color-green-500);
        --beck-info: var(--color-sky-500);
    }
    """,
```

The `body .beck-svg` selector outranks the engine's own defaults, which it injects into each SVG at
render time. See [Match your theme and colours](/docs/guides/theme) for the full token list.

## Show the source and the render together

Because Pennington ships TreeSitter `:symbol` source embeds, you can keep a diagram's YAML in one
`.beck.yaml` file and show both its source and its render from that single file — no duplication.
Pull the source with a `yaml:symbol` fence, then render the same file with a `beck:symbol` fence
(the body is one file path per line; each file renders independently). All the fence flags apply
(`beck:symbol,static`, `beck:symbol,style=sketch`):

````markdown
```yaml:symbol
wwwroot/examples/platform.beck.yaml
```
```beck:symbol
wwwroot/examples/platform.beck.yaml
```
````

```yaml:symbol
wwwroot/examples/guides/pennington-platform.beck.yaml
```
```beck:symbol
wwwroot/examples/guides/pennington-platform.beck.yaml
```

Paths resolve against `BeckOptions.ContentRoot`, which defaults to the working directory — set it to
match your TreeSitter `ContentRoot` so both `:symbol` forms address files the same way:

```csharp
builder.Services.AddPenningtonBeck(beck =>
{
    beck.ContentRoot = "../..";
});
```

This is the convention these docs use throughout, showing each diagram's source beside the diagram
itself.

## Fullscreen zoom

Each rendered embed carries a zoom button that opens the diagram in a full-screen lightbox — the
package's one piece of client JavaScript; rendering stays server-side. Turn it off to emit bare SVG
with no client behavior:

```csharp
builder.Services.AddPenningtonBeck(beck =>
{
    beck.Zoom = false;
});
```

Next, learn the language in [Your first diagram](/docs/tutorials/first-diagram), browse the looks in
[Pick a built-in style](/docs/guides/styles), generate diagrams from your model in
[Generate diagrams from your code](/docs/guides/generate), or fine-tune colours in
[Match your theme and colours](/docs/guides/theme).
