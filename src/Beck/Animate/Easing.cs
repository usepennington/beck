using System.Globalization;
using System.Text;
using Beck.Model;

namespace Beck.Animate;

/// <summary>
/// Analytic easing functions (Penner-equivalent, matching GSAP) and their CSS
/// compilation. <c>linear</c> → <c>linear</c>, <c>steps</c> → <c>steps(12)</c>;
/// everything else is sampled into a CSS <c>linear()</c> timing function at
/// adaptive points, visually indistinguishable from GSAP (§9.3/§10).
/// </summary>
internal sealed record Ease(string Token, Func<double, double> Fn, int Steps = 12)
{
    public bool IsLinear => Token == "none";
    public bool IsSteps => Token.StartsWith("steps", StringComparison.Ordinal);
}

internal static class Easing
{
    public static readonly Ease Linear = new("none", t => t);
    public static readonly Ease Power1In = new("power1.in", t => t * t);
    public static readonly Ease Power2In = new("power2.in", t => t * t * t);
    public static readonly Ease Power2Out = new("power2.out", t => 1 - Math.Pow(1 - t, 3));
    public static readonly Ease Power2InOut = new("power2.inOut", t => t < 0.5 ? 4 * t * t * t : 1 - Math.Pow(-2 * t + 2, 3) / 2);
    public static readonly Ease ExpoInOut = new("expo.inOut", t =>
        t == 0 ? 0 : t == 1 ? 1 : t < 0.5 ? Math.Pow(2, 20 * t - 10) / 2 : (2 - Math.Pow(2, -20 * t + 10)) / 2);
    public static readonly Ease SineInOut = new("sine.inOut", t => -(Math.Cos(Math.PI * t) - 1) / 2);
    public static readonly Ease Steps12 = new("steps(12)", t => Math.Floor(t * 12) / 12);
    public static readonly Ease BounceOut = new("bounce.out", BounceOutFn);

    /// <summary>Overshoot ease (<c>back.out(overshoot)</c>): s = 1.70158·overshoot.</summary>
    public static Ease BackOut(double overshoot)
    {
        double s = 1.70158 * overshoot;
        return new($"back.out({overshoot})", t => { double u = t - 1; return u * u * ((s + 1) * u + s) + 1; });
    }

    /// <summary>Oscillating ease (<c>elastic.out(amplitude, period)</c>).</summary>
    public static Ease ElasticOut(double amplitude, double period)
    {
        double a = Math.Max(amplitude, 1);
        double p = period;
        double s = p / (2 * Math.PI) * Math.Asin(1 / a);
        return new($"elastic.out({amplitude},{period})", t =>
            t == 0 ? 0 : t == 1 ? 1 : a * Math.Pow(2, -10 * t) * Math.Sin((t - s) * (2 * Math.PI) / p) + 1);
    }

    /// <summary>A <c>steps(n)</c> ease with an arbitrary step count (mirrors <see cref="Steps12"/>,
    /// which is just <c>StepsN(12)</c>) — used by <see cref="StyleMotion.TrailSteps"/> for a
    /// style-chosen hard-cut trail reveal.</summary>
    public static Ease StepsN(int n) => new($"steps({n})", t => Math.Floor(t * n) / Math.Max(1, n), Math.Max(1, n));

    private static double BounceOutFn(double t)
    {
        const double n1 = 7.5625, d1 = 2.75;
        if (t < 1 / d1) return n1 * t * t;
        if (t < 2 / d1) { t -= 1.5 / d1; return n1 * t * t + 0.75; }
        if (t < 2.5 / d1) { t -= 2.25 / d1; return n1 * t * t + 0.9375; }
        t -= 2.625 / d1; return n1 * t * t + 0.984375;
    }

    /// <summary>The packet ease token → concrete ease (timeline.ts PACKET_EASE map).</summary>
    public static Ease ForPacket(PacketEase ease) => ease switch
    {
        PacketEase.Linear => Linear,
        PacketEase.Smooth => Power2InOut,
        PacketEase.Accelerate => Power2In,
        PacketEase.Decelerate => Power2Out,
        PacketEase.Expo => ExpoInOut,
        PacketEase.Sine => SineInOut,
        PacketEase.Steps => Steps12,
        PacketEase.Bounce => BounceOut,
        _ => Linear,
    };

    /// <summary>The CSS timing function for an ease.</summary>
    public static string ToCss(Ease e)
    {
        if (e.IsLinear) return "linear";
        if (e.IsSteps) return $"steps({e.Steps})";
        return Sample(e.Fn);
    }

    private static string Sample(Func<double, double> fn)
    {
        var pts = new List<(double T, double V)>();
        for (int k = 0; k < 16; k++) { double t = k / 15.0; pts.Add((t, fn(t))); }

        bool inserted = true;
        while (inserted && pts.Count < 48)
        {
            inserted = false;
            for (int i = 0; i < pts.Count - 1 && pts.Count < 48; i++)
            {
                double tm = (pts[i].T + pts[i + 1].T) / 2;
                double vlin = (pts[i].V + pts[i + 1].V) / 2;
                double vm = fn(tm);
                if (Math.Abs(vm - vlin) > 0.005) { pts.Insert(i + 1, (tm, vm)); i++; inserted = true; }
            }
        }

        var sb = new StringBuilder("linear(");
        for (int i = 0; i < pts.Count; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append(Fmt(pts[i].V, 4));
            sb.Append(' ').Append(Fmt(pts[i].T * 100, 2)).Append('%');
        }
        return sb.Append(')').ToString();
    }

    private static string Fmt(double n, int dp)
    {
        double r = Math.Round(n, dp);
        return r.ToString("0.####", CultureInfo.InvariantCulture);
    }
}
