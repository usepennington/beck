using Beck.Rendering;
using Beck.Rendering.Skia;
using Xunit;

namespace Beck.Rendering.Tests;

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

        foreach (string file in new[] { "arch-simple", "arch-grouped", "arch-flow", "arch-kitchen", "state", "class", "sample-architecture" })
        {
            string yaml = File.ReadAllText(Path.Combine(corpus, file + ".yaml"));
            string svg = BeckSvg.Render(yaml, options);
            Assert.StartsWith("<svg", svg);
            File.WriteAllText(Path.Combine(outDir, file + ".svg"), svg);
        }
    }
}
