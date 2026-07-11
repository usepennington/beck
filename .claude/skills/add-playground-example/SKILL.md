---
name: add-playground-example
description: Add a new example diagram (or a whole new diagram-type group) to the Beck docs playground picker at docs/Beck.Docs. Use when asked to "add X to the playground", "put a flowchart/sequence/etc. example in the playground", "add a new example to the playground dropdown", or to wire up a new diagram type in the playground's Examples menu. Covers authoring the example YAML, registering it in the picker, and adding the type pill colour.
---

# Add a playground example

The `/playground` page (docs site) has an **Examples** dropdown grouped by diagram type. Each entry
loads a YAML file over HTTP and renders it live. Adding one means three things at most:

1. **Author** the example YAML (always).
2. **Register** it in the picker's `ExampleGroups` (always).
3. **Add a type pill colour** in `BrandStyling.cs` — **only when introducing a new diagram type**.

Then verify it renders. Details below.

## The three files

| Concern | File |
| --- | --- |
| Example YAML | `docs/Beck.Docs/wwwroot/examples/<name>.beck.yaml` |
| Picker registration | `docs/Beck.Docs.Client/PlaygroundIsland.razor` → `ExampleGroups` array |
| Type pill colour (new type only) | `docs/Beck.Docs/BrandStyling.cs` → `.pg-pill[data-type="…"]` rules |

> **Location matters.** Playground examples live at the **root** of `wwwroot/examples/`. The
> `wwwroot/examples/guides/`, `/reference/`, `/styles/` subfolders belong to the docs *pages* (loaded
> by `` ```beck:symbol `` fences) — putting a playground example there means the picker's `Src` path
> (`/examples/<name>.beck.yaml`) won't resolve.

## 1. Author the example YAML

Write `docs/Beck.Docs/wwwroot/examples/<name>.beck.yaml`. Match the house style of the existing
examples (read a couple first, e.g. `webhook-retry.beck.yaml`, `order-lifecycle.beck.yaml`):

- Lead with `type:` (`architecture` | `sequence` | `state` | `class` | `flowchart` | `mindmap`).
- `meta:` with a `title`, an optional one-line `subtitle`, and a `direction` (`TB`/`LR`).
- A **recognisable real-world scenario**, kept small — roughly 4–7 nodes reads best in the pane.
- **Accent tokens, never hex**: `primary` `success` `warn` `danger` `info` `neutral`. Colours are
  CSS tokens so the diagram adopts the site palette and the playground colour-scheme picker.
- Add a `note:` to a few edges / links / transitions — the engine derives a flow from these and
  narrates them in the caption bar (no `flow:` block needed for a nice default animation).
- Keep it deterministic: no counters, timestamps, or RNG anywhere.

Schema references: `docs/Beck.Docs/Content/docs/reference/yaml.md`, and the per-type model builders
in `src/Beck/Model/*Builder.cs` (they list every field and the exact tokens).

## 2. Register it in the picker

In `docs/Beck.Docs.Client/PlaygroundIsland.razor`, find the `ExampleGroups` static array.

**Adding to an existing type** — append an `Ex` to that group's `Items`:

```csharp
new Ex("Deploy gate", "Build, gate on green, deploy, then smoke-test with a rollback loop", "/examples/flowchart-deploy.beck.yaml"),
```

`Ex(Label, Desc, Src)` — `Label` is the bold title, `Desc` the one-line grey blurb, `Src` the site
path `/examples/<name>.beck.yaml` (leading slash; fetched relative to the app base). Two examples per
type is the established cadence for the smaller types.

**Adding a new diagram type** — add a new `ExGroup`:

```csharp
new("flowchart", "Flowchart", "◇", "flow", [
    new Ex("Deploy gate", "Build, gate on green, deploy, then smoke-test with a rollback loop", "/examples/flowchart-deploy.beck.yaml"),
    new Ex("Ticket triage", "An inbound support ticket routed by urgency and prior art", "/examples/flowchart-triage.beck.yaml")
]),
```

`ExGroup(Type, Label, Glyph, Short, Items)`:
- **`Type`** must equal the diagram's `type:` — it drives the pill's `data-type` (step 3).
- **`Glyph` + `Short`** are the pill's icon and 3–5 char tag. Pick a distinct unicode glyph.
  Existing: `⬡ arch` · `▷ seq` · `◈ state` · `▤ class` · `◇ flow` · `❋ map`.

## 3. Add the type pill colour (new type only)

Skip this if you reused an existing type. For a new `Type`, add a light **and** a `.dark` rule in
`docs/Beck.Docs/BrandStyling.cs`, next to the other `.pg-pill[data-type="…"]` rules. Pick a hue
distinct from the ones already in use (emerald / sky / amber / violet / teal / rose):

```css
.pg-pill[data-type="flowchart"]       { color: #0f766e; background: color-mix(in srgb, #14b8a6 15%, transparent); border-color: color-mix(in srgb, #14b8a6 30%, transparent); }
.dark .pg-pill[data-type="flowchart"] { color: #5eead4; }
```

Keep the `color-mix(...)` + literal-fallback shape of the neighbours (don't resolve to a flat hex).

## 4. Verify it renders

The picker loads examples at **runtime over HTTP**, so a docs build will *not* catch a malformed
YAML. Verify one of these ways:

- **Quick engine check (preferred).** Drop a temporary `[Theory]` into `tests/Beck.Tests` that reads
  each new file and renders it, then delete it after:

  ```csharp
  [Theory]
  [InlineData("flowchart-deploy")]
  public void PlaygroundExampleRenders(string name)
  {
      var font = TestFonts.Spec();
      using var measurer = new SkiaTextMeasurer(font);
      var path = Path.Combine(/* repo root */, "docs", "Beck.Docs", "wwwroot", "examples", name + ".beck.yaml");
      var info = BeckSvg.RenderWithInfo(File.ReadAllText(path), new SvgRenderOptions { Measurer = measurer, Font = font });
      Assert.StartsWith("<svg", info.Svg);
  }
  ```

  Run: `dotnet test tests/Beck.Tests/Beck.Tests.csproj --filter "FullyQualifiedName~PlaygroundExampleRenders"`.
  Dump `info.Svg` to a file and confirm the **viewBox starts at `0 0`** and there are **no negative
  path coordinates** — off-canvas routes are the signature of a routing regression.

- **Full visual check.** `dotnet run --project docs/Beck.Docs`, open `/playground`, pick the new
  entry from the Examples dropdown, and confirm it loads, animates, and the type pill shows the right
  colour. (A screenshot harness works too: render the YAML to SVG, embed it in an HTML page served
  over `http://` — `file://` is blocked in the browser tools — and screenshot it.)

Remove any temporary test/scratch files before finishing.

## What NOT to touch

- **Don't** register the example anywhere else — there's no manifest; `ExampleGroups` is the single
  source of truth for the picker.
- **Don't** add the file under `wwwroot/examples/guides|reference|styles/` (those are for docs pages).
- **Don't** hardcode colours in the YAML or resolve `--beck-*`/`--color-*` tokens to literals.
