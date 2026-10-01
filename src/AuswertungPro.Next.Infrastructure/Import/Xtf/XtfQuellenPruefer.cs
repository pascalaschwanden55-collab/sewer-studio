using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Application.UseCases.Import.Quellen;

namespace AuswertungPro.Next.Infrastructure.Import.Xtf;

/// <summary>
/// Liest eine XTF-Datei stroemend und zaehlt, was drinsteht.
///
/// Warum stroemend und nicht ueber einen Textausschnitt: Die bisherige Erkennung las die
/// ersten 64 KiB als Text und suchte darin eine Zeichenfolge. Das ist an zwei Stellen
/// falsch — ein langer Kopf schiebt den Modellnamen aus dem Fenster, und der Kopf sagt
/// ohnehin nichts darueber, ob die Datei Untersuchungen enthaelt. Ein
/// <see cref="XmlReader"/> laeuft ohne Groessengrenze durch und baut trotzdem kein
/// vollstaendiges Dokument im Speicher auf.
///
/// DTD und externe Entitaeten sind gesperrt (dieselbe Regel wie
/// <c>SafeXmlDocumentLoader</c>): Es sind Kundendateien aus fremder Hand.
/// </summary>
public sealed class XtfQuellenPruefer : IXtfQuellenPruefer
{
    private static readonly XmlReaderSettings Settings = new()
    {
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null,
        IgnoreComments = true,
        IgnoreWhitespace = true,
        IgnoreProcessingInstructions = true
    };

    /// <summary>
    /// Bauwerksklassen, die eine Datei zur Katasterquelle machen. Bewusst ohne
    /// Modellpraefix: Alt (<c>SIA405_Abwasser.Abwasser.Kanal</c>) und neu
    /// (<c>SIA405_Abwasser_2020_LV95.Abwasser.Kanal</c>) sollen gleich behandelt werden.
    /// </summary>
    private static readonly string[] Stammdatenklassen =
    [
        ".Kanal", ".Haltung", ".Normschacht", ".Abwasserknoten", ".Rohrprofil", ".Haltungspunkt"
    ];

    public XtfQuellenmerkmale Pruefe(string pfad)
    {
        if (string.IsNullOrWhiteSpace(pfad))
            return XtfQuellenmerkmale.NichtLesbar("kein Pfad angegeben");

        var modelle = new List<string>();
        var untersuchungen = 0;
        var kanalschaeden = 0;
        var normschachtschaeden = 0;
        var stammdaten = 0;
        var dateiverweise = 0;
        // Mehrfachmenge: derselbe Schacht kann zweimal begangen worden sein.
        var untersuchungsKennungen = new List<string>();
        var inhalte = new Dictionary<string, int>(StringComparer.Ordinal);
        var basket = "";

        try
        {
            using var stream = File.OpenRead(pfad);
            using var reader = XmlReader.Create(stream, Settings);

            while (reader.Read())
            {
                if (reader.NodeType != XmlNodeType.Element)
                    continue;

                var name = reader.LocalName;

                if (string.Equals(name, "MODEL", StringComparison.OrdinalIgnoreCase))
                {
                    var modellname = reader.GetAttribute("NAME");
                    if (!string.IsNullOrWhiteSpace(modellname))
                        modelle.Add(modellname);
                    using var modelReader = reader.ReadSubtree();
                    AddBeleg(inhalte, XElement.Load(modelReader).ToString(SaveOptions.DisableFormatting));
                    continue;
                }

                if (reader.Depth == 2 && reader.GetAttribute("BID") is not null)
                {
                    basket = reader.Name + "|" + reader.GetAttribute("BID");
                    continue;
                }

                if (reader.Depth != 3 || !name.Contains('.', StringComparison.Ordinal))
                    continue;

                // Ein Objekt auf einmal: auch unbekannte Klassen und alle Felder
                // zählen zum Beweis. TIDs/REFs bleiben absichtlich erhalten. Eine
                // Umnummerierung ist ohne vollständigen Zuordnungsbeweis KEINE Kopie.
                using var subtree = reader.ReadSubtree();
                var objekt = XElement.Load(subtree);
                AddBeleg(inhalte, basket + "|" + objekt.ToString(SaveOptions.DisableFormatting));

                // Der Klassenname steht hinter dem letzten Punkt; das Modellpraefix davor
                // aendert sich mit jeder Modellversion und darf hier nichts entscheiden.
                if (name.EndsWith(".Untersuchung", StringComparison.OrdinalIgnoreCase))
                {
                    untersuchungen++;
                    untersuchungsKennungen.Add(LiesUntersuchungsKennung(objekt));
                }
                else if (name.EndsWith(".Kanalschaden", StringComparison.OrdinalIgnoreCase))
                    kanalschaeden++;
                else if (name.EndsWith(".Normschachtschaden", StringComparison.OrdinalIgnoreCase))
                    normschachtschaeden++;
                else if (name.EndsWith(".Datei", StringComparison.OrdinalIgnoreCase))
                    dateiverweise++;
                else if (IstStammdatenklasse(name))
                    stammdaten++;
            }
        }
        catch (Exception ex) when (ex is XmlException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return XtfQuellenmerkmale.NichtLesbar(ex.Message);
        }

        return new XtfQuellenmerkmale(modelle, untersuchungen, kanalschaeden, normschachtschaeden, stammdaten)
        {
            Dateiverweise = dateiverweise,
            Untersuchungsfingerabdruck = Fingerabdruck(untersuchungsKennungen),
            Inhaltsbelege = inhalte
        };
    }

    /// <summary>
    /// Fachliche Kennung einer Untersuchung: Bezeichnung und Zeitpunkt.
    ///
    /// Ausdruecklich NICHT die TID. Gemessen am 2026-09-05 an den drei Exporten von
    /// Andermatt Zone 2.11: WinCan vergibt bei jedem Export neue TIDs, obwohl dieselben
    /// 48 Untersuchungen drinstehen. Eine TID identifiziert also die Zeile in DIESER
    /// Datei, nicht die Untersuchung in der Wirklichkeit.
    ///
    /// Bezeichnung plus Zeitpunkt bleibt dagegen stabil und trennt zugleich eine
    /// Wiederholungsbefahrung derselben Haltung an einem anderen Tag.
    /// </summary>
    private static string LiesUntersuchungsKennung(XElement untersuchung)
    {
        string Feld(string name) => untersuchung.Elements()
            .FirstOrDefault(e => e.Name.LocalName.Equals(name, StringComparison.OrdinalIgnoreCase))?.Value.Trim() ?? "";
        return Feld("Bezeichnung") + "|" + Feld("Zeitpunkt");
    }

    private static void AddBeleg(Dictionary<string, int> belege, string inhalt)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(inhalt)));
        belege[hash] = belege.GetValueOrDefault(hash) + 1;
    }

    /// <summary>
    /// SHA-256 ueber die sortierten fachlichen Untersuchungskennungen. Zwei Exporte
    /// derselben Zone ergeben denselben Wert, unabhaengig von Dateigroesse,
    /// Elementreihenfolge und neu vergebenen TIDs.
    /// </summary>
    private static string Fingerabdruck(List<string> kennungen)
    {
        if (kennungen.Count == 0)
            return "";

        kennungen.Sort(StringComparer.Ordinal);
        var text = string.Join("\n", kennungen);
        var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(hash);
    }

    private static bool IstStammdatenklasse(string elementname)
    {
        foreach (var klasse in Stammdatenklassen)
        {
            if (elementname.EndsWith(klasse, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
