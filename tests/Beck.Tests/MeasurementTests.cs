using Beck.Skia;
using Beck.Text;
using Xunit;

namespace Beck.Tests;

/// <summary>Smoke tests for the Skia measurer: native Skia loads and advances are sane.</summary>
public sealed class MeasurementTests
{
    [Fact]
    public void Skia_LoadsAndMeasures()
    {
        using var m = new SkiaTextMeasurer(TestFonts.Spec());
        var t = m.Measure("API Server", FontRole.CardTitle);
        // 14px SemiBold "API Server" is ~60–90px wide; ascent/descent positive.
        Assert.InRange(t.Width, 40, 120);
        Assert.True(t.Ascent > 0);
        Assert.True(t.Descent > 0);
    }

    [Fact]
    public void Skia_WidthGrowsWithText()
    {
        using var m = new SkiaTextMeasurer(TestFonts.Spec());
        var a = m.Measure("A", FontRole.CardTitle).Width;
        var ab = m.Measure("AB", FontRole.CardTitle).Width;
        Assert.True(ab > a);
    }

    [Fact]
    public void Skia_MonoRoleUsesMonoFont()
    {
        using var m = new SkiaTextMeasurer(TestFonts.Spec());
        // In a monospace font every glyph has the same advance.
        var one = m.Measure("i", FontRole.ClassMember).Width;
        var two = m.Measure("W", FontRole.ClassMember).Width;
        Assert.Equal(one, two, 3);
    }
}