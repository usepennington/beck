using Beck.Docs.Components;
using Pennington.FrontMatter;
using Pennington.Infrastructure;
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
});

// `:symbol` source embeds — pull real source out of files by path. ContentRoot is
// the project root so fence bodies resolve against wwwroot/examples/*.beck.yaml etc.
builder.Services.AddTreeSitter(treeSitter =>
{
    treeSitter.ContentRoot = ".";
});

// Blazor static SSR — unlocks MapRazorComponents<App>() and @page routing so we
// own the entire chrome (custom landing, playground, sidebar) instead of a template.
builder.Services.AddRazorComponents();

var app = builder.Build();

// UsePennington first: it registers redirect / sitemap / llms.txt endpoints that the
// Blazor catch-all (@page "/{*Path}") would otherwise swallow.
app.UsePennington();
app.UseAntiforgery();

// Serve RCL static web assets (Beck's _content/Beck/beck.global.js). Bare hosts must
// opt in; DocSite hosts get this for free.
app.MapStaticAssets();

app.MapRazorComponents<App>();

// `dotnet run` serves live; `dotnet run -- build [baseUrl] [outDir]` emits static HTML.
await app.RunOrBuildAsync(args);
