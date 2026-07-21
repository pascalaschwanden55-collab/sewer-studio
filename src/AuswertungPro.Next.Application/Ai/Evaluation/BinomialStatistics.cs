namespace AuswertungPro.Next.Application.Ai.Evaluation;

internal sealed record BinomialRateEstimate(
    int Trials,
    int Occurrences,
    double Rate,
    double WilsonLower95,
    double WilsonUpper95,
    double ExactUpper95);

internal static class BinomialStatistics
{
    private const double Z95 = 1.959963984540054;

    public static BinomialRateEstimate EstimateRate95(int trials, int occurrences)
    {
        var safeTrials = Math.Max(0, trials);
        var safeOccurrences = Math.Clamp(occurrences, 0, safeTrials);
        var rate = safeTrials == 0 ? 0.0 : (double)safeOccurrences / safeTrials;
        var (lower, upper) = Wilson95(safeTrials, safeOccurrences);

        return new BinomialRateEstimate(
            safeTrials,
            safeOccurrences,
            rate,
            lower,
            upper,
            ExactUpper95(safeTrials, safeOccurrences));
    }

    public static double ExactUpper95(int trials, int occurrences)
    {
        if (trials <= 0)
            return 1.0;

        occurrences = Math.Clamp(occurrences, 0, trials);
        if (occurrences == trials)
            return 1.0;

        const double alpha = 0.05;
        var low = (double)occurrences / trials;
        var high = 1.0;
        for (var i = 0; i < 80; i++)
        {
            var mid = (low + high) / 2.0;
            var cdf = BinomialCdf(occurrences, trials, mid);
            if (cdf > alpha)
                low = mid;
            else
                high = mid;
        }

        return (low + high) / 2.0;
    }

    private static (double Lower, double Upper) Wilson95(int trials, int occurrences)
    {
        if (trials <= 0)
            return (0.0, 1.0);

        var proportion = (double)occurrences / trials;
        var zSquared = Z95 * Z95;
        var denominator = 1.0 + zSquared / trials;
        var center = (proportion + zSquared / (2.0 * trials)) / denominator;
        var margin = Z95
                     * Math.Sqrt((proportion * (1.0 - proportion) + zSquared / (4.0 * trials)) / trials)
                     / denominator;

        return (
            Math.Clamp(center - margin, 0.0, 1.0),
            Math.Clamp(center + margin, 0.0, 1.0));
    }

    private static double BinomialCdf(int maxOccurrences, int trials, double probability)
    {
        if (probability <= 0)
            return 1.0;
        if (probability >= 1)
            return maxOccurrences >= trials ? 1.0 : 0.0;

        var term = Math.Pow(1.0 - probability, trials);
        var sum = term;
        for (var k = 0; k < maxOccurrences; k++)
        {
            term *= (trials - k) / (double)(k + 1) * probability / (1.0 - probability);
            sum += term;
        }

        return Math.Clamp(sum, 0.0, 1.0);
    }
}
