using Beck.Model;
using Xunit;

namespace Beck.Tests;

/// <summary>
/// Focused unit tests for the model port's tolerances, JS-semantics helpers, and
/// error paths — the branches the corpus parity gate can't reach (no error inputs
/// live in the corpus). Expected messages are verbatim from the TS BeckError text.
/// </summary>
public sealed class ModelUnitTests
{
    // ---- JS numeric semantics ----

    [Theory]
    [InlineData(2.5, 3)]     // half rounds toward +∞ (not banker's)
    [InlineData(-2.5, -2)]
    [InlineData(0.5, 1)]
    [InlineData(-0.5, 0)]    // Math.floor(-0.5 + 0.5) == 0
    [InlineData(2.4, 2)]
    public void JsRound_MatchesJavaScript(double input, double expected) =>
        Assert.Equal(expected, Js.Round(input));

    [Theory]
    [InlineData(3.0, "3")]
    [InlineData(-1.0, "-1")]
    [InlineData(1.5, "1.5")]
    [InlineData(1.2, "1.2")]
    [InlineData(0.0, "0")]
    public void JsStr_MatchesJavaScript(double input, string expected) =>
        Assert.Equal(expected, Js.Str(input));

    // ---- coercion tolerances (via the model) ----

    [Fact]
    public void QuotedNumericString_CoercesToNumber()
    {
        var m = Validate.LoadDiagram("type: architecture\nmeta: { spacing: { rank: \"150\" } }\nnodes: [{ id: a }]\n");
        Assert.Equal(150, m.Meta.Spacing.Rank);
    }

    [Theory]
    [InlineData("false", false)] // quoted bool string
    [InlineData("true", true)]
    public void QuotedBoolString_CoercesToBool(string value, bool expected)
    {
        var m = Validate.LoadDiagram($"type: architecture\nmeta: {{ animate: \"{value}\" }}\nnodes: [{{ id: a }}]\n");
        Assert.Equal(expected, m.Meta.Animate);
    }

    [Fact]
    public void PlainNumberTitle_StringifiesLikeJs()
    {
        var m = Validate.LoadDiagram("type: architecture\nnodes: [{ id: a, title: 2 }]\n");
        Assert.Equal("2", m.Nodes[0].Title);
        Assert.Equal("a", m.Nodes[0].Id);
    }

    [Fact]
    public void PlainBoolTitle_StringifiesToTrue()
    {
        var m = Validate.LoadDiagram("type: architecture\nnodes: [{ id: a, title: true }]\n");
        Assert.Equal("true", m.Nodes[0].Title);
    }

    [Fact]
    public void NarrateBool_TogglesEnabled()
    {
        var on = Validate.LoadDiagram("type: architecture\nmeta: { narrate: true }\nnodes: [{ id: a }]\n");
        var off = Validate.LoadDiagram("type: architecture\nmeta: { narrate: false }\nnodes: [{ id: a }]\n");
        Assert.True(on.Meta.Narration.Enabled);
        Assert.False(off.Meta.Narration.Enabled);
    }

    [Fact]
    public void BurstCount_ClampsAndRounds()
    {
        string Yaml(string count) =>
            $"type: architecture\nnodes: [{{ id: a }}, {{ id: b }}]\nedges: [{{ from: a, to: b }}]\n" +
            $"flow: {{ steps: [{{ burst: {{ from: a, to: b, count: {count} }} }}] }}\n";
        Assert.Equal(24, ((BurstStep)Validate.LoadDiagram(Yaml("100")).Flow.Steps[0]).Count);
        Assert.Equal(1, ((BurstStep)Validate.LoadDiagram(Yaml("0")).Flow.Steps[0]).Count);
        Assert.Equal(3, ((BurstStep)Validate.LoadDiagram(Yaml("2.5")).Flow.Steps[0]).Count); // Js.Round
    }

    // ---- error paths (verbatim TS messages) ----

    private static BeckYamlException Throws(string yaml) =>
        Assert.Throws<BeckYamlException>(() => Validate.LoadDiagram(yaml));

    [Fact]
    public void EmptyNodes_Throws() =>
        Assert.Equal("A diagram needs at least one node under `nodes`",
            Throws("type: architecture\nnodes: []\n").Message);

    [Fact]
    public void DuplicateNodeId_Throws() =>
        Assert.Equal("Duplicate node id \"a\"",
            Throws("type: architecture\nnodes: [{ id: a }, { id: a }]\n").Message);

    [Fact]
    public void UnknownEdgeSource_Throws() =>
        Assert.Equal("Edge references unknown source \"x\"",
            Throws("type: architecture\nnodes: [{ id: a }]\nedges: [{ from: x, to: a }]\n").Message);

    [Fact]
    public void UnknownEdgeTarget_Throws() =>
        Assert.Equal("Edge references unknown target \"y\"",
            Throws("type: architecture\nnodes: [{ id: a }]\nedges: [{ from: a, to: y }]\n").Message);

    [Fact]
    public void OneOf_MessageFormat() =>
        Assert.Equal("`meta.direction` must be one of: TB, BT, LR, RL (got \"sideways\")",
            Throws("type: architecture\nmeta: { direction: sideways }\nnodes: [{ id: a }]\n").Message);

    [Fact]
    public void TwoGroups_Throws() =>
        Assert.Equal("\"a\" is in two groups (\"g1\" and \"g2\")",
            Throws("type: architecture\nnodes: [{ id: a }]\n" +
                   "groups: [{ id: g1, members: [a] }, { id: g2, members: [a] }]\n").Message);

    [Fact]
    public void SelfContainingGroup_Throws() =>
        Assert.Equal("Group \"g\" cannot contain itself",
            Throws("type: architecture\nnodes: [{ id: a }]\ngroups: [{ id: g, members: [g] }]\n").Message);

    [Fact]
    public void UnknownGroupMember_Throws() =>
        Assert.Equal("Group \"g\" references unknown node or group \"z\"",
            Throws("type: architecture\nnodes: [{ id: a }]\ngroups: [{ id: g, members: [z] }]\n").Message);

    [Fact]
    public void UnknownFlowStep_Throws() =>
        Assert.Equal(
            "A flow step must have one of: packet, burst, status, highlight, pulse, activate, stream, working, idle, fail, narrate, phase, wait, reset, parallel",
            Throws("type: architecture\nnodes: [{ id: a }]\nflow: { steps: [{ bogus: 1 }] }\n").Message);

    [Fact]
    public void ReservedStateId_Throws() =>
        Assert.Equal(
            "\"[*]\" is reserved — reference the start/end pseudo-state from a transition instead",
            Throws("type: state\nstates: [{ id: \"[*]\" }]\ntransitions: []\n").Message);

    [Fact]
    public void MustBeMapping_Throws() =>
        Assert.Equal("`document` must be a mapping",
            Throws("- just\n- a\n- list\n").Message);
}