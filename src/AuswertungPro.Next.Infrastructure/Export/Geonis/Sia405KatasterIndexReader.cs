using System.Xml;
using System.Xml.Linq;
using AuswertungPro.Next.Application.Export.Geonis;
using AuswertungPro.Next.Infrastructure.Import.Xtf;

namespace AuswertungPro.Next.Infrastructure.Export.Geonis;

/// <summary>
/// Erster Lesedurchgang ueber die Kataster-XTF: sammelt Identitaet (TID/OBJ_ID), die heutigen
/// Werte und die Modellangaben. Streamend, weil die Katasterdatei mehrere hundert Megabyte gross ist.
/// </summary>
public sealed class Sia405KatasterIndexReader : ISia405KatasterIndexReader
{
    private static readonly string[] Klassen = { "Haltung", "Kanal", "Normschacht", "Rohrprofil" };

    public Sia405KatasterIndex Lies(string katasterXtfPfad)
    {
        if (string.IsNullOrWhiteSpace(katasterXtfPfad))
            throw new ArgumentException("Pfad zur Kataster-XTF fehlt.", nameof(katasterXtfPfad));
        if (!File.Exists(katasterXtfPfad))
            throw new FileNotFoundException($"Kataster-XTF nicht gefunden: {katasterXtfPfad}", katasterXtfPfad);

        var haltungen = new Dictionary<string, Sia405KatasterHaltung>(StringComparer.Ordinal);
        var mehrdeutigeHaltungen = new HashSet<string>(StringComparer.Ordinal);
        var schaechte = new Dictionary<string, Sia405KatasterSchacht>(StringComparer.Ordinal);
        var mehrdeutigeSchaechte = new HashSet<string>(StringComparer.Ordinal);
        var kanaele = new Dictionary<string, Sia405KatasterKanal>(StringComparer.Ordinal);
        var rohrprofile = new Dictionary<string, Sia405KatasterRohrprofil>(StringComparer.Ordinal);
        var materialVokabular = new Dictionary<string, string>(StringComparer.Ordinal);
        var zustandVokabular = new HashSet<string>(StringComparer.Ordinal);
        var reihenfolge = new Sia405AttributReihenfolge();
        var modelle = new List<Sia405ModellReferenz>();

        string? transferNamespace = null;
        string? headerVersion = null;
        string? topicPrefix = null;
        string? letzteAenderungBeispiel = null;
        var behaelter = new Dictionary<string, string>(StringComparer.Ordinal);

        var settings = new XmlReaderSettings
        {
            IgnoreWhitespace = true,
            IgnoreComments = true,
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null
        };

        using var reader = XmlReader.Create(katasterXtfPfad, settings);

        // ReadFrom bewegt den Reader bereits auf den naechsten Knoten — skipRead verhindert,
        // dass danach ein weiteres Read() ein Objekt ueberspringt.
        var skipRead = false;
        while (skipRead || reader.Read())
        {
            skipRead = false;

            if (reader.NodeType != XmlNodeType.Element)
                continue;

            var local = reader.LocalName;

            if (string.Equals(local, "TRANSFER", StringComparison.Ordinal))
            {
                transferNamespace ??= reader.NamespaceURI;
                continue;
            }

            if (string.Equals(local, "HEADERSECTION", StringComparison.Ordinal))
            {
                headerVersion ??= reader.GetAttribute("VERSION");
                continue;
            }

            if (string.Equals(local, "MODEL", StringComparison.Ordinal))
            {
                modelle.Add(new Sia405ModellReferenz(
                    reader.GetAttribute("NAME") ?? string.Empty,
                    reader.GetAttribute("VERSION") ?? string.Empty,
                    reader.GetAttribute("URI") ?? string.Empty));
                continue;
            }

            // Nur der Behaelter (BASKET) traegt ein BID. Eine Datei kann mehrere Behaelter
            // enthalten; welcher gemeint ist, entscheidet spaeter der Praefix der Fachobjekte.
            var bid = reader.GetAttribute("BID");
            if (bid is not null)
            {
                behaelter.TryAdd(local, bid);
                continue;
            }

            var klasse = KlasseVonElement(local);
            if (klasse is null)
                continue;

            // Der Modellpraefix kommt aus den Fachobjekten selbst, nicht aus dem ersten Behaelter.
            topicPrefix ??= PraefixAusElement(local, klasse);

            // ReadFrom hat den Reader bereits weiterbewegt — das gilt auch, wenn kein Element
            // zurueckkommt. Darum zuerst merken, dann pruefen.
            var gelesen = XNode.ReadFrom(reader);
            skipRead = true;
            if (gelesen is not XElement objekt)
                continue;

            var tid = objekt.Attribute("TID")?.Value;
            if (string.IsNullOrWhiteSpace(tid))
                continue;

            reihenfolge.Beobachte(klasse, objekt.Elements().Select(e => e.Name.LocalName).ToList());
            letzteAenderungBeispiel ??= Sauber(Kind(objekt, "Letzte_Aenderung"));

            switch (klasse)
            {
                case "Haltung":
                    NimmHaltungAuf(objekt, tid!, haltungen, mehrdeutigeHaltungen, materialVokabular);
                    break;
                case "Kanal":
                    kanaele[tid!] = new Sia405KatasterKanal(
                        tid!,
                        Sauber(Kind(objekt, "OBJ_ID")),
                        Sauber(Kind(objekt, "Bezeichnung")),
                        Sauber(Kind(objekt, "Baulicher_Zustand")),
                        Sauber(Kind(objekt, "Bemerkung")));
                    MerkeZustand(objekt, zustandVokabular);
                    break;
                case "Normschacht":
                    NimmSchachtAuf(objekt, tid!, schaechte, mehrdeutigeSchaechte);
                    MerkeZustand(objekt, zustandVokabular);
                    break;
                case "Rohrprofil":
                    rohrprofile[tid!] = new Sia405KatasterRohrprofil(
                        tid!,
                        Sauber(Kind(objekt, "OBJ_ID")),
                        Sauber(Kind(objekt, "Bezeichnung")),
                        Sauber(Kind(objekt, "Profiltyp")),
                        Sauber(Kind(objekt, "HoehenBreitenverhaeltnis")));
                    break;
            }
        }

        if (topicPrefix is null && behaelter.Count > 0)
            topicPrefix = behaelter.Keys.First();

        string? basketId = null;
        if (topicPrefix is not null && behaelter.TryGetValue(topicPrefix, out var gefundenerBasket))
            basketId = gefundenerBasket;

        return new Sia405KatasterIndex
        {
            Modell = new Sia405ModellAngaben(
                string.IsNullOrWhiteSpace(transferNamespace) ? "http://www.interlis.ch/INTERLIS2.3" : transferNamespace!,
                string.IsNullOrWhiteSpace(headerVersion) ? "2.3" : headerVersion!,
                topicPrefix ?? string.Empty,
                basketId,
                modelle),
            Haltungen = haltungen,
            MehrdeutigeHaltungen = mehrdeutigeHaltungen,
            Schaechte = schaechte,
            MehrdeutigeSchaechte = mehrdeutigeSchaechte,
            KanaeleNachTid = kanaele,
            RohrprofileNachTid = rohrprofile,
            MaterialVokabular = materialVokabular,
            ZustandVokabular = zustandVokabular,
            LetzteAenderungBeispiel = letzteAenderungBeispiel,
            AttributReihenfolge = reihenfolge
        };
    }

    private static void NimmHaltungAuf(
        XElement objekt,
        string tid,
        Dictionary<string, Sia405KatasterHaltung> haltungen,
        HashSet<string> mehrdeutige,
        Dictionary<string, string> materialVokabular)
    {
        var bezeichnung = Sauber(Kind(objekt, "Bezeichnung"));
        var material = Sauber(Kind(objekt, "Material"));
        MerkeMaterial(material, materialVokabular);

        if (bezeichnung is null)
            return;

        var key = Sia405NameKey.Normalize(bezeichnung);
        if (key.Length == 0)
            return;

        if (mehrdeutige.Contains(key))
            return;

        var eintrag = new Sia405KatasterHaltung(
            bezeichnung,
            tid,
            Sauber(Kind(objekt, "OBJ_ID")),
            VerweisAuf(objekt, "abwasserbauwerk"),
            VerweisAuf(objekt, "rohrprofil"),
            Sauber(Kind(objekt, "Lichte_Hoehe")),
            Sauber(Kind(objekt, "Lichte_Breite")),
            material);

        if (haltungen.ContainsKey(key))
        {
            // Doppelte Bezeichnung: beide Seiten verwerfen. Ein "erster Treffer gewinnt" waere
            // genau der Fehler, der im Kataster die falsche Haltung ueberschreibt.
            haltungen.Remove(key);
            mehrdeutige.Add(key);
            return;
        }

        haltungen[key] = eintrag;
    }

    private static void NimmSchachtAuf(
        XElement objekt,
        string tid,
        Dictionary<string, Sia405KatasterSchacht> schaechte,
        HashSet<string> mehrdeutige)
    {
        var bezeichnung = Sauber(Kind(objekt, "Bezeichnung"));
        if (bezeichnung is null)
            return;

        var key = Sia405NameKey.Normalize(bezeichnung);
        if (key.Length == 0 || mehrdeutige.Contains(key))
            return;

        var eintrag = new Sia405KatasterSchacht(
            bezeichnung,
            tid,
            Sauber(Kind(objekt, "OBJ_ID")),
            Sauber(Kind(objekt, "Dimension1")),
            Sauber(Kind(objekt, "Dimension2")),
            Sauber(Kind(objekt, "Baulicher_Zustand")),
            Sauber(Kind(objekt, "Bemerkung")));

        if (schaechte.ContainsKey(key))
        {
            schaechte.Remove(key);
            mehrdeutige.Add(key);
            return;
        }

        schaechte[key] = eintrag;
    }

    private static void MerkeMaterial(string? material, Dictionary<string, string> vokabular)
    {
        if (string.IsNullOrWhiteSpace(material))
            return;

        // Schluessel ist der Programmwert (so, wie ihn der Import erzeugt), Wert die
        // Originalschreibweise der Katasterdatei.
        var programmwert = XtfValueNormalizer.NormalizeSiaMaterial(material);
        if (string.IsNullOrWhiteSpace(programmwert))
            return;

        vokabular.TryAdd(programmwert.ToUpperInvariant(), material.Trim());
    }

    private static void MerkeZustand(XElement objekt, HashSet<string> vokabular)
    {
        var zustand = Sauber(Kind(objekt, "Baulicher_Zustand"));
        if (zustand is not null)
            vokabular.Add(zustand);
    }

    private static string? KlasseVonElement(string localName)
    {
        foreach (var klasse in Klassen)
        {
            if (string.Equals(localName, klasse, StringComparison.Ordinal)
                || localName.EndsWith("." + klasse, StringComparison.Ordinal))
            {
                return klasse;
            }
        }

        return null;
    }

    private static string? PraefixAusElement(string localName, string klasse)
    {
        var suffix = "." + klasse;
        return localName.EndsWith(suffix, StringComparison.Ordinal)
            ? localName[..^suffix.Length]
            : null;
    }

    private static string? Kind(XElement objekt, string name)
        => objekt.Elements()
            .FirstOrDefault(e => string.Equals(e.Name.LocalName, name, StringComparison.Ordinal))
            ?.Value;

    /// <summary>
    /// Sucht eine Rollenreferenz tolerant (z. B. "rohrprofilRef", "RohrprofilRef"): die genaue
    /// Schreibweise unterscheidet sich zwischen Modellversionen.
    /// </summary>
    private static string? VerweisAuf(XElement objekt, string rollenname)
        => objekt.Elements()
            .FirstOrDefault(e => e.Name.LocalName.EndsWith("Ref", StringComparison.OrdinalIgnoreCase)
                                 && e.Name.LocalName.Contains(rollenname, StringComparison.OrdinalIgnoreCase))
            ?.Attribute("REF")?.Value;

    private static string? Sauber(string? wert)
        => string.IsNullOrWhiteSpace(wert) ? null : wert.Trim();
}
