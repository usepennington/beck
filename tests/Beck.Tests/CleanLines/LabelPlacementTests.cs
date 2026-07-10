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