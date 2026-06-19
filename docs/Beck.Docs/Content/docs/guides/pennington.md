---
title: Add Beck to a Pennington site
description: Wire the engine into a Pennington DocSite or bare host, and adopt your MonorailCSS palette.
order: 21
sectionLabel: How-to guides
uid: docs.guide.pennington
---

This guide shows you how to add Beck to a [Pennington](https://usepennington.github.io/pennington/)
site — including the engine, writing a fenced diagram, and adopting your MonorailCSS palette. For a
generic Tailwind host, see [Add Beck to your site](/docs/guides/install) instead.

## Add the package

```bash
dotnet add package Beck
```

## Include the engine

Beck serves its engine as a static web asset at `/_content/Beck/beck.global.js`. How you reference it
depends on which Pennington host you run.

**DocSite host.** Add the script through `DocSiteOptions`, and Beck's `ScriptTag` gives you the exact
markup:

```csharp
builder.Services.AddDocSite(options =>
{
    options.AdditionalHtmlHeadContent = Beck.BeckAssets.ScriptTag;
});
```

**Bare host** (your own `App.razor`). Put the tag in your `<head>` directly, and serve the RCL's
static assets — a DocSite does this for you, a bare host opts in:

```razor
@* App.razor <head> *@
<script src="/_content/Beck/beck.global.js" defer></script>
```

```csharp
// Program.cs — after UsePennington(), before MapRazorComponents<App>()
app.MapStaticAssets();
```

## Write a diagram

Pennington renders a fenced ` ```beck ` block through Markdig as `<code class="language-beck">`, and
the engine hydrates it on load — no Markdig extension, no server-side step:

````markdown
```beck
meta: { direction: LR }
nodes:
  - { id: web, title: Web App, kind: user }
  - { id: api, title: API, kind: gateway }
  - { id: db, title: Postgres, kind: db }
edges:
  - { from: web, to: api }
  - { from: api, to: db, label: queries }
```
````

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
    body .beck-root {
        --beck-success: var(--color-green-500);
        --beck-info: var(--color-sky-500);
    }
    """,
```

The `body .beck-root` selector outranks the engine's own defaults, which it injects into the document
head at render time. See [Match your theme and colours](/docs/guides/theme) for the full token list.

## Show the source and the render together

Because Pennington ships TreeSitter `:symbol` source embeds, you can keep a diagram's YAML in one
`.beck.yaml` file and show both its source and its live render from that single file — no
duplication. Pull the source with a `:symbol` fence, then render the same file with a
`<beck-diagram>` element:

````markdown
```yaml:symbol
wwwroot/examples/platform.beck.yaml
```
<beck-diagram src="/examples/platform.beck.yaml" mode="auto"></beck-diagram>
````

```yaml:symbol
wwwroot/examples/guides/pennington-platform.beck.yaml
```
<beck-diagram src="/examples/guides/pennington-platform.beck.yaml" mode="auto"></beck-diagram>

This is the convention these docs use throughout, and it is how the [examples](/examples/) gallery
shows each diagram's source beside the diagram itself.

Next, learn the language in [Your first diagram](/docs/tutorials/first-diagram), generate diagrams
from your model in [Generate diagrams from your code](/docs/guides/generate), or fine-tune colours in
[Match your theme and colours](/docs/guides/theme).
