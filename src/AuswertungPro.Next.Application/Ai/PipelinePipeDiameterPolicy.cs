using System.Globalization;

namespace AuswertungPro.Next.Application.Ai;

/// <summary>Bindet die Batch-Messung an den Haltungs-DN und kennzeichnet ihre Grenzen.</summary>
public static class PipelinePipeDiameterPolicy
{
    public static int? Parse(string? raw)
    {
        var normalized = raw?.Trim().Replace(" ", "").Replace("'", "").Replace(',', '.');
        if (!decimal.TryParse(normalized, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture, out var diameter)
            || diameter <= 0 || diameter > int.MaxValue || decimal.Truncate(diameter) != diameter)
            return null;

        return (int)diameter;
    }

    public static int? Resolve(PipelineConfig config)
        => Positive(config.PipeDiameterMm) ?? Positive(config.PipeDiameterMmOverride);

    public static IReadOnlyList<string> Warnings(PipelineConfig config)
    {
        var holding = Positive(config.PipeDiameterMm);
        var global = Positive(config.PipeDiameterMmOverride);
        if (holding is null && global is null)
            return ["Rohrdurchmesser der Haltung fehlt oder ist ungültig. Ohne DN werden keine maskenbasierten Millimeterwerte berechnet."];

        var warnings = new List<string>();
        if (holding is null)
            warnings.Add($"Haltungs-DN unbekannt: Die globale Vorgabe DN{global} wird ersatzweise verwendet. Durchmesser vor der Übernahme prüfen.");
        else if (global is not null && global != holding)
            warnings.Add($"Die Analyse verwendet den Haltungs-DN {holding} mm. Die abweichende globale Vorgabe DN{global} wird nicht verwendet.");

        warnings.Add("Rohrbild nicht kalibriert: Maskenmasse und daraus abgeleitete Schadensstufen sind Schätzungen mit der Annahme, dass das Rohr 70 % der Bildbreite einnimmt. Vor der Übernahme prüfen.");
        return warnings;
    }

    private static int? Positive(int? value) => value is > 0 ? value : null;
}
