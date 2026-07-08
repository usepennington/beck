---
title: Add Beck to a Pennington site
description: Render fenced beck diagrams to static SVG at build time with a code-block preprocessor, measure with your own fonts, and adopt your MonorailCSS palette.
order: 21
sectionLabel: Setup
uid: docs.guide.pennington
---

This guide shows you how to add Beck to a [Pennington](https://usepennington.github.io/pennington/)
site so a fenced ` ```beck ` block renders to a static, self-animating `<svg>` at **build time** — no
client JavaScript. For a generic (non-Pennington) ASP.NET host, see [Add Beck to your
site](/docs/guides/install) instead.

## Add the package

Beck renders with **`Beck`**, a pure-C# package. Add **`Beck.Skia`** too — the
preprocessor below measures text with it so cards match your fonts exactly (strongly recommended):

```bash
dotnet add package Beck
dotnet add package Beck.Skia
```

## Render fenced diagrams

Pennington hands every fenced code block to any `ICodeBlockPreprocessor` you register. Add one that
renders the `beck` language through `BeckSvg.Render` and returns finished HTML:

```csharp
using Beck.Rendering;
using Beck.Skia;
using Beck.Rendering.Text;
using Pennington.Markdown.Extensions;

// Turns a ```beck (inline YAML) or ```beck:symbol (a .beck.yaml file path) fence into a static SVG.
// Priority 500 runs it before the source-embed preprocessor, so a `beck` fence is never treated as
// a plain source listing. Add `,static` to a fence for a still frame.
public sealed class BeckFencePreprocessor : ICodeBlockPreprocessor
{
    public int Priority => 500;

    private readonly BeckFontSpec _font = new()
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

    private readonly SkiaTextMeasurer _measurer;
    public BeckFencePreprocessor() => _measurer = new SkiaTextMeasurer(_font);

    public CodeBlockPreprocessResult? TryProcess(string code, string languageId)
    {
        var id = languageId.Trim();
        if (id != "beck" && !id.StartsWith("beck:") && !id.StartsWith("beck,")) return null;

        bool isFile = id.Contains(":symbol");
        var animation = id.Contains(",static") ? AnimationMode.Static : AnimationMode.Full;
        string yaml = isFile ? File.ReadAllText(Path.GetFullPath(code.Trim())) : code;

        string svg = BeckSvg.Render(yaml, new SvgRenderOptions
        {
            Measurer = _measurer,   // exact card sizing over your fonts (see below)
            Font = _font,
            Animation = animation,
        });
        // SkipTransform: this is finished HTML — the highlighter must not reprocess it.
        return new CodeBlockPreprocessResult($"<div class=\"beck-embed\">{svg}</div>", "beck", SkipTransform: true);
    }
}
```

Register it in `Program.cs`:

```csharp
builder.Services.AddSingleton<ICodeBlockPreprocessor, BeckFencePreprocessor>();
```

Now any ` ```beck ` fence in your Markdown renders to an inline SVG at build time.

> [!IMPORTANT]
> **Measure with your site's own fonts.** The preprocessor above wires a `SkiaTextMeasurer` at the
> `.ttf` files your CSS serves, so Beck sizes each card to the text the browser will actually draw.
> `BeckSvg.Render(yaml)` also works with no measurer at all — a built-in default measures against
> Inter/IBM Plex Mono metrics — but if your site renders diagrams in different fonts, cards drift a
> little tight or roomy. Skia is the recommended setup for anything you publish.

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
Pull the source with a `yaml:symbol` fence, then render the same file with a `beck:symbol` fence —
the preprocessor turns it into static SVG at build time. Add `,static` (`beck:symbol,static`) for a
still frame:

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

This is the convention these docs use throughout, showing each diagram's source beside the diagram
itself.

Next, learn the language in [Your first diagram](/docs/tutorials/first-diagram), generate diagrams
from your model in [Generate diagrams from your code](/docs/guides/generate), or fine-tune colours in
[Match your theme and colours](/docs/guides/theme).
