using System.Text.RegularExpressions;
using Beck.Rendering;
using Xunit;

namespace Beck.Tests;

/// <summary>
/// Targeted assertions for the <c>metro</c> style's <see cref="StyleArtwork.Metro"/> artwork: the
/// station-dot terminals emitted at edge endpoints and the train-capsule packet glyph. These pin the
/// artwork's structural contract beyond the generic <see cref="StyleSmokeTests"/> invariants.
/// </summary>
public sealed class MetroArtworkTests
{
    private static readonly string CorpusDir = Path.Combine(AppContext.BaseDirectory, "Corpus");

    private static readonly Regex EdgePath = new("<path class=\"beck-edge[^\"]*\"", RegexOptions.Compiled);
    private static readonly Regex Station = new("class=\"beck-station\"", RegexOptions.Compiled);

    // Every architecture edge is one continuous <path class="beck-edge ..."> and drops exactly two
    // station dots (one at each anchor endpoint), so the station count is 2× the edge-path count.
    [Fact]
    public void Metro_DropsTwoStationsPerEdge()
    {
        string yaml = File.ReadAllText(Path.Combine(CorpusDir, "arch-kitchen.yaml"));
        string svg = BeckSvg.Render(yaml, new SvgRenderOptions { Style = MetroStyle.Instance });

        int edges = EdgePath.Matches(svg).Count;
        int stations = Station.Matches(svg).Count;
        Assert.True(edges > 0, "expected at least one edge in arch-kitchen");
        Assert.Equal(2 * edges, stations);
    }

    // Classic (no station artwork) emits no station dots — the seam is byte-inert off metro.
    [Fact]
    public void Classic_EmitsNoStations()
    {
        string yaml = File.ReadAllText(Path.Combine(CorpusDir, "arch-kitchen.yaml"));
        string svg = BeckSvg.Render(yaml, new SvgRenderOptions { Style = BeckStyle.Classic });
        Assert.DoesNotContain("beck-station", svg);
    }

    // The train packet is the one glyph that rotates with the path: offset-rotate:auto (every other
    // packet glyph pins offset-rotate:0deg upright). Its capsule is a rounded <rect> riding offset-path.
    [Fact]
    public void Metro_TrainPacketRotatesWithPath()
    {
        string yaml = File.ReadAllText(Path.Combine(CorpusDir, "arch-kitchen.yaml"));
        string svg = BeckSvg.Render(yaml, new SvgRenderOptions { Style = MetroStyle.Instance });

        // The capsule is a rounded <rect class="beck-packet ..."> riding offset-path with auto rotation.
        // (Packet *labels* legitimately stay offset-rotate:0deg upright, so we don't assert its absence.)
        Assert.Contains("offset-rotate:auto", svg);
        Assert.Matches(new Regex("<rect class=\"beck-packet[^>]*offset-rotate:auto"), svg);
    }
}
