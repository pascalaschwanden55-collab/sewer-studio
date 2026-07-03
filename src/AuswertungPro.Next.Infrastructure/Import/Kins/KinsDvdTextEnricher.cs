using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Infrastructure.Import.Kins;

/// <summary>Ergebnis der kiDVDaten.txt-Anreicherung.</summary>
public sealed record KinsDvdTextEnrichResult(
    int TimecodesGesetzt,
    int LaengenGesetzt,
    int DatumGesetzt,
    IReadOnlyList<string> Messages);

/// <summary>
/// Reichert ein bereits importiertes Projekt (massgebliche Quelle: VSAKEK-XTF)
/// mit Daten aus der KINS kiDVDaten.txt an: Video-Timecodes je Beobachtung
/// (@Pos, Meter-Match), inspizierte Laenge (letzter Meterstand — das XTF
/// liefert immer 0) und Aufnahmedatum aus kiDVinfo.txt.
/// Es werden NUR leere Werte gefuellt — Prioritaet: UserEdit &gt; XTF &gt; TXT.
/// </summary>
public static class KinsDvdTextEnricher
{
    public static KinsDvdTextEnrichResult Apply(Project project, string kiDvDatenPath)
    {
        var messages = new List<string>();

        if (project is null || string.IsNullOrWhiteSpace(kiDvDatenPath) || !File.Exists(kiDvDatenPath))
        {
            messages.Add("kiDVDaten.txt nicht gefunden — Anreicherung uebersprungen.");
            return new KinsDvdTextEnrichResult(0, 0, 0, messages);
        }

        var bloecke = ParseBloecke(kiDvDatenPath);
        var aufnahmeDatum = KinsImportService.TryReadRecordingDate(
            Path.GetDirectoryName(kiDvDatenPath) ?? kiDvDatenPath);

        var timecodes = 0;
        var laengen = 0;
        var daten = 0;

        foreach (var (header, beobachtungen) in bloecke)
        {
            var record = FindeHaltung(project, header);
            if (record is null)
            {
                messages.Add($"kiDVDaten: Haltung {header.From}-{header.To} nicht im Projekt — uebersprungen.");
                continue;
            }

            // Beide Revisionen befuellen; gezaehlt wird nur die sichtbare (Current).
            SetzeTimecodes(record.Protocol?.Original.Entries, beobachtungen);
            timecodes += SetzeTimecodes(record.Protocol?.Current.Entries, beobachtungen);

            laengen += SetzeLaenge(record, beobachtungen);
            daten += SetzeDatum(record, aufnahmeDatum);
        }

        return new KinsDvdTextEnrichResult(timecodes, laengen, daten, messages);
    }

    // ------------------------------------------------------------------
    // Parsing: kiDVDaten.txt in Bloecke (Kopfzeile + Beobachtungen)
    // ------------------------------------------------------------------

    private static List<(KinsHoldingHeader Header, List<ProtocolEntry> Beobachtungen)> ParseBloecke(string pfad)
    {
        var bloecke = new List<(KinsHoldingHeader, List<ProtocolEntry>)>();
        KinsHoldingHeader? aktuellerHeader = null;
        var aktuelleEintraege = new List<ProtocolEntry>();

        void Flush()
        {
            if (aktuellerHeader is null)
                return;
            bloecke.Add((aktuellerHeader.Value, aktuelleEintraege));
            aktuellerHeader = null;
            aktuelleEintraege = new List<ProtocolEntry>();
        }

        foreach (var rohzeile in KinsImportService.ReadTextLines(pfad))
        {
            var zeile = rohzeile?.TrimEnd() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(zeile))
                continue;

            if (KinsTextLineParser.TryParseHeaderLine(zeile, out var header))
            {
                Flush();
                aktuellerHeader = header;
                continue;
            }

            if (aktuellerHeader is null)
                continue;

            if (KinsTextLineParser.TryParseObservationLine(zeile, out var eintrag))
                aktuelleEintraege.Add(eintrag);
        }

        Flush();
        return bloecke;
    }

    // ------------------------------------------------------------------
    // Haltung finden: Name {From}-{To}, sonst Schacht oben/unten
    // ------------------------------------------------------------------

    private static HaltungRecord? FindeHaltung(Project project, KinsHoldingHeader header)
    {
        var schluessel = Normalisiere($"{header.From}-{header.To}");

        var perName = project.Data.FirstOrDefault(r =>
            string.Equals(Normalisiere(r.GetFieldValue("Haltungsname")), schluessel, StringComparison.OrdinalIgnoreCase));
        if (perName is not null)
            return perName;

        return project.Data.FirstOrDefault(r =>
            string.Equals((r.GetFieldValue("Schacht_oben") ?? "").Trim(), header.From, StringComparison.OrdinalIgnoreCase) &&
            string.Equals((r.GetFieldValue("Schacht_unten") ?? "").Trim(), header.To, StringComparison.OrdinalIgnoreCase));
    }

    private static string Normalisiere(string? wert)
        => string.IsNullOrWhiteSpace(wert)
            ? string.Empty
            : wert.Trim().Replace(" ", string.Empty).ToUpperInvariant();

    // ------------------------------------------------------------------
    // Timecodes: Meter-Match (1 Dezimalstelle), gleiche Meter in Reihenfolge
    // ------------------------------------------------------------------

    private static int SetzeTimecodes(IList<ProtocolEntry>? ziele, List<ProtocolEntry> txtBeobachtungen)
    {
        if (ziele is null || ziele.Count == 0)
            return 0;

        // Pro Aufruf eigene Warteschlangen, damit Original UND Current befuellt werden koennen.
        var txtProMeter = txtBeobachtungen
            .Where(e => !string.IsNullOrWhiteSpace(e.Mpeg))
            .GroupBy(e => Math.Round(e.MeterStart ?? 0d, 1))
            .ToDictionary(g => g.Key, g => new Queue<ProtocolEntry>(g));

        var gesetzt = 0;
        foreach (var ziel in ziele)
        {
            if (ziel.Source != ProtocolEntrySource.Imported)
                continue;
            if (!string.IsNullOrWhiteSpace(ziel.Mpeg) || ziel.Zeit is not null)
                continue; // XTF-Timecode hat Vorrang
            if (ziel.MeterStart is not double meter)
                continue;

            if (!txtProMeter.TryGetValue(Math.Round(meter, 1), out var warteschlange) || warteschlange.Count == 0)
                continue; // lieber kein Timecode als ein falscher Videosprung

            var quelle = warteschlange.Dequeue();
            ziel.Mpeg = quelle.Mpeg;
            ziel.Zeit = quelle.Zeit;
            gesetzt++;
        }

        return gesetzt;
    }

    // ------------------------------------------------------------------
    // Laenge + Datum (nur leere/Null-Werte, nie UserEdited)
    // ------------------------------------------------------------------

    private static int SetzeLaenge(HaltungRecord record, List<ProtocolEntry> beobachtungen)
    {
        if (IstUserEdited(record, "Haltungslaenge_m"))
            return 0;

        var vorhanden = (record.GetFieldValue("Haltungslaenge_m") ?? string.Empty).Trim();
        var istLeerOderNull = string.IsNullOrWhiteSpace(vorhanden)
            || (double.TryParse(vorhanden.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var wert) && wert == 0d);
        if (!istLeerOderNull)
            return 0;

        var maxMeter = beobachtungen
            .Select(e => e.MeterEnd ?? e.MeterStart)
            .Where(m => m.HasValue)
            .Select(m => m!.Value)
            .DefaultIfEmpty(0d)
            .Max();
        if (maxMeter <= 0d)
            return 0;

        record.SetFieldValue(
            "Haltungslaenge_m",
            maxMeter.ToString("0.0##", CultureInfo.InvariantCulture),
            FieldSource.Legacy,
            userEdited: false);
        return 1;
    }

    private static int SetzeDatum(HaltungRecord record, DateTime? aufnahmeDatum)
    {
        if (aufnahmeDatum is null || IstUserEdited(record, "Datum_Jahr"))
            return 0;

        if (!string.IsNullOrWhiteSpace(record.GetFieldValue("Datum_Jahr")))
            return 0;

        record.SetFieldValue(
            "Datum_Jahr",
            aufnahmeDatum.Value.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture),
            FieldSource.Legacy,
            userEdited: false);
        return 1;
    }

    private static bool IstUserEdited(HaltungRecord record, string feld)
        => record.FieldMeta.TryGetValue(feld, out var meta) && meta.UserEdited;
}
