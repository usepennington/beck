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
/// step-round edge, an edge cutting through a node) fail the run outright, quoting the seed so
/// the case reproduces.</para>
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
        double SkewedFaceRate, double BendsPerEdge, double TightEdgeRate);

    /// <summary>
    /// The butterfly's own soft scorecard (mindmap edges are S-curves, so the orthogonal
    /// <see cref="Baseline"/> metrics don't apply). All three ratchet lower-is-better: less wasted
    /// canvas per node, shorter mean threads, and a half-balance nearer 1.0 (perfectly even halves).
    /// </summary>
    private sealed record MindmapBaseline(double AreaPerNode, double MeanEdgeLength, double HalfBalance);

    /// <summary>
    /// <c>Goldens/cleanlines.json</c> holds one baseline per fuzzed diagram type, keyed by name. The
    /// architecture entry is the original scorecard (values unchanged by the flowchart and mindmap
    /// chaos monkeys' introduction); flowchart and mindmap are their own keys so none ever contend.
    /// </summary>
    private sealed record BaselineFile(Baseline Architecture, Baseline Flowchart, MindmapBaseline Mindmap);

    private static BaselineFile ReadBaselines() => JsonSerializer.Deserialize<BaselineFile>(
        File.ReadAllText(Path.GetFullPath(BaselinePath)),
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

    private static void WriteBaselines(BaselineFile file) => File.WriteAllText(Path.GetFullPath(BaselinePath),
        JsonSerializer.Serialize(file, new JsonSerializerOptions { WriteIndented = true }));

    [Fact]
    public void ChaosMonkey_NoHardViolations()
    {
        var failures = new List<string>();
        for (var seed = 0; seed < SeedCount; seed++)
        {
            var r = LineQuality.Analyze(DiagramFuzzer.Yaml(seed));
            foreach (var d in r.Violations.Where(d => d.IsHard).Take(3))
            {
                failures.Add($"seed {seed}: [{d.Kind}] {d.Detail}");
            }

            if (failures.Count > 30)
            {
                break;
            }
        }
        Assert.True(failures.Count == 0,
            $"{failures.Count} routing violations over {SeedCount} fuzzed diagrams "
            + $"(reproduce with BECK_SEED=<seed> ... --filter DumpSeed):\n  " + string.Join("\n  ", failures));
    }

    /// <summary>
    /// The flowchart sibling of <see cref="ChaosMonkey_NoHardViolations"/>: same pipeline, same hard
    /// gate, over <see cref="DiagramFuzzer.FlowchartYaml"/> instead. Reproduce a failing seed with
    /// <c>BECK_SEED=&lt;seed&gt; BECK_FUZZ=flowchart dotnet test --filter FullyQualifiedName~DumpSeed</c>.
    /// </summary>
    [Fact]
    public void ChaosMonkey_Flowchart_NoHardViolations()
    {
        var failures = new List<string>();
        for (var seed = 0; seed < SeedCount; seed++)
        {
            var r = LineQuality.Analyze(DiagramFuzzer.FlowchartYaml(seed));
            foreach (var d in r.Violations.Where(d => d.IsHard).Take(3))
            {
                failures.Add($"seed {seed}: [{d.Kind}] {d.Detail}");
            }

            if (failures.Count > 30)
            {
                break;
            }
        }
        Assert.True(failures.Count == 0,
            $"{failures.Count} routing violations over {SeedCount} fuzzed flowcharts "
            + $"(reproduce with BECK_SEED=<seed> BECK_FUZZ=flowchart ... --filter DumpSeed):\n  " + string.Join("\n  ", failures));
    }

    [Fact]
    public void ChaosMonkey_AestheticsDoNotRegress()
    {
        var (got, edges, faces, worst) = Aggregate(DiagramFuzzer.Yaml);

        _out.WriteLine(Scorecard(got, edges, faces));
        _out.WriteLine("");
        _out.WriteLine("Worst offenders (seed → micro-jogs + merged runs per edge):");
        foreach (var w in worst.OrderByDescending(w => w.Score).Take(8))
        {
            _out.WriteLine(FormattableString.Invariant(
                $"  seed {w.Seed,4}  score {w.Score:0.00}  edges {w.R.Edges,2}  jogs {w.R.MicroJogs,2}  merged {w.R.MergedRuns,2}  straight {w.R.StraightRate:0%}"));
        }

        if (Environment.GetEnvironmentVariable("BECK_REGEN") == "1")
        {
            var current = File.Exists(Path.GetFullPath(BaselinePath)) ? ReadBaselines() : new BaselineFile(got, got, new MindmapBaseline(0, 0, 0));
            WriteBaselines(current with { Architecture = got });
            return;
        }

        var want = ReadBaselines().Architecture;
        var regressions = Compare(got, want);
        Assert.True(regressions.Count == 0,
            "Line quality regressed against Goldens/cleanlines.json (architecture):\n  "
            + string.Join("\n  ", regressions)
            + "\n\nIf the change is an intentional trade, re-record with BECK_REGEN=1.");
    }

    /// <summary>
    /// The flowchart sibling of <see cref="ChaosMonkey_AestheticsDoNotRegress"/>, ratcheted against
    /// its own <c>Flowchart</c> key in <c>Goldens/cleanlines.json</c> so the two fuzzers never share —
    /// or perturb — a baseline. Same <c>BECK_REGEN=1</c> regen path.
    /// </summary>
    [Fact]
    public void ChaosMonkey_Flowchart_AestheticsDoNotRegress()
    {
        var (got, edges, faces, worst) = Aggregate(DiagramFuzzer.FlowchartYaml);

        _out.WriteLine(Scorecard(got, edges, faces));
        _out.WriteLine("");
        _out.WriteLine("Worst offenders (seed → micro-jogs + merged runs per edge):");
        foreach (var w in worst.OrderByDescending(w => w.Score).Take(8))
        {
            _out.WriteLine(FormattableString.Invariant(
                $"  seed {w.Seed,4}  score {w.Score:0.00}  edges {w.R.Edges,2}  jogs {w.R.MicroJogs,2}  merged {w.R.MergedRuns,2}  straight {w.R.StraightRate:0%}"));
        }

        if (Environment.GetEnvironmentVariable("BECK_REGEN") == "1")
        {
            var current = File.Exists(Path.GetFullPath(BaselinePath)) ? ReadBaselines() : new BaselineFile(got, got, new MindmapBaseline(0, 0, 0));
            WriteBaselines(current with { Flowchart = got });
            return;
        }

        var want = ReadBaselines().Flowchart;
        var regressions = Compare(got, want);
        Assert.True(regressions.Count == 0,
            "Line quality regressed against Goldens/cleanlines.json (flowchart):\n  "
            + string.Join("\n  ", regressions)
            + "\n\nIf the change is an intentional trade, re-record with BECK_REGEN=1.");
    }

    /// <summary>
    /// The mindmap chaos monkey's hard gate: every fuzzed butterfly must route without an off-canvas
    /// coordinate, a node overlap, a broken tree shape, or an off-centre root. Mindmap violations are
    /// all hard by construction (there is no soft <c>Defect</c> among them), so none is filtered out.
    /// Reproduce a failing seed with
    /// <c>BECK_SEED=&lt;seed&gt; BECK_FUZZ=mindmap dotnet test --filter FullyQualifiedName~DumpSeed</c>.
    /// </summary>
    [Fact]
    public void ChaosMonkey_Mindmap_NoHardViolations()
    {
        var failures = new List<string>();
        for (var seed = 0; seed < SeedCount; seed++)
        {
            var r = MindmapQuality.Analyze(DiagramFuzzer.MindMapYaml(seed));
            foreach (var d in r.Violations.Take(3))
            {
                failures.Add($"seed {seed}: [{d.Kind}] {d.Detail}");
            }

            if (failures.Count > 30)
            {
                break;
            }
        }
        Assert.True(failures.Count == 0,
            $"{failures.Count} butterfly violations over {SeedCount} fuzzed mindmaps "
            + $"(reproduce with BECK_SEED=<seed> BECK_FUZZ=mindmap ... --filter DumpSeed):\n  " + string.Join("\n  ", failures));
    }

    /// <summary>
    /// The mindmap sibling of the aesthetics ratchets, against its own <c>Mindmap</c> key in
    /// <c>Goldens/cleanlines.json</c>. All three metrics are lower-is-better. Same <c>BECK_REGEN=1</c>
    /// regen path — it seeds the Mindmap key alone, leaving Architecture and Flowchart byte-identical.
    /// </summary>
    [Fact]
    public void ChaosMonkey_Mindmap_AestheticsDoNotRegress()
    {
        double area = 0, edge = 0, balance = 0;
        var worst = new List<(double Balance, int Seed, MindmapReport R)>();
        for (var seed = 0; seed < SeedCount; seed++)
        {
            var r = MindmapQuality.Analyze(DiagramFuzzer.MindMapYaml(seed));
            area += r.Metrics.AreaPerNode; edge += r.Metrics.MeanEdgeLength; balance += r.Metrics.HalfBalance;
            worst.Add((r.Metrics.HalfBalance, seed, r));
        }
        var got = new MindmapBaseline(area / SeedCount, edge / SeedCount, balance / SeedCount);

        _out.WriteLine(FormattableString.Invariant($"mindmap scorecard over {SeedCount} butterflies"));
        _out.WriteLine(FormattableString.Invariant($"  area / node        {got.AreaPerNode:0} px2"));
        _out.WriteLine(FormattableString.Invariant($"  mean edge length   {got.MeanEdgeLength:0.0} px"));
        _out.WriteLine(FormattableString.Invariant($"  half balance       {got.HalfBalance:0.000} (heavier / lighter half height)"));
        _out.WriteLine("");
        _out.WriteLine("Most lopsided (seed → half balance):");
        foreach (var w in worst.OrderByDescending(w => w.Balance).Take(8))
        {
            _out.WriteLine(FormattableString.Invariant(
                $"  seed {w.Seed,4}  balance {w.Balance:0.00}  nodes {w.R.Nodes,2}  edges {w.R.Edges,2}"));
        }

        if (Environment.GetEnvironmentVariable("BECK_REGEN") == "1")
        {
            var current = File.Exists(Path.GetFullPath(BaselinePath)) ? ReadBaselines() : new BaselineFile(default!, default!, got);
            WriteBaselines(current with { Mindmap = got });
            return;
        }

        var want = ReadBaselines().Mindmap;
        var regressions = new List<string>();
        void LowerIsBetter(string name, double g, double w, double tol)
        {
            if (g > w + tol)
            {
                regressions.Add(FormattableString.Invariant($"{name}: {g:0.000} > baseline {w:0.000}"));
            }
        }
        LowerIsBetter("area per node", got.AreaPerNode, want.AreaPerNode, want.AreaPerNode * 0.02);
        LowerIsBetter("mean edge length", got.MeanEdgeLength, want.MeanEdgeLength, 1.0);
        LowerIsBetter("half balance", got.HalfBalance, want.HalfBalance, 0.02);
        Assert.True(regressions.Count == 0,
            "Mindmap aesthetics regressed against Goldens/cleanlines.json (mindmap):\n  "
            + string.Join("\n  ", regressions)
            + "\n\nIf the change is an intentional trade, re-record with BECK_REGEN=1.");
    }

    /// <summary>
    /// Determinism: a fuzzed mindmap renders byte-identically twice. Same YAML + same options ⇒
    /// byte-identical SVG is a load-bearing engine invariant; this exercises it over the mindmap
    /// pipeline for 25 seeds spread across the fuzz range.
    /// </summary>
    [Fact]
    public void Mindmap_RenderIsDeterministic()
    {
        for (var seed = 0; seed < SeedCount; seed += SeedCount / 25)
        {
            var yaml = DiagramFuzzer.MindMapYaml(seed);
            Assert.Equal(BeckSvg.Render(yaml), BeckSvg.Render(yaml));
        }
    }

    private static (Baseline Got, int Edges, int Faces, List<(double Score, int Seed, QualityReport R)> Worst) Aggregate(
        Func<int, string> yamlFor)
    {
        int edges = 0, straight = 0, jogs = 0, merged = 0, faces = 0, skewed = 0, bends = 0, tight = 0;
        var worst = new List<(double Score, int Seed, QualityReport R)>();

        for (var seed = 0; seed < SeedCount; seed++)
        {
            var r = LineQuality.Analyze(yamlFor(seed));
            edges += r.Edges; straight += r.StraightEdges; jogs += r.MicroJogs;
            merged += r.MergedRuns; faces += r.Faces; skewed += r.SkewedFaces; bends += r.Bends;
            tight += r.TightEdges;
            if (r.Edges > 0)
            {
                worst.Add(((r.MicroJogs + r.MergedRuns) / (double)r.Edges, seed, r));
            }
        }

        var got = new Baseline(
            StraightRate: (double)straight / edges,
            MicroJogsPerEdge: (double)jogs / edges,
            MergedRunsPerEdge: (double)merged / edges,
            SkewedFaceRate: faces == 0 ? 0 : (double)skewed / faces,
            BendsPerEdge: (double)bends / edges,
            TightEdgeRate: (double)tight / edges);

        return (got, edges, faces, worst);
    }

    private static List<string> Compare(Baseline got, Baseline want)
    {
        var regressions = new List<string>();
        void HigherIsBetter(string name, double g, double w, double tol = 0.005)
        {
            if (g < w - tol)
            {
                regressions.Add(FormattableString.Invariant($"{name}: {g:0.0000} < baseline {w:0.0000}"));
            }
        }
        void LowerIsBetter(string name, double g, double w, double tol = 0.005)
        {
            if (g > w + tol)
            {
                regressions.Add(FormattableString.Invariant($"{name}: {g:0.0000} > baseline {w:0.0000}"));
            }
        }
        HigherIsBetter("straight-edge rate", got.StraightRate, want.StraightRate);
        LowerIsBetter("micro-jogs per edge", got.MicroJogsPerEdge, want.MicroJogsPerEdge);
        LowerIsBetter("merged runs per edge", got.MergedRunsPerEdge, want.MergedRunsPerEdge);
        LowerIsBetter("skewed-face rate", got.SkewedFaceRate, want.SkewedFaceRate);
        LowerIsBetter("bends per edge", got.BendsPerEdge, want.BendsPerEdge, 0.01);
        LowerIsBetter("tight-clearance edges", got.TightEdgeRate, want.TightEdgeRate);
        return regressions;
    }

    private static string Scorecard(Baseline b, int edges, int faces) => string.Join('\n', new[]
    {
        $"clean-line scorecard over {SeedCount} diagrams / {edges} edges / {faces} shared faces",
        FormattableString.Invariant($"  straight edges     {b.StraightRate:0.0%}"),
        FormattableString.Invariant($"  bends per edge     {b.BendsPerEdge:0.000}"),
        FormattableString.Invariant($"  micro-jogs / edge  {b.MicroJogsPerEdge:0.000}"),
        FormattableString.Invariant($"  merged run pairs   {b.MergedRunsPerEdge:0.000} per edge"),
        FormattableString.Invariant($"  skewed fans        {b.SkewedFaceRate:0.0%} of shared faces"),
        FormattableString.Invariant($"  tight clearance    {b.TightEdgeRate:0.0%} of edges within {LineQuality.TightPx}px of a node"),
    });

    /// <summary>
    /// Debugging affordance, not a gate: dumps one fuzzed seed's YAML, node boxes and routed
    /// polylines so a monkey failure can be read by eye. Defaults to the architecture fuzzer; set
    /// <c>BECK_FUZZ=flowchart</c> or <c>BECK_FUZZ=mindmap</c> to dump from
    /// <see cref="DiagramFuzzer.FlowchartYaml"/> / <see cref="DiagramFuzzer.MindMapYaml"/> instead.
    /// <c>BECK_SEED=468 dotnet test --filter FullyQualifiedName~DumpSeed -l "console;verbosity=detailed"</c>
    /// </summary>
    [Fact]
    public void DumpSeed()
    {
        if (Environment.GetEnvironmentVariable("BECK_SEED") is not { } s || !int.TryParse(s, out var seed))
        {
            return;
        }

        var fuzz = Environment.GetEnvironmentVariable("BECK_FUZZ");
        if (fuzz == "mindmap")
        {
            var mmYaml = DiagramFuzzer.MindMapYaml(seed);
            _out.WriteLine(mmYaml);
            var routed = MindmapQuality.Route(mmYaml);
            var layout = routed.Layout;
            var mm = MindmapQuality.Analyze(routed);
            _out.WriteLine(FormattableString.Invariant(
                $"seed {seed}: {mm.Nodes} nodes, {mm.Edges} edges, canvas {layout.Width}×{layout.Height}"));
            foreach (var (id, rect) in layout.Nodes.OrderBy(k => k.Key))
            {
                _out.WriteLine(FormattableString.Invariant(
                    $"  node {id,-14} x {rect.X:0.#}..{rect.X + rect.W:0.#}  y {rect.Y:0.#}..{rect.Y + rect.H:0.#}"));
            }

            foreach (var d in mm.Violations)
            {
                _out.WriteLine($"  !! [{d.Kind}] {d.Detail}");
            }
            return;
        }

        var yaml = fuzz == "flowchart" ? DiagramFuzzer.FlowchartYaml(seed) : DiagramFuzzer.Yaml(seed);
        _out.WriteLine(yaml);
        var r = LineQuality.Analyze(yaml);
        _out.WriteLine(Describe($"seed {seed}", yaml, r));
        foreach (var d in r.Violations)
        {
            _out.WriteLine($"  !! [{d.Kind}] {d.Detail}");
        }
    }

    /// <summary>Debugging affordance: score and dump an arbitrary YAML file. BECK_YAML=&lt;path&gt;.</summary>
    [Fact]
    public void DumpYaml()
    {
        if (Environment.GetEnvironmentVariable("BECK_YAML") is not { } path || !File.Exists(path))
        {
            return;
        }

        var yaml = File.ReadAllText(path);
        var r = LineQuality.Analyze(yaml);
        _out.WriteLine(Describe(Path.GetFileName(path), yaml, r));
        var (_, layout, _) = LineQuality.Route(yaml);
        foreach (var (id, rect) in layout.Groups.OrderBy(k => k.Key))
        {
            _out.WriteLine(FormattableString.Invariant(
                $"  group {id,-8} x {rect.X:0.#}..{rect.X + rect.W:0.#}  y {rect.Y:0.#}..{rect.Y + rect.H:0.#}"));
        }

        _out.WriteLine($"  canvas {layout.Width} × {layout.Height}");
        foreach (var d in r.Violations)
        {
            _out.WriteLine($"  !! [{d.Kind}] {d.Detail}");
        }
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
        var yaml = which == nameof(ServeAndBuild) ? ServeAndBuild : ThreeIntoOne;
        var r = LineQuality.Analyze(yaml);
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
        {
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"  {e.Edge.Id,-24} {string.Join(" → ", e.Points.Select(p => $"({p.X:0.#},{p.Y:0.#})"))}");
        }

        foreach (var (id, rect) in layout.Nodes.OrderBy(k => k.Key))
        {
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"  node {id,-10} x {rect.X:0.#}..{rect.X + rect.W:0.#}  y {rect.Y:0.#}..{rect.Y + rect.H:0.#}  c ({rect.X + rect.W / 2:0.#},{rect.Y + rect.H / 2:0.#})");
        }

        return sb.ToString();
    }
}