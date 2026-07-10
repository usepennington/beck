using Beck.Styles;
using Xunit;

namespace Beck.Tests;

/// <summary>
/// The per-style pulse character (<see cref="StyleMotion.Pulse"/>): every packet arrival fires a
/// node pulse, and each built-in style renders it with its own identity effect instead of the one
/// classic ripple (the "pulsing has no theme-specific character" gap). These tests pin each style's
/// compiled signature — the overlay element and/or its keyframe recipe — plus the classic
/// byte-identity anchor (<see cref="PulseEffect.Ripple"/> must reproduce the historical output,
/// which <see cref="ClassicSvgGoldenTests"/> already freezes at the whole-file level).
/// </summary>
public sealed class StylePulseTests
{
    // One hop → one arrival pulse on the destination card.
    private const string FlowYaml = """
        type: architecture
        nodes:
          - { id: a, title: Client }
          - { id: b, title: Engine }
        edges:
          - { from: a, to: b }
        flow:
          steps:
            - packet: { from: a, to: b }
        """;

    private static string Render(BeckStyle style) =>
        BeckSvg.Render(FlowYaml, new SvgRenderOptions { Style = style, IdSuffix = "pu15etst" });

    [Fact]
    public void EveryBuiltInStyle_DeclaresItsPulseEffect()
    {
        Assert.Equal(PulseEffect.Ripple, BeckStyle.Classic.Motion.Pulse);
        Assert.Equal(PulseEffect.SurveyRing, BlueprintStyle.Instance.Motion.Pulse);
        Assert.Equal(PulseEffect.MarkerPop, SketchStyle.Instance.Motion.Pulse);
        Assert.Equal(PulseEffect.Flash, MinimalStyle.Instance.Motion.Pulse);
        Assert.Equal(PulseEffect.Slam, BrutalistStyle.Instance.Motion.Pulse);
        Assert.Equal(PulseEffect.Flash, ExtrudeStyle.Instance.Motion.Pulse);
        Assert.Equal(PulseEffect.Flicker, TerminalStyle.Instance.Motion.Pulse);
        Assert.Equal(PulseEffect.GlowRing, GlowStyle.Instance.Motion.Pulse);
        Assert.Equal(PulseEffect.Led, CircuitStyle.Instance.Motion.Pulse);
    }

    // The "no zoom" rule: these styles pin the card in place — the overlay cue carries the whole
    // arrival/highlight read. Classic, sketch (whose pop IS the transform), blueprint, and extrude
    // (whose press-down is its identity) keep their transforms.
    [Fact]
    public void NoZoomStyles_DisableTheLift()
    {
        Assert.False(MinimalStyle.Instance.Motion.LiftEnabled);
        Assert.False(TerminalStyle.Instance.Motion.LiftEnabled);
        Assert.False(GlowStyle.Instance.Motion.LiftEnabled);
        Assert.False(CircuitStyle.Instance.Motion.LiftEnabled);
        Assert.False(BrutalistStyle.Instance.Motion.LiftEnabled);
        Assert.True(BeckStyle.Classic.Motion.LiftEnabled);
        Assert.True(SketchStyle.Instance.Motion.LiftEnabled);
        Assert.True(ExtrudeStyle.Instance.Motion.LiftEnabled);
    }

    [Fact]
    public void Classic_KeepsTheHistoricalRipple()
    {
        var svg = Render(BeckStyle.Classic);
        // The classic ripple element + its scale(1.15) expansion, untouched.
        Assert.Contains("transform:scale(1.15)", svg);
        Assert.Contains("translateY(-2px) scale(1.04)", svg);
    }

    [Fact]
    public void Blueprint_SurveyRing_IsOffsetLinearAndRectangular()
    {
        var svg = Render(BlueprintStyle.Instance);
        // Linear (un-eased, technical) outward scale — not classic's Power2Out ripple curve.
        Assert.Contains("animation-timing-function:linear", svg);
        Assert.Contains("transform:scale(1.22)", svg);
        Assert.DoesNotContain("transform:scale(1.15)", svg);
    }

    [Fact]
    public void Sketch_MarkerPop_JoltsTheCard_WithNoOverlayElement()
    {
        var svg = Render(SketchStyle.Instance);
        // The card itself pops…
        Assert.Contains("scale(1.08)", svg);
        // …and no ripple overlay element (or track) is emitted at all.
        Assert.DoesNotContain("brip", svg);
        Assert.DoesNotContain("translateY(-2px) scale(1.04)", svg);
    }

    [Fact]
    public void Minimal_Flash_TintsTheCardFace_WithoutMoving()
    {
        var svg = Render(MinimalStyle.Instance);
        // A filled wash (no stroke ring), peaking at 0.45 × the 0.4 amplitude.
        Assert.Contains("fill=\"var(--beck-packet)\" opacity=\"0\"/>", svg);
        Assert.Contains("opacity:0.18;", svg);
        Assert.DoesNotContain("transform:scale(1.15)", svg);
        // LiftEnabled=false: the card never zooms — no lift transform anywhere.
        Assert.DoesNotContain("translateY(-2px) scale(1.04)", svg);
    }

    [Fact]
    public void Brutalist_Slam_SnapsAThickBorder_NoEasingNoScalingNoZoom()
    {
        var svg = Render(BrutalistStyle.Instance);
        // OverlayStroke 2 × 2.4 — the slammed border.
        Assert.Contains("stroke-width=\"4.8\"", svg);
        // Hard cuts: the slam keyframes carry no timing function and no transform, and the card
        // itself never lifts (LiftEnabled=false).
        Assert.DoesNotContain("transform:scale(1.15)", svg);
        Assert.DoesNotContain("translateY(-2px) scale(1.04)", svg);
    }

    [Fact]
    public void Terminal_Flicker_BlinksTwice()
    {
        var svg = Render(TerminalStyle.Instance);
        // Two on-windows at 0.5 amplitude → the on-value appears in two paired windows;
        // the invert wash is a filled rect over the card face.
        Assert.Contains("fill=\"var(--beck-packet)\" opacity=\"0\"/>", svg);
        var on = svg.Split("opacity:0.5;").Length - 1;
        Assert.True(on >= 4, $"expected two on/off flicker windows (≥4 stops at 0.5), saw {on}");
        // LiftEnabled=false: phosphor cells don't move.
        Assert.DoesNotContain("translateY(-2px) scale(1.04)", svg);
    }

    [Fact]
    public void Glow_GlowRing_CarriesABloomHalo_WithoutMoving()
    {
        var svg = Render(GlowStyle.Instance);
        Assert.Contains("filter:drop-shadow(0 0 6px var(--beck-packet))", svg);
        Assert.Contains("transform:scale(1.3)", svg);
        Assert.DoesNotContain("translateY(-2px) scale(1.04)", svg);
    }

    [Fact]
    public void Circuit_Led_BlinksAnAmberCornerDot_WithoutMoving()
    {
        var svg = Render(CircuitStyle.Instance);
        Assert.Contains("r=\"3\" fill=\"var(--beck-gold)\"", svg);
        Assert.DoesNotContain("transform:scale(1.15)", svg);
        Assert.DoesNotContain("translateY(-2px) scale(1.04)", svg);
    }

    [Fact]
    public void CustomStyle_PulseColor_IsSanitized()
    {
        var custom = BeckStyle.Classic with
        {
            Name = "custom-pulse",
            Motion = BeckStyle.Classic.Motion with
            {
                Pulse = PulseEffect.Flash,
                PulseColor = "red}</style><script>url(evil)",
            },
        };
        var svg = BeckSvg.Render(FlowYaml, new SvgRenderOptions { Style = custom, IdSuffix = "pu15etst" });
        Assert.DoesNotContain("</style><script>", svg);
        Assert.DoesNotContain("red}", svg);
        Assert.DoesNotContain("url(evil)", svg);
    }

    [Fact]
    public void PulseEffects_AreDeterministic()
    {
        foreach (var style in BeckStyles.All)
        {
            Assert.Equal(Render(style), Render(style));
        }
    }
}