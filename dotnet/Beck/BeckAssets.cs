namespace Beck;

/// <summary>
/// Locations and snippets for wiring the Beck client into an ASP.NET Core host.
/// The package serves the prebuilt client as a static web asset, so a consumer
/// only needs to include the script once in their page head.
/// </summary>
public static class BeckAssets
{
    /// <summary>
    /// The static web asset path the Beck client is served at. Root-relative (leading
    /// <c>/</c>) so it resolves the same from any page depth — a page at
    /// <c>/reference/architecture</c> still loads <c>/_content/Beck/beck.global.js</c>,
    /// not <c>/reference/_content/…</c>. Hosts that deploy under a sub-path (e.g.
    /// Pennington's base-URL rewriter) prefix root-relative asset paths automatically,
    /// so this also survives sub-path deploys — a bare relative path would not.
    /// </summary>
    public const string ScriptPath = "/_content/Beck/beck.global.js";

    /// <summary>
    /// A ready-to-inject script tag for the page head (e.g. via a Pennington
    /// <c>DocSiteOptions.AdditionalHtmlHeadContent</c>). Uses the root-relative
    /// <see cref="ScriptPath"/> so it loads correctly from any route.
    /// </summary>
    public static string ScriptTag { get; } = $"<script src=\"{ScriptPath}\" defer></script>";
}
