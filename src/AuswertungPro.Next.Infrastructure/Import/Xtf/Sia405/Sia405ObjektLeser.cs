using System.Xml.Linq;

namespace AuswertungPro.Next.Infrastructure.Import.Xtf.Sia405;

/// <summary>Ein <c>Kanal</c> der Datei, Werte so wie sie dort stehen.</summary>
internal sealed class Sia405KanalObjekt
{
    public string Tid { get; init; } = "";
    public string Bezeichnung { get; set; } = "";
    public string Standortname { get; set; } = "";
    public string Status { get; set; } = "";
    public string Nutzungsart { get; set; } = "";
    public string Bemerkung { get; set; } = "";
    public string Zugaenglichkeit { get; set; } = "";

    /// <summary>
    /// Die Zustandsklasse aus der Datei (Z0 bis Z4). Sie wurde frueher nicht gelesen,
    /// wodurch jeder Import den Zustand verlor: Die nachlaufende VSA-Bewertung fand in
    /// einer Stammdaten-XTF keine Befunde und setzte "Leitung i.O." (Klasse 4). Aus
    /// einem exportierten Z0 wurde so beim Zurueckimportieren eine 4.
    /// </summary>
    public string BaulicherZustand { get; set; } = "";
    public string Eigentuemer { get; set; } = "";

    /// <summary>Die Kennung der verwiesenen Organisation.</summary>
    public string EigentuemerRef { get; set; } = "";
    public string DatenherrRef { get; set; } = "";
    public string DatenlieferantRef { get; set; } = "";
    public string Baujahr { get; set; } = "";
    public string Rohrlaenge { get; set; } = "";
    public string Funktion { get; set; } = "";
    // Die uebrigen Kanalfelder, die SewerStudio selbst hinausschreibt. Bis 2026-09-03
    // wurden sie nicht gelesen; die Rundreise Export, Import, Vergleich verlor sie.
    public string FunktionHydraulisch { get; set; } = "";
    public string Sanierungsbedarf { get; set; } = "";
    public string Verbindungsart { get; set; } = "";
    public string BettungUmhuellung { get; set; } = "";
    public string Bruttokosten { get; set; } = "";
}

/// <summary>Eine <c>Haltung</c> der Datei; Verweise bleiben hier unaufgeloeste Kennungen.</summary>
internal sealed class Sia405HaltungObjekt
{
    public string Tid { get; init; } = "";
    public string Bezeichnung { get; set; } = "";
    public string Laenge { get; set; } = "";
    public string LichteHoehe { get; set; } = "";
    public string LichteBreite { get; set; } = "";
    public string Material { get; set; } = "";
    public string KanalRef { get; set; } = "";
    public string VonRef { get; set; } = "";
    public string NachRef { get; set; } = "";
    public string LetzteAenderung { get; set; } = "";
    /// <summary>Die Kennung des Rohrprofils, das Profiltyp und Hoehen-Breiten-Verhaeltnis traegt.</summary>
    public string RohrprofilRef { get; set; } = "";
    public string Lagebestimmung { get; set; } = "";
}

/// <summary>Ein <c>Haltungspunkt</c>: technischer Name und Verweis auf seinen Abwasserknoten.</summary>
internal readonly record struct Sia405Haltungspunkt(string Bezeichnung, string? AbwassernetzelementRef);

/// <summary>Ein <c>Rohrprofil</c>: Profiltyp und Hoehen-Breiten-Verhaeltnis als Rohwerte.</summary>
internal readonly record struct Sia405Rohrprofil(string Profiltyp, string Verhaeltnis);

/// <summary>Alle fuer die Haltungen gelesenen Objekte einer Datei, je nach Kennung nachschlagbar.</summary>
internal sealed class Sia405Bestand
{
    public Dictionary<string, Sia405KanalObjekt> Kanaele { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, Sia405KanalObjekt> KanaeleNachBezeichnung { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>In Dateireihenfolge; die Reihenfolge der Haltungen im Projekt folgt ihr.</summary>
    public Dictionary<string, Sia405HaltungObjekt> Haltungen { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Haltungen ohne TID. Sie werden nicht uebernommen (ohne Kennung kein Verweis, kein
    /// Abgleich), seit 01.10.2026 aber gemeldet; bis dahin fielen sie still weg.
    /// </summary>
    public List<Sia405HaltungObjekt> HaltungenOhneTid { get; } = new();
    public Dictionary<string, Sia405Haltungspunkt> Haltungspunkte { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Kennung des Abwasserknotens -> Bezeichnung.</summary>
    public Dictionary<string, string> Abwasserknoten { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, Sia405Rohrprofil> Rohrprofile { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Kennung der Organisation -> Bezeichnung.</summary>
    public Dictionary<string, string> Organisationen { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Die Kennungen ALLER Organisationen der Datei, auch ohne Bezeichnung. Ein Verweis auf
    /// eine Kennung, die hier fehlt, zeigt nach Norm erlaubt ausserhalb der Datei (EXTERNAL).
    /// </summary>
    public HashSet<string> OrganisationenInDatei { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Schritt 1 des SIA405-Haltungsimports: liest die Objekte der Datei. Nur lesen — keine
/// Verweise aufloesen, keine Werte deuten, nichts ins Projekt schreiben. Das folgt in
/// <c>Sia405Beziehungen</c> und <c>Sia405HaltungAbbildung</c>.
/// </summary>
internal static class Sia405ObjektLeser
{
    public static Sia405Bestand Lies(XDocument doc)
    {
        // Die Organisationen der Datei, damit EigentuemerRef aufgeloest werden kann.
        // Sie stehen im Topic "Administration", also ausserhalb der Fachdaten-Baskets —
        // deshalb ueber das ganze Dokument gelesen.
        var bestand = new Sia405Bestand
        {
            Organisationen = LiesOrganisationen(doc, out var alleOrganisationen),
            OrganisationenInDatei = alleOrganisationen
        };

        var baskets = doc.Descendants()
            .Where(e => e.Name.LocalName.EndsWith("SIA405_Abwasser.SIA405_Abwasser", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var scope = baskets.Count > 0 ? baskets.SelectMany(b => b.Descendants()) : doc.Descendants();

        foreach (var node in scope)
        {
            var tid = (string?)node.Attribute("TID");
            if (IstKlasse(node, "Kanal"))
            {
                if (string.IsNullOrWhiteSpace(tid)) continue;
                var kanal = LiesKanal(node, tid);
                bestand.Kanaele[tid] = kanal;
                if (!string.IsNullOrWhiteSpace(kanal.Bezeichnung))
                    bestand.KanaeleNachBezeichnung[kanal.Bezeichnung] = kanal;
            }

            if (IstKlasse(node, "Haltung"))
            {
                if (string.IsNullOrWhiteSpace(tid))
                    bestand.HaltungenOhneTid.Add(LiesHaltung(node, ""));
                else
                    bestand.Haltungen[tid] = LiesHaltung(node, tid);
            }

            // Rohrprofil: Profiltyp und Hoehen-Breiten-Verhaeltnis haengen nicht an der
            // Haltung, sondern an diesem Objekt. Ohne es kaeme die Breite eines Rechteck-
            // oder Eiprofils nie im Programm an.
            if (IstKlasse(node, "Rohrprofil"))
            {
                if (string.IsNullOrWhiteSpace(tid)) continue;
                string profiltyp = "", verhaeltnis = "";
                foreach (var child in node.Elements())
                {
                    switch (child.Name.LocalName)
                    {
                        case "Profiltyp": profiltyp = child.Value; break;
                        case "HoehenBreitenverhaeltnis": verhaeltnis = child.Value; break;
                    }
                }
                bestand.Rohrprofile[tid] = new Sia405Rohrprofil(profiltyp, verhaeltnis);
            }

            if (IstKlasse(node, "Haltungspunkt"))
            {
                if (string.IsNullOrWhiteSpace(tid)) continue;
                string bezeichnung = "";
                string? abwRef = null;
                foreach (var child in node.Elements())
                {
                    switch (child.Name.LocalName)
                    {
                        case "Bezeichnung": bezeichnung = child.Value; break;
                        case "AbwassernetzelementRef": abwRef = (string?)child.Attribute("REF"); break;
                    }
                }
                bestand.Haltungspunkte[tid] = new Sia405Haltungspunkt(bezeichnung, abwRef);
            }

            if (IstKlasse(node, "Abwasserknoten"))
            {
                if (string.IsNullOrWhiteSpace(tid)) continue;
                string bezeichnung = "";
                foreach (var child in node.Elements())
                {
                    if (child.Name.LocalName == "Bezeichnung")
                        bezeichnung = child.Value;
                }
                bestand.Abwasserknoten[tid] = bezeichnung;
            }
        }

        return bestand;
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
    public static Dictionary<string, string> LiesOrganisationen(XDocument doc)
        => LiesOrganisationen(doc, out _);

    /// <summary>Wie oben; <paramref name="alle"/> enthaelt auch die Kennungen ohne Bezeichnung.</summary>
    internal static Dictionary<string, string> LiesOrganisationen(XDocument doc, out HashSet<string> alle)
    {
        var jeTid = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        alle = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var node in doc.Descendants())
        {
            if (!IstKlasse(node, "Organisation"))
                continue;

            var tid = (string?)node.Attribute("TID");
            var bezeichnung = node.Elements()
                .FirstOrDefault(e => e.Name.LocalName.Equals("Bezeichnung", StringComparison.OrdinalIgnoreCase))
                ?.Value?.Trim();

            if (!string.IsNullOrWhiteSpace(tid))
                alle.Add(tid!);
            if (!string.IsNullOrWhiteSpace(tid) && !string.IsNullOrWhiteSpace(bezeichnung))
                jeTid[tid!] = bezeichnung!;
        }

        return jeTid;
    }

    /// <summary>Elementname ist die Klasse selbst oder endet auf <c>.Klasse</c>.</summary>
    private static bool IstKlasse(XElement node, string klasse)
    {
        var lokal = node.Name.LocalName;
        return lokal.Equals(klasse, StringComparison.OrdinalIgnoreCase)
               || lokal.EndsWith("." + klasse, StringComparison.OrdinalIgnoreCase);
    }

    private static Sia405KanalObjekt LiesKanal(XElement node, string tid)
    {
        var kd = new Sia405KanalObjekt { Tid = tid };
        foreach (var child in node.Elements())
        {
            switch (child.Name.LocalName)
            {
                case "Bezeichnung": kd.Bezeichnung = child.Value; break;
                case "Standortname": kd.Standortname = child.Value; break;
                case "Status": kd.Status = child.Value; break;
                case "Nutzungsart_Ist": kd.Nutzungsart = child.Value; break;
                case "Bemerkung": kd.Bemerkung = child.Value; break;
                case "Zugaenglichkeit": kd.Zugaenglichkeit = child.Value; break;
                case "BaulicherZustand": kd.BaulicherZustand = child.Value; break;
                case "Eigentuemer": kd.Eigentuemer = child.Value; break;
                // In SIA405 ist der Eigentuemer ein Verweis, kein Text. Die
                // Kennung wird gemerkt und spaeter gegen die Organisationen der
                // Datei aufgeloest — ohne das ging der Eigentuemer bei jedem
                // Import verloren, und beim naechsten Export fehlte genau er.
                case "EigentuemerRef":
                    kd.EigentuemerRef = (string?)child.Attribute("REF") ?? "";
                    break;
                case "DatenherrRef":
                    kd.DatenherrRef = (string?)child.Attribute("REF") ?? "";
                    break;
                case "DatenlieferantRef":
                    kd.DatenlieferantRef = (string?)child.Attribute("REF") ?? "";
                    break;
                case "Baujahr": kd.Baujahr = child.Value; break;
                case "Rohrlaenge": kd.Rohrlaenge = child.Value; break;
                // Das Modell schreibt "FunktionHierarchisch" mit grossem H. Die zwei
                // anderen Schreibweisen stammen aus aelteren Lieferungen; bis
                // 2026-09-03 fehlte ausgerechnet die des Modells.
                case "FunktionHierarchisch": kd.Funktion = child.Value; break;
                case "Funktionhierarchisch": kd.Funktion = child.Value; break;
                case "Funktion_hierarchisch": kd.Funktion = child.Value; break;
                case "FunktionHydraulisch": kd.FunktionHydraulisch = child.Value; break;
                case "Sanierungsbedarf": kd.Sanierungsbedarf = child.Value; break;
                case "Verbindungsart": kd.Verbindungsart = child.Value; break;
                case "Bettung_Umhuellung": kd.BettungUmhuellung = child.Value; break;
                case "Bruttokosten": kd.Bruttokosten = child.Value; break;
            }
        }
        return kd;
    }

    private static Sia405HaltungObjekt LiesHaltung(XElement node, string tid)
    {
        var hd = new Sia405HaltungObjekt { Tid = tid };
        foreach (var child in node.Elements())
        {
            switch (child.Name.LocalName)
            {
                case "Bezeichnung": hd.Bezeichnung = child.Value; break;
                case "LaengeEffektiv": hd.Laenge = child.Value; break;
                case "Lichte_Hoehe": hd.LichteHoehe = child.Value; break;
                case "Lichte_Breite": hd.LichteBreite = child.Value; break;
                case "Material": hd.Material = child.Value; break;
                case "Letzte_Aenderung": hd.LetzteAenderung = child.Value; break;
                case "AbwasserbauwerkRef": hd.KanalRef = (string?)child.Attribute("REF") ?? ""; break;
                case "vonHaltungspunktRef": hd.VonRef = (string?)child.Attribute("REF") ?? ""; break;
                case "nachHaltungspunktRef": hd.NachRef = (string?)child.Attribute("REF") ?? ""; break;
                case "RohrprofilRef": hd.RohrprofilRef = (string?)child.Attribute("REF") ?? ""; break;
                case "Lagebestimmung": hd.Lagebestimmung = child.Value; break;
            }
        }
        return hd;
    }
}
