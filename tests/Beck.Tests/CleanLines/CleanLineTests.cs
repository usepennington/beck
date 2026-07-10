using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Xunit;
using Xunit.Abstractions;

namespace Beck.Tests.CleanLines;

/// <summary>
/// The clean-line chaos monkey. Fuzzes <see cref="SeedCount"/> seeded diagrams through
/// model → size → layout → route and scores every one with <see cref="LineQuality"/>.
///
/// <para>Hard violations (off-canvas, anchor off its face or on a corner, a diagonal on a
/// step-round edge) fail the run outright, quoting the seed so the case reproduces.</para>
///
/// <para>Aesthetics — straight-edge rate, micro-jogs, merged runs, skewed fans — are ratcheted
/// against <c>Goldens/cleanlines.json</c>: a change may improve them freely, but a regression
/// beyond the recorded tolerance fails. Re-record with
/// <c>BECK_REGEN=1 dotnet test --filter FullyQualifiedName~CleanLine</c>, and only when the
/// numbers moved the right way.</para>
/// </summary>
public sealed class CleanLineTests
{
    private readonly ITestOutputHelper _out;
    public CleanLineTests(ITestOutputHelper output) => _out = output;

    private const int SeedCount = 1500;

    private static string SourceDir([CallerFilePath] string self = "") => Path.GetDirectoryName(self)!;
    private static string BaselinePath => Path.Combine(SourceDir(), "..", "Goldens", "cleanlines.json");

    private sealed record Baseline(
        double StraightRate, double MicroJogsPerEdge, double MergedRunsPerEdge,
        double SkewedFaceRate, double BendsPerEdge, int ThroughNodeDiagrams);

    [Fact]
    public void ChaosMonkey_NoHardViolations()
    {
        var failures = new List<string>();
        for (int seed = 0; seed < SeedCount; seed++)
        {
            QualityReport r = LineQuality.Analyze(DiagramFuzzer.Yaml(seed));
            foreach (Defect d in r.Violations.Where(d => d.IsHard).Take(3))
                failures.Add($"seed {seed}: [{d.Kind}] {d.Detail}");
            if (failures.Count > 30) break;
        }
        Assert.True(failures.Count == 0,
            $"{failures.Count} routing violations over {SeedCount} fuzzed diagrams "
            + $"(reproduce with BECK_SEED=<seed> ... --filter DumpSeed):\n  " + string.Join("\n  ", failures));
    }

    [Fact]
    public void ChaosMonkey_AestheticsDoNotRegress()
    {
        int edges = 0, straight = 0, jogs = 0, merged = 0, faces = 0, skewed = 0, bends = 0, throughNode = 0;
        var worst = new List<(double Score, int Seed, QualityReport R)>();

        for (int seed = 0; seed < SeedCount; seed++)
        {
            QualityReport r = LineQuality.Analyze(DiagramFuzzer.Yaml(seed));
            edges += r.Edges; straight += r.StraightEdges; jogs += r.MicroJogs;
            merged += r.MergedRuns; faces += r.Faces; skewed += r.SkewedFaces; bends += r.Bends;
            if (r.Violations.Any(d => d.Kind == "through-node")) throughNode++;
            if (r.Edges > 0) worst.Add(((r.MicroJogs + r.MergedRuns) / (double)r.Edges, seed, r));
        }

        var got = new Baseline(
            StraightRate: (double)straight / edges,
            MicroJogsPerEdge: (double)jogs / edges,
            MergedRunsPerEdge: (double)merged / edges,
            SkewedFaceRate: faces == 0 ? 0 : (double)skewed / faces,
            BendsPerEdge: (double)bends / edges,
            ThroughNodeDiagrams: throughNode);

        _out.WriteLine(Scorecard(got, edges, faces));
        _out.WriteLine("");
        _out.WriteLine("Worst offenders (seed → micro-jogs + merged runs per edge):");
        foreach (var w in worst.OrderByDescending(w => w.Score).Take(8))
            _out.WriteLine(FormattableString.Invariant(
                $"  seed {w.Seed,4}  score {w.Score:0.00}  edges {w.R.Edges,2}  jogs {w.R.MicroJogs,2}  merged {w.R.MergedRuns,2}  straight {w.R.StraightRate:0%}"));

        if (Environment.GetEnvironmentVariable("BECK_REGEN") == "1")
        {
            File.WriteAllText(Path.GetFullPath(BaselinePath),
                JsonSerializer.Serialize(got, new JsonSerializerOptions { WriteIndented = true }));
            return;
        }

        Baseline want = JsonSerializer.Deserialize<Baseline>(
            File.ReadAllText(Path.GetFullPath(BaselinePath)),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

        var regressions = new List<string>();
        void HigherIsBetter(string name, double g, double w, double tol = 0.005)
        {
            if (g < w - tol) regressions.Add(FormattableString.Invariant($"{name}: {g:0.0000} < baseline {w:0.0000}"));
        }
        void LowerIsBetter(string name, double g, double w, double tol = 0.005)
        {
            if (g > w + tol) regressions.Add(FormattableString.Invariant($"{name}: {g:0.0000} > baseline {w:0.0000}"));
        }
        HigherIsBetter("straight-edge rate", got.StraightRate, want.StraightRate);
        LowerIsBetter("micro-jogs per edge", got.MicroJogsPerEdge, want.MicroJogsPerEdge);
        LowerIsBetter("merged runs per edge", got.MergedRunsPerEdge, want.MergedRunsPerEdge);
        LowerIsBetter("skewed-face rate", got.SkewedFaceRate, want.SkewedFaceRate);
        LowerIsBetter("bends per edge", got.BendsPerEdge, want.BendsPerEdge, 0.01);
        if (got.ThroughNodeDiagrams > want.ThroughNodeDiagrams)
            regressions.Add($"diagrams with an edge cutting a node: {got.ThroughNodeDiagrams} > baseline {want.ThroughNodeDiagrams}");

        Assert.True(regressions.Count == 0,
            "Line quality regressed against Goldens/cleanlines.json:\n  "
            + string.Join("\n  ", regressions)
            + "\n\nIf the change is an intentional trade, re-record with BECK_REGEN=1.");
    }

    private static string Scorecard(Baseline b, int edges, int faces) => string.Join('\n', new[]
    {
        $"clean-line scorecard over {SeedCount} diagrams / {edges} edges / {faces} shared faces",
        FormattableString.Invariant($"  straight edges     {b.StraightRate:0.0%}"),
        FormattableString.Invariant($"  bends per edge     {b.BendsPerEdge:0.000}"),
        FormattableString.Invariant($"  micro-jogs / edge  {b.MicroJogsPerEdge:0.000}"),
        FormattableString.Invariant($"  merged run pairs   {b.MergedRunsPerEdge:0.000} per edge"),
        FormattableString.Invariant($"  skewed fans        {b.SkewedFaceRate:0.0%} of shared faces"),
        $"  through-node       {b.ThroughNodeDiagrams} diagrams ({(double)b.ThroughNodeDiagrams / SeedCount:0.0%})",
    });

    /// <summary>
    /// Debugging affordance, not a gate: dumps one fuzzed seed's YAML, node boxes and routed
    /// polylines so a monkey failure can be read by eye.
    /// <c>BECK_SEED=468 dotnet test --filter FullyQualifiedName~DumpSeed -l "console;verbosity=detailed"</c>
    /// </summary>
    [Fact]
    public void DumpSeed()
    {
        if (Environment.GetEnvironmentVariable("BECK_SEED") is not string s || !int.TryParse(s, out int seed)) return;
        string yaml = DiagramFuzzer.Yaml(seed);
        _out.WriteLine(yaml);
        QualityReport r = LineQuality.Analyze(yaml);
        _out.WriteLine(Describe($"seed {seed}", yaml, r));
        foreach (Defect d in r.Violations) _out.WriteLine($"  !! [{d.Kind}] {d.Detail}");
    }

    /// <summary>Debugging affordance: score and dump an arbitrary YAML file. BECK_YAML=&lt;path&gt;.</summary>
    [Fact]
    public void DumpYaml()
    {
        if (Environment.GetEnvironmentVariable("BECK_YAML") is not string path || !File.Exists(path)) return;
        string yaml = File.ReadAllText(path);
        QualityReport r = LineQuality.Analyze(yaml);
        _out.WriteLine(Describe(Path.GetFileName(path), yaml, r));
        var (_, layout, _) = LineQuality.Route(yaml);
        foreach (var (id, rect) in layout.Groups.OrderBy(k => k.Key))
            _out.WriteLine(FormattableString.Invariant(
                $"  group {id,-8} x {rect.X:0.#}..{rect.X + rect.W:0.#}  y {rect.Y:0.#}..{rect.Y + rect.H:0.#}"));
        _out.WriteLine($"  canvas {layout.Width} × {layout.Height}");
        foreach (Defect d in r.Violations) _out.WriteLine($"  !! [{d.Kind}] {d.Detail}");
    }

    // ---- pinned real-world shapes: the two diagrams that motivated the harness ----

    public const string ServeAndBuild = """
        type: architecture
        meta: { direction: TB }
        nodes:
          - { id: browser, title: Browser, kind: user }
          - { id: kestrel, title: Kestrel, subtitle: "dev serve" }
          - { id: crawler, title: Build crawler }
          - { id: pipeline, title: ASP.NET request pipeline, subtitle: "middleware · response processors · renderers", accent: primary }
          - { id: output, title: Output directory, kind: db }
        edges:
          - { from: browser, to: kestrel, label: HTTP, arrow: both }
          - { from: kestrel, to: pipeline }
          - { from: crawler, to: pipeline, label: in-process }
          - { from: pipeline, to: output, label: build writes HTML }
        """;

    public const string ThreeIntoOne = """
        type: architecture
        meta: { direction: TB }
        nodes:
          - { id: markdown, title: Markdown folders }
          - { id: razor, title: Razor pages }
          - { id: custom, title: Custom sources, subtitle: "API reference · taxonomy · redirects" }
          - { id: pipeline, title: Content pipeline, subtitle: "discover · parse · render", accent: primary }
        edges:
          - { from: markdown, to: pipeline, note: "Every source reports routes into the same site model" }
          - { from: razor, to: pipeline }
          - { from: custom, to: pipeline }
        """;

    [Theory]
    [InlineData(nameof(ServeAndBuild))]
    [InlineData(nameof(ThreeIntoOne))]
    public void PinnedShapes_HaveNoViolations(string which)
    {
        string yaml = which == nameof(ServeAndBuild) ? ServeAndBuild : ThreeIntoOne;
        QualityReport r = LineQuality.Analyze(yaml);
        _out.WriteLine(Describe(which, yaml, r));
        Assert.Empty(r.Violations);
    }

    private static string Describe(string name, string yaml, QualityReport r)
    {
        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"{name}: {r.Edges} edges, {r.StraightEdges} straight ({r.StraightRate:0%}), "
            + $"{r.MicroJogs} micro-jogs, {r.MergedRuns} merged runs, {r.SkewedFaces}/{r.Faces} skewed fans");
        var (_, layout, edges) = LineQuality.Route(yaml);
        foreach (var e in edges)
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"  {e.Edge.Id,-24} {string.Join(" → ", e.Points.Select(p => $"({p.X:0.#},{p.Y:0.#})"))}");
        foreach (var (id, rect) in layout.Nodes.OrderBy(k => k.Key))
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"  node {id,-10} x {rect.X:0.#}..{rect.X + rect.W:0.#}  y {rect.Y:0.#}..{rect.Y + rect.H:0.#}  c ({rect.X + rect.W / 2:0.#},{rect.Y + rect.H / 2:0.#})");
        return sb.ToString();
    }
}
