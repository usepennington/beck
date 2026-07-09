using System.Globalization;
using Beck.Rendering;
using Beck.Rendering.Svg;
using Beck.Rendering.Text;
using Xunit;

namespace Beck.Tests;

/// <summary>M0 scaffolding sanity: the id scheme, options defaults, and entry guards.</summary>
public sealed class ScaffoldingTests
{
    [Fact]
    public void ContentHash_IsEightLowerHexChars()
    {
        string h = ContentHash.Of("type: architecture\n");
        Assert.Equal(8, h.Length);
        Assert.Matches("^[0-9a-f]{8}$", h);
    }

    [Fact]
    public void ContentHash_IsDeterministic_AndDistinguishesInput()
    {
        Assert.Equal(ContentHash.Of("a"), ContentHash.Of("a"));
        Assert.NotEqual(ContentHash.Of("a"), ContentHash.Of("b"));
    }

    [Fact]
    public void ResolveIdSuffix_IdSuffixOverrideWins()
    {
        var opts = new SvgRenderOptions { IdSuffix = "deadbeef" };
        Assert.Equal("deadbeef", BeckSvg.ResolveIdSuffix("anything", opts));
    }

    [Fact]
    public void ResolveIdSuffix_SameInputSameHash_DifferentOptionsDiffer()
    {
        const string yaml = "type: architecture\nnodes: [a]\n";
        var a = BeckSvg.ResolveIdSuffix(yaml, new SvgRenderOptions());
        var b = BeckSvg.ResolveIdSuffix(yaml, new SvgRenderOptions());
        var c = BeckSvg.ResolveIdSuffix(yaml, new SvgRenderOptions { Animation = AnimationMode.Static });
        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
    }

    [Fact]
    public void Options_Defaults()
    {
        var opts = new SvgRenderOptions();
        Assert.Same(InterMetricsMeasurer.Instance, opts.Measurer);
        Assert.Equal(AnimationMode.Full, opts.Animation);
        Assert.Equal(TextLengthGuard.All, opts.TextLengthGuard);
#pragma warning disable CS0618 // exercising the (obsolete, still-hashed) default
        Assert.False(opts.EmbedFonts);
#pragma warning restore CS0618
        Assert.Null(opts.Theme);
        Assert.Null(opts.Font);
        Assert.Null(opts.IdSuffix);
        Assert.Null(opts.Style);
        Assert.Null(opts.Styles);
    }

    [Fact]
    public void Round2_MatchesTsRounding()
    {
        Assert.Equal("12.35", BeckSvg.Round2(12.3456));
        Assert.Equal("12", BeckSvg.Round2(12.0));
        Assert.Equal("0.1", BeckSvg.Round2(0.1));
        // Invariant culture: decimal point, not comma.
        Assert.Equal("3.14", BeckSvg.Round2(3.14159));
    }

    [Fact]
    public void Render_NullYaml_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => BeckSvg.Render(null!));
    }

    [Fact]
    public void BeckYamlException_AppendsLineSuffix()
    {
        Assert.Equal("bad thing", new BeckYamlException("bad thing").Message);
        Assert.Equal("bad thing (line 7)", new BeckYamlException("bad thing", 7).Message);
    }

    [Fact]
    public void Round2_UsesInvariantCulture_UnderCommaLocale()
    {
        var prior = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            Assert.Equal("1.5", BeckSvg.Round2(1.5));
        }
        finally
        {
            CultureInfo.CurrentCulture = prior;
        }
    }
}
