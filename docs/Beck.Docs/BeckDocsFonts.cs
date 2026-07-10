using Beck.Text;

namespace Beck.Docs;

/// <summary>
/// The site's brand fonts — IBM Plex Sans + IBM Plex Mono — as a
/// <see cref="BeckFontSpec"/>. The build-time SVG renderer measures text with these
/// exact files (shipped under <c>fonts/</c>, copied next to the assembly), so card
/// sizing matches what the browser lays out from the Google-Fonts-loaded families
/// declared in <see cref="BrandStyling"/>.
/// </summary>
internal static class BeckDocsFonts
{
    private static string Dir => Path.Combine(AppContext.BaseDirectory, "fonts");
    private static string F(string file) => Path.Combine(Dir, file);

    public static BeckFontSpec Spec() => new()
    {
        Family = "IBM Plex Sans",
        MonoFamily = "IBM Plex Mono",
        Files = new Dictionary<int, string>
        {
            [400] = F("IBMPlexSans-Regular.ttf"),
            [500] = F("IBMPlexSans-Medium.ttf"),
            [600] = F("IBMPlexSans-SemiBold.ttf"),
            [700] = F("IBMPlexSans-Bold.ttf"),
        },
        MonoFiles = new Dictionary<int, string>
        {
            [400] = F("IBMPlexMono-Regular.ttf"),
            [500] = F("IBMPlexMono-Medium.ttf"),
            [700] = F("IBMPlexMono-Bold.ttf"),
        },
    };
}