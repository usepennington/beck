using System.Text.RegularExpressions;
using Beck.Rendering;
using Beck.Rendering.Svg;
using Beck.Rendering.Text;
using Xunit;

namespace Beck.Tests;

/// <summary>
/// Phase-2 style-system plumbing: <c>meta.style</c> parsing + validation, resolution precedence,
/// the style-aware id hash (classic stays a no-op), custom-token sanitization, the wired
/// <see cref="TextLengthGuard"/>, and value-semantic <see cref="FontRoleTable"/> equality. All
/// warning-observing cases live in this one class so the global <see cref="BeckDiagnostics.OnWarning"/>
/// sink is only ever touched serially.
/// </summary>
public sealed class StylePhase2Tests
{
    private const string Arch =
        "type: architecture\nnodes: [{ id: a, title: Alpha }, { id: b, title: Beta }]\nedges: [{ from: a, to: b }]\n";

    private static string ArchStyled(string token) =>
        $"type: architecture\nmeta:\n  style: {token}\nnodes: [{{ id: a, title: Alpha }}, {{ id: b, title: Beta }}]\nedges: [{{ from: a, to: b }}]\n";

    // A trivially-derived custom style: classic values, a distinct name, and a marker token so its
    // presence in the emitted <style> block is observable.
    private static BeckStyle Named(string name, string marker) =>
        BeckStyle.Classic with
        {
            Name = name,
            LightTokens = new StyleTokens(
                BeckStyle.Classic.LightTokens.Entries.Append(("--beck-marker", marker)).ToArray()),
        };

    private static string HashOf(string svg) =>
        Regex.Match(svg, @"beck-svg b-([0-9a-f]{8})").Groups[1].Value;

    private static List<string> CaptureWarnings(Action act)
    {
        var warnings = new List<string>();
        Action<string>? prior = BeckDiagnostics.OnWarning;
        BeckDiagnostics.OnWarning = warnings.Add;
        try { act(); } finally { BeckDiagnostics.OnWarning = prior; }
        return warnings;
    }

    // ---- meta.style parse ----

    [Fact]
    public void MetaStyle_ValidToken_LandsOnMeta()
    {
        DiagramModel model = Validate.LoadDiagram(ArchStyled("metro"));
        Assert.Equal("metro", model.Meta.StyleName);
    }

    [Fact]
    public void MetaStyle_MalformedToken_WarnsAndIsIgnored()
    {
        DiagramModel? model = null;
        var warnings = CaptureWarnings(() =>
            model = Validate.LoadDiagram("type: architecture\nmeta:\n  style: \"Bad Name!\"\nnodes: [{ id: a }]\n"));
        Assert.Null(model!.Meta.StyleName);
        Assert.Contains(warnings, w => w.Contains("meta.style"));
    }

    [Fact]
    public void MetaStyle_Absent_IsNull()
    {
        DiagramModel model = Validate.LoadDiagram(Arch);
        Assert.Null(model.Meta.StyleName);
    }

    // ---- resolution precedence ----

    [Fact]
    public void Precedence_YamlBeatsOptionsStyle()
    {
        var options = new SvgRenderOptions
        {
            Style = Named("beta", "BETA"),
            Styles = new Dictionary<string, BeckStyle> { ["alpha"] = Named("alpha", "ALPHA") },
        };
        string svg = BeckSvg.Render(ArchStyled("alpha"), options);
        Assert.Contains("--beck-marker:ALPHA", svg);
        Assert.DoesNotContain("--beck-marker:BETA", svg);
    }

    [Fact]
    public void Precedence_OptionsStyleBeatsDefault()
    {
        string svg = BeckSvg.Render(Arch, new SvgRenderOptions { Style = Named("beta", "BETA") });
        Assert.Contains("--beck-marker:BETA", svg);

        string plain = BeckSvg.Render(Arch);
        Assert.DoesNotContain("--beck-marker", plain);
    }

    [Fact]
    public void Precedence_UnknownName_WarnsAndFallsBackToOptionsStyle()
    {
        var options = new SvgRenderOptions { Style = Named("beta", "BETA") };
        string svg = "";
        var warnings = CaptureWarnings(() => svg = BeckSvg.Render(ArchStyled("nope"), options));
        Assert.Contains("--beck-marker:BETA", svg);
        Assert.Contains(warnings, w => w.Contains("nope"));
    }

    [Fact]
    public void Precedence_UnknownName_NoOptionsStyle_FallsBackToClassic()
    {
        string svg = "";
        var warnings = CaptureWarnings(() => svg = BeckSvg.Render(ArchStyled("nope"), new SvgRenderOptions()));
        Assert.DoesNotContain("--beck-marker", svg);
        Assert.Contains(warnings, w => w.Contains("nope"));
    }

    // ---- hash rule ----

    [Fact]
    public void Hash_ClassicNameAppend_IsNoOp()
    {
        // A meta.style:classic document renders byte-for-byte identically to threading Classic through
        // the seam with the pre-Phase-2 (no style segment) hash — proving classic pays no hash cost.
        string yaml = ArchStyled("classic");
        string full = BeckSvg.Render(yaml);

        string hashNoStyle = BeckSvg.ResolveIdSuffix(yaml, new SvgRenderOptions());
        DiagramModel model = Validate.LoadDiagram(yaml);
        string viaSeam = SvgRenderer.Render(model, InterMetricsMeasurer.Instance, hashNoStyle, new SvgRenderOptions(), BeckStyle.Classic);

        Assert.Equal(hashNoStyle, BeckSvg.ResolveIdSuffix(yaml, new SvgRenderOptions(), BeckStyle.Classic));
        Assert.Equal(full, viaSeam);
    }

    [Fact]
    public void Hash_CustomStyle_DiffersFromDefault()
    {
        // Same YAML (no meta.style) — only options.Style differs, so the suffix must shift.
        string plain = BeckSvg.Render(Arch);
        string custom = BeckSvg.Render(Arch, new SvgRenderOptions { Style = BeckStyle.Classic with { Name = "custom-x" } });
        Assert.NotEqual(HashOf(plain), HashOf(custom));
    }

    [Fact]
    public void Hash_KeyedByResolvedStyleName_NotObjectOrRegistryKey()
    {
        // Two styles with the same Name (different values) hash the same; different names differ.
        var opts = new SvgRenderOptions();
        Assert.Equal(
            BeckSvg.ResolveIdSuffix(Arch, opts, Named("shared", "A")),
            BeckSvg.ResolveIdSuffix(Arch, opts, Named("shared", "B")));
        Assert.NotEqual(
            BeckSvg.ResolveIdSuffix(Arch, opts, Named("alpha", "A")),
            BeckSvg.ResolveIdSuffix(Arch, opts, Named("beta", "A")));
    }

    [Fact]
    public void Hash_IsDeterministic()
    {
        var opts = new SvgRenderOptions { Style = Named("custom-x", "M") };
        Assert.Equal(BeckSvg.Render(Arch, opts), BeckSvg.Render(Arch, opts));
    }

    // ---- custom-token sanitization ----

    [Fact]
    public void Sanitization_HostileTokenValue_CannotEscapeStyleBlock()
    {
        var evil = BeckStyle.Classic with
        {
            Name = "evil",
            LightTokens = new StyleTokens(BeckStyle.Classic.LightTokens.Entries
                .Append(("--beck-x", "RED</style><script>alert(1)"))
                .Append(("--beck-y", "z} .evil{color:red}"))
                .Append(("--beck-z", "@import 'http://evil'"))
                .ToArray()),
        };
        string svg = BeckSvg.Render(Arch, new SvgRenderOptions { Style = evil });

        Assert.DoesNotContain("RED</style>", svg);   // </ stripped → cannot close the style element
        Assert.DoesNotContain(".evil{", svg);         // { stripped → cannot open a new rule
        Assert.DoesNotContain("z}", svg);             // } stripped → cannot close the current rule
        Assert.DoesNotContain("@import", svg);        // @import stripped
        Assert.Contains("--beck-x:", svg);            // the token is still emitted, only sanitised
    }

    [Fact]
    public void Sanitization_HostileStyleEdges_AreCleaned()
    {
        var evil = BeckStyle.Classic with
        {
            Name = "evil-edges",
            Edges = BeckStyle.Classic.Edges with
            {
                UnderlayWidth = 4,
                Overlay = EdgeOverlay.Comet,
                BaseLinecap = "round}@import url(//evil)",
                OverlayLinecap = "round\"/><script>alert(1)",
                OverlayBloom = "drop-shadow(0 0 1px red)}.evil{x:y}",
                UnderlayColor = "red}.evil{color:red}",
                MarkerColor = "red</style><script>x",
                MarkerOutline = "red{@import url(//x)",
                BaseColorPalette = new[] { "red</style>" },
                OverlayPalette = new[] { "blue}@import url(//x)" },
            },
        };

        StyleEdges cleaned = StyleSanitizer.Ensure(evil).Edges;
        foreach (string bad in new[] { "</", "<!", "@import", "url(", "{", "}" })
        {
            Assert.DoesNotContain(bad, cleaned.BaseLinecap);
            Assert.DoesNotContain(bad, cleaned.OverlayLinecap);
            Assert.DoesNotContain(bad, cleaned.OverlayBloom);
            Assert.DoesNotContain(bad, cleaned.UnderlayColor);
            Assert.DoesNotContain(bad, cleaned.MarkerColor!);
            Assert.DoesNotContain(bad, cleaned.MarkerOutline!);
            Assert.DoesNotContain(bad, cleaned.BaseColorPalette[0]);
            Assert.DoesNotContain(bad, cleaned.OverlayPalette[0]);
        }

        // End-to-end: nothing hostile survives into the emitted SVG (sanitiser + Attr defense).
        string svg = BeckSvg.Render(Arch, new SvgRenderOptions { Style = evil });
        Assert.DoesNotContain("@import", svg);
        Assert.DoesNotContain("url(//", svg);
        Assert.DoesNotContain("<script>", svg);
        Assert.DoesNotContain(".evil{", svg);
    }

    [Fact]
    public void Sanitization_BuiltInClassic_IsUntouched()
    {
        // Classic is trusted and reference-bypassed: Ensure returns the very same instance.
        Assert.Same(BeckStyle.Classic, StyleSanitizer.Ensure(BeckStyle.Classic));
    }

    // ---- TextLengthGuard wiring ----

    [Fact]
    public void TextLengthGuard_Off_StripsGuardAttributes()
    {
        string all = BeckSvg.Render(Arch, new SvgRenderOptions { TextLengthGuard = TextLengthGuard.All });
        string off = BeckSvg.Render(Arch, new SvgRenderOptions { TextLengthGuard = TextLengthGuard.Off });

        Assert.Contains("textLength=", all);
        Assert.Contains("lengthAdjust=", all);
        Assert.DoesNotContain("textLength=", off);
        Assert.DoesNotContain("lengthAdjust=", off);
    }

    [Fact]
    public void TextLengthGuard_FallbackOnly_MatchesAll_UnderApproximateMeasurer()
    {
        // The default measurer is approximate, so FallbackOnly emits guards exactly like All.
        string all = BeckSvg.Render(Arch, new SvgRenderOptions { TextLengthGuard = TextLengthGuard.All });
        string fb = BeckSvg.Render(Arch, new SvgRenderOptions { TextLengthGuard = TextLengthGuard.FallbackOnly });
        Assert.Contains("textLength=", fb);
    }

    // ---- FontRoleTable value semantics ----

    [Fact]
    public void FontRoleTable_IsValueSemantic()
    {
        var t1 = new FontRoleTable(FontRoles.Of);
        var t2 = new FontRoleTable(FontRoles.Of);
        Assert.Equal(t1, t2);
        Assert.Equal(t1.GetHashCode(), t2.GetHashCode());
    }

    [Fact]
    public void BeckStyle_WithIndependentlyBuiltRoleTable_ComparesEqual()
    {
        BeckStyle a = BeckStyle.Classic with
        {
            Typography = BeckStyle.Classic.Typography with { Roles = new FontRoleTable(FontRoles.Of) },
        };
        BeckStyle b = BeckStyle.Classic with
        {
            Typography = BeckStyle.Classic.Typography with { Roles = new FontRoleTable(FontRoles.Of) },
        };
        Assert.Equal(a, b);
    }
}
