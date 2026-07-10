using Beck.Text;

namespace Beck.Tests;

/// <summary>The pinned Inter + IBM Plex Mono font set used by the measurement gates.</summary>
internal static class TestFonts
{
    private static string Dir => Path.Combine(AppContext.BaseDirectory, "fonts");
    private static string Path_(string file) => Path.Combine(Dir, file);

    /// <summary>A <see cref="BeckFontSpec"/> over the committed Inter/Plex TTFs.</summary>
    public static BeckFontSpec Spec() => new()
    {
        Family = "Inter",
        MonoFamily = "IBM Plex Mono",
        Files = new Dictionary<int, string>
        {
            [400] = Path_("Inter-Regular.ttf"),
            [500] = Path_("Inter-Medium.ttf"),
            [600] = Path_("Inter-SemiBold.ttf"),
            [700] = Path_("Inter-Bold.ttf"),
        },
        MonoFiles = new Dictionary<int, string>
        {
            [400] = Path_("IBMPlexMono-Regular.ttf"),
            [500] = Path_("IBMPlexMono-Medium.ttf"),
            [700] = Path_("IBMPlexMono-Bold.ttf"),
        },
    };
}