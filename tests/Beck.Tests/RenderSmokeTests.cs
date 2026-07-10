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

        var corpus = Path.Combine(AppContext.BaseDirectory, "Corpus");
        var outDir = Path.Combine(RepoRoot, "tools", "oracle", "rendered");
        Directory.CreateDirectory(outDir);

        foreach (var file in new[] { "arch-simple", "arch-grouped", "arch-flow", "arch-kitchen", "state", "class", "sample-architecture", "sequence", "seq-kitchen", "sample-sequence" })
        {
            var yaml = File.ReadAllText(Path.Combine(corpus, file + ".yaml"));
            var svg = BeckSvg.Render(yaml, options);
            Assert.StartsWith("<svg", svg);
            File.WriteAllText(Path.Combine(outDir, file + ".svg"), svg);
        }
    }

    [Fact]
    public void FitModeControlsInlineMaxWidth()
    {
        const string nodes = """
            nodes:
              - { id: a, title: Node A }
              - { id: b, title: Node B }
            edges:
              - { from: a, to: b }
            """;

        // Default (shrink): responsive — scales down inside a narrow container.
        var shrink = BeckSvg.Render(nodes);
        Assert.Contains("style=\"max-width:100%;height:auto\"", shrink);

        // fit: scroll pins natural size (the width attribute's value) so the host scrolls.
        var scroll = BeckSvg.Render("meta:\n  fit: scroll\n" + nodes);
        Assert.DoesNotContain("max-width:100%", scroll);
        Assert.Matches("style=\"max-width:[0-9.]+px;height:auto\"", scroll);
    }

    [Fact]
    public void ScrubModeDrivesAnimationsOffTheViewTimeline()
    {
        var font = TestFonts.Spec();
        using var measurer = new SkiaTextMeasurer(font);
        var yaml = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Corpus", "arch-flow.yaml"));

        var full = BeckSvg.Render(yaml, new SvgRenderOptions { Measurer = measurer, Font = font });
        var scrub = BeckSvg.Render(yaml, new SvgRenderOptions { Measurer = measurer, Font = font, Animation = AnimationMode.Scrub });

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