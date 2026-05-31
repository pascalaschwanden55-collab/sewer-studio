using System.Globalization;

namespace AuswertungPro.Next.Application.Ai.Training;

/// <summary>
/// Fachliche Plausibilitaet eines TrainingSample (D7).
/// Verwirft nur objektiv falsche Samples, damit die KI nicht aus Muell lernt.
/// Reine Logik, kein I/O.
/// </summary>
public static class TrainingSamplePlausibility
{
    public static bool IsFachlichPlausibel(TrainingSample sample, out string reason)
    {
        if (sample.MeterStart < 0)
        {
            reason = $"Negativer Meterstand ({sample.MeterStart}).";
            return false;
        }

        if (sample.MeterEnd < sample.MeterStart)
        {
            reason = $"Meter-Ende ({sample.MeterEnd}) vor Meter-Start ({sample.MeterStart}).";
            return false;
        }

        var meta = sample.CodeMeta;
        if (meta is not null)
        {
            if (TryParseInt(meta.Severity, out var severity) && (severity < 1 || severity > 5))
            {
                reason = $"Severity {severity} ausserhalb 1-5.";
                return false;
            }

            if (meta.Parameters.TryGetValue("vsa.querschnitt.prozent", out var querschnittRaw)
                && TryParseDouble(querschnittRaw, out var querschnitt)
                && (querschnitt < 0 || querschnitt > 100))
            {
                reason = $"Querschnitt {querschnitt}% ausserhalb 0-100.";
                return false;
            }
        }

        reason = string.Empty;
        return true;
    }

    private static bool TryParseInt(string? value, out int result)
    {
        result = 0;
        return !string.IsNullOrWhiteSpace(value)
            && int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out result);
    }

    private static bool TryParseDouble(string? value, out double result)
    {
        result = 0;
        return !string.IsNullOrWhiteSpace(value)
            && double.TryParse(
                value.Trim().Replace(',', '.'),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out result);
    }
}
