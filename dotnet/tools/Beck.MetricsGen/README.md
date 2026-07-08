# Beck.MetricsGen

Dev-time code generator for the embedded font-metrics tables the managed
`EmbeddedMetricsMeasurer` reads. It measures the committed OFL fonts with the **same**
SkiaSharp + HarfBuzzSharp stack `Beck.Skia`'s `SkiaTextMeasurer` uses, then emits per-glyph
advance-per-em tables (plus per-em ascent/descent) as `dotnet/Beck/Text/*MetricsData.g.cs`.

This project is **not** packed and **not** in `Beck.slnx` — that solution carries only the
shipping engine, its Skia plug-in, the sample, the tests, and the docs site. Run it on demand:

```bash
# Writes SourceSerifMetricsData.g.cs / ArchivoMetricsData.g.cs / ShantellSansMetricsData.g.cs
# into dotnet/Beck/Text (default output dir, resolved from this project's path).
dotnet run --project dotnet/tools/Beck.MetricsGen -c Release

# Or an explicit output dir:
dotnet run --project dotnet/tools/Beck.MetricsGen -c Release -- <outputDir>
```

Each generated table mirrors `InterMetricsData.g.cs` exactly in shape (`SansWeights`, `SansAscent`,
`SansDescent`, `SansFallback`, `SansAscii`, `SansExtra`, `MonoWeights`, `MonoAscent`, `MonoDescent`,
`MonoAdvance`) over the identical charset (ASCII 32–126, Latin-1 supplement, and a handful of
Unicode punctuation/arrows). It differs only in data and class name. The Inter table itself is
**not** regenerated here (it predates this tool and is the byte-identity anchor); it still lives in
`Beck.Tests/MetricsTableGenerator.cs`.

## Mono coverage

Every table's `Mono*` fields are IBM Plex Mono, measured from the copies under `fonts/ibm-plex-mono/`
(the same TTFs `Beck.Tests` pins). The values come out byte-identical to `InterMetricsData`'s mono
section, so mono roles (class members, sequence messages) measure the same under every style.

## Committed fonts (reproducible offline)

All font binaries and their OFL license files live under `fonts/<family>/` so codegen needs no
network. Weights are the ones the `FontRoles` table resolves (400/500/600/700), plus 800 for Archivo
(brutalist's uppercase-800).

| Family | Version / source | Files | Weights measured |
|---|---|---|---|
| **Source Serif 4** | 4.005R — [adobe-fonts/source-serif release `4.005R`](https://github.com/adobe-fonts/source-serif/releases/tag/4.005R), `source-serif-4.005_Desktop.zip` → `OTF/` static instances | `SourceSerif4-Regular.otf`, `-Semibold.otf`, `-Bold.otf` | 400, 600, 700 |
| **Archivo** | [Omnibus-Type/Archivo](https://github.com/Omnibus-Type/Archivo) `fonts/ttf/` static instances, commit `b5d63988ce19d044d3e10362de730af00526b672` | `Archivo-Regular/Medium/SemiBold/Bold/ExtraBold.ttf` | 400, 500, 600, 700, 800 |
| **Shantell Sans** | 1.011 — [arrowtype/shantell-sans release `1.011`](https://github.com/arrowtype/shantell-sans/releases/tag/1.011), `shantell_sans-for-googlefonts.zip` (variable font) | `ShantellSans-VariableFont.ttf` | 400 (default instance) |
| **IBM Plex Mono** | pinned copies of the `Beck.Tests` fonts | `IBMPlexMono-Regular/Medium/Bold.ttf` | 400, 500, 700 |

### Two honest approximations (both absorbed by the `textLength` guard)

- **Source Serif 4 has no Medium (500) static.** Weight 500 resolves to the nearest available
  weight (400) via the measurer's nearest-weight rule. Editorial's role table is predominantly
  400, so the practical impact is nil.
- **Shantell Sans ships variable-only** — no static release assets, and the pinned shaping stack
  (HarfBuzzSharp 7.3.0.3 / SkiaSharp 2.88.9, matching `Beck.Skia`) exposes no axis-instancing API.
  Using a newer shaper just for codegen would diverge from the runtime measurement `Beck.Skia`
  performs. So Shantell is measured at its **default variable instance** and labelled weight 400;
  all requested weights resolve to it. This is exactly the embedded-table contract: an
  approximation the per-text `textLength` guard squeezes to fit.
