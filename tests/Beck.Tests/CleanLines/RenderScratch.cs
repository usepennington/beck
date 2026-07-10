using Beck.Rendering;
using Xunit;

namespace Beck.Tests.CleanLines;

/// <summary>Debugging affordance: dump corpus diagrams to SVG for eyeballing. BECK_SVG_OUT=&lt;dir&gt;.</summary>
public sealed class RenderScratch
{
    [Fact]
    public void DumpCorpusSvg()
    {
        if (Environment.GetEnvironmentVariable("BECK_SVG_OUT") is not string outDir) return;
        Directory.CreateDirectory(outDir);
        string corpus = Path.Combine(AppContext.BaseDirectory, "Corpus");
        foreach (string name in new[] { "arch-flow", "arch-kitchen", "class" })
        {
            string yaml = File.ReadAllText(Path.Combine(corpus, name + ".yaml"));
            string svg = BeckSvg.Render(yaml, new SvgRenderOptions { Animation = AnimationMode.Static });
            File.WriteAllText(Path.Combine(outDir, name + ".svg"), svg);
        }
        foreach (var (name, yaml) in new[] { ("three-into-one", CleanLineTests.ThreeIntoOne), ("serve-and-build", CleanLineTests.ServeAndBuild) })
            File.WriteAllText(Path.Combine(outDir, name + ".svg"),
                BeckSvg.Render(yaml, new SvgRenderOptions { Animation = AnimationMode.Static }));
        if (Environment.GetEnvironmentVariable("BECK_YAML") is string extra && File.Exists(extra))
            File.WriteAllText(Path.Combine(outDir, "extra.svg"),
                BeckSvg.Render(File.ReadAllText(extra), new SvgRenderOptions { Animation = AnimationMode.Static }));
    }
}
