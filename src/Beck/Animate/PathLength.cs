using System.Globalization;

namespace Beck.Animate;

/// <summary>
/// Length of an SVG path <c>d</c> (the M/L/Q paths StepRound emits, plus C for
/// s-curves) — the C# stand-in for <c>getTotalLength()</c>. Lines are exact;
/// quadratic/cubic segments are chord-flattened (deterministic, sub-0.05px). Used
/// for hop durations (<c>len/speed</c>) and trail dash lengths.
/// </summary>
internal static class PathLength
{
    private const int Steps = 32;

    public static double Of(string d)
    {
        // SVG path data glues commands to their first coordinate ("M106 80Q106.5 128 …") and separates
        // numbers with spaces and/or commas. Lex into a flat token list where each command letter is its
        // own token and each number is one token, so a bowed path (Shaping.Bow) measures correctly.
        var tokens = Lex(d);
        double length = 0;
        double cx = 0, cy = 0;      // current point
        double sx = 0, sy = 0;      // current subpath start (for Z)
        char cmd = '\0';
        int i = 0;
        double Num() => double.Parse(tokens[i++], NumberStyles.Float, CultureInfo.InvariantCulture);
        static bool IsCommand(string t) => t.Length == 1 && char.IsLetter(t[0]);
        while (i < tokens.Count)
        {
            if (IsCommand(tokens[i])) cmd = tokens[i++][0];
            else if (cmd == 'M') cmd = 'L';   // extra coords after M are implicit L (SVG spec)
            else if (cmd == '\0') { i++; continue; }
            switch (cmd)
            {
                case 'M': sx = cx = Num(); sy = cy = Num(); break;
                case 'L':
                {
                    double x = Num(), y = Num();
                    length += Dist(cx, cy, x, y); cx = x; cy = y;
                    break;
                }
                case 'Q':
                {
                    double qx = Num(), qy = Num(), x = Num(), y = Num();
                    length += Quad(cx, cy, qx, qy, x, y); cx = x; cy = y;
                    break;
                }
                case 'C':
                {
                    double a = Num(), b = Num(), c = Num(), e = Num(), x = Num(), y = Num();
                    length += Cubic(cx, cy, a, b, c, e, x, y); cx = x; cy = y;
                    break;
                }
                case 'Z': case 'z':
                    length += Dist(cx, cy, sx, sy); cx = sx; cy = sy;
                    cmd = '\0';   // Z takes no params; a following number can't implicitly repeat it
                    break;
                default:
                    // A command the emitters never produce (H/V/A/…); consume one token to stay live.
                    i++;
                    break;
            }
        }
        return length;
    }

    /// <summary>
    /// Split SVG path <c>d</c> into single-letter command tokens and numeric tokens, tolerating
    /// commands glued to their first coordinate and comma/space number separators (both valid SVG that
    /// the shaping layer emits). Numbers accept a leading sign, one decimal point, and an exponent.
    /// </summary>
    private static List<string> Lex(string d)
    {
        var tokens = new List<string>();
        int n = d.Length;
        for (int i = 0; i < n;)
        {
            char c = d[i];
            if (char.IsWhiteSpace(c) || c == ',') { i++; continue; }
            if (char.IsLetter(c)) { tokens.Add(c.ToString()); i++; continue; }
            int start = i;
            if (c == '+' || c == '-') i++;
            bool dot = false;
            while (i < n && (char.IsDigit(d[i]) || (d[i] == '.' && !dot)))
            {
                if (d[i] == '.') dot = true;
                i++;
            }
            if (i < n && (d[i] == 'e' || d[i] == 'E'))
            {
                i++;
                if (i < n && (d[i] == '+' || d[i] == '-')) i++;
                while (i < n && char.IsDigit(d[i])) i++;
            }
            if (i == start) i++;   // guard: an unexpected char never stalls the scan
            else tokens.Add(d.Substring(start, i - start));
        }
        return tokens;
    }

    private static double Dist(double x1, double y1, double x2, double y2)
    {
        double dx = x2 - x1, dy = y2 - y1;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static double Quad(double x0, double y0, double x1, double y1, double x2, double y2)
    {
        double len = 0, px = x0, py = y0;
        for (int k = 1; k <= Steps; k++)
        {
            double t = (double)k / Steps, u = 1 - t;
            double x = u * u * x0 + 2 * u * t * x1 + t * t * x2;
            double y = u * u * y0 + 2 * u * t * y1 + t * t * y2;
            len += Dist(px, py, x, y); px = x; py = y;
        }
        return len;
    }

    private static double Cubic(double x0, double y0, double x1, double y1, double x2, double y2, double x3, double y3)
    {
        double len = 0, px = x0, py = y0;
        for (int k = 1; k <= Steps; k++)
        {
            double t = (double)k / Steps, u = 1 - t;
            double x = u * u * u * x0 + 3 * u * u * t * x1 + 3 * u * t * t * x2 + t * t * t * x3;
            double y = u * u * u * y0 + 3 * u * u * t * y1 + 3 * u * t * t * y2 + t * t * t * y3;
            len += Dist(px, py, x, y); px = x; py = y;
        }
        return len;
    }
}
