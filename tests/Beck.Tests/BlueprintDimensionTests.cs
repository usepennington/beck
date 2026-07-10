using System.Text.RegularExpressions;
using Beck.Rendering;
using Beck.Styles;
using Xunit;

namespace Beck.Tests;

/// <summary>
/// Targeted assertions for the <c>blueprint</c> style's <see cref="StyleArtwork.Blueprint"/> artwork:
/// the technical-drawing dimension line drawn along each group box's top edge (a thin extension rule +
/// two perpendicular witness ticks). These pin the artwork's structural contract beyond the generic
/// <see cref="StyleSmokeTests"/> invariants: one dimension annotation per group, correct geometry from
/// the group rect, token-driven colour, and none on diagrams without groups or on classic.
/// </summary>
public sealed class BlueprintDimensionTests
{
    private static readonly string CorpusDir = Path.Combine(AppContext.BaseDirectory, "Corpus");
    private static string ArchKitchen() => File.ReadAllText(Path.Combine(CorpusDir, "arch-kitchen.yaml"));

    private static readonly Regex GroupRect = new("<rect class=\"beck-group\"", RegexOptions.Compiled);
    private static readonly Regex DimensionG = new("<g class=\"beck-dimension\"", RegexOptions.Compiled);
    // A full dimension group: the rule line then the two witness ticks, capturing every coordinate.
    private static readonly Regex DimensionFull = new(
        "<g class=\"beck-dimension\" style=\"fill:none;stroke:var\\(--beck-dimension[^\"]*\\)[^\"]*\">" +
        "<line x1=\"([0-9.]+)\" y1=\"([0-9.]+)\" x2=\"([0-9.]+)\" y2=\"([0-9.]+)\"/>" +
        "<line x1=\"([0-9.]+)\" y1=\"([0-9.]+)\" x2=\"([0-9.]+)\" y2=\"([0-9.]+)\"/>" +
        "<line x1=\"([0-9.]+)\" y1=\"([0-9.]+)\" x2=\"([0-9.]+)\" y2=\"([0-9.]+)\"/></g>" +
        "|<g class=\"beck-dimension\"",
        RegexOptions.Compiled);

    [Fact]
    public void Blueprint_DrawsOneDimensionLinePerGroup()
    {
        string svg = BeckSvg.Render(ArchKitchen(), new SvgRenderOptions { Style = BlueprintStyle.Instance });
        int groups = GroupRect.Matches(svg).Count;
        int dims = DimensionG.Matches(svg).Count;
        Assert.True(groups > 0, "arch-kitchen should have group boxes");
        Assert.Equal(groups, dims);
    }

    [Fact]
    public void Classic_EmitsNoDimensionLines()
    {
        string svg = BeckSvg.Render(ArchKitchen(), new SvgRenderOptions { Style = BeckStyle.Classic });
        Assert.DoesNotContain("<g class=\"beck-dimension\"", svg);
    }

    [Fact]
    public void Blueprint_DiagramsWithoutGroups_EmitNoDimensionMarkup()
    {
        // Sequence/class/state have no group boxes, so no dimension <g> is drawn (the --beck-dimension
        // token is still declared in <style>, but that is not the markup).
        foreach (string diagram in new[] { "seq-kitchen", "class", "state" })
        {
            string svg = BeckSvg.Render(File.ReadAllText(Path.Combine(CorpusDir, diagram + ".yaml")),
                new SvgRenderOptions { Style = BlueprintStyle.Instance });
            Assert.DoesNotContain("<g class=\"beck-dimension\"", svg);
        }
    }

    [Fact]
    public void Blueprint_DimensionGeometryMatchesGroupRect_AndStaysNonNegative()
    {
        // The dimension rule spans the group's top edge offset up by DimensionTick; the two witness
        // ticks stand at the box's left/right x, from the top edge (y) up past the rule. Read the geometry
        // straight from the emitted lines and check it against a group rect + the tick constant.
        string svg = BeckSvg.Render(ArchKitchen(), new SvgRenderOptions { Style = BlueprintStyle.Instance });
        double gap = BlueprintStyle.Instance.Geometry.DimensionTick;
        Assert.True(gap > 0);
        double over = gap / 3;

        // Group rects, keyed for lookup by their x (left edge).
        var groupRects = Regex.Matches(svg, "<rect class=\"beck-group\" x=\"([0-9.]+)\" y=\"([0-9.]+)\" width=\"([0-9.]+)\"")
            .Select(m => (
                X: double.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture),
                Y: double.Parse(m.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture),
                W: double.Parse(m.Groups[3].Value, System.Globalization.CultureInfo.InvariantCulture)))
            .ToList();
        Assert.NotEmpty(groupRects);

        var dims = DimensionFull.Matches(svg).Where(m => m.Groups[1].Success).ToList();
        Assert.Equal(groupRects.Count, dims.Count);

        foreach (Match m in dims)
        {
            double[] c = Enumerable.Range(1, 12)
                .Select(i => double.Parse(m.Groups[i].Value, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
            // rule: (rx0, dy)->(rx1, dy); tickL: (rx0, y)->(rx0, tickTop); tickR: (rx1, y)->(rx1, tickTop)
            double rx0 = c[0], dy = c[1], rx1 = c[2];
            double edgeY = c[5];               // witness tick bottom == group top edge y
            double tickTop = c[7];
            Assert.Equal(dy, c[3], 3);          // rule is horizontal
            Assert.Equal(rx0, c[4], 3);          // left tick x
            Assert.Equal(rx1, c[8], 3);          // right tick x
            Assert.Equal(edgeY - gap, dy, 3);    // rule sits `gap` above the edge
            Assert.Equal(dy - over, tickTop, 3); // witness overshoots the rule by `over`

            // This dimension must line up with a real group rect (same left x, width, top edge).
            var g = groupRects.First(r => Math.Abs(r.X - rx0) < 0.5);
            Assert.Equal(g.Y, edgeY, 3);
            Assert.Equal(g.X + g.W, rx1, 3);

            // All coordinates stay on the always-positive canvas.
            Assert.All(c, v => Assert.True(v >= 0, $"negative dimension coord {v}"));
        }
    }

    [Fact]
    public void Blueprint_DimensionColour_GoesThroughToken_NoResolvedLiteral()
    {
        // The stroke flows through --beck-dimension (fallback --beck-group-border) — no hex in shape CSS.
        string svg = BeckSvg.Render(ArchKitchen(), new SvgRenderOptions { Style = BlueprintStyle.Instance });
        Assert.Contains("stroke:var(--beck-dimension, var(--beck-group-border))", svg);
        // The token itself is defined in the stylesheet (a subtle primary mix), light + dark.
        Assert.Contains("--beck-dimension:color-mix", svg);
    }
}
