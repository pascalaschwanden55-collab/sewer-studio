using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using AuswertungPro.Next.Application.UseCases.Import.Kataster;

namespace AuswertungPro.Next.Infrastructure.Import.Kataster;

/// <summary>
/// Liest amtliche Haltungsbezeichnungen aus einer SIA405-Katasterdatei (INTERLIS 2, XTF).
///
/// Die Datei kann sehr gross sein — der Abwasserkataster Uri umfasst 603 MB mit 94'109
/// Haltungen. Deshalb wird stroemend mit <see cref="XmlReader"/> gelesen und nur das
/// Noetigste behalten: Schachtbezeichnungen, die Zuordnung Haltungspunkt -> Schacht und
/// je Haltung ihre Bezeichnung samt beiden Endpunkten.
///
/// Aufbau in INTERLIS:
///   Abwasserknoten  TID + Bezeichnung          (der Schacht)
///   Haltungspunkt   TID + Verweis auf Knoten   (der Anschlusspunkt)
///   Haltung         Bezeichnung + vonHaltungspunktRef / nachHaltungspunktRef
///
/// Rein lesend, ohne KI und ohne Netzzugriff.
/// </summary>
public static class SiaKatasterXtfReader
{
    /// <summary>
    /// Liest die Datei. Ein Lesefehler ergibt ein leeres Verzeichnis statt einer Ausnahme:
    /// Der Katasterabgleich ist eine Zusatzpruefung und darf einen Import nie stoppen.
    /// </summary>
    public static IKatasterHaltungsverzeichnis Lies(string? xtfPfad)
    {
        if (string.IsNullOrWhiteSpace(xtfPfad) || !File.Exists(xtfPfad))
            return KatasterHaltungsverzeichnis.Leer;

        try
        {
            return LiesStreng(xtfPfad);
        }
        catch (Exception)
        {
            return KatasterHaltungsverzeichnis.Leer;
        }
    }

    internal static IKatasterHaltungsverzeichnis LiesStreng(string xtfPfad)
    {
        var knotenBezeichnung = new Dictionary<string, string>(StringComparer.Ordinal);
        var punktZuKnoten = new Dictionary<string, string>(StringComparer.Ordinal);
        var rohHaltungen = new List<(string Bezeichnung, string VonPunkt, string NachPunkt)>();

        var einstellungen = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            IgnoreComments = true,
            IgnoreWhitespace = true
        };

        using var leser = XmlReader.Create(xtfPfad, einstellungen);
        while (leser.Read())
        {
            if (leser.NodeType != XmlNodeType.Element)
                continue;

            var art = KurzName(leser.LocalName);
            switch (art)
            {
                case "Abwasserknoten":
                {
                    var tid = leser.GetAttribute("TID");
                    var felder = LiesObjekt(leser);
                    if (!string.IsNullOrWhiteSpace(tid)
                        && felder.TryGetValue("Bezeichnung", out var bez)
                        && !string.IsNullOrWhiteSpace(bez))
                    {
                        knotenBezeichnung[tid!] = bez.Trim();
                    }

                    break;
                }

                case "Haltungspunkt":
                {
                    var tid = leser.GetAttribute("TID");
                    var felder = LiesObjekt(leser);
                    // Der Verweis auf den Knoten heisst je nach Modellstand unterschiedlich.
                    if (!string.IsNullOrWhiteSpace(tid)
                        && (felder.TryGetValue("abwassernetzelementRef", out var r)
                            || felder.TryGetValue("AbwassernetzelementRef", out r)
                            || felder.TryGetValue("AbwasserknotenRef", out r))
                        && !string.IsNullOrWhiteSpace(r))
                    {
                        punktZuKnoten[tid!] = r;
                    }

                    break;
                }

                case "Haltung":
                {
                    var felder = LiesObjekt(leser);
                    felder.TryGetValue("Bezeichnung", out var bez);
                    felder.TryGetValue("vonHaltungspunktRef", out var von);
                    felder.TryGetValue("nachHaltungspunktRef", out var nach);
                    if (!string.IsNullOrWhiteSpace(bez)
                        && !string.IsNullOrWhiteSpace(von)
                        && !string.IsNullOrWhiteSpace(nach))
                    {
                        rohHaltungen.Add((bez.Trim(), von, nach));
                    }

                    break;
                }
            }
        }

        var haltungen = new List<KatasterHaltung>(rohHaltungen.Count);
        foreach (var (bez, von, nach) in rohHaltungen)
        {
            if (!punktZuKnoten.TryGetValue(von, out var vonKnoten)
                || !punktZuKnoten.TryGetValue(nach, out var nachKnoten))
            {
                continue;
            }

            if (knotenBezeichnung.TryGetValue(vonKnoten, out var oben)
                && knotenBezeichnung.TryGetValue(nachKnoten, out var unten))
            {
                haltungen.Add(new KatasterHaltung(oben, unten, bez));
            }
        }

        return new KatasterHaltungsverzeichnis(haltungen);
    }

    /// <summary>
    /// Liest die direkten Kindelemente des aktuellen Objekts als Feld -> Wert.
    /// Werte sind entweder der Textinhalt oder bei Verweisen das REF-Attribut.
    ///
    /// Bewusst ueber den Teilbaum: Ein Vorwaerts-Lesen mit ReadElementContentAsString
    /// steht danach bereits auf dem naechsten Element und ueberspringt damit jedes
    /// zweite Feld. Ein einzelnes Objekt ist klein, das Streaming bleibt erhalten.
    /// </summary>
    private static Dictionary<string, string> LiesObjekt(XmlReader leser)
    {
        var felder = new Dictionary<string, string>(StringComparer.Ordinal);
        if (leser.IsEmptyElement)
            return felder;

        using var teilbaum = leser.ReadSubtree();
        var objekt = System.Xml.Linq.XElement.Load(teilbaum);

        foreach (var kind in objekt.Elements())
        {
            var verweis = kind.Attribute("REF")?.Value;
            felder[kind.Name.LocalName] = !string.IsNullOrWhiteSpace(verweis)
                ? verweis!
                : kind.Value;
        }

        return felder;
    }

    /// <summary>"SIA405_ABWASSER_2020_LV95.SIA405_Abwasser.Haltung" -> "Haltung".</summary>
    private static string KurzName(string localName)
    {
        var punkt = localName.LastIndexOf('.');
        return punkt >= 0 && punkt < localName.Length - 1 ? localName[(punkt + 1)..] : localName;
    }
}
