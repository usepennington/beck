using Beck.Rendering;
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
}
