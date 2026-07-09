# Social-card background art

`social-card-art.png` (2400×1260 = 2× the 1200×630 OG canvas) is the shared background for
every social card: the site's dark dot-grid surface with a real rendered Beck diagram
(`wwwroot/examples/event-driven.beck.yaml`) ghosted along the right edge, fading out toward
the text side. `SocialCardGenerator` draws the per-page title/description over it with
Ashcroft at request/build time — only the art is baked.

## Why a baked PNG instead of rasterizing at build time

The engine's SVG styles itself through a CSS cascade — `var(--beck-*)` tokens, `color-mix()`,
class selectors — which C#-side SVG rasterizers (Svg.Skia et al.) cannot evaluate. A real
browser is the only faithful renderer, so the art is screenshot once and committed.

## Regenerating (only needed if the brand palette or hero diagram changes)

1. Render the diagram to static dark SVG exactly as the site would:
   `BeckSvg.Render(yaml, new SvgRenderOptions { Measurer = skiaOverSiteFonts, Font = spec,
   Animation = AnimationMode.Static, Theme = ThemeMode.Dark })`.
2. Open `social-card-art.template.html`, replace the `<!--SVG-->` placeholder with that SVG,
   and serve it over http (browsers block `file://` font loads).
3. In a browser at ≥1280×760: hide `#text` (it exists only to judge the text zone Ashcroft
   will use), set `document.body.style.zoom = 2`, and screenshot the `#card` element —
   that's the 2400×1260 PNG.
4. Overwrite `social-card-art.png`, then eyeball a few cards at `/og/home.png` and
   `/social-cards/docs/guides/install.png` under `dotnet run`.
