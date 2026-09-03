using System.Xml.Linq;
using AuswertungPro.Next.Application.Xtf;
using AuswertungPro.Next.Domain.Models;
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

            var bezeichnung = (Kind("Bezeichnung") ?? "").Trim();
            if (bezeichnung.Length == 0)
                continue;

            elemente.Add(new XtfNormschachtElement(
                bezeichnung,
                Kind("Funktion"),
                Kind("Material"),
                Kind("Dimension1"),
                Kind("Dimension2"),
                Kind("Eigentuemer") ?? Verweis(node, "EigentuemerRef", organisationen),
                Kind("BaulicherZustand")));
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
