using System.Globalization;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.QgisBridge;

/// <summary>
/// Unveraenderliche Momentaufnahme der Projektdaten fuer die QGIS-Bridge.
/// Wird auf dem UI-Thread erstellt (billige Feld-Kopien), damit der GeoJSON-Bau
/// danach gefahrlos im Hintergrund laufen kann (Project.Data ist UI-gebunden).
/// </summary>
internal sealed record QgisProjectSnapshot(
    Guid ProjectId,
    string ProjectName,
    string CurrentHolding,
    IReadOnlyList<QgisHaltungSnapshot> Haltungen,
    long SelectionStamp = 0)
{
    public static QgisProjectSnapshot Empty { get; } =
        new(Guid.Empty, "", "", Array.Empty<QgisHaltungSnapshot>());

    public static QgisProjectSnapshot Capture(Project? project, string? currentHolding, long selectionStamp = 0)
    {
        var holding = currentHolding?.Trim() ?? "";
        if (project is null)
            return Empty with { CurrentHolding = holding, SelectionStamp = selectionStamp };

        var haltungen = new List<QgisHaltungSnapshot>(project.Data.Count);
        foreach (var record in project.Data)
        {
            var name = record.GetFieldValue("Haltungsname")?.Trim();
            if (string.IsNullOrEmpty(name))
                continue;

            haltungen.Add(new QgisHaltungSnapshot(
                name,
                TryParseCondition(record.GetFieldValue("Zustandsklasse")),
                IsGegenFliessrichtung(record.GetFieldValue("Inspektionsrichtung")),
                CaptureDamages(record)));
        }

        return new QgisProjectSnapshot(project.Id, project.Name, holding, haltungen, selectionStamp);
    }

    /// <summary>
    /// Schaeden einer Haltung: das kuratierte Protokoll (inkl. manueller und KI-Eintraege)
    /// hat Vorrang; nur wenn keines existiert, dienen die importierten VSA-Feststellungen
    /// als Fallback. So spiegelt QGIS immer den aktuellen Stand aus SewerStudio.
    /// </summary>
    private static IReadOnlyList<QgisDamageSnapshot> CaptureDamages(HaltungRecord record)
    {
        var entries = record.Protocol?.Current?.Entries;
        if (entries is { Count: > 0 })
        {
            var fromProtocol = new List<QgisDamageSnapshot>(entries.Count);
            foreach (var entry in entries)
            {
                if (entry.IsDeleted)
                    continue;

                fromProtocol.Add(new QgisDamageSnapshot(
                    Code: EmptyToNull(entry.Code),
                    Beschreibung: EmptyToNull(entry.Beschreibung),
                    MeterStart: entry.MeterStart,
                    MeterEnd: entry.MeterEnd,
                    IsStreckenschaden: entry.IsStreckenschaden,
                    Severity: EmptyToNull(entry.CodeMeta?.Severity),
                    Mpeg: EmptyToNull(entry.Mpeg),
                    Raw: null,
                    Quantifizierung1: null,
                    Quantifizierung2: null,
                    EZD: null,
                    EZS: null,
                    EZB: null,
                    Source: "protokoll"));
            }

            if (fromProtocol.Count > 0)
                return fromProtocol;
        }

        var findings = record.VsaFindings;
        if (findings is null || findings.Count == 0)
            return Array.Empty<QgisDamageSnapshot>();

        var fromFindings = new List<QgisDamageSnapshot>(findings.Count);
        foreach (var finding in findings)
        {
            fromFindings.Add(new QgisDamageSnapshot(
                Code: EmptyToNull(finding.KanalSchadencode),
                Beschreibung: null,
                MeterStart: finding.MeterStart ?? finding.SchadenlageAnfang,
                MeterEnd: finding.MeterEnd ?? finding.SchadenlageEnde,
                IsStreckenschaden: false,
                Severity: null,
                Mpeg: EmptyToNull(finding.MPEG),
                Raw: EmptyToNull(finding.Raw),
                Quantifizierung1: EmptyToNull(finding.Quantifizierung1),
                Quantifizierung2: EmptyToNull(finding.Quantifizierung2),
                EZD: finding.EZD,
                EZS: finding.EZS,
                EZB: finding.EZB,
                Source: "import"));
        }

        return fromFindings;
    }

    private static int? TryParseCondition(string? raw)
        => int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;

    /// <summary>Feldwerte laut FieldCatalog: "In Fliessrichtung" / "Gegen Fliessrichtung" / leer.</summary>
    private static bool IsGegenFliessrichtung(string? inspektionsrichtung)
        => inspektionsrichtung?.TrimStart().StartsWith("gegen", StringComparison.OrdinalIgnoreCase) == true;

    private static string? EmptyToNull(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    // ── Fingerprints fuer den Payload-Cache ─────────────────────────────────
    // Aendert sich der Fingerprint nicht, kann der Server die zuletzt
    // serialisierten GeoJSON-Bytes wiederverwenden statt sie neu zu bauen.

    /// <summary>Fingerprint fuer network.geojson: Haltungsnamen + Zustandsklassen + XTF-Stand.</summary>
    public QgisPayloadFingerprint NetworkFingerprint(long xtfTicks)
    {
        var hash = new HashCode();
        foreach (var haltung in Haltungen)
        {
            hash.Add(haltung.Haltungsname, StringComparer.OrdinalIgnoreCase);
            hash.Add(haltung.Zustandsklasse);
        }

        return new QgisPayloadFingerprint(xtfTicks, hash.ToHashCode(), Haltungen.Count);
    }

    /// <summary>Fingerprint fuer damages.geojson: alle Schadenfelder + XTF-Stand.</summary>
    public QgisPayloadFingerprint DamagesFingerprint(long xtfTicks)
    {
        var hash = new HashCode();
        var count = 0;
        foreach (var haltung in Haltungen)
        {
            hash.Add(haltung.Haltungsname, StringComparer.OrdinalIgnoreCase);
            hash.Add(haltung.Zustandsklasse);
            // Richtung beeinflusst die Punktpositionen -> muss den Cache invalidieren.
            hash.Add(haltung.GegenFliessrichtung);
            foreach (var damage in haltung.Schaeden)
            {
                hash.Add(damage.Code);
                hash.Add(damage.Beschreibung);
                hash.Add(damage.MeterStart);
                hash.Add(damage.MeterEnd);
                hash.Add(damage.IsStreckenschaden);
                hash.Add(damage.Severity);
                hash.Add(damage.Source);
                count++;
            }
        }

        return new QgisPayloadFingerprint(xtfTicks, hash.ToHashCode(), count);
    }

    /// <summary>Fingerprint fuer current.geojson: gewaehlte Haltung + deren Zustand/Schadenzahl + XTF-Stand.</summary>
    public QgisPayloadFingerprint CurrentFingerprint(long xtfTicks)
    {
        var record = Haltungen.FirstOrDefault(h =>
            string.Equals(h.Haltungsname, CurrentHolding, StringComparison.OrdinalIgnoreCase));

        var hash = new HashCode();
        hash.Add(CurrentHolding, StringComparer.OrdinalIgnoreCase);
        hash.Add(record?.Zustandsklasse);
        hash.Add(record?.GegenFliessrichtung ?? false);
        hash.Add(record?.Schaeden.Count ?? -1);

        return new QgisPayloadFingerprint(xtfTicks, hash.ToHashCode(), 1);
    }
}

/// <summary>Cache-Schluessel eines serialisierten Bridge-Payloads (Wertegleichheit).</summary>
internal readonly record struct QgisPayloadFingerprint(long XtfTicks, int Hash, int Count);

internal sealed record QgisHaltungSnapshot(
    string Haltungsname,
    int? Zustandsklasse,
    bool GegenFliessrichtung,
    IReadOnlyList<QgisDamageSnapshot> Schaeden);

internal sealed record QgisDamageSnapshot(
    string? Code,
    string? Beschreibung,
    double? MeterStart,
    double? MeterEnd,
    bool IsStreckenschaden,
    string? Severity,
    string? Mpeg,
    string? Raw,
    string? Quantifizierung1,
    string? Quantifizierung2,
    int? EZD,
    int? EZS,
    int? EZB,
    string Source);
