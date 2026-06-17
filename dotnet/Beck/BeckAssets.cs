namespace Beck;

/// <summary>
/// Locations and snippets for wiring the Beck client into an ASP.NET Core host.
/// The package serves the prebuilt client as a static web asset, so a consumer
/// only needs to include the script once in their page head.
/// </summary>
public static class BeckAssets
{
    /// <summary>The static web asset path the Beck client is served at.</summary>
    public const string ScriptPath = "_content/Beck/beck.global.js";

    /// <summary>
    /// A ready-to-inject script tag for the page head (e.g. via a Pennington
    /// <c>DocSiteOptions.AdditionalHtmlHeadContent</c>). Relative so it survives
    /// sub-path deploys.
    /// </summary>
    public static string ScriptTag { get; } = $"<script src=\"{ScriptPath}\" defer></script>";
}
