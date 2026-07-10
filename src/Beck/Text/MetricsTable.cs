using Beck.Rendering.Text;

namespace Beck.Text;

/// <summary>
/// A data-only snapshot of one embedded font family's advance table — the arrays a generated
/// <c>*MetricsData.g.cs</c> class exposes, wrapped behind a common shape so
/// <see cref="EmbeddedMetricsMeasurer"/> can measure against any family. Sans advances are
/// per-glyph, per weight; mono advances are one fixed pitch per weight (IBM Plex Mono, shared by
/// every table). The non-ASCII <c>SansExtra</c> rows are pre-indexed into a dictionary for O(1)
/// lookup on the measure hot path.
/// </summary>
internal sealed class MetricsTable
{
    public required int[] SansWeights { get; init; }
    public required double[] SansAscent { get; init; }
    public required double[] SansDescent { get; init; }
    public required double[] SansFallback { get; init; }
    public required double[][] SansAscii { get; init; }
    public required IReadOnlyDictionary<int, double[]> SansExtra { get; init; }
    public required int[] MonoWeights { get; init; }
    public required double[] MonoAscent { get; init; }
    public required double[] MonoDescent { get; init; }
    public required double[] MonoAdvance { get; init; }

    /// <summary>Build a table from a generated data class's raw arrays (the <c>SansExtra</c> pairs are
    /// folded into the lookup dictionary).</summary>
    public static MetricsTable Build(
        int[] sansWeights, double[] sansAscent, double[] sansDescent, double[] sansFallback,
        double[][] sansAscii, (int Cp, double[] Adv)[] sansExtra,
        int[] monoWeights, double[] monoAscent, double[] monoDescent, double[] monoAdvance)
    {
        var extra = new Dictionary<int, double[]>(sansExtra.Length);
        foreach (var (cp, adv) in sansExtra) extra[cp] = adv;
        return new MetricsTable
        {
            SansWeights = sansWeights,
            SansAscent = sansAscent,
            SansDescent = sansDescent,
            SansFallback = sansFallback,
            SansAscii = sansAscii,
            SansExtra = extra,
            MonoWeights = monoWeights,
            MonoAscent = monoAscent,
            MonoDescent = monoDescent,
            MonoAdvance = monoAdvance,
        };
    }
}

/// <summary>Maps a <see cref="MetricsFont"/> to its lazily-built embedded <see cref="MetricsTable"/>.</summary>
internal static class MetricsTables
{
    /// <summary>The embedded table for <paramref name="font"/>; <see cref="MetricsFont.Inter"/> is the
    /// classic default.</summary>
    public static MetricsTable For(MetricsFont font) => font switch
    {
        MetricsFont.SourceSerif => SourceSerif,
        MetricsFont.Archivo => Archivo,
        MetricsFont.ShantellSans => ShantellSans,
        _ => Inter,
    };

    public static readonly MetricsTable Inter = MetricsTable.Build(
        InterMetricsData.SansWeights, InterMetricsData.SansAscent, InterMetricsData.SansDescent,
        InterMetricsData.SansFallback, InterMetricsData.SansAscii, InterMetricsData.SansExtra,
        InterMetricsData.MonoWeights, InterMetricsData.MonoAscent, InterMetricsData.MonoDescent,
        InterMetricsData.MonoAdvance);

    public static readonly MetricsTable SourceSerif = MetricsTable.Build(
        SourceSerifMetricsData.SansWeights, SourceSerifMetricsData.SansAscent, SourceSerifMetricsData.SansDescent,
        SourceSerifMetricsData.SansFallback, SourceSerifMetricsData.SansAscii, SourceSerifMetricsData.SansExtra,
        SourceSerifMetricsData.MonoWeights, SourceSerifMetricsData.MonoAscent, SourceSerifMetricsData.MonoDescent,
        SourceSerifMetricsData.MonoAdvance);

    public static readonly MetricsTable Archivo = MetricsTable.Build(
        ArchivoMetricsData.SansWeights, ArchivoMetricsData.SansAscent, ArchivoMetricsData.SansDescent,
        ArchivoMetricsData.SansFallback, ArchivoMetricsData.SansAscii, ArchivoMetricsData.SansExtra,
        ArchivoMetricsData.MonoWeights, ArchivoMetricsData.MonoAscent, ArchivoMetricsData.MonoDescent,
        ArchivoMetricsData.MonoAdvance);

    public static readonly MetricsTable ShantellSans = MetricsTable.Build(
        ShantellSansMetricsData.SansWeights, ShantellSansMetricsData.SansAscent, ShantellSansMetricsData.SansDescent,
        ShantellSansMetricsData.SansFallback, ShantellSansMetricsData.SansAscii, ShantellSansMetricsData.SansExtra,
        ShantellSansMetricsData.MonoWeights, ShantellSansMetricsData.MonoAscent, ShantellSansMetricsData.MonoDescent,
        ShantellSansMetricsData.MonoAdvance);
}
