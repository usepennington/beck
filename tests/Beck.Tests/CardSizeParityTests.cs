using System.Text.Json;
using Beck.Layout;
using Beck.Model;
using Beck.Rendering;
using Beck.Skia;
using Beck.Rendering.Text;
using Beck.Text;
using Xunit;

namespace Beck.Tests;

/// <summary>
/// M2 gate: <see cref="CardSizer"/> reproduces the browser's per-node
/// <c>getBoundingClientRect</c> (SizeMap) to ±1px, with the same pinned fonts.
/// Golden = <c>Goldens/measure/cards.json</c>, captured via the real TS engine's
/// <c>measureNodes</c> (tools/oracle/measure.html + the playground). Class-member
/// widths in the golden are measured with IBM Plex Mono pinned — the font the C#
/// renderer emits (the JS engine lets them fall back to the system mono).
/// </summary>
public sealed class CardSizeParityTests
{
    private static readonly string CorpusDir = Path.Combine(AppContext.BaseDirectory, "Corpus");
    private static readonly string Golden = Path.Combine(AppContext.BaseDirectory, "Goldens", "measure", "cards.json");

    private sealed record WH(double W, double H);

    [Fact]
    public void CardSizer_MatchesBrowser()
    {
        var goldens = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, WH>>>(
            File.ReadAllText(Golden), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

        using var measurer = new SkiaTextMeasurer(TestFonts.Spec());
        var fails = new List<string>();
        double maxAbs = 0;
        int chec1 = 0;

        foreach (var (file, nodes) in goldens)
        {
            DiagramModel model = Validate.LoadDiagram(File.ReadAllText(Path.Combine(CorpusDir, file + ".yaml")));
            var byId = model.Nodes.ToDictionary(n => n.Id);
            foreach (var (id, want) in nodes)
            {
                Size got = CardSizer.Measure(byId[id], measurer);
                double dw = got.W - want.W, dh = got.H - want.H;
                maxAbs = Math.Max(maxAbs, Math.Max(Math.Abs(dw), Math.Abs(dh)));
                chec1++;
                if (Math.Abs(dw) > 1 || Math.Abs(dh) > 1)
                    fails.Add($"  {file}/{id,-12} browser={want.W}x{want.H}  sizer={got.W}x{got.H}  Δ=({dw:+0;-0},{dh:+0;-0})");
            }
        }

        Assert.True(fails.Count == 0,
            $"{fails.Count}/{chec1} cards off by >1px (max Δ {maxAbs:0.0}px):\n{string.Join("\n", fails)}");
    }
}
