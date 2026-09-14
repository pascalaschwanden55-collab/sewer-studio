using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace AuswertungPro.Next.Application.UseCases.Xtf;

/// <summary>Die Zahlen genau eines Paketlaufs, aus denen die Liesmich-Datei entsteht.</summary>
public sealed record XtfPaketKennzahlen(
    string Projekt,
    string AenderungsDatei,
    string VollstaendigeDatei,
    int Feldauftraege,
    string AenderungsBericht,
    string VollstaendigerBericht);

/// <summary>
/// Die Liesmich-Datei des Katasterpakets. Sie ist fuer den Empfaenger geschrieben, der die
/// Uebernahme baut — nicht fuer die eigene Oberflaeche. Reine Textregel: Sie nennt nur, was
/// in den Berichten wirklich steht, und erfindet keine Zahl.
/// </summary>
public static class KatasterPaketLiesmich
{
    private static readonly Regex Umfang = new(
        @"In die Datei:\s*(?<h>\d+)\s*Haltungen,\s*(?<s>\d+)\s*Schaechte\s*\((?<o>\d+)\s*Objekte",
        RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    public static string Baue(XtfPaketKennzahlen k, DateTime zeitpunkt)
    {
        ArgumentNullException.ThrowIfNull(k);
        var t = new StringBuilder();
        t.AppendLine("SewerStudio — Lieferung für den Kataster");
        t.AppendLine("=========================================");
        t.AppendLine(CultureInfo.InvariantCulture, $"Projekt:  {k.Projekt}");
        t.AppendLine(CultureInfo.InvariantCulture, $"Erstellt: {zeitpunkt.ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture)}");
        t.AppendLine();
        t.AppendLine("Quelle ist der Projektstand in SewerStudio. Alle Objekte behalten ihre");
        t.AppendLine("Originalkennungen (TID) aus der GeoShop-Lieferung.");
        t.AppendLine();

        t.AppendLine("1 Aenderungen");
        t.AppendLine("-------------");
        t.AppendLine(CultureInfo.InvariantCulture, $"Datei: {k.AenderungsDatei}");
        if (Zahlen(k.AenderungsBericht) is { } a) t.AppendLine(a);
        if (k.Feldauftraege > 0)
            t.AppendLine(CultureInfo.InvariantCulture, $"Enthält {k.Feldauftraege} Feldaufträge im Modell SewerStudio_Zusatz_2026.");
        t.AppendLine();
        t.AppendLine("NUR die Einträge SewerStudio_Zusatz_2026.Zusatzdaten.Aenderung sind Schreib-");
        t.AppendLine("aufträge. Jeder nennt ObjektTid und Feld. Alle übrigen Objekte der Datei sind");
        t.AppendLine("Bezugskontext und dürfen nicht übernommen werden: Sie tragen den Stand der");
        t.AppendLine("GeoShop-Quelle und würden spätere Änderungen im Kataster überschreiben.");
        t.AppendLine("Fehlt bei einem Auftrag der Wert, soll das Feld geleert werden.");
        t.AppendLine();
        t.AppendLine("Diese Datei lässt sich nur lesen, wenn SewerStudio_Zusatz_2026.ili im Modell-");
        t.AppendLine("verzeichnis liegt. Sie ist beigelegt. Ohne sie bricht der INTERLIS-Leser mit");
        t.AppendLine("\"model(s) not found\" ab — auch für die normalen Objekte in derselben Datei.");
        t.AppendLine();

        t.AppendLine("2 Vollstaendig");
        t.AppendLine("--------------");
        t.AppendLine(CultureInfo.InvariantCulture, $"Datei: {k.VollstaendigeDatei}");
        if (Zahlen(k.VollstaendigerBericht) is { } v) t.AppendLine(v);
        t.AppendLine();
        t.AppendLine("Reines DSS_2020_1_LV95 und SIA405_Base_Abwasser_1_LV95, ohne Zusatzmodell.");
        t.AppendLine("Diese Datei lässt sich ohne Vorbereitung lesen. Die Änderungen sind hier");
        t.AppendLine("bereits in den Normfeldern eingearbeitet. Sie hat aber keine Auftragsliste");
        t.AppendLine("und trennt deshalb nicht zwischen Bezugskontext und Änderung.");
        t.AppendLine();

        t.AppendLine("Zu beachten");
        t.AppendLine("-----------");
        // Beide Fassungen enthalten dieselben Deckel — zaehlen, nicht addieren.
        var deckel = Math.Max(Deckel(k.AenderungsBericht), Deckel(k.VollstaendigerBericht));
        if (deckel > 0)
        {
            t.AppendLine(CultureInfo.InvariantCulture,
                $"* {deckel} Deckel haben in der Quelle keine Bezeichnung. INTERLIS verlangt sie,");
            t.AppendLine("  deshalb steht dort die Originalkennung als technische Bezeichnung.");
            t.AppendLine("  Diese Werte gehören nicht in den Kataster.");
        }

        t.AppendLine("* Erfasste Sanierungen sind neue Erhaltungsereignisse und im Kataster noch");
        t.AppendLine("  nicht vorhanden.");
        t.AppendLine("* Datenherr und Datenlieferant sind externe Verweise. Die GeoShop-Quelle");
        t.AppendLine("  enthält keine Organisationsobjekte, deshalb gehen sie nicht mit. Eine");
        t.AppendLine("  Prüfung mit --allObjectsAccessible meldet sie als fehlend.");
        t.AppendLine();
        t.AppendLine("Die Dateien sind gegen den im Programm hinterlegten Modellvertrag geprüft.");
        t.AppendLine("Eine unabhängige INTERLIS-Prüfung und ein Rückimport gehören nicht dazu.");
        t.AppendLine("Der vollständige Bericht je Fassung liegt als Bericht.txt im jeweiligen Ordner.");
        return t.ToString();
    }

    private static string? Zahlen(string bericht)
    {
        var treffer = Umfang.Match(bericht ?? "");
        return treffer.Success
            ? $"Umfang: {treffer.Groups["h"].Value} Haltungen, {treffer.Groups["s"].Value} Schächte, {treffer.Groups["o"].Value} Objekte."
            : null;
    }

    private static int Deckel(string bericht) => (bericht ?? "").Split('\n')
        .Count(z => z.Contains("GeoShop liefert keine Pflichtbezeichnung", StringComparison.Ordinal));
}
