using System.Globalization;
using System.Text.Json;
using Beck.Rendering;
using Beck.Rendering.Route;
using Xunit;

namespace Beck.Rendering.Tests;

/// <summary>
/// M4 gate: <see cref="EdgePainter"/> reproduces the reference <c>routeEdges</c>
/// path <c>d</c> for every edge, token-by-token within ±0.01, when fed the same
/// (M3 golden) layout. Also asserts the standing invariant: no negative path
/// coordinates (the off-canvas routing-regression signature). Goldens captured by
/// feeding the M3 layout into the browser's routeEdges.
/// </summary>
public sealed class RouteParityTests
{
    private static readonly string CorpusDir = Path.Combine(AppContext.BaseDirectory, "Corpus");
    private static readonly string LayoutDir = Path.Combine(AppContext.BaseDirectory, "Goldens", "layout");
    private static readonly string RouteGolden = Path.Combine(AppContext.BaseDirectory, "Goldens", "route", "edges.json");
    private static JsonSerializerOptions Opts => new() { PropertyNameCaseInsensitive = true };

    private sealed record RectDto(double X, double Y, double W, double H);
    private sealed record LayoutDto(Dictionary<string, RectDto> Nodes, Dictionary<string, RectDto> Groups, double Width, double Height);

    private static readonly Dictionary<string, Dictionary<string, string>> Golden =
        JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(File.ReadAllText(RouteGolden), Opts)!;

    public static IEnumerable<object[]> Files() => Golden.Keys.Select(f => new object[] { f });

    [Theory]
    [MemberData(nameof(Files))]
    public void Route_MatchesOracle(string file)
    {
        var ld = JsonSerializer.Deserialize<LayoutDto>(
            File.ReadAllText(Path.Combine(LayoutDir, file + ".layout.json")), Opts)!;
        static Dictionary<string, Rect> Rects(Dictionary<string, RectDto> m) =>
            m.ToDictionary(kv => kv.Key, kv => new Rect(kv.Value.X, kv.Value.Y, kv.Value.W, kv.Value.H));
        var layout = new LayoutResult(Rects(ld.Nodes), Rects(ld.Groups), ld.Width, ld.Height);

        DiagramModel model = Validate.LoadDiagram(File.ReadAllText(Path.Combine(CorpusDir, file + ".yaml")));
        var got = EdgePainter.RouteEdges(model, layout).ToDictionary(r => r.Edge.Id, r => r.D);

        var fails = new List<string>();
        foreach (var (id, want) in Golden[file])
        {
            if (!got.TryGetValue(id, out string? mine)) { fails.Add($"  {id}: missing"); continue; }
            string? diff = ComparePath(want, mine);
            if (diff != null) fails.Add($"  {id}: {diff}\n      oracle: {want}\n      got:    {mine}");
            foreach (double n in Numbers(mine))
                if (n < -0.01) fails.Add($"  {id}: NEGATIVE coordinate {n} (off-canvas routing regression)");
        }
        Assert.True(fails.Count == 0, $"{file} route mismatch:\n{string.Join("\n", fails)}");
    }

    /// <summary>Token-by-token path compare: commands exact, numbers within ±0.01.</summary>
    private static string? ComparePath(string a, string b)
    {
        string[] ta = Tokens(a), tb = Tokens(b);
        if (ta.Length != tb.Length) return $"token count {ta.Length} vs {tb.Length}";
        for (int i = 0; i < ta.Length; i++)
        {
            bool na = double.TryParse(ta[i], NumberStyles.Float, CultureInfo.InvariantCulture, out double va);
            bool nb = double.TryParse(tb[i], NumberStyles.Float, CultureInfo.InvariantCulture, out double vb);
            if (na && nb) { if (Math.Abs(va - vb) > 0.01) return $"token {i}: {ta[i]} vs {tb[i]}"; }
            else if (ta[i] != tb[i]) return $"token {i}: '{ta[i]}' vs '{tb[i]}'";
        }
        return null;
    }

    private static string[] Tokens(string d) =>
        d.Replace(',', ' ').Split(' ', StringSplitOptions.RemoveEmptyEntries);

    private static IEnumerable<double> Numbers(string d)
    {
        foreach (string t in Tokens(d))
            if (double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out double n)) yield return n;
    }
}
