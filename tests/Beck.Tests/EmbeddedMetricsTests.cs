using Beck;
using Beck.Layout;
using Beck.Model;
using Beck.Rendering;
using Beck.Rendering.Text;
using Beck.Text;
using Xunit;

namespace Beck.Tests;

/// <summary>
/// Gates the Phase-3 embedded-metrics infrastructure: the generated Source Serif / Archivo /
/// Shantell Sans tables, the generalized <see cref="EmbeddedMetricsMeasurer"/>, and the
/// <see cref="MetricsFont"/> selection wired through <see cref="BeckSvg.ResolveMeasurer"/>. The
/// classic Inter path stays byte-identical (see <see cref="StyleByteIdentityTests"/>); here we prove
/// the new tables are sane and actually get selected for a non-Inter style.
/// </summary>
public sealed class EmbeddedMetricsTests
{
    private static readonly MetricsFont[] AllFonts = Enum.GetValues<MetricsFont>();

    // A non-Inter style: classic in every respect but its metrics key. Distinct name so it never
    // collides with a built-in in resolution.
    private static BeckStyle SerifStyle => BeckStyle.Classic with
    {
        Name = "test-serif",
        Typography = BeckStyle.Classic.Typography with { MetricsFont = MetricsFont.SourceSerif },
    };

    [Theory]
    [InlineData(MetricsFont.Inter)]
    [InlineData(MetricsFont.SourceSerif)]
    [InlineData(MetricsFont.Archivo)]
    [InlineData(MetricsFont.ShantellSans)]
    public void Table_KnownGlyphs_HavePositiveSaneMetrics(MetricsFont font)
    {
        var m = EmbeddedMetricsMeasurer.For(font);

        // A few glyphs that exist in every Latin font, at a normal role.
        foreach (string text in new[] { "M", "Checkout API", "«interface»", "42" })
        {
            TextMetrics tm = m.Measure(text, FontRole.CardTitle);
            Assert.True(tm.Width > 0, $"{font}: '{text}' width should be > 0");
        }

        TextMetrics one = m.Measure("Storefront", FontRole.CardTitle);
        double size = FontRoles.Of(FontRole.CardTitle).SizePx;
        // Ascent/descent are per-em × size: ascent ~0.9–1.2em, descent ~0.2–0.4em for these families.
        Assert.InRange(one.Ascent / size, 0.5, 1.5);
        Assert.InRange(one.Descent / size, 0.1, 0.6);
    }

    [Theory]
    [InlineData(MetricsFont.Inter)]
    [InlineData(MetricsFont.SourceSerif)]
    [InlineData(MetricsFont.Archivo)]
    [InlineData(MetricsFont.ShantellSans)]
    public void Mono_IsFixedPitch_AndReusesPlexAcrossTables(MetricsFont font)
    {
        var m = EmbeddedMetricsMeasurer.For(font);

        // ClassMember is a monospace role. Every glyph shares one advance, so N chars = N × 1 char,
        // and a digit measures the same as a wide letter.
        double one = m.Measure("0", FontRole.ClassMember).Width;
        double five = m.Measure("00000", FontRole.ClassMember).Width;
        double wideLetter = m.Measure("W", FontRole.ClassMember).Width;
        Assert.True(one > 0);
        Assert.Equal(5 * one, five, 6);
        Assert.Equal(one, wideLetter, 6); // uniform pitch: digit == letter

        // Mono coverage is IBM Plex Mono in every table, so mono metrics match the Inter table exactly.
        TextMetrics mine = m.Measure("POST /x", FontRole.MsgText);
        TextMetrics inter = EmbeddedMetricsMeasurer.For(MetricsFont.Inter).Measure("POST /x", FontRole.MsgText);
        Assert.Equal(inter.Width, mine.Width, 9);
        Assert.Equal(inter.Ascent, mine.Ascent, 9);
        Assert.Equal(inter.Descent, mine.Descent, 9);
    }

    [Fact]
    public void NonInterTables_MeasureSansDifferentlyThanInter()
    {
        double inter = EmbeddedMetricsMeasurer.For(MetricsFont.Inter).Measure("MMM", FontRole.CardTitle).Width;
        foreach (MetricsFont font in AllFonts.Where(f => f != MetricsFont.Inter))
        {
            double other = EmbeddedMetricsMeasurer.For(font).Measure("MMM", FontRole.CardTitle).Width;
            Assert.True(Math.Abs(other - inter) > 0.5,
                $"{font} should size 'MMM' differently than Inter (inter={inter}, {font}={other})");
        }
    }

    [Fact]
    public void For_ReturnsStableCachedInstance()
    {
        Assert.Same(EmbeddedMetricsMeasurer.For(MetricsFont.SourceSerif),
                    EmbeddedMetricsMeasurer.For(MetricsFont.SourceSerif));
    }

    [Fact]
    public void ResolveMeasurer_DefaultMeasurer_ClassicStyle_KeepsInterInstance()
    {
        // Default options + classic style must keep the exact default instance — the byte-identity anchor.
        ITextMeasurer chosen = BeckSvg.ResolveMeasurer(new SvgRenderOptions(), BeckStyle.Classic);
        Assert.Same(InterMetricsMeasurer.Instance, chosen);
    }

    [Fact]
    public void ResolveMeasurer_DefaultMeasurer_NonInterStyle_SwapsInEmbeddedTable()
    {
        ITextMeasurer chosen = BeckSvg.ResolveMeasurer(new SvgRenderOptions(), SerifStyle);
        Assert.NotSame(InterMetricsMeasurer.Instance, chosen);
        Assert.IsType<EmbeddedMetricsMeasurer>(chosen);

        // ...and it is genuinely the serif table: 'MMM' differs from Inter.
        double serif = chosen.Measure("MMM", FontRole.CardTitle).Width;
        double inter = InterMetricsMeasurer.Instance.Measure("MMM", FontRole.CardTitle).Width;
        Assert.NotEqual(inter, serif, 3);
    }

    [Fact]
    public void ResolveMeasurer_ExplicitMeasurer_OverridesTableSelection()
    {
        var explicitMeasurer = new StubMeasurer();
        // Even with a non-Inter style, an explicitly-set measurer wins (Skia exactness is preserved).
        ITextMeasurer chosen = BeckSvg.ResolveMeasurer(
            new SvgRenderOptions { Measurer = explicitMeasurer }, SerifStyle);
        Assert.Same(explicitMeasurer, chosen);
    }

    [Fact]
    public void NonInterTable_ChangesMeasuredCardSize()
    {
        // The table must reach layout, not just resolution: the same node sized with the serif table
        // yields a different card box than with Inter (a long multi-word title makes the width diverge).
        DiagramModel model = Validate.LoadDiagram(
            "type: architecture\nnodes: [{ id: a, title: \"Payment Gateway\" }]\nedges: []\n");
        NodeModel node = model.Nodes[0];
        StyleGeometry geo = BeckStyle.Classic.Geometry;

        Size inter = CardSizer.Measure(node, InterMetricsMeasurer.Instance, geo);
        Size serif = CardSizer.Measure(node, EmbeddedMetricsMeasurer.For(MetricsFont.SourceSerif), geo);
        Assert.NotEqual(inter, serif);
    }

    private sealed class StubMeasurer : ITextMeasurer
    {
        public TextMetrics Measure(string text, FontRole role) => new(text.Length, 10, 3);
    }
}
