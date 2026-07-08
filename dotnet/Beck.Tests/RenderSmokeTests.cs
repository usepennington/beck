using Beck.Rendering;
using Beck.Skia;
using Xunit;

namespace Beck.Tests;

/// <summary>Renders corpus diagrams to SVG files for visual inspection (tools/oracle/rendered).</summary>
public sealed class RenderSmokeTests
{
    private static string RepoRoot =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    [Fact]
    public void RenderCorpusToFiles()
    {
        var font = TestFonts.Spec();
        using var measurer = new SkiaTextMeasurer(font);
        var options = new SvgRenderOptions { Measurer = measurer, Font = font };

        string corpus = Path.Combine(AppContext.BaseDirectory, "Corpus");
        string outDir = Path.Combine(RepoRoot, "tools", "oracle", "rendered");
        Directory.CreateDirectory(outDir);

        foreach (string file in new[] { "arch-simple", "arch-grouped", "arch-flow", "arch-kitchen", "state", "class", "sample-architecture", "sequence", "seq-kitchen", "sample-sequence" })
        {
            string yaml = File.ReadAllText(Path.Combine(corpus, file + ".yaml"));
            string svg = BeckSvg.Render(yaml, options);
            Assert.StartsWith("<svg", svg);
            File.WriteAllText(Path.Combine(outDir, file + ".svg"), svg);
        }
    }

    [Fact]
    public void ScrubModeDrivesAnimationsOffTheViewTimeline()
    {
        var font = TestFonts.Spec();
        using var measurer = new SkiaTextMeasurer(font);
        string yaml = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Corpus", "arch-flow.yaml"));

        string full = BeckSvg.Render(yaml, new SvgRenderOptions { Measurer = measurer, Font = font });
        string scrub = BeckSvg.Render(yaml, new SvgRenderOptions { Measurer = measurer, Font = font, Animation = AnimationMode.Scrub });

        // Full loops on a time cycle; Scrub swaps that for `auto` + a scroll timeline.
        Assert.Contains("linear infinite;", full);
        Assert.DoesNotContain("animation-timeline", full);
        Assert.Contains("auto linear both", scrub);
        Assert.Contains("animation-timeline:view(block 90% 10%)", scrub);
        // Same keyframes either way (the choreography is identical, only its clock differs).
        Assert.Contains("@keyframes kp0-", full);
        Assert.Contains("@keyframes kp0-", scrub);
    }
}
