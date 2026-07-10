using System.Text.RegularExpressions;
using Xunit;

namespace Beck.Tests;

/// <summary>
/// Targeted assertions for the <see cref="StyleArtwork.Metro"/> transit artwork seam (station-dot
/// terminals at edge endpoints) and the <see cref="PacketGlyph.Train"/> capsule glyph. No built-in
/// style ships them any more; they remain engine seams a custom style can compose, exercised here
/// through a Classic-derived custom style. These pin the structural contract beyond the generic
/// <see cref="StyleSmokeTests"/> invariants.
/// </summary>
public sealed class MetroArtworkTests
{
    private static readonly string _corpusDir = Path.Combine(AppContext.BaseDirectory, "Corpus");

    // Classic + the transit seams: station dots at edge anchors, train-capsule packets, and
    // width-sane markers for thick lines.
    private static readonly BeckStyle _transit = BeckStyle.Classic with
    {
        Name = "custom-transit",
        Artwork = StyleArtwork.Metro,
        Geometry = BeckStyle.Classic.Geometry with { StationRadius = 4.5 },
        Motion = BeckStyle.Classic.Motion with { PacketGlyph = PacketGlyph.Train },
        Edges = StyleEdges.Classic with { MarkerScaleToWidth = true },
    };

    // The BASE edge path only (class "beck-edge beck-edge--<kind>") — deliberately NOT any sibling
    // "beck-edge-overlay" path, which also starts with "beck-edge" and would double the count.
    private static readonly Regex _edgePath = new("<path class=\"beck-edge beck-edge--[^\"]*\"", RegexOptions.Compiled);
    private static readonly Regex _station = new("class=\"beck-station\"", RegexOptions.Compiled);

    // Every architecture edge is one continuous <path class="beck-edge ..."> and drops exactly two
    // station dots (one at each anchor endpoint), so the station count is 2× the edge-path count.
    [Fact]
    public void TransitArtwork_DropsTwoStationsPerEdge()
    {
        var yaml = File.ReadAllText(Path.Combine(_corpusDir, "arch-kitchen.yaml"));
        var svg = BeckSvg.Render(yaml, new SvgRenderOptions { Style = _transit });

        var edges = _edgePath.Matches(svg).Count;
        var stations = _station.Matches(svg).Count;
        Assert.True(edges > 0, "expected at least one edge in arch-kitchen");
        Assert.Equal(2 * edges, stations);
    }

    // Classic (no station artwork) emits no station dots — the seam is byte-inert off.
    [Fact]
    public void Classic_EmitsNoStations()
    {
        var yaml = File.ReadAllText(Path.Combine(_corpusDir, "arch-kitchen.yaml"));
        var svg = BeckSvg.Render(yaml, new SvgRenderOptions { Style = BeckStyle.Classic });
        Assert.DoesNotContain("beck-station", svg);
    }

    // The train packet is the one glyph that rotates with the path: offset-rotate:auto (every other
    // packet glyph pins offset-rotate:0deg upright). Its capsule is a rounded <rect> riding offset-path.
    [Fact]
    public void TrainPacket_RotatesWithPath()
    {
        var yaml = File.ReadAllText(Path.Combine(_corpusDir, "arch-kitchen.yaml"));
        var svg = BeckSvg.Render(yaml, new SvgRenderOptions { Style = _transit });

        // The capsule is a rounded <rect class="beck-packet ..."> riding offset-path with auto rotation.
        // (Packet *labels* legitimately stay offset-rotate:0deg upright, so we don't assert its absence.)
        Assert.Contains("offset-rotate:auto", svg);
        Assert.Matches(new Regex("<rect class=\"beck-packet[^>]*offset-rotate:auto"), svg);
    }

    // MarkerScaleToWidth switches markers to an absolute userSpaceOnUse size that grows only
    // sub-linearly with the line width, so thick-line arrowheads stay sane instead of blowing up
    // into strokeWidth-scaled blobs. Classic (no scale-to-width) never emits it.
    [Fact]
    public void MarkerScaleToWidth_SanesThickLineArrowheads()
    {
        var yaml = File.ReadAllText(Path.Combine(_corpusDir, "arch-kitchen.yaml"));
        var svg = BeckSvg.Render(yaml, new SvgRenderOptions { Style = _transit });
        Assert.Contains("markerUnits=\"userSpaceOnUse\"", svg);

        // 6 (classic arrow base) · √EdgeStroke — grown with the line yet nowhere near the 6·w blob a
        // naive strokeWidth scale would produce.
        var widths = new Regex("markerWidth=\"([^\"]*)\"").Matches(svg)
            .Select(m => double.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture)).ToList();
        Assert.NotEmpty(widths);
        Assert.All(widths, w => Assert.InRange(w, 6.0, 20.0));

        Assert.DoesNotContain("markerUnits", BeckSvg.Render(yaml, new SvgRenderOptions { Style = BeckStyle.Classic }));
    }
}