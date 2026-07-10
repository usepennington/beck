using System.Text.RegularExpressions;
using Beck.Layout;
using Beck.Model;
using Beck.Rendering;
using Beck.Rendering.Text;
using Beck.Styles;
using Beck.Text;
using Xunit;

namespace Beck.Tests;

/// <summary>
/// Targeted assertions for the <c>terminal</c> style's <c>[bracketed]</c> node-title affordance
/// (<see cref="StyleTypography.TitlePrefix"/>/<see cref="StyleTypography.TitleSuffix"/>). These pin the
/// data-driven label decoration's contract beyond the generic <see cref="StyleSmokeTests"/> invariants:
/// the brackets reach every primary node-title role, they widen the <em>measured</em> box (so the run
/// is not squeezed by the <c>textLength</c> guard), and non-title text stays bare.
/// </summary>
public sealed class TerminalBracketTests
{
    private static readonly string CorpusDir = Path.Combine(AppContext.BaseDirectory, "Corpus");
    private static string ArchKitchen() => File.ReadAllText(Path.Combine(CorpusDir, "arch-kitchen.yaml"));

    // A rendered node title <text> whose content is the captured (possibly bracketed) run.
    private static readonly Regex NodeTitle = new("<text class=\"beck-node-title\"[^>]*>([^<]*)</text>", RegexOptions.Compiled);

    [Fact]
    public void Terminal_WrapsCardTitlesInBrackets_ClassicDoesNot()
    {
        string yaml = ArchKitchen();
        string terminal = BeckSvg.Render(yaml, new SvgRenderOptions { Style = TerminalStyle.Instance });
        string classic = BeckSvg.Render(yaml, new SvgRenderOptions { Style = BeckStyle.Classic });

        var termTitles = NodeTitle.Matches(terminal).Select(m => m.Groups[1].Value).ToList();
        Assert.True(termTitles.Count > 0, "arch-kitchen should render node titles");
        // Every rendered card/pill title under terminal is [bracketed].
        Assert.All(termTitles, t => Assert.True(t.StartsWith("[") && t.EndsWith("]"), $"expected bracketed title, got '{t}'"));

        // Classic renders the same titles bare (no leading '[').
        var classicTitles = NodeTitle.Matches(classic).Select(m => m.Groups[1].Value).ToList();
        Assert.All(classicTitles, t => Assert.False(t.StartsWith("["), $"classic must not bracket titles, got '{t}'"));
    }

    [Fact]
    public void Terminal_BracketsReachClassTitlesAndGhostLabels()
    {
        // Class-diagram class titles and architecture ghost labels are also primary node titles.
        string cls = BeckSvg.Render(File.ReadAllText(Path.Combine(CorpusDir, "class.yaml")), new SvgRenderOptions { Style = TerminalStyle.Instance });
        Assert.Matches(new Regex("<text class=\"beck-class-title\"[^>]*>\\[[^<]*\\]</text>"), cls);

        string arch = BeckSvg.Render(ArchKitchen(), new SvgRenderOptions { Style = TerminalStyle.Instance });
        Assert.Matches(new Regex("<text class=\"beck-ghost-label\"[^>]*>\\[[^<]*\\]</text>"), arch);
    }

    [Fact]
    public void Terminal_SubtitleAndStatusStayBare()
    {
        // Only the title carries brackets; the subtitle and status pill render verbatim.
        string yaml = "type: architecture\n" +
                      "nodes: [{ id: a, title: Gateway, subtitle: edge, status: live }]\nedges: []\n";
        string svg = BeckSvg.Render(yaml, new SvgRenderOptions { Style = TerminalStyle.Instance });

        Assert.Matches(new Regex("<text class=\"beck-node-title\"[^>]*>\\[Gateway\\]</text>"), svg);
        Assert.Matches(new Regex("<text class=\"beck-node-subtitle\"[^>]*>edge</text>"), svg);
        Assert.Matches(new Regex("<text class=\"beck-status-text\"[^>]*>live</text>"), svg);
        Assert.DoesNotContain("[edge]", svg);
        Assert.DoesNotContain("[live]", svg);
    }

    [Fact]
    public void Terminal_BracketsAreTextLengthGuarded_AndMatchTheMeasuredRun()
    {
        // The rendered bracketed run carries a textLength guard, and the guard is the width the sizer
        // measured for the *bracketed* string — proving the decoration ran before measurement (no
        // desync that would squeeze the glyphs). We re-measure "[Gateway]" and expect it in the markup.
        string yaml = "type: architecture\nnodes: [{ id: a, title: Gateway }]\nedges: []\n";
        string svg = BeckSvg.Render(yaml, new SvgRenderOptions { Style = TerminalStyle.Instance });

        Match title = Regex.Match(svg, "<text class=\"beck-node-title\"[^>]*textLength=\"([0-9.]+)\"[^>]*>\\[Gateway\\]</text>");
        Assert.True(title.Success, "bracketed title must carry a textLength guard");

        double guarded = double.Parse(title.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        FontRoleSpec spec = TerminalStyle.Instance.Typography.Roles.Of(FontRole.CardTitle);
        double measured = InterMetricsMeasurer.Instance.Measure("[Gateway]", FontRole.CardTitle, spec).Width;
        // The guard is the measured advance of the bracketed run (rounded to 2dp in the emitter).
        Assert.Equal(measured, guarded, 1);
    }

    [Fact]
    public void Terminal_BracketedTitle_WidensMeasuredCard_OverClassic()
    {
        // The brackets add width: the same node sized with terminal's prefix/suffix must produce a
        // strictly wider card box than the undecorated classic measurement (same geometry + measurer).
        // A title comfortably past CardMinW so the auto-grow (not the min-width floor) governs both.
        NodeModel node = Validate.LoadDiagram("type: architecture\nnodes: [{ id: a, title: \"Payment Gateway Service\" }]\nedges: []\n").Nodes[0];
        StyleGeometry geo = BeckStyle.Classic.Geometry;
        ITextMeasurer m = InterMetricsMeasurer.Instance;

        Size bare = CardSizer.Measure(node, m, geo, BeckStyle.Classic.Typography.Roles);
        Size bracketed = CardSizer.Measure(node, m, geo, TerminalStyle.Instance.Typography.Roles, "[", "]");
        Assert.True(bracketed.W > bare.W, $"brackets should widen the card: bare={bare.W} bracketed={bracketed.W}");
    }

    [Fact]
    public void Terminal_DiagramTitle_StaysUnbracketed()
    {
        // meta.title is not a node title — it must not gain brackets.
        string yaml = "type: architecture\nmeta: { title: My System }\nnodes: [{ id: a, title: Node }]\nedges: []\n";
        string svg = BeckSvg.Render(yaml, new SvgRenderOptions { Style = TerminalStyle.Instance });
        Assert.Matches(new Regex("<text class=\"beck-title\"[^>]*>My System</text>"), svg);
        Assert.DoesNotContain("[My System]", svg);
    }
}
