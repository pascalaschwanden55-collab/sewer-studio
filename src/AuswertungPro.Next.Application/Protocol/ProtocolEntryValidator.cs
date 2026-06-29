namespace AuswertungPro.Next.Application.Protocol;

/// <summary>
/// Eingabe-Werte fuer die VSA-Feldvalidierung.
/// Alle Felder sind Rohwerte (Strings), wie sie aus UI-Feldern oder ViewModel kommen.
/// </summary>
public sealed record VsaFieldInputs
{
    public string? Distanz { get; init; }
    public string? Video { get; init; }
    public string? UhrVon { get; init; }
    public string? UhrBis { get; init; }
    public string? Q1 { get; init; }
    public string? Q2 { get; init; }
    public string? Strecke { get; init; }
    public string? Ez { get; init; }
    public string? Schachtbereich { get; init; }
    public bool IsStreckenschaden { get; init; }
}

/// <summary>
/// Ergebnis der VSA-Feldvalidierung.
/// </summary>
public sealed class VsaValidationResult
{
    public bool DistanzOk { get; init; }
    public bool VideoOk { get; init; }
    public bool UhrVonOk { get; init; }
    public bool UhrBisOk { get; init; }
    public bool Q1Ok { get; init; }
    public bool Q2Ok { get; init; }
    public bool StreckeOk { get; init; }
    public bool EzOk { get; init; }
    public bool SchachtbereichOk { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Reine Validierungslogik fuer VSA-Protokollfelder.
/// Kein UI-Bezug – nimmt Plain-Strings entgegen und gibt Fehlerlisten zurueck.
/// Konsolidiert die frueher doppelten Methoden ValidateVsaUiFields / ValidateVsaFields.
/// </summary>
public static class ProtocolEntryValidator
{
    /// <summary>
    /// Validiert alle VSA-Felder und gibt ein strukturiertes Ergebnis zurueck.
    /// </summary>
    /// <param name="code">Normierter Schadenscode (kann leer sein).</param>
    /// <param name="inputs">Rohwerte der VSA-Felder.</param>
    /// <param name="catalogDef">
    /// Optionale Katalogdefinition fuer den Code (wird fuer Pflichtfeld-Pruefung genutzt).
    /// </param>
    /// <param name="requireDistanz">
    /// Wenn true und Code nicht leer, ist die Distanz ein Pflichtfeld.
    /// </param>
    public static VsaValidationResult ValidateVsaFields(
        string code,
        VsaFieldInputs inputs,
        CodeDefinition? catalogDef = null,
        bool requireDistanz = true)
    {
        var errors = new List<string>();
        var hasCode = !string.IsNullOrWhiteSpace(code);

        // Distanz
        var distanzOk = ProtocolEntryInputNormalizer.TryParseOptionalDouble(inputs.Distanz ?? string.Empty, out var distanz);
        if (!distanzOk)
        {
            errors.Add("VSA: Distanz ist ungueltig.");
        }
        else if (requireDistanz && hasCode && !distanz.HasValue)
        {
            distanzOk = false;
            errors.Add("VSA: Distanz (m) ist erforderlich.");
        }

        // Video-Uhrzeit
        var videoOk = ProtocolEntryInputNormalizer.TryParseOptionalTimeSpan(inputs.Video ?? string.Empty, out _);
        if (!videoOk)
            errors.Add("VSA: Uhrzeit (Video) ist ungueltig.");

        // Uhrzeiger von
        var uhrVonOk = ProtocolEntryInputNormalizer.TryNormalizeClockPosition(inputs.UhrVon, out _, out var hasUhrVon);
        if (!uhrVonOk)
            errors.Add("VSA: Uhrzeit von nur 00 bis 12.");

        // Uhrzeiger bis
        var uhrBisOk = ProtocolEntryInputNormalizer.TryNormalizeClockPosition(inputs.UhrBis, out _, out var hasUhrBis);
        if (!uhrBisOk)
            errors.Add("VSA: Uhrzeit bis nur 00 bis 12.");

        // Q1
        var q1Ok = ProtocolEntryInputNormalizer.TryParseOptionalDouble(inputs.Q1 ?? string.Empty, out _);
        if (!q1Ok)
            errors.Add("VSA: Quantifizierung 1 ist ungueltig.");

        // Q2
        var q2Ok = ProtocolEntryInputNormalizer.TryParseOptionalDouble(inputs.Q2 ?? string.Empty, out _);
        if (!q2Ok)
            errors.Add("VSA: Quantifizierung 2 ist ungueltig.");

        // Strecke
        var streckeOk = ProtocolEntryInputNormalizer.TryNormalizeStrecke(inputs.Strecke, out _, out var hasStrecke);
        if (!streckeOk)
            errors.Add("VSA: Strecke nur im Format A1/B1/C1...");
        if (inputs.IsStreckenschaden && !hasStrecke)
        {
            streckeOk = false;
            errors.Add("VSA: Strecke ist bei Streckenschaden erforderlich.");
        }

        // EZ
        var ezOk = ProtocolEntryInputNormalizer.TryNormalizeEz(inputs.Ez, out _, out _);
        if (!ezOk)
            errors.Add("VSA: EZ nur EZ0 bis EZ4.");

        // Schachtbereich
        var schachtbereichOk = ProtocolEntryInputNormalizer.TryNormalizeSchachtbereich(inputs.Schachtbereich, out _, out _);
        if (!schachtbereichOk)
            errors.Add("VSA: Schachtbereich nur A/B/D/F/H/I/J.");

        // Katalog-abhaengige Pflichtfelder (Uhr-von, Q1, Q2)
        if (catalogDef is not null)
        {
            var hasClock = catalogDef.Parameters.Any(p =>
                string.Equals(p.Type, "clock", StringComparison.OrdinalIgnoreCase));
            if (hasClock && !hasUhrVon)
            {
                uhrVonOk = false;
                errors.Add("VSA: Uhr von ist erforderlich.");
            }

            var hasQuant1 = catalogDef.Parameters.Any(p =>
                string.Equals(p.Name, "Quant1", StringComparison.OrdinalIgnoreCase)
                || string.Equals(p.Name, "Quantifizierung 1", StringComparison.OrdinalIgnoreCase));
            var hasQuant2 = catalogDef.Parameters.Any(p =>
                string.Equals(p.Name, "Quant2", StringComparison.OrdinalIgnoreCase)
                || string.Equals(p.Name, "Quantifizierung 2", StringComparison.OrdinalIgnoreCase));

            if (!hasQuant1 && !string.IsNullOrWhiteSpace(inputs.Q1))
            {
                q1Ok = false;
                errors.Add("VSA: Quantifizierung 1 ist fuer diesen Code nicht vorgesehen.");
            }

            if (!hasQuant2 && !string.IsNullOrWhiteSpace(inputs.Q2))
            {
                q2Ok = false;
                errors.Add("VSA: Quantifizierung 2 ist fuer diesen Code nicht vorgesehen.");
            }
        }

        // Uhr bis ohne Uhr von
        if (hasUhrBis && !hasUhrVon)
        {
            uhrVonOk = false;
            errors.Add("VSA: Uhr von ist erforderlich, wenn Uhr bis gesetzt ist.");
        }

        return new VsaValidationResult
        {
            DistanzOk = distanzOk,
            VideoOk = videoOk,
            UhrVonOk = uhrVonOk,
            UhrBisOk = uhrBisOk,
            Q1Ok = q1Ok,
            Q2Ok = q2Ok,
            StreckeOk = streckeOk,
            EzOk = ezOk,
            SchachtbereichOk = schachtbereichOk,
            Errors = errors
        };
    }
}
