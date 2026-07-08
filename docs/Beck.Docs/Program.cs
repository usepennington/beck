using Beck.Docs;
using Beck.Docs.Components;
using Beck.Docs.Components.Reference;
using Mdazor;
using Pennington.FrontMatter;
using Pennington.Infrastructure;
using Pennington.MonorailCss;
using Pennington.TreeSitter;

var builder = WebApplication.CreateBuilder(args);

// Bare Pennington host: register the markdown content pipeline and point one
// source at Content/. DocFrontMatter is the core front-matter shape (title,
// description, order, sectionLabel, uid, tags) for docs on a bare host.
builder.Services.AddPennington(penn =>
{
    penn.SiteTitle = "Beck";
    penn.ContentRootPath = "Content";

    penn.AddMarkdownContent<DocFrontMatter>(md =>
    {
        md.ContentPath = "Content";
        md.BasePageUrl = "/";
    });

    penn.AddLlmsTxt();
});

// The shared C# Beck renderer: one Skia measurer over the site's fonts, reused by the
// build-time fence preprocessor and the live playground (see BeckDiagramRenderer).
builder.Services.AddSingleton<BeckDiagramRenderer>();

// Render ```beck fences to static, self-animating inline SVG at build time via the
// pure-C# engine (the-bad-idea.md, M10) — content diagrams need no client JS. Priority
// 500 beats the tree-sitter source-embed preprocessor; every other fence is deferred.
builder.Services.AddSingleton<Pennington.Markdown.Extensions.ICodeBlockPreprocessor, BeckSvgPreprocessor>();

// MonorailCSS: scans compiled IL + watched source for utility-class literals and serves
// them from /styles.css. The semantic palette maps the brand green to `primary`, slate to
// `base`, and a violet secondary to `accent`. MonorailCSS only emits ramps that a utility
// class actually references: --color-primary-* and --color-base-* (the configured slots) plus
// the named ramps used on the site (emerald, green, amber, red, blue, sky, …). The ramps are
// NOT flipped under .dark — dark mode picks a different shade per utility. The Beck engine
// reads these via its --beck-* bridge, so primary/base-derived accents adopt this palette for
// free; note the `accent` slot is surfaced as --color-accent-* by NO emitted ramp here (and
// the configured violet is not emitted as --color-violet-* either), so --beck-info falls back
// to its literal violet unless BrandStyling remaps it onto an emitted ramp.
builder.Services.AddMonorailCss(_ => new MonorailCssOptions
{
    ColorScheme = new NamedColorScheme
    {
        PrimaryColorName = ColorName.Emerald, // brand green (#1f9d63-ish)
        AccentColorName = ColorName.Violet,   // secondary pop
        BaseColorName = ColorName.Slate,      // neutral grays
    },
    ExtraStyles = BrandStyling.ExtraStyles,
});

// `:symbol` source embeds — pull real source out of files by path. ContentRoot is
// the project root so fence bodies resolve against wwwroot/examples/*.beck.yaml etc.
builder.Services.AddTreeSitter(treeSitter =>
{
    treeSitter.ContentRoot = ".";
});

// Blazor static SSR (unlocks MapRazorComponents<App>() and @page routing so we own the
// entire chrome) PLUS interactive-WebAssembly components: the /playground island lives in
// the Beck.Docs.Client project and renders diagrams entirely in the browser via the C#
// engine (compiled to WASM) — no server, no client JS engine. Every other page stays static
// SSR. Because it is client-side, the whole site still deploys as static files.
builder.Services.AddRazorComponents()
    .AddInteractiveWebAssemblyComponents();

// The playground island injects HttpClient (to fetch example .beck.yaml files) — it must
// resolve during server prerender too, even though the fetch only runs after WASM boots.
builder.Services.AddScoped(_ => new HttpClient());

// Mdazor component registry: Razor components embeddable in Markdown. BeckGallery reflects over
// the Beck.Authoring token enums and renders a live preview of every value, so the reference
// pages auto-track the schema instead of hand-maintaining tables. Used by Content/docs/reference.
builder.Services.AddMdazorComponent<BeckGallery>();
builder.Services.AddMdazorComponent<IconGallery>();

var app = builder.Build();

// UsePennington first: it registers redirect / sitemap / llms.txt endpoints that the
// Blazor catch-all (@page "/{*Path}") would otherwise swallow.
app.UsePennington();

// Mount /styles.css. Like UsePennington, this must run before MapRazorComponents so the
// Blazor catch-all (@page "/{*Path}") doesn't swallow the stylesheet endpoint.
app.UseMonorailCss();

app.UseAntiforgery();

// Serve static web assets: the fonts/JS/CSS and, crucially, the WASM playground's
// _framework bundle. Bare hosts must opt in; DocSite hosts get this for free.
app.MapStaticAssets();

app.MapRazorComponents<App>()
    // The /playground route is a static-SSR page in this host; its interactive UI is a
    // WebAssembly island (Beck.Docs.Client) bundled via the project reference. No routable
    // components live in the client assembly, so no AddAdditionalAssemblies is needed.
    .AddInteractiveWebAssemblyRenderMode();

// `dotnet run` serves live; `dotnet run -- build [baseUrl] [outDir]` emits static HTML.
await app.RunOrBuildAsync(args);
