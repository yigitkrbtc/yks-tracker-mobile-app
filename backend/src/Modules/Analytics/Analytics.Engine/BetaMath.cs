namespace Nirengi.Modules.Analytics.Engine;

/// <summary>
/// Beta distribution functions without external packages. Accuracy is checked against scipy in the golden
/// dataset (reference/reference.py) and in BetaMathTests.
/// </summary>
public static class BetaMath
{
    private const double Epsilon = 1e-15;
    private const double Tiny = 1e-300;
    private const int MaxIterations = 20_000;

    private static readonly double[] Lanczos =
    [
        0.99999999999980993, 676.5203681218851, -1259.1392167224028, 771.32342877765313, -176.61502916214059,
        12.507343278686905, -0.13857109526572012, 9.9843695780195716e-6, 1.5056327351493116e-7,
    ];

    // 8-point Gauss–Legendre nodes and weights on [-1, 1].
    private static readonly double[] GaussNodes =
    [
        -0.9602898564975363, -0.7966664774136267, -0.5255324099163290, -0.1834346424956498,
        0.1834346424956498, 0.5255324099163290, 0.7966664774136267, 0.9602898564975363,
    ];

    private static readonly double[] GaussWeights =
    [
        0.1012285362903763, 0.2223810344533745, 0.3137066458778873, 0.3626837833783620,
        0.3626837833783620, 0.3137066458778873, 0.2223810344533745, 0.1012285362903763,
    ];

    public static double Mean(double a, double b) => a / (a + b);

    public static double Variance(double a, double b) => a * b / ((a + b) * (a + b) * (a + b + 1));

    public static double LogGamma(double x)
    {
        if (x < 0.5)
        {
            return Math.Log(Math.PI / Math.Abs(Math.Sin(Math.PI * x))) - LogGamma(1 - x);
        }

        x -= 1;
        var sum = Lanczos[0];
        for (var i = 1; i < Lanczos.Length; i++)
        {
            sum += Lanczos[i] / (x + i);
        }

        var t = x + 7.5;
        return (0.5 * Math.Log(2 * Math.PI)) + ((x + 0.5) * Math.Log(t)) - t + Math.Log(sum);
    }

    private static double LogBeta(double a, double b) => LogGamma(a) + LogGamma(b) - LogGamma(a + b);

    public static double Density(double x, double a, double b)
    {
        if (x <= 0 || x >= 1)
        {
            return 0;
        }

        return Math.Exp(((a - 1) * Math.Log(x)) + ((b - 1) * Math.Log(1 - x)) - LogBeta(a, b));
    }

    /// <summary>Regularized incomplete beta I_x(a, b) = P(X ≤ x) for X ~ Beta(a, b).</summary>
    public static double Cdf(double x, double a, double b)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(a);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(b);
        if (x <= 0)
        {
            return 0;
        }

        if (x >= 1)
        {
            return 1;
        }

        var front = Math.Exp((a * Math.Log(x)) + (b * Math.Log(1 - x)) - LogBeta(a, b));
        return x < (a + 1) / (a + b + 2)
            ? front * ContinuedFraction(x, a, b) / a
            : 1 - (front * ContinuedFraction(1 - x, b, a) / b);
    }

    // Lentz's method for the continued fraction of the incomplete beta function.
    private static double ContinuedFraction(double x, double a, double b)
    {
        var qab = a + b;
        var qap = a + 1;
        var qam = a - 1;
        var c = 1.0;
        var d = 1 - (qab * x / qap);
        d = Math.Abs(d) < Tiny ? Tiny : d;
        d = 1 / d;
        var h = d;
        for (var m = 1; m <= MaxIterations; m++)
        {
            var m2 = 2 * m;
            var aa = m * (b - m) * x / ((qam + m2) * (a + m2));
            d = 1 + (aa * d);
            d = Math.Abs(d) < Tiny ? Tiny : d;
            c = 1 + (aa / c);
            c = Math.Abs(c) < Tiny ? Tiny : c;
            d = 1 / d;
            h *= d * c;
            aa = -(a + m) * (qab + m) * x / ((a + m2) * (qap + m2));
            d = 1 + (aa * d);
            d = Math.Abs(d) < Tiny ? Tiny : d;
            c = 1 + (aa / c);
            c = Math.Abs(c) < Tiny ? Tiny : c;
            d = 1 / d;
            var delta = d * c;
            h *= delta;
            if (Math.Abs(delta - 1) < Epsilon)
            {
                return h;
            }
        }

        return h;
    }

    /// <summary>Inverse of <see cref="Cdf"/>: the p-quantile of Beta(a, b).</summary>
    public static double Quantile(double p, double a, double b)
    {
        if (p <= 0)
        {
            return 0;
        }

        if (p >= 1)
        {
            return 1;
        }

        // Safeguarded Newton: bisection keeps the bracket, Newton speeds up where the density is usable.
        double lo = 0, hi = 1;
        var x = Mean(a, b);
        for (var i = 0; i < 200; i++)
        {
            var f = Cdf(x, a, b) - p;
            if (Math.Abs(f) < 1e-15)
            {
                return x;
            }

            if (f < 0)
            {
                lo = x;
            }
            else
            {
                hi = x;
            }

            var density = Density(x, a, b);
            var next = density > 0 ? x - (f / density) : double.NaN;
            if (double.IsNaN(next) || next <= lo || next >= hi)
            {
                next = (lo + hi) / 2;
            }

            if (Math.Abs(next - x) < 1e-15)
            {
                return next;
            }

            x = next;
        }

        return x;
    }

    /// <summary>
    /// P(X &gt; Y) for independent X ~ Beta(a1, b1), Y ~ Beta(a2, b2): ∫ f_X(x)·F_Y(x) dx with composite
    /// Gauss–Legendre over the part of X's support that holds all but 1e-12 of its mass. Needs a1, b1 ≥ 1
    /// (bounded density), which holds for posteriors of a Beta(1, 1) prior.
    /// </summary>
    public static double ProbabilityGreater(double a1, double b1, double a2, double b2)
    {
        if (a1 < 1 || b1 < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(a1), "Shape parameters of X must be at least 1.");
        }

        var lo = Quantile(1e-13, a1, b1);
        var hi = Quantile(1 - 1e-13, a1, b1);
        const int panels = 64;
        var width = (hi - lo) / panels;
        var sum = 0.0;
        for (var p = 0; p < panels; p++)
        {
            var mid = lo + ((p + 0.5) * width);
            for (var k = 0; k < GaussNodes.Length; k++)
            {
                var x = mid + (GaussNodes[k] * width / 2);
                sum += GaussWeights[k] * Density(x, a1, b1) * Cdf(x, a2, b2);
            }
        }

        return Math.Clamp(sum * width / 2, 0, 1);
    }
}
