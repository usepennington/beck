using Beck.Rendering;
using Beck.Rendering.Text;
using HarfBuzzSharp;
using SkiaSharp;

namespace Beck.Skia;

/// <summary>
/// Exact text measurement: HarfBuzz shapes each run (so kerning + ligatures land
/// exactly as a browser does), SkiaSharp supplies vertical metrics. Constructed
/// from a <see cref="BeckFontSpec"/>; pass the same spec to
/// <see cref="SvgRenderOptions.Font"/> so the SVG asks for the measured font.
/// </summary>
public sealed class SkiaTextMeasurer : ITextMeasurer, IDisposable
{
    private readonly BeckFontSpec _spec;
    private readonly Dictionary<(bool Mono, int Weight), Loaded> _fonts = new();

    // One measurer is shared across concurrent renders (a singleton preprocessor at build
    // time, concurrent requests when serving). Both the font cache and HarfBuzz shaping —
    // hb_shape mutates the hb_font's internal buffers and is not reentrant on a shared font —
    // need exclusive access. Measurement is microseconds, so serializing costs nothing here.
    private readonly object _gate = new();

    private sealed record Loaded(SKTypeface Typeface, Font HbFont, int UnitsPerEm, Face Face, Blob Blob);

    public SkiaTextMeasurer(BeckFontSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        _spec = spec;
    }

    /// <summary>Exact, font-file-backed measurement — never an approximation, so
    /// <see cref="TextLengthGuard.FallbackOnly"/> suppresses the <c>textLength</c> guard under it.</summary>
    public bool IsApproximate => false;

    /// <inheritdoc />
    public TextMetrics Measure(string text, FontRole role) => Measure(text, FontRoles.Of(role));

    /// <inheritdoc />
    public TextMetrics Measure(string text, FontRole role, FontRoleSpec spec) => Measure(text, spec);

    /// <summary>The exact-measurement primitive: shape <paramref name="text"/> against
    /// <paramref name="spec"/> (the caller resolves role → spec, classic or style-remapped). Uppercase
    /// specs shape the transformed string, so a style that uppercases a role widens its measured box.</summary>
    public TextMetrics Measure(string text, FontRoleSpec spec)
    {
        FontRoleSpec s = spec;
        string t = s.Uppercase ? text.ToUpperInvariant() : text;

        lock (_gate)
        {
            Loaded f = Load(s.Mono, s.Weight);

            double width = 0;
            if (t.Length > 0)
            {
                using var buffer = new HarfBuzzSharp.Buffer();
                buffer.AddUtf16(t);
                buffer.GuessSegmentProperties();
                f.HbFont.Shape(buffer);
                long advance = 0;
                foreach (GlyphPosition p in buffer.GlyphPositions) advance += p.XAdvance;
                width = advance * s.SizePx / f.UnitsPerEm;
                // CSS letter-spacing adds a gap after every character (Chrome keeps the trailing gap).
                if (s.LetterSpacingEm != 0) width += t.Length * s.LetterSpacingEm * s.SizePx;
            }

            using var font = new SKFont(f.Typeface, (float)s.SizePx);
            font.GetFontMetrics(out SKFontMetrics m);
            return new TextMetrics(width, -m.Ascent, m.Descent);
        }
    }

    private Loaded Load(bool mono, int weight)
    {
        var key = (mono, weight);
        if (_fonts.TryGetValue(key, out Loaded? cached)) return cached;

        IReadOnlyDictionary<int, string>? files = mono ? _spec.MonoFiles : _spec.Files;
        if (files is null || files.Count == 0)
            throw new InvalidOperationException(
                $"BeckFontSpec provides no {(mono ? "monospace " : "")}font files for weight {weight}.");

        string path = files[NearestWeight(files.Keys, weight)];
        SKTypeface typeface = SKTypeface.FromFile(path)
            ?? throw new InvalidOperationException($"Could not load font file '{path}'.");

        var blob = Blob.FromFile(path);
        var face = new Face(blob, 0);
        int upem = face.UnitsPerEm;
        var hbFont = new Font(face);
        hbFont.SetFunctionsOpenType();
        hbFont.SetScale(upem, upem);

        var loaded = new Loaded(typeface, hbFont, upem, face, blob);
        _fonts[key] = loaded;
        return loaded;
    }

    private static int NearestWeight(IEnumerable<int> available, int want)
    {
        int best = -1, bestDistance = int.MaxValue;
        foreach (int w in available)
        {
            int d = Math.Abs(w - want);
            if (d < bestDistance) { bestDistance = d; best = w; }
        }
        return best;
    }

    /// <summary>Releases the cached typefaces and HarfBuzz resources.</summary>
    public void Dispose()
    {
        foreach (Loaded f in _fonts.Values)
        {
            f.HbFont.Dispose();
            f.Face.Dispose();
            f.Blob.Dispose();
            f.Typeface.Dispose();
        }
        _fonts.Clear();
    }
}
