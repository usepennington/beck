using Beck.Rendering;
using Beck.Rendering.Svg;
using Beck.Skia;
using Xunit;

namespace Beck.Tests;

/// <summary>
/// Inline-svg <c>icon:</c> values are a stored-XSS channel (untrusted YAML is
/// spliced verbatim into the emitted document). These guard the allowlist
/// sanitizer: benign hand-authored icons pass through unchanged, hostile markup
/// is rejected and never reaches the SVG.
/// </summary>
public sealed class IconSanitizerTests
{
    private const string BenignIcon =
        """<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.6"><rect x="3" y="4" width="18" height="6" rx="1.5"/><circle cx="12" cy="12" r="3"/><path d="M7 7h.01"/></svg>""";

    [Theory]
    [InlineData(BenignIcon)]
    [InlineData("""<svg viewBox="0 0 24 24"><g fill="#f00" transform="translate(1 2)"><polyline points="1,1 2,2"/></g></svg>""")]
    [InlineData("""<svg viewBox="0 0 10 10"><defs><linearGradient id="g"><stop offset="0" stop-color="red"/></linearGradient></defs><rect width="10" height="10" fill="url(#g)"/></svg>""")]
    public void BenignInlineSvg_PassesThroughVerbatim(string icon)
    {
        Assert.True(Icons.IsKnownIcon(icon));
        Assert.Equal(icon, Icons.ResolveIcon(icon));
    }

    [Theory]
    [InlineData("""<svg onload="alert(1)"><path d="M0 0"/></svg>""")]                       // event handler attribute
    [InlineData("""<svg><script>alert(1)</script></svg>""")]                                  // script element
    [InlineData("""<svg><foreignObject><body onload="alert(1)"/></foreignObject></svg>""")]   // foreignObject escape hatch
    [InlineData("""<svg><a href="javascript:alert(1)"><rect/></a></svg>""")]                  // javascript: url + <a>
    [InlineData("""<svg><use xlink:href="#x"/></svg>""")]                                      // <use>/xlink
    [InlineData("""<svg><image href="http://evil/x.svg"/></svg>""")]                           // external image ref
    [InlineData("""<svg><rect fill="url(http://evil/x)"/></svg>""")]                           // external url() reference
    [InlineData("""<svg><rect style="background:url(javascript:alert(1))"/></svg>""")]         // scheme hidden in style
    [InlineData("""<svg><animate onbegin="alert(1)"/></svg>""")]                               // animation + on* handler
    [InlineData("""<svg><!--<script>alert(1)</script>--><rect/></svg>""")]                     // comment-smuggled markup
    [InlineData("<svg><rect onload=alert(1)/></svg>")]                                          // unquoted on* handler (HTML parses it)
    public void HostileInlineSvg_IsRejected(string icon)
    {
        Assert.False(Icons.IsKnownIcon(icon));
        Assert.Null(Icons.ResolveIcon(icon));
    }

    [Fact]
    public void HostilePayload_NeverReachesRenderedSvg()
    {
        var font = TestFonts.Spec();
        using var measurer = new SkiaTextMeasurer(font);
        var options = new SvgRenderOptions { Measurer = measurer, Font = font };

        string yaml =
            "type: architecture\n" +
            "nodes:\n" +
            "  - id: a\n" +
            "    title: A\n" +
            "    icon: \"<svg onload='alert(1)'><script>alert(1)</script></svg>\"\n";

        string svg = BeckSvg.Render(yaml, options);

        Assert.DoesNotContain("onload", svg);
        Assert.DoesNotContain("<script", svg);
        Assert.DoesNotContain("alert(1)", svg);
    }

    [Fact]
    public void BenignPayload_IsSplicedIntoRenderedSvg()
    {
        var font = TestFonts.Spec();
        using var measurer = new SkiaTextMeasurer(font);
        var options = new SvgRenderOptions { Measurer = measurer, Font = font };

        string yaml =
            "type: architecture\n" +
            "nodes:\n" +
            "  - id: a\n" +
            "    title: A\n" +
            "    icon: \"<svg viewBox='0 0 24 24'><circle cx='12' cy='12' r='9'/></svg>\"\n";

        string svg = BeckSvg.Render(yaml, options);

        // The author's vector content survives verbatim (quotes and all); the
        // renderer only tints it via the injected beck-icon class.
        Assert.Contains("<circle cx='12' cy='12' r='9'/>", svg);
        Assert.Contains("beck-icon", svg);
    }
}
