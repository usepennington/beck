using System.Globalization;

namespace Beck.Rendering.Animate;

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
        var tokens = d.Replace(",", " ").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        double length = 0;
        double cx = 0, cy = 0;
        int i = 0;
        double Num() => double.Parse(tokens[i++], NumberStyles.Float, CultureInfo.InvariantCulture);
        while (i < tokens.Length)
        {
            string cmd = tokens[i++];
            switch (cmd)
            {
                case "M": cx = Num(); cy = Num(); break;
                case "L":
                {
                    double x = Num(), y = Num();
                    length += Dist(cx, cy, x, y); cx = x; cy = y;
                    break;
                }
                case "Q":
                {
                    double qx = Num(), qy = Num(), x = Num(), y = Num();
                    length += Quad(cx, cy, qx, qy, x, y); cx = x; cy = y;
                    break;
                }
                case "C":
                {
                    double a = Num(), b = Num(), c = Num(), e = Num(), x = Num(), y = Num();
                    length += Cubic(cx, cy, a, b, c, e, x, y); cx = x; cy = y;
                    break;
                }
                default:
                    // A bare number after a command repeats it implicitly; StepRound never emits that.
                    break;
            }
        }
        return length;
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
