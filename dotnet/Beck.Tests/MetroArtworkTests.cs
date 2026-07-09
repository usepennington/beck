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

    // The BASE edge path only (class "beck-edge beck-edge--<kind>") — deliberately NOT the sibling
    // "beck-edge-overlay" train path, which also starts with "beck-edge" and would double the count.
    private static readonly Regex EdgePath = new("<path class=\"beck-edge beck-edge--[^\"]*\"", RegexOptions.Compiled);
    private static readonly Regex Station = new("class=\"beck-station\"", RegexOptions.Compiled);
    private static readonly Regex BaseEdgeD = new("<path class=\"beck-edge beck-edge--[^\"]*\" d=\"([^\"]*)\"", RegexOptions.Compiled);
    private static readonly Regex OverlayD = new("<path class=\"beck-edge-overlay [^\"]*\" d=\"([^\"]*)\"", RegexOptions.Compiled);
    private static List<string> Matches(Regex r, string s) => r.Matches(s).Select(m => m.Groups[1].Value).ToList();

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

    // Metro's ambient identity motion (mock 1i): a WHITE TRAIN Comet overlay on EVERY architecture edge —
    // a short round-capped dot (width 3.5, CometDash 3) sharing each base edge's exact d, mirroring the
    // mock's `stroke:#f8fafc;stroke-width:3.5;stroke-dasharray:3 300;animation:ptd 3s linear infinite`. The
    // overlay carries no palette, so its hue is the single --beck-edge-overlay fallback (metro points it at
    // the station-fill white), independent of the per-line BaseColorPalette beneath. Compiled onto a 3s
    // shared-cycle loop, reduced-motion guarded, no delay chain.
    [Fact]
    public void Metro_WhiteTrainOverlayOnEveryLine_SharesD_NoDelayChain()
    {
        string svg = BeckSvg.Render(File.ReadAllText(Path.Combine(CorpusDir, "arch-kitchen.yaml")),
            new SvgRenderOptions { Style = MetroStyle.Instance });

        var baseD = Matches(BaseEdgeD, svg);
        var overlayD = Matches(OverlayD, svg);
        Assert.NotEmpty(overlayD);
        Assert.Equal(baseD.Count, overlayD.Count);
        foreach (string od in overlayD) Assert.Contains(od, baseD);   // shares the edge's exact d, never a split

        // A width-3.5 round-capped white train on the single overlay fallback hue (NOT the per-line palette),
        // a 3px lit dash.
        Assert.Contains("stroke:var(--beck-edge-overlay, var(--beck-accent));stroke-width:3.5;stroke-linecap:round;", svg);
        Assert.Contains("stroke-dasharray:3 ", svg);

        // Compiled 3s shared-cycle loop, glides linear (a train, not a tick), guarded, no delay chain.
        Assert.Matches(@"\.beo0-[0-9a-z]+\{animation:kbeo0-[0-9a-z]+ 3s linear infinite;\}", svg);
        Assert.Contains("@keyframes kbeo0-", svg);
        Assert.Contains("@media (prefers-reduced-motion:no-preference)", svg);
        Assert.DoesNotContain("animation-delay", svg);
    }

    // The same white train rides sequence messages too (the mock runs a train on every hop), sharing each
    // message path's d — the overlay is the ambient identity on every line, not just architecture edges.
    [Fact]
    public void Metro_WhiteTrainOverlayOnSequenceMessages()
    {
        string svg = BeckSvg.Render(File.ReadAllText(Path.Combine(CorpusDir, "seq-kitchen.yaml")),
            new SvgRenderOptions { Style = MetroStyle.Instance });
        var baseD = Matches(BaseEdgeD, svg);
        var overlayD = Matches(OverlayD, svg);
        Assert.NotEmpty(overlayD);
        foreach (string od in overlayD) Assert.Contains(od, baseD);
    }

    // The metro jury gripe: a thick 5px transit line blows a strokeWidth-scaled arrowhead into a blob.
    // MarkerScaleToWidth switches markers to an absolute userSpaceOnUse size that grows only sub-linearly
    // with the line width, so the heads stay sane. Classic (thin edges, no scale-to-width) never emits it.
    [Fact]
    public void Metro_MarkerScaleToWidth_SanesThickLineArrowheads()
    {
        string yaml = File.ReadAllText(Path.Combine(CorpusDir, "arch-kitchen.yaml"));
        string svg = BeckSvg.Render(yaml, new SvgRenderOptions { Style = MetroStyle.Instance });
        Assert.Contains("markerUnits=\"userSpaceOnUse\"", svg);

        // 6 (classic arrow base) · √5 (EdgeStroke) ≈ 13.42 — grown for the thick line yet nowhere near the
        // 6·5 = 30px blob a naive strokeWidth scale would produce.
        var widths = new Regex("markerWidth=\"([^\"]*)\"").Matches(svg)
            .Select(m => double.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture)).ToList();
        Assert.NotEmpty(widths);
        Assert.All(widths, w => Assert.InRange(w, 6.0, 20.0));

        Assert.DoesNotContain("markerUnits", BeckSvg.Render(yaml, new SvgRenderOptions { Style = BeckStyle.Classic }));
    }
}
