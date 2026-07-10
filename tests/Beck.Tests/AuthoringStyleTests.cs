using Beck.Authoring;
using Beck.Model;
using Xunit;

namespace Beck.Tests;

/// <summary>
/// The authoring side of the <c>meta.style</c> schema contract: all four builders emit
/// <c>style:</c> under <c>meta</c> and the emitted YAML round-trips through the parser onto
/// <c>DiagramMeta.StyleName</c>. Covers both the <c>string</c> and <see cref="BeckStyle"/> overloads.
/// </summary>
public sealed class AuthoringStyleTests
{
    private static string StyleNameOf(string yaml) => Validate.LoadDiagram(yaml).Meta.StyleName!;

    [Fact]
    public void DiagramBuilder_StyleString_EmitsAndRoundTrips()
    {
        var yaml = new DiagramBuilder("T").Style("sketch").Node("a", "A").ToYaml();
        Assert.Contains("style: sketch", yaml);
        Assert.Equal("sketch", StyleNameOf(yaml));
    }

    [Fact]
    public void SequenceDiagramBuilder_StyleString_EmitsAndRoundTrips()
    {
        var yaml = new SequenceDiagramBuilder("T").Style("sketch")
            .Participant("a", "A").Participant("b", "B").Message("a", "b", "hi").ToYaml();
        Assert.Contains("style: sketch", yaml);
        Assert.Equal("sketch", StyleNameOf(yaml));
    }

    [Fact]
    public void StateDiagramBuilder_StyleString_EmitsAndRoundTrips()
    {
        var yaml = new StateDiagramBuilder("T").Style("sketch")
            .State("s1", "S1").State("s2", "S2").Transition("s1", "s2").ToYaml();
        Assert.Contains("style: sketch", yaml);
        Assert.Equal("sketch", StyleNameOf(yaml));
    }

    [Fact]
    public void ClassDiagramBuilder_StyleString_EmitsAndRoundTrips()
    {
        var yaml = new ClassDiagramBuilder("T").Style("sketch").Class("c", "C").ToYaml();
        Assert.Contains("style: sketch", yaml);
        Assert.Equal("sketch", StyleNameOf(yaml));
    }

    [Fact]
    public void DiagramBuilder_StyleFromBeckStyle_EmitsItsName()
    {
        var yaml = new DiagramBuilder("T").Style(BeckStyle.Classic).Node("a", "A").ToYaml();
        Assert.Contains("style: classic", yaml);
        Assert.Equal("classic", StyleNameOf(yaml));
    }

    [Fact]
    public void NoStyle_OmitsTheKey()
    {
        var yaml = new DiagramBuilder("T").Node("a", "A").ToYaml();
        Assert.DoesNotContain("style:", yaml);
    }
}