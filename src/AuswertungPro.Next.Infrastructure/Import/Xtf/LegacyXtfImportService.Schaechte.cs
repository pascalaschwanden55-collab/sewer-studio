using System.Xml.Linq;
using AuswertungPro.Next.Application.Xtf;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Infrastructure.Import.Common;
using ImportRunContext = AuswertungPro.Next.Application.Import.ImportRunContext;

namespace AuswertungPro.Next.Infrastructure.Import.Xtf;

/// <summary>
/// Schaechte aus der SIA405-XTF (Klasse <c>Normschacht</c>).
///
/// Bis 2026-08-30 legte kein XTF-Weg Schaechte an. Der QGIS-Export Zone 1.17 enthaelt
/// 295 Normschacht-Objekte mit Funktion (100 %), Material (100 %), Dimension1/2 (98 %)
/// und Eigentuemer (289 von 295) — nichts davon kam an. Gemessen an allen 17 echten
/// Projekten waren alle 122 vorhandenen Eigentumsangaben von Hand gesetzt.
///
/// Die fachliche Abbildung liegt in <see cref="XtfNormschachtStammdaten"/>; hier bleibt
/// nur das Lesen der Datei und das Zusammenfuehren mit dem Projekt.
/// </summary>
public sealed partial class LegacyXtfImportService
{
    /// <summary>
    /// Liest die Normschaechte einer SIA405-XTF. Nur lesend; ohne Bezeichnung wird
    /// uebersprungen, weil sich ein Schacht ohne Nummer spaeter keinem Protokoll
    /// zuordnen laesst.
    /// </summary>
    internal static List<XtfNormschachtElement> ParseSia405Schaechte(XDocument doc)
    {
        ArgumentNullException.ThrowIfNull(doc);

        var elemente = new List<XtfNormschachtElement>();
        var organisationen = LiesOrganisationen(doc);

        foreach (var node in doc.Descendants())
        {
            var lokal = node.Name.LocalName;
            if (!lokal.Equals("Normschacht", StringComparison.OrdinalIgnoreCase)
                && !lokal.EndsWith(".Normschacht", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string? Kind(string name) => node.Elements()
                .FirstOrDefault(e => string.Equals(e.Name.LocalName, name, StringComparison.OrdinalIgnoreCase))
                ?.Value;

            string? WertOderVerweis(string name, string refName)
            {
                var direkt = Kind(name);
                return string.IsNullOrWhiteSpace(direkt)
                    ? Verweis(node, refName, organisationen)
                    : direkt;
            }

            var bezeichnung = (Kind("Bezeichnung") ?? "").Trim();
            if (bezeichnung.Length == 0)
                continue;

            elemente.Add(new XtfNormschachtElement(
                bezeichnung,
                Kind("Funktion"),
                Kind("Material"),
                Kind("Dimension1"),
                Kind("Dimension2"),
                WertOderVerweis("Eigentuemer", "EigentuemerRef"),
                Kind("BaulicherZustand"),
                Kind("Bemerkung"),
                Kind("Status"),
                Kind("Sanierungsbedarf"),
                Kind("Baujahr"),
                WertOderVerweis("Datenherr", "DatenherrRef"),
                WertOderVerweis("Datenlieferant", "DatenlieferantRef"),
                (string?)node.Attribute("TID")));
        }

        return elemente;
    }

    /// <summary>
    /// Die Organisationen der Datei als Kennung -> Bezeichnung.
    ///
    /// In SIA405 ist der Eigentuemer kein Text, sondern ein Verweis auf ein Objekt im
    /// Topic <c>Administration</c>. Wer nur nach einem Element <c>Eigentuemer</c> sucht,
    /// findet in einer normkonformen Datei nichts — und der Eigentuemer geht beim Import
    /// verloren. Genau der fehlt dann beim naechsten Export wieder, denn dort ist er
    /// Pflicht.
    /// </summary>
    private static Dictionary<string, string> LiesOrganisationen(XDocument doc)
    {
        var jeTid = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var node in doc.Descendants())
        {
            var lokal = node.Name.LocalName;
            if (!lokal.Equals("Organisation", StringComparison.OrdinalIgnoreCase)
                && !lokal.EndsWith(".Organisation", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var tid = (string?)node.Attribute("TID");
            var bezeichnung = node.Elements()
                .FirstOrDefault(e => e.Name.LocalName.Equals("Bezeichnung", StringComparison.OrdinalIgnoreCase))
                ?.Value?.Trim();

            if (!string.IsNullOrWhiteSpace(tid) && !string.IsNullOrWhiteSpace(bezeichnung))
                jeTid[tid!] = bezeichnung!;
        }

        return jeTid;
    }

    /// <summary>Die Bezeichnung hinter einem Verweis, oder <c>null</c>.</summary>
    internal static string? Verweis(XElement node, string name, IReadOnlyDictionary<string, string> ziele)
    {
        var referenz = node.Elements()
            .FirstOrDefault(e => e.Name.LocalName.Equals(name, StringComparison.OrdinalIgnoreCase))
            ?.Attribute("REF")?.Value;

        return !string.IsNullOrWhiteSpace(referenz) && ziele.TryGetValue(referenz!, out var bezeichnung)
            ? bezeichnung
            : null;
    }

    /// <summary>
    /// Fuehrt die gelesenen Normschaechte mit dem Projekt zusammen: bekannte Nummer
    /// aktualisieren, neue anlegen. Ein von Hand gesetztes Feld bleibt unveraendert —
    /// <see cref="SchachtRecord.SetFieldValue(string, string?, FieldSource, bool)"/>
    /// weist einen automatischen Schreibvorgang darauf ab.
    /// </summary>
    private static int MergeSchaechteIntoProject(
        Project project,
        IReadOnlyList<XtfNormschachtElement> elemente,
        ImportStats stats,
        ImportRunContext? ctx = null)
    {
        var beruehrt = 0;

        foreach (var element in elemente)
        {
            var paare = XtfNormschachtStammdaten.Feldpaare(element);
            if (paare.Count == 0)
                continue;

            var schluessel = NormalizeHoldingKey(element.Bezeichnung);
            if (string.IsNullOrWhiteSpace(schluessel))
                continue;

            var ziel = FindeSchacht(project, schluessel);
            if (ziel is null)
            {
                ziel = new SchachtRecord();
                if (ctx is null)
                    project.SchaechteData.Add(ziel);
                else
                    ctx.WithCollectionLock(() => project.SchaechteData.Add(ziel));
                stats.CreatedRecords++;
            }

            foreach (var (feld, wert) in paare)
                ziel.SetFieldValue(feld, wert, FieldSource.Xtf405, userEdited: false);

            beruehrt++;
        }

        return beruehrt;
    }

    /// <summary>
    /// Fuehrt die Schachtbegehungen einer VSA-KEK-Datei mit dem Projekt zusammen.
    ///
    /// Zwei Begehungen desselben Schachts sind EIN Bauwerk — der zweite Lauf trifft
    /// denselben Datensatz. Ein vorhandenes Protokoll wird nie ersetzt: Es kann von Hand
    /// bearbeitet worden sein, und ein Import darf das nicht ueberschreiben.
    /// </summary>
    private static int MergeVsaKekSchaechteIntoProject(
        Project project,
        IReadOnlyList<XtfSchachtUntersuchung> begehungen,
        ImportStats stats,
        ImportRunContext? ctx = null)
    {
        var beruehrt = 0;

        foreach (var begehung in begehungen)
        {
            var schluessel = NormalizeHoldingKey(begehung.Nummer);
            if (string.IsNullOrWhiteSpace(schluessel))
                continue;

            var ziel = FindeSchacht(project, schluessel);
            if (ziel is null)
            {
                ziel = new SchachtRecord();
                if (ctx is null)
                    project.SchaechteData.Add(ziel);
                else
                    ctx.WithCollectionLock(() => project.SchaechteData.Add(ziel));
                stats.CreatedRecords++;
            }

            ziel.SetFieldValue("Schachtnummer", begehung.Nummer.Trim(), FieldSource.Xtf, userEdited: false);

            if (!string.IsNullOrWhiteSpace(begehung.Zeitpunkt))
                ziel.SetFieldValue(FieldKeys.InspectionYear, begehung.Zeitpunkt, FieldSource.Xtf, userEdited: false);

            // Erfassungsart und Operateur gehoeren zur Untersuchung, nicht zu den
            // Stammdaten. "Ausgefuehrt_durch" ist bewusst NICHT das Ziel — dort steht
            // die ausfuehrende Firmenart einer Sanierung, kein Kamerabediener.
            var kontext = new List<string>();
            if (!string.IsNullOrWhiteSpace(begehung.Erfassungsart))
                kontext.Add($"Erfassung: {begehung.Erfassungsart.Trim()}");
            if (!string.IsNullOrWhiteSpace(begehung.Operateur))
                kontext.Add($"Operateur: {begehung.Operateur.Trim()}");
            if (kontext.Count > 0)
                ziel.SetFieldValue("Bemerkungen", string.Join(", ", kontext), FieldSource.Xtf, userEdited: false);

            if (begehung.Eintraege.Count > 0)
                LegeBegehungAb(ziel, begehung, stats);

            beruehrt++;
        }

        return beruehrt;
    }

    /// <summary>
    /// Legt die Schaeden einer Begehung ab, ohne je etwas zu ueberschreiben.
    ///
    /// Der erste Bestand wird Original und Arbeitskopie. Jede weitere Begehung — und
    /// jede Begehung zu einem bereits vorhandenen Protokoll — kommt als zusaetzliche
    /// Revision in die Historie und wird gemeldet.
    ///
    /// Gemessen an Andermatt Zone 2.11: Schacht 2200 wurde zweimal begangen. Wer die
    /// zweite Begehung einfach auslaesst, verliert 11 Schachtschaeden still; wer sie
    /// als Ersatz einsetzt, verliert die 11 der ersten. Beides waere falsch — welche
    /// Begehung gilt, entscheidet die Fachperson.
    /// </summary>
    private static void LegeBegehungAb(
        SchachtRecord ziel,
        XtfSchachtUntersuchung begehung,
        ImportStats stats)
    {
        // Die ursprüngliche Untersuchung ist bereits erhalten, auch wenn Current
        // inzwischen von Hand geändert wurde. Gleiche Schäden bei anderer TID oder
        // anderem Datum bleiben dagegen eine eigene Begehung.
        if (!string.IsNullOrEmpty(begehung.ImportFingerprint) && ziel.Protocol is { } vorhanden
            && new[] { vorhanden.Original, vorhanden.Current }.Concat(vorhanden.History ?? [])
                .Any(r => r?.ImportFingerprint == begehung.ImportFingerprint))
            return;

        var beschriftung = string.IsNullOrWhiteSpace(begehung.Zeitpunkt)
            ? "Import aus VSA-KEK-XTF (Schachtbegehung)"
            : $"Import aus VSA-KEK-XTF (Schachtbegehung vom {begehung.Zeitpunkt})";

        if (!HatProtokollinhalt(ziel))
        {
            ziel.Protocol = new ProtocolDocument
            {
                HaltungId = begehung.Nummer.Trim(),
                Original = new ProtocolRevision
                {
                    ImportFingerprint = begehung.ImportFingerprint,
                    Comment = beschriftung,
                    Entries = begehung.Eintraege.Select(ProtocolEntryCloner.CloneLegacyProtocolEntry).ToList()
                },
                Current = new ProtocolRevision
                {
                    ImportFingerprint = begehung.ImportFingerprint,
                    Comment = "Arbeitskopie",
                    Entries = begehung.Eintraege.Select(ProtocolEntryCloner.CloneLegacyProtocolEntry).ToList()
                }
            };
            return;
        }

        var dokument = ziel.Protocol!;
        dokument.History ??= new List<ProtocolRevision>();
        dokument.History.Add(new ProtocolRevision
        {
            ImportFingerprint = begehung.ImportFingerprint,
            Comment = "Weitere " + beschriftung,
            Entries = begehung.Eintraege.Select(ProtocolEntryCloner.CloneLegacyProtocolEntry).ToList()
        });

        stats.Uncertain++;
        stats.Messages.Add(new ImportMessage
        {
            Level = "Warn",
            Context = "XTF",
            Message = $"Schacht {begehung.Nummer.Trim()}: weitere Begehung"
                      + (string.IsNullOrWhiteSpace(begehung.Zeitpunkt) ? "" : $" vom {begehung.Zeitpunkt}")
                      + $" mit {begehung.Eintraege.Count} Schaden/Schaeden als Revision abgelegt. "
                      + "Welche Begehung gilt, muss von Hand entschieden werden."
        });
    }

    /// <summary>Traegt der Schacht schon Protokollzeilen, die ein Import nicht ersetzen darf?</summary>
    private static bool HatProtokollinhalt(SchachtRecord record)
        => record.Protocol is { } dokument
           && ((dokument.Original?.Entries.Count ?? 0) > 0
               || (dokument.Current?.Entries.Count ?? 0) > 0
               || (dokument.History?.Count ?? 0) > 0);

    /// <summary>
    /// Sucht einen Schacht ueber seine Nummer. Die Schluesselfelder sind dieselben, die
    /// der WinCan- und der SchachtPro-Import verwenden — sonst legte jeder Weg seinen
    /// eigenen Datensatz fuer denselben Schacht an.
    /// </summary>
    private static SchachtRecord? FindeSchacht(Project project, string schluessel)
    {
        foreach (var record in project.SchaechteData)
        {
            foreach (var feld in SchachtSchluesselfelder)
            {
                var wert = record.GetFieldValue(feld);
                if (string.IsNullOrWhiteSpace(wert))
                    continue;

                if (string.Equals(NormalizeHoldingKey(wert), schluessel, StringComparison.OrdinalIgnoreCase))
                    return record;
            }
        }

        return null;
    }

    /// <summary>
    /// Muss deckungsgleich mit den Schluesselfeldern von WinCan und SchachtPro bleiben;
    /// <c>XtfSchachtSchluesselfelderTests</c> haelt das fest.
    ///
    /// <c>NR.</c> und <c>Nr.</c> stehen bewusst NICHT hier: Sie tragen in den echten
    /// Projekten bei 257 von 257 Schaechten eine laufende Nummer. Ein Schacht mit der
    /// Nummer "1" wuerde sonst auf den ersten Schacht der Liste treffen.
    /// </summary>
    internal static readonly string[] SchachtSchluesselfelder =
    [
        "Schachtnummer",
        "SchachtNr",
        "Schacht",
        "Schacht-Nr",
        "Schacht Nummer",
        "Schacht ID",
        "Schacht-ID"
    ];
}
