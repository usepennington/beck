using Beck.Skia;
using Beck.Text;
using Xunit;

namespace Beck.Tests;

/// <summary>
/// Gates the managed <see cref="InterMetricsMeasurer"/>: it renders zero-config (no Skia, no font
/// files) and its embedded metrics track the exact Skia measurement closely enough that card
/// sizing is sound (the <c>textLength</c> guard absorbs the rest).
/// </summary>
public class ManagedMeasurerTests
{
    private const string Yaml = """
        type: architecture
        meta: { title: Checkout }
        nodes:
          - { id: web, title: Storefront, kind: user }
          - { id: api, title: Checkout API, kind: gateway }
          - { id: db, title: Ledger, kind: db }
        edges:
          - { from: web, to: api }
          - { from: api, to: db, label: record }
        """;

    [Fact]
    public void DefaultOptions_RenderZeroConfig_ProducesSvg()
    {
        // The whole point: BeckSvg.Render(yaml) with no options must work out of the box —
        // the default measurer is no longer a throwing stub.
        var svg = BeckSvg.Render(Yaml);

        Assert.Contains("<svg", svg);
        Assert.Contains("Checkout API", svg);
        Assert.Contains("Storefront", svg);
    }

    [Theory]
    [InlineData(FontRole.CardTitle, "Checkout API")]
    [InlineData(FontRole.CardSubtitle, "orders service")]
    [InlineData(FontRole.PillTitle, "Placed")]
    [InlineData(FontRole.EdgeLabel, "record")]
    [InlineData(FontRole.GroupLabel, "Data Plane")]      // uppercased by the role
    [InlineData(FontRole.ClassStereotype, "«interface»")] // guillemets (non-ASCII extras)
    [InlineData(FontRole.ClassMember, "+ Total(): Money")] // monospace
    [InlineData(FontRole.MsgText, "POST /orders")]         // monospace
    [InlineData(FontRole.DiagramTitle, "Order Lifecycle")]
    public void ManagedMetrics_TrackSkia(FontRole role, string text)
    {
        using var skia = new SkiaTextMeasurer(TestFonts.Spec());
        var exact = skia.Measure(text, role);
        var managed = InterMetricsMeasurer.Instance.Measure(text, role);

        // Width: a per-glyph sum ignores kerning, so allow a small band. Ascent/descent come from
        // the same font metrics, so they should match tightly.
        Assert.InRange(managed.Width / exact.Width, 0.93, 1.07);
        Assert.InRange(managed.Ascent / exact.Ascent, 0.98, 1.02);
        Assert.InRange(managed.Descent / exact.Descent, 0.98, 1.02);
    }

    // Visual side-by-side (managed default vs exact Skia) for a manual eyeball. Not run in CI.
    // BECK_RENDER_SAMPLE=<path> dotnet test --filter FullyQualifiedName~RenderSampleHtml
    [Fact]
    public void RenderSampleHtml()
    {
        var outPath = Environment.GetEnvironmentVariable("BECK_RENDER_SAMPLE");
        if (string.IsNullOrEmpty(outPath))
        {
            return;
        }

        var managed = BeckSvg.Render(Yaml, new SvgRenderOptions { Animation = AnimationMode.Static });
        using var skia = new SkiaTextMeasurer(TestFonts.Spec());
        var exact = BeckSvg.Render(Yaml, new SvgRenderOptions
        {
            Measurer = skia,
            Font = TestFonts.Spec(),
            Animation = AnimationMode.Static,
        });

        File.WriteAllText(outPath,
            "<!doctype html><meta charset=utf-8><body style='margin:0;padding:24px;background:#fff;font-family:system-ui'>"
            + "<div style='display:flex;gap:40px;align-items:flex-start'>"
            + $"<div><h3>Managed default (Inter metrics)</h3><div style='width:360px'>{managed}</div></div>"
            + $"<div><h3>Skia exact</h3><div style='width:360px'>{exact}</div></div>"
            + "</div>");
    }
}