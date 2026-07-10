using Beck.Animate;
using Beck.Svg;
using Xunit;

namespace Beck.Tests;

/// <summary>
/// <see cref="PathLength.Of"/> is the C# stand-in for <c>getTotalLength()</c> that feeds flow hop
/// durations and trail dash lengths. These pin that it parses real SVG path data — commands glued to
/// their first coordinate ("M106 80Q106.5 128 …") and comma/space separators — so a bowed path
/// (any BowAmplitude&gt;0 style, e.g. sketch) measures its true length instead of collapsing to 0.
/// </summary>
public sealed class PathLengthTests
{
    [Fact]
    public void StraightLine_MeasuresExactChord()
    {
        Assert.Equal(10, PathLength.Of("M0 0L10 0"), 3);
        Assert.Equal(10, PathLength.Of("M0 0 10 0"), 3);   // implicit L after M
        Assert.Equal(10, PathLength.Of("M0,0L10,0"), 3);   // comma separators
    }

    // Regression: a glued quadratic ("M0 0Q5 5 10 0") measured 0 because the old tokenizer split on
    // whitespace and matched exact "M"/"Q" tokens, so "M0" and "Q5" never matched. It must now measure
    // a real length strictly longer than the 10px chord between its endpoints.
    [Fact]
    public void GluedQuadratic_MeasuresNonZeroLengthAboveChord()
    {
        var len = PathLength.Of("M0 0Q5 5 10 0");
        Assert.True(len > 10, $"expected a bowed length above the 10px chord, got {len}");
    }

    // A bow produced by the shaping seam (the actual path the sketch style emits) measures a positive
    // length above its straight-line distance — the fix the flow schedule/trails depend on.
    [Fact]
    public void BowedPath_FromShaping_MeasuresPositiveLength()
    {
        // A single straight run of length 100 bowed sideways by amplitude 8.
        var d = Shaping.BowLine(0, 0, 100, 0, 8, "seed-1");
        Assert.StartsWith("M", d);
        Assert.Contains("Q", d);

        var len = PathLength.Of(d);
        Assert.True(len > 100, $"a bowed run must be longer than its 100px chord, got {len}");
        Assert.True(len < 130, $"a low-amplitude bow must stay near the chord, got {len}");

        // Deterministic: same string in ⇒ same measured length.
        Assert.Equal(len, PathLength.Of(d), 6);
    }

    // End to end: a BowAmplitude>0 style produces base edge paths that all measure non-zero (the bug
    // sent every bowed edge to length 0, collapsing hop durations to the floor and making trails
    // permanently visible via stroke-dasharray:0).
    [Fact]
    public void BowedStyle_EdgePaths_AllMeasureNonZero()
    {
        var yaml = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "Corpus", "arch-kitchen.yaml"));
        var svg = BeckSvg.Render(yaml, new SvgRenderOptions
        {
            Style = BeckStyle.Classic with { Edges = StyleEdges.Classic with { BowAmplitude = 6 } },
        });

        var edgeD = System.Text.RegularExpressions.Regex
            .Matches(svg, "<path class=\"beck-edge beck-edge--[^\"]*\" d=\"([^\"]*)\"")
            .Select(m => m.Groups[1].Value)
            .ToList();

        Assert.NotEmpty(edgeD);
        foreach (var d in edgeD)
        {
            Assert.True(PathLength.Of(d) > 0, $"bowed edge measured 0: {d}");
        }
    }
}