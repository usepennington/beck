using Beck.Rendering;
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
        Assert.Equal(PulseEffect.StationRipple, MetroStyle.Instance.Motion.Pulse);
        Assert.Equal(PulseEffect.InkFrame, EditorialStyle.Instance.Motion.Pulse);
    }

    [Fact]
    public void Classic_KeepsTheHistoricalRipple()
    {
        string svg = Render(BeckStyle.Classic);
        // The classic ripple element + its scale(1.15) expansion, untouched.
        Assert.Contains("transform:scale(1.15)", svg);
        Assert.Contains("translateY(-2px) scale(1.04)", svg);
    }

    [Fact]
    public void Blueprint_SurveyRing_IsOffsetLinearAndRectangular()
    {
        string svg = Render(BlueprintStyle.Instance);
        // Linear (un-eased, technical) outward scale — not classic's Power2Out ripple curve.
        Assert.Contains("animation-timing-function:linear", svg);
        Assert.Contains("transform:scale(1.22)", svg);
        Assert.DoesNotContain("transform:scale(1.15)", svg);
    }

    [Fact]
    public void Sketch_MarkerPop_JoltsTheCard_WithNoOverlayElement()
    {
        string svg = Render(SketchStyle.Instance);
        // The card itself pops…
        Assert.Contains("scale(1.08)", svg);
        // …and no ripple overlay element (or track) is emitted at all.
        Assert.DoesNotContain("brip", svg);
        Assert.DoesNotContain("translateY(-2px) scale(1.04)", svg);
    }

    [Fact]
    public void Minimal_Flash_TintsTheCardFace()
    {
        string svg = Render(MinimalStyle.Instance);
        // A filled wash (no stroke ring), peaking at 0.45 × the 0.4 amplitude.
        Assert.Contains("fill=\"var(--beck-packet)\" opacity=\"0\"/>", svg);
        Assert.Contains("opacity:0.18;", svg);
        Assert.DoesNotContain("transform:scale(1.15)", svg);
    }

    [Fact]
    public void Brutalist_Slam_SnapsAThickBorder_NoEasingNoScaling()
    {
        string svg = Render(BrutalistStyle.Instance);
        // OverlayStroke 2 × 2.4 — the slammed border.
        Assert.Contains("stroke-width=\"4.8\"", svg);
        // Hard cuts: the slam keyframes carry no timing function and no transform.
        Assert.DoesNotContain("transform:scale(1.15)", svg);
    }

    [Fact]
    public void Terminal_Flicker_BlinksTwice()
    {
        string svg = Render(TerminalStyle.Instance);
        // Two on-windows at 0.5 amplitude → the on-value appears in two paired windows;
        // the invert wash is a filled rect over the card face.
        Assert.Contains("fill=\"var(--beck-packet)\" opacity=\"0\"/>", svg);
        int on = svg.Split("opacity:0.5;").Length - 1;
        Assert.True(on >= 4, $"expected two on/off flicker windows (≥4 stops at 0.5), saw {on}");
    }

    [Fact]
    public void Glow_GlowRing_CarriesABloomHalo()
    {
        string svg = Render(GlowStyle.Instance);
        Assert.Contains("filter:drop-shadow(0 0 6px var(--beck-packet))", svg);
        Assert.Contains("transform:scale(1.3)", svg);
    }

    [Fact]
    public void Circuit_Led_BlinksAnAmberCornerDot()
    {
        string svg = Render(CircuitStyle.Instance);
        Assert.Contains("r=\"3\" fill=\"var(--beck-gold)\"", svg);
        Assert.DoesNotContain("transform:scale(1.15)", svg);
    }

    [Fact]
    public void Metro_StationRipple_RadiatesFromTheCardCentre()
    {
        string svg = Render(MetroStyle.Instance);
        Assert.Contains("r=\"9\" fill=\"none\"", svg);
        Assert.Contains("transform:scale(2.6)", svg);
    }

    [Fact]
    public void Editorial_InkFrame_IsRedHeldAndPacedByPulseDur()
    {
        string svg = Render(EditorialStyle.Instance);
        Assert.Contains("rx=\"2\" fill=\"none\" stroke=\"var(--beck-danger)\"", svg);
        // No scaling — the frame inks in and lifts, it never expands.
        Assert.DoesNotContain("transform:scale(1.15)", svg);
        Assert.Equal(1.4, EditorialStyle.Instance.Motion.PulseDur);
    }

    [Fact]
    public void CustomStyle_PulseColor_IsSanitized()
    {
        BeckStyle custom = BeckStyle.Classic with
        {
            Name = "custom-pulse",
            Motion = BeckStyle.Classic.Motion with
            {
                Pulse = PulseEffect.Flash,
                PulseColor = "red}</style><script>url(evil)",
            },
        };
        string svg = BeckSvg.Render(FlowYaml, new SvgRenderOptions { Style = custom, IdSuffix = "pu15etst" });
        Assert.DoesNotContain("</style><script>", svg);
        Assert.DoesNotContain("red}", svg);
        Assert.DoesNotContain("url(evil)", svg);
    }

    [Fact]
    public void PulseEffects_AreDeterministic()
    {
        foreach (BeckStyle style in BeckStyles.All)
            Assert.Equal(Render(style), Render(style));
    }
}
