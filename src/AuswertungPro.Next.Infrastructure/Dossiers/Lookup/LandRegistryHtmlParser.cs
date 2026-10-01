using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;

using AuswertungPro.Next.Application.Dossiers.Lookup;

namespace AuswertungPro.Next.Infrastructure.Dossiers.Lookup;

/// <summary>
/// Liest den oeffentlichen Grundbuchauszug des Kantons Uri.
///
/// Die Quelle ist eine Webseite, keine Schnittstelle. Der Aufbau kann sich
/// jederzeit aendern. Deshalb gilt durchgehend: was nicht sicher erkannt wird,
/// ergibt null oder bleibt leer — nie ein geratener Wert. Ein falscher Name in
/// einem Brief an den Eigentuemer waere schlimmer als eine leere Stelle.
///
/// Aufbau der Seite, an dem sich der Parser orientiert:
///   Grundbuch &lt;Gemeinde&gt;
///   Liegenschaft Nr. &lt;Nummer&gt;
///   ... Gebaeude, &lt;Strasse&gt; &lt;Haus-Nr.&gt; (&lt;Flaeche&gt;)
///   Eigentuemer
///   [Lit.A:]  &lt;Name&gt;  &lt;Adresse&gt;  [&lt;Anteil&gt;]
///   Anmerkungen...
/// </summary>
public static class LandRegistryHtmlParser
{
    private static readonly Regex GebaeudeZeile = new(
        @"Gebäude,\s*(?<strasse>[^,(]+?)\s+(?<nr>\d+[a-zA-Z]?)\s*\(",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex PlzOrt = new(
        @"\b(?<plz>\d{4})\s+(?<ort>[^,]+)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Kennzeichnungszeile. Der Buchstabe fehlt beim Stockwerkeigentum ("Lit.:"),
    /// und dort steht der ganze Eintrag hinter dem Doppelpunkt auf derselben Zeile.
    /// </summary>
    private static readonly Regex LitZeile = new(
        @"^Lit\.\s*(?<buchstabe>[A-Z])?\s*:\s*(?<inhalt>.*)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>Personenname in der Klammer: "... von StWE S1021 (Kurt Beispiel), 31/100 ...".</summary>
    private static readonly Regex NameInKlammern = new(
        @"\((?<name>[^()]+)\)", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex StockwerkNummer = new(
        @"StWE\s*(?<nr>S?\d+[A-Za-z]?)", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>Anteil am Zeilenende: ", 31/100 Miteigentum".</summary>
    private static readonly Regex AnteilAmEnde = new(
        @",\s*(?<anteil>\d+/\d+\s+\S.*)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex AnteilZeile = new(
        @"^\d+/\d+\s", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static LandRegistryEntry? Parse(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return null;

        var zeilen = ZeilenAusHtml(html);

        var eigentuemerIndex = zeilen.FindIndex(
            z => z.StartsWith("Eigentümer", StringComparison.OrdinalIgnoreCase));
        if (eigentuemerIndex < 0)
            return null;

        var ende = zeilen.FindIndex(
            eigentuemerIndex + 1,
            z => z.StartsWith("Anmerkungen", StringComparison.OrdinalIgnoreCase));

        // Ohne diesen Abschluss ist der Aufbau der Seite nicht wiedererkannt.
        // Dann lieber gar nichts liefern, als den restlichen Seitentext als
        // Eigentuemer zu lesen.
        if (ende < 0)
            return null;

        var block = zeilen.GetRange(eigentuemerIndex + 1, ende - eigentuemerIndex - 1);

        // "Keine" steht als erste Angabe des Blocks. An der Blocklaenge darf das
        // nicht haengen: eine zusaetzliche Hinweiszeile wuerde den Schutz sonst
        // aushebeln und "Keine" zu einem Eigentuemernamen machen.
        var ohneEigentuemer = block.Count > 0
            && string.Equals(block[0], "Keine", StringComparison.OrdinalIgnoreCase);

        var eigentuemer = ohneEigentuemer
            ? new List<LandRegistryOwner>()
            : LiesEigentuemer(block);

        var (strasse, hausNr) = LiesGebaeudeadresse(zeilen);
        var (plz, ort) = LiesPlzOrt(eigentuemer, zeilen);

        return new LandRegistryEntry(strasse, hausNr, plz, ort, eigentuemer, ohneEigentuemer);
    }

    /// <summary>
    /// Wandelt das HTML in Textzeilen. Bewusst ohne HTML-Bibliothek: die Seite
    /// besteht aus Tabellenzellen, deren Text zeilenweise gelesen werden kann.
    /// </summary>
    private static List<string> ZeilenAusHtml(string html)
    {
        var ohneSkript = Regex.Replace(
            html, "<script.*?</script>|<style.*?</style>", " ",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);

        var text = Regex.Replace(ohneSkript, "<[^>]+>", "\n");
        text = WebUtility.HtmlDecode(text);

        return text
            .Split('\n')
            .Select(z => Regex.Replace(z, @"[\s ]+", " ").Trim())
            .Where(z => z.Length > 0)
            .ToList();
    }

    private static List<LandRegistryOwner> LiesEigentuemer(List<string> block)
    {
        var ergebnis = new List<LandRegistryOwner>();

        var kennzeichnung = string.Empty;
        string? name = null;
        var adresse = string.Empty;
        var anteil = string.Empty;

        void Abschliessen()
        {
            if (!string.IsNullOrWhiteSpace(name))
                ergebnis.Add(new LandRegistryOwner(kennzeichnung, name!, adresse, anteil));

            name = null;
            adresse = string.Empty;
            anteil = string.Empty;
            kennzeichnung = string.Empty;
        }

        foreach (var zeile in block)
        {
            // Zweite Lage: "Keine" ist eine Angabe, kein Name — auch dann nicht,
            // wenn die Zeile mitten im Block auftaucht.
            if (string.Equals(zeile, "Keine", StringComparison.OrdinalIgnoreCase))
            {
                Abschliessen();
                continue;
            }

            var lit = LitZeile.Match(zeile);
            if (lit.Success)
            {
                Abschliessen();
                kennzeichnung = lit.Groups["buchstabe"].Success
                    ? "Lit." + lit.Groups["buchstabe"].Value
                    : string.Empty;

                var inhalt = lit.Groups["inhalt"].Value.Trim();

                // Klassische Form: hinter dem Doppelpunkt steht nichts, der Name
                // folgt in der naechsten Zeile.
                if (inhalt.Length == 0)
                    continue;

                // Stockwerkeigentum: der ganze Eintrag steht in dieser einen Zeile.
                var (stwe, stweName, stweAnteil) = ZerlegeEinzeiler(inhalt);
                if (stwe.Length > 0)
                    kennzeichnung = stwe;
                name = stweName;
                anteil = stweAnteil;
                Abschliessen();
                continue;
            }

            if (AnteilZeile.IsMatch(zeile))
            {
                anteil = zeile;
                Abschliessen();
                continue;
            }

            if (name is null)
            {
                name = zeile;
                continue;
            }

            if (adresse.Length == 0)
            {
                adresse = zeile;
                continue;
            }

            // Eine dritte Zeile ohne Anteil beginnt einen neuen Eigentuemer.
            Abschliessen();
            name = zeile;
        }

        Abschliessen();
        return ergebnis;
    }

    /// <summary>
    /// Zerlegt die einzeilige Stockwerkeigentums-Form:
    ///   "Jeweiliger Eigentuemer von StWE S1021 (Kurt Beispiel), 31/100 Miteigentum"
    ///
    /// Fehlt die Klammer, bleibt der Registertext als Name stehen. Er ist
    /// erkennbar kein Personenname, und ein stilles Weglassen waere schlimmer:
    /// dann verschwaende ein Eigentuemer spurlos aus dem Dossier.
    /// </summary>
    private static (string Kennzeichnung, string Name, string Anteil) ZerlegeEinzeiler(string inhalt)
    {
        var anteil = string.Empty;
        var rest = inhalt;

        var anteilTreffer = AnteilAmEnde.Match(rest);
        if (anteilTreffer.Success)
        {
            anteil = anteilTreffer.Groups["anteil"].Value.Trim();
            rest = rest[..anteilTreffer.Index];
        }

        var kennzeichnung = string.Empty;
        var stwe = StockwerkNummer.Match(rest);
        if (stwe.Success)
            kennzeichnung = "StWE " + stwe.Groups["nr"].Value;

        var klammern = NameInKlammern.Matches(rest);
        var name = klammern.Count > 0
            ? klammern[^1].Groups["name"].Value.Trim()
            : rest.Trim();

        return (kennzeichnung, name, anteil);
    }

    private static (string Strasse, string HausNr) LiesGebaeudeadresse(List<string> zeilen)
    {
        foreach (var zeile in zeilen)
        {
            var treffer = GebaeudeZeile.Match(zeile);
            if (treffer.Success)
            {
                return (treffer.Groups["strasse"].Value.Trim(),
                        treffer.Groups["nr"].Value.Trim());
            }
        }

        return (string.Empty, string.Empty);
    }

    /// <summary>
    /// PLZ und Ort der Liegenschaft. Sie stehen nur in den Eigentuemeradressen —
    /// und der Eigentuemer kann auswaerts wohnen. Deshalb zaehlt nur eine
    /// Adresse, deren Ort auch im Kopf ("Grundbuch &lt;Gemeinde&gt;") steht.
    /// </summary>
    private static (string Plz, string Ort) LiesPlzOrt(
        List<LandRegistryOwner> eigentuemer, List<string> zeilen)
    {
        var kopf = zeilen.FirstOrDefault(
            z => z.StartsWith("Grundbuch ", StringComparison.OrdinalIgnoreCase));
        var gemeinde = kopf is null ? string.Empty : kopf["Grundbuch ".Length..].Trim();

        foreach (var besitzer in eigentuemer)
        {
            var treffer = PlzOrt.Match(besitzer.AddressLine);
            if (!treffer.Success)
                continue;

            var ort = treffer.Groups["ort"].Value.Trim();
            if (gemeinde.Length > 0
                && !ort.Equals(gemeinde, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return (treffer.Groups["plz"].Value, ort);
        }

        return (string.Empty, gemeinde);
    }
}
