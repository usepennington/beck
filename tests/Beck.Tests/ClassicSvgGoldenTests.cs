using Beck.Rendering;
using Beck;
using Beck.Styles;
using Xunit;

namespace Beck.Tests;

/// <summary>
/// Frozen full-SVG goldens for the default (classic) look over three representative corpus diagrams
/// — an architecture diagram with groups/icons/flow, a sequence diagram, and a class diagram. Unlike
/// <see cref="StyleByteIdentityTests"/> (which only proves default == explicit Classic, a
/// self-referential check), these pin the actual rendered bytes so a future change that silently
/// alters classic output is caught. Rendered with the default managed measurer and a fixed
/// <see cref="SvgRenderOptions.IdSuffix"/> so the goldens are independent of the hash function.
/// Regenerate deliberately (only for an intended visual change) from the current engine.
/// </summary>
public sealed class ClassicSvgGoldenTests
{
    private static readonly string CorpusDir = Path.Combine(AppContext.BaseDirectory, "Corpus");
    private static readonly string GoldenDir = Path.Combine(AppContext.BaseDirectory, "Goldens", "svg");

    // A pinned suffix keeps the golden stable regardless of the content-hash algorithm.
    private const string PinnedSuffix = "cla551c0";

    public static IEnumerable<object[]> Diagrams() => new[]
    {
        new object[] { "arch-kitchen" },
        new object[] { "seq-kitchen" },
        new object[] { "class" },
    };

    [Theory]
    [MemberData(nameof(Diagrams))]
    public void ClassicSvg_MatchesGolden(string name)
    {
        string yaml = File.ReadAllText(Path.Combine(CorpusDir, name + ".yaml"));
        string actual = BeckSvg.Render(yaml, new SvgRenderOptions { IdSuffix = PinnedSuffix });
        string golden = File.ReadAllText(Path.Combine(GoldenDir, name + ".svg"));
        Assert.Equal(golden, actual);
    }

    // A single frozen golden per non-classic built-in style (Phase 3 policy: full corpus for
    // classic only, one representative golden + the StyleSmokeTests invariants for the rest).
    // Each style pins its own IdSuffix so the golden is independent of the content-hash algorithm.
    public static IEnumerable<object[]> Styles() => StyleCases.Select(c => new object[] { c.Golden });

    private static readonly (string Golden, string Suffix, BeckStyle Style)[] StyleCases =
    {
        ("minimal",   "min1ma1c", MinimalStyle.Instance),
        ("terminal",  "term1na1", TerminalStyle.Instance),
        ("blueprint", "b1uepr1n", BlueprintStyle.Instance),
        ("glow",      "g10wg10w", GlowStyle.Instance),
        ("brutalist", "brut4115", BrutalistStyle.Instance),
        ("sketch",    "sk3tchg0", SketchStyle.Instance),
        ("extrude",   "extrud30", ExtrudeStyle.Instance),
        ("circuit",   "c1rcu1t0", CircuitStyle.Instance),
    };

    [Theory]
    [MemberData(nameof(Styles))]
    public void StyleSvg_MatchesGolden(string golden)
    {
        var c = StyleCases.Single(s => s.Golden == golden);
        string yaml = File.ReadAllText(Path.Combine(CorpusDir, "arch-kitchen.yaml"));
        string actual = BeckSvg.Render(yaml, new SvgRenderOptions { IdSuffix = c.Suffix, Style = c.Style });
        Assert.Equal(File.ReadAllText(Path.Combine(GoldenDir, golden + ".svg")), actual);
    }

    /// <summary>
    /// Rewrites every SVG golden in the SOURCE tree from the current engine. Guarded by
    /// <c>BECK_REGEN=1</c>, so a normal run skips it. Only for an intentional visual change —
    /// diff the result before committing.
    /// </summary>
    [Fact]
    public void Regenerate()
    {
        if (Environment.GetEnvironmentVariable("BECK_REGEN") != "1") return;
        string srcGoldens = Path.Combine(SourceDir(), "Goldens", "svg");
        foreach (string name in new[] { "arch-kitchen", "seq-kitchen", "class" })
        {
            string yaml = File.ReadAllText(Path.Combine(CorpusDir, name + ".yaml"));
            File.WriteAllText(Path.Combine(srcGoldens, name + ".svg"),
                BeckSvg.Render(yaml, new SvgRenderOptions { IdSuffix = PinnedSuffix }));
        }
        string kitchen = File.ReadAllText(Path.Combine(CorpusDir, "arch-kitchen.yaml"));
        foreach (var c in StyleCases)
            File.WriteAllText(Path.Combine(srcGoldens, c.Golden + ".svg"),
                BeckSvg.Render(kitchen, new SvgRenderOptions { IdSuffix = c.Suffix, Style = c.Style }));
    }

    private static string SourceDir([System.Runtime.CompilerServices.CallerFilePath] string self = "") =>
        Path.GetDirectoryName(self)!;
}
