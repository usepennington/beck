using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using HarfBuzzSharp;
using SkiaSharp;

// Beck.MetricsGen — offline codegen for the embedded font-metrics tables the managed
// EmbeddedMetricsMeasurer reads (Beck/Text/*MetricsData.g.cs). Each table mirrors
// InterMetricsData exactly in shape (SansWeights/SansAscii/SansExtra + Mono*), differing only
// in data and class name. Measures the committed OFL fonts with HarfBuzz/Skia — the same stack
// Beck.Skia's SkiaTextMeasurer uses — so a table matches what a host supplying these fonts would
// measure. Mono coverage reuses IBM Plex Mono verbatim (identical to the Inter table).
//
//   dotnet run --project tools/Beck.MetricsGen -c Release [<outputDir>]
//
// <outputDir> defaults to Beck/Text (resolved from this file's path). Do not add to CI.

string toolDir = ToolDir();
string fontsDir = Path.Combine(toolDir, "fonts");
string outDir = args.Length > 0
    ? Path.GetFullPath(args[0])
    : Path.GetFullPath(Path.Combine(toolDir, "..", "..", "Beck", "Text"));

Directory.CreateDirectory(outDir);

// The charset InterMetricsData covers: ASCII 32..126, Latin-1 supplement, and the handful of
// Unicode punctuation/arrows that show up in diagram text. Kept byte-for-byte identical so every
// table covers exactly the same glyphs the Inter table does.
int[] extras =
[
    .. Enumerable.Range(0xA0, 0x60),                 // Latin-1 supplement A0..FF
    0x2013, 0x2014,                                  // en / em dash
    0x2018, 0x2019, 0x201C, 0x201D,                  // curly quotes
    0x2022, 0x2026,                                  // bullet, ellipsis
    0x2190, 0x2191, 0x2192, 0x2193,                  // arrows
];

// Mono is always IBM Plex Mono, shared across every table (reused, never regenerated per family).
var monoDir = Path.Combine(fontsDir, "ibm-plex-mono");
var mono = new (int W, string File)[]
{
    (400, "IBMPlexMono-Regular.ttf"),
    (500, "IBMPlexMono-Medium.ttf"),
    (700, "IBMPlexMono-Bold.ttf"),
};

var families = new Family[]
{
    // Source Serif 4 — static OTF release instances (editorial). No Medium(500) static ships,
    // so measurement resolves 500 to the nearest available weight (400).
    new("SourceSerifMetricsData", "Source Serif 4",
        Path.Combine(fontsDir, "source-serif-4"),
        new[]
        {
            (400, "SourceSerif4-Regular.otf"),
            (600, "SourceSerif4-Semibold.otf"),
            (700, "SourceSerif4-Bold.otf"),
        }),

    // Archivo — static TTF instances (brutalist / metro headers). Includes 800 (ExtraBold) so the
    // brutalist role table's uppercase-800 resolves exactly instead of snapping down to 700.
    new("ArchivoMetricsData", "Archivo",
        Path.Combine(fontsDir, "archivo"),
        new[]
        {
            (400, "Archivo-Regular.ttf"),
            (500, "Archivo-Medium.ttf"),
            (600, "Archivo-SemiBold.ttf"),
            (700, "Archivo-Bold.ttf"),
            (800, "Archivo-ExtraBold.ttf"),
        }),

    // Shantell Sans — VARIABLE-ONLY family (no static release assets; the shipping HarfBuzz 7.3.0.3
    // and SkiaSharp 2.88.9 expose no axis-instancing API, and using a newer shaper would diverge from
    // Beck.Skia's runtime measurement). Measured at the font's default variable instance and labelled
    // 400: a weight-invariant approximation the textLength guard absorbs, exactly the embedded-table
    // contract. Sketch's role table is predominantly one weight, so the practical error is tiny.
    new("ShantellSansMetricsData", "Shantell Sans",
        Path.Combine(fontsDir, "shantell-sans"),
        new[] { (400, "ShantellSans-VariableFont.ttf") }),
};

foreach (Family fam in families)
{
    string code = Emit(fam, extras, mono, monoDir);
    string path = Path.Combine(outDir, fam.ClassName + ".g.cs");
    File.WriteAllText(path, code);
    Console.WriteLine($"wrote {path}  ({fam.Weights.Length} sans weight(s))");
}

Console.WriteLine("done.");
return 0;

static string Emit(Family fam, int[] extras, (int W, string File)[] mono, string monoDir)
{
    int[] weights = fam.Weights.Select(w => w.Item1).ToArray();
    var sansAscent = new double[weights.Length];
    var sansDescent = new double[weights.Length];
    var sansFallback = new double[weights.Length];
    var ascii = new double[weights.Length][];
    var extra = new Dictionary<int, double[]>();

    for (int wi = 0; wi < weights.Length; wi++)
    {
        using var f = Loaded.Open(Path.Combine(fam.Dir, fam.Weights[wi].Item2));
        (sansAscent[wi], sansDescent[wi]) = f.VMetrics();

        ascii[wi] = new double[95];
        for (int cp = 32; cp <= 126; cp++) ascii[wi][cp - 32] = f.Advance(cp) ?? 0;

        // fallback = mean lowercase advance (a typical glyph width)
        sansFallback[wi] = Enumerable.Range('a', 26).Select(c => f.Advance(c) ?? 0).Average();

        foreach (int cp in extras)
        {
            double? adv = f.Advance(cp);
            if (adv is null) continue; // glyph absent → measurer falls back
            if (!extra.TryGetValue(cp, out var row)) extra[cp] = row = new double[weights.Length];
            row[wi] = adv.Value;
        }
    }

    // Mono (IBM Plex Mono) — reused verbatim so every table's mono coverage is identical.
    var monoAscent = new double[mono.Length];
    var monoDescent = new double[mono.Length];
    var monoAdvance = new double[mono.Length];
    for (int wi = 0; wi < mono.Length; wi++)
    {
        using var f = Loaded.Open(Path.Combine(monoDir, mono[wi].File));
        (monoAscent[wi], monoDescent[wi]) = f.VMetrics();
        monoAdvance[wi] = f.Advance('x') ?? 0.6;
    }

    var sb = new StringBuilder();
    sb.AppendLine("// <auto-generated>");
    sb.AppendLine($"//   Embedded font metrics for the managed EmbeddedMetricsMeasurer: advance-per-em for a Latin");
    sb.AppendLine($"//   charset from {fam.Display} (sans) + IBM Plex Mono, plus per-em ascent/descent. Regenerate with");
    sb.AppendLine("//   the Beck.MetricsGen console (tools/Beck.MetricsGen). Do not edit by hand.");
    sb.AppendLine("// </auto-generated>");
    sb.AppendLine("namespace Beck.Rendering.Text;");
    sb.AppendLine();
    sb.AppendLine($"internal static class {fam.ClassName}");
    sb.AppendLine("{");

    Arr(sb, "SansWeights", weights);
    DblArr(sb, "SansAscent", sansAscent);
    DblArr(sb, "SansDescent", sansDescent);
    DblArr(sb, "SansFallback", sansFallback);

    sb.AppendLine("    public static readonly double[][] SansAscii =");
    sb.AppendLine("    {");
    for (int wi = 0; wi < weights.Length; wi++)
        sb.Append("        new double[] { ").Append(string.Join(", ", ascii[wi].Select(D))).AppendLine(" },");
    sb.AppendLine("    };");
    sb.AppendLine();

    sb.AppendLine("    // (codepoint, advance-per-em per sans weight)");
    sb.AppendLine("    public static readonly (int Cp, double[] Adv)[] SansExtra =");
    sb.AppendLine("    {");
    foreach (var (cp, row) in extra.OrderBy(kv => kv.Key))
        sb.Append("        (0x").Append(cp.ToString("X")).Append(", new double[] { ").Append(string.Join(", ", row.Select(D))).AppendLine(" }),");
    sb.AppendLine("    };");
    sb.AppendLine();

    Arr(sb, "MonoWeights", mono.Select(m => m.W).ToArray());
    DblArr(sb, "MonoAscent", monoAscent);
    DblArr(sb, "MonoDescent", monoDescent);
    DblArr(sb, "MonoAdvance", monoAdvance);

    sb.AppendLine("}");
    return sb.ToString();
}

static string D(double v) => v.ToString("0.####", CultureInfo.InvariantCulture);

static void Arr(StringBuilder sb, string name, int[] vals) =>
    sb.Append("    public static readonly int[] ").Append(name).Append(" = { ")
      .Append(string.Join(", ", vals)).AppendLine(" };");

static void DblArr(StringBuilder sb, string name, double[] vals) =>
    sb.Append("    public static readonly double[] ").Append(name).Append(" = { ")
      .Append(string.Join(", ", vals.Select(D))).AppendLine(" };");

static string ToolDir([CallerFilePath] string path = "") => Path.GetDirectoryName(path)!;

internal sealed record Family(string ClassName, string Display, string Dir, (int, string)[] Weights);

/// <summary>A loaded face — HarfBuzz for shaping advances, Skia for vertical metrics.</summary>
internal sealed class Loaded : IDisposable
{
    private readonly SKTypeface _typeface;
    private readonly HarfBuzzSharp.Font _hb;
    private readonly Face _face;
    private readonly Blob _blob;
    private readonly int _upem;

    private Loaded(SKTypeface t, HarfBuzzSharp.Font h, Face fa, Blob b, int upem)
        => (_typeface, _hb, _face, _blob, _upem) = (t, h, fa, b, upem);

    public static Loaded Open(string path)
    {
        var typeface = SKTypeface.FromFile(path) ?? throw new FileNotFoundException(path);
        var blob = Blob.FromFile(path);
        var face = new Face(blob, 0);
        int upem = face.UnitsPerEm;
        var hb = new HarfBuzzSharp.Font(face);
        hb.SetFunctionsOpenType();
        hb.SetScale(upem, upem);
        return new Loaded(typeface, hb, face, blob, upem);
    }

    /// <summary>Advance-per-em for one codepoint, or null if the font has no glyph for it.</summary>
    public double? Advance(int codepoint)
    {
        using var buffer = new HarfBuzzSharp.Buffer();
        buffer.AddUtf16(char.ConvertFromUtf32(codepoint));
        buffer.GuessSegmentProperties();
        _hb.Shape(buffer);
        var infos = buffer.GlyphInfos;
        if (infos.Length == 0 || infos[0].Codepoint == 0) return null; // .notdef → absent
        long adv = 0;
        foreach (var p in buffer.GlyphPositions) adv += p.XAdvance;
        return adv / (double)_upem;
    }

    public (double Ascent, double Descent) VMetrics()
    {
        using var font = new SKFont(_typeface, 1f);
        font.GetFontMetrics(out var m);
        return (-m.Ascent, m.Descent); // at size 1, metrics are per-em
    }

    public void Dispose()
    {
        _hb.Dispose(); _face.Dispose(); _blob.Dispose(); _typeface.Dispose();
    }
}
