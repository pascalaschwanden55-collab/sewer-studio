using System;
using System.Globalization;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Vsa;

namespace AuswertungPro.Next.Infrastructure.Costs;

/// <summary>
/// Liest Standard-Werte (DN, Laenge, Anschlussanzahl) aus einem <see cref="HaltungRecord"/>
/// und gibt sie als typisierte Ergebnisse zurueck (kein WPF, keine Seiteneffekte).
/// </summary>
public static class MeasureImportDefaultsResolver
{
    /// <summary>
    /// Ergebnis der Import-Auflosung. Felder sind null, wenn der Datensatz keinen
    /// verwertbaren Wert enthaelt.
    /// </summary>
    public sealed record ImportDefaults(
        int? Dn,
        decimal? LengthMeters,
        int Connections);

    /// <summary>
    /// Liest DN, Laenge und Anschlussanzahl aus dem Haltungsdatensatz.
    /// Die Laenge wird kulturunabhaengig geparst ("45.30" wird nicht zu 4530).
    /// </summary>
    public static ImportDefaults Resolve(HaltungRecord haltungRecord)
    {
        // DN aus Import-Feld
        int? dn = null;
        var dnValue = haltungRecord.GetFieldValue(FieldKeys.NominalDiameterMm);
        if (!string.IsNullOrWhiteSpace(dnValue) && int.TryParse(dnValue, out var parsedDn))
            dn = parsedDn;

        // Laenge kulturunabhaengig parsen
        decimal? lengthM = null;
        var lengthValue = haltungRecord.GetFieldValue(FieldKeys.HoldingLengthMeters);
        if (!string.IsNullOrWhiteSpace(lengthValue) && decimal.TryParse(
                lengthValue.Trim().Replace(',', '.'),
                NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedLength))
            lengthM = parsedLength;

        // Anschlussanzahl aus Schadenscodierung oder explizitem Feld ableiten
        var connections = ConnectionCountEstimator.EstimateFromRecord(haltungRecord) ?? 0;

        return new ImportDefaults(dn, lengthM, connections);
    }
}
