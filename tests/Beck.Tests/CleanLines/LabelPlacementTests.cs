using System.Globalization;
using System.Text.RegularExpressions;
using Xunit;

namespace Beck.Tests.CleanLines;

/// <summary>
/// Edge labels must never be drawn across another edge when a placement exists that isn't.
/// Both sides of a segment routinely score negative — above lands on a parallel neighbour, below
/// noses into a card's bounding-box corner — and the placer used to compare them by a single
/// minimum clearance. A flat -4 for "on a line" then looked worse than -3 for "3px into a rounded
/// card's empty corner" by less than the tie tolerance, so the symmetric above/below tie-break
/// kept whichever was tried first. Which is always above.
/// </summary>
public sealed class LabelPlacementTests
{
    // Two states with a transition each way: the rungs sit 20px apart, so a label offset above the
    // lower rung lands squarely on the upper one.
    private const string CircuitBreaker = """
        type: state
        meta:
          title: Circuit Breaker
          direction: LR
        states:
          - { id: closed, title: Closed, subtitle: calls pass through, accent: success }
          - { id: open, title: Open, subtitle: calls fail fast, accent: danger }
          - { id: half, title: Half-Open, subtitle: one probe call, accent: warn }
        transitions:
          - { from: "[*]", to: closed }
          - { from: closed, to: closed, label: success }
          - { from: closed, to: open, label: trip threshold }
          - { from: open, to: open, label: fail fast }
          - { from: open, to: half, label: cooldown elapsed }
          - { from: half, to: closed, label: probe ok }
          - { from: half, to: open, label: probe fails }
        """;

    private static readonly Regex _labelRe = new(
        """<text class="beck-edge-label"[^>]*\sy="(?<y>[-\d.]+)"[^>]*>(?<text>[^<]*)</text>""",
        RegexOptions.Compiled);

    private static double LabelY(string svg, string text)
    {
        foreach (Match m in _labelRe.Matches(svg))
        {
            if (m.Groups["text"].Value == text)
            {
                return double.Parse(m.Groups["y"].Value, CultureInfo.InvariantCulture);
            }
        }

        throw new Xunit.Sdk.XunitException($"no edge label '{text}' in the rendered SVG");
    }

    private const string ShortGap = """
        type: architecture
        meta:
          direction: LR
          spacing: { rank: 56, node: 32 }
        nodes:
          - { id: lir, title: LIR, icon: stream }
          - id: emit
            title: Emitter
            icon: function
            items:
              - System.Reflection.Metadata
              - writes the IL itself
          - { id: dll, title: Assembly, subtitle: .dll, kind: external, icon: file }
          - { id: cfg, title: Runtime config, subtitle: what the host reads, kind: external, icon: file }
          - { id: pdb, title: Debug database, subtitle: lines of Mew, kind: external, icon: file }
        edges:
          - { from: lir, to: emit, label: write }
          - { from: emit, to: dll }
          - { from: emit, to: cfg }
          - { from: emit, to: pdb }
        """;

    [Fact]
    public void ShortGap_LabelSitsBetweenTheCards()
    {
        // A 56px rank gap holds "write" with room to spare, but only at the gap's middle. The
        // router splits the straight run at its midpoint, and the placer used to sample each half
        // at one point near its far end, so both candidates nosed into a card and the label was
        // drawn across LIR's border. The middle of the run must be a candidate.
        var svg = BeckSvg.Render(ShortGap, new SvgRenderOptions { Animation = AnimationMode.Static });

        var label = Regex.Match(svg, """<text class="beck-edge-label"[^>]*\sx="(?<x>[-\d.]+)"[^>]*textLength="(?<w>[-\d.]+)"[^>]*>write</text>""");
        Assert.True(label.Success, "no 'write' label in the rendered SVG");
        double x = double.Parse(label.Groups["x"].Value, CultureInfo.InvariantCulture);
        double w = double.Parse(label.Groups["w"].Value, CultureInfo.InvariantCulture);

        static double NodeX(string svg, string id) => double.Parse(
            Regex.Match(svg, $"""data-node="{id}" transform="translate\((?<x>[-\d.]+),""").Groups["x"].Value, CultureInfo.InvariantCulture);
        static double NodeW(string svg, string id) => double.Parse(
            Regex.Match(svg, @"data-node=""" + id + @"""[^>]*>.*?\swidth=""(?<w>[-\d.]+)""", RegexOptions.Singleline).Groups["w"].Value, CultureInfo.InvariantCulture);
        var gapLeft = NodeX(svg, "lir") + NodeW(svg, "lir");
        var gapRight = NodeX(svg, "emit");

        Assert.True(x - w / 2 > gapLeft + 2 && x + w / 2 < gapRight - 2,
            $"'write' spans {x - w / 2:0.#}..{x + w / 2:0.#} but the gap between the cards is {gapLeft:0.#}..{gapRight:0.#}");
    }

    [Fact]
    public void ParallelTransition_LabelClearsTheOppositeRung()
    {
        var svg = BeckSvg.Render(CircuitBreaker, new SvgRenderOptions { Animation = AnimationMode.Static });

        // open→half runs at y=91.5; half→open runs back at y=71.5. "cooldown elapsed" belongs to the
        // lower rung, so it must sit below it — not in the 20px slot between them, on top of the
        // "probe fails" rung.
        var cooldown = LabelY(svg, "cooldown elapsed");
        Assert.True(cooldown > 91.5,
            $"'cooldown elapsed' sits at y={cooldown}, above its own rung (91.5) and across the "
            + "half→open rung at 71.5; it should be placed below.");

        // Its counterpart labels the upper rung and correctly sits above it.
        var probeFails = LabelY(svg, "probe fails");
        Assert.True(probeFails < 71.5, $"'probe fails' sits at y={probeFails}, expected above its rung (71.5)");

        // The two labels end up on opposite sides of the pair, not stacked in the gap between them.
        Assert.True(cooldown - probeFails > 40,
            $"labels are crowded into the gap between the rungs: probe fails y={probeFails}, cooldown y={cooldown}");
    }
}