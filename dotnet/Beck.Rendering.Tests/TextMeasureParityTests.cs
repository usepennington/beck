using System.Text.Json;
using Beck.Rendering.Skia;
using Beck.Rendering.Text;
using Xunit;

namespace Beck.Rendering.Tests;

/// <summary>
/// Isolates text-measurement fidelity from the box model: the Skia measurer's
/// advance width must match the browser's <c>getBoundingClientRect</c> width for
/// the same pinned fonts, per role. Golden = <c>Goldens/measure/text.json</c>,
/// captured via the font-pinned oracle page (tools/oracle/measure.html).
/// </summary>
public sealed class TextMeasureParityTests
{
    private sealed record Case(string Role, string Text, double Width);

    [Fact]
    public void SkiaText_MatchesBrowser()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Goldens", "measure", "text.json");
        var cases = JsonSerializer.Deserialize<Case[]>(File.ReadAllText(path),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

        using var m = new SkiaTextMeasurer(TestFonts.Spec());
        var rows = cases.Select(c =>
        {
            double skia = m.Measure(c.Text, Enum.Parse<FontRole>(c.Role)).Width;
            return (c, skia, delta: skia - c.Width);
        }).ToList();

        double maxAbs = rows.Max(r => Math.Abs(r.delta));
        double mean = rows.Average(r => Math.Abs(r.delta));
        string worst = string.Join("\n", rows.OrderByDescending(r => Math.Abs(r.delta)).Take(15)
            .Select(r => $"  {r.c.Role,-16} \"{r.c.Text}\"  browser={r.c.Width,8:0.00}  skia={r.skia,8:0.00}  Δ={r.delta,+7:0.00}"));

        // HarfBuzz matches the browser's shaping to ~0.02px (browser 1/64px rounding);
        // 0.1px catches real regressions with margin.
        Assert.True(maxAbs <= 0.1,
            $"Skia text width off by up to {maxAbs:0.00}px (mean {mean:0.00}px) across {rows.Count} cases. Worst:\n{worst}");
    }
}
