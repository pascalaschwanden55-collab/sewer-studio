using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using static AuswertungPro.Next.Pipeline.Tests.TestRepoPaths;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Waechter fuer die zentrale Eigene-Meldung-Regel von <c>UserError.Describe</c> (Aufgabe 10b,
/// Fix-Runde 1): Eine <see cref="InvalidOperationException"/>/<see cref="ArgumentException"/>,
/// die nachweislich aus <c>AuswertungPro.Next.Domain</c>, <c>.Application</c> oder
/// <c>.Infrastructure</c> geworfen wird, zeigt seit Fix-Runde 1 ihren eigenen Ausnahmetext
/// UNGEFILTERT an - ohne Uebersetzung. Ein englischer Text an so einer Stelle erscheint damit
/// direkt beim Nutzer. Genau das ist in Fix-Runde 2 an einer zweiten, uebersehenen Stelle in
/// <c>VisionPipelineClient.GetAsync</c> passiert: Der urspruengliche Audit (Fix-Runde 1) suchte
/// nur auf derselben Zeile wie <c>throw new …Exception(</c> nach einer Zeichenkette und fand die
/// Nachricht nicht, weil sie in einer eigenen Zeile stand.
///
/// Dieser Waechter durchsucht deshalb ALLE <c>new InvalidOperationException(</c>/
/// <c>new ArgumentException(</c>-Konstruktionen in den drei Schichten (unabhaengig von
/// Zeilenumbruechen zwischen der Klammer und der Zeichenkette) und schlaegt fehl, sobald das
/// erste Argument eine Zeichenkette ist, die mit einem typischen englischen Wort beginnt. Ein
/// neuer Fund muss entweder uebersetzt oder in <see cref="Ausnahmen"/> mit Begruendung
/// eingetragen werden - eine dritte Option gibt es nicht.
/// </summary>
public sealed class UserErrorEigeneMeldungenSpracheTests
{
    /// <summary>Die drei Schichten, die <c>UserError.Describe</c> als "eigene Assembly" erkennt.</summary>
    private static readonly string[] EigeneSchichten =
    [
        "AuswertungPro.Next.Domain",
        "AuswertungPro.Next.Application",
        "AuswertungPro.Next.Infrastructure"
    ];

    /// <summary>
    /// Vorgabe des Reviews: typische englische Start-Woerter/-Phrasen einer Fehlermeldung,
    /// gross-/kleinschreibungsunabhaengig. Bewusst eine Positivliste von Anfaengen statt eines
    /// generischen "kein deutsches Wort"-Tests - letzterer traefe auch kurze technische Begriffe
    /// wie Bezeichner oder Zahlen faelschlich.
    /// </summary>
    private static readonly Regex EnglischerStart = new(
        "^(Failed|Cannot|Can't|Could not|Unable|Invalid|Unknown|Missing|Expected|No |The |Not |Only |Must |Value |Unexpected|Error)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Fix-Runde 1 zu 10c2: Die Liste der geprueften Ausnahmetypen folgt dem, was
    /// <c>UserError.Describe</c> woertlich zeigt — die fuenf Standardtypen plus JEDE in den drei
    /// Schichten deklarierte Ausnahmeklasse (per Quelltextsuche <c>class X : …Exception</c>,
    /// damit eine neue eigene Klasse automatisch mitgeprueft wird).
    /// </summary>
    private static readonly string[] StandardTypen =
        ["InvalidOperationException", "ArgumentException", "IOException", "InvalidDataException", "JsonException"];

    private static readonly Regex AusnahmeKlasse = new(
        @"\bclass\s+(\w+Exception)\s*(?:\([^)]*\))?\s*:\s*[\w.]*Exception\b",
        RegexOptions.Compiled);

    private static Regex NeueEigeneAusnahme(IEnumerable<string> typen) => new(
        // Fix-Runde 2: auch voll qualifiziert ("new System.IO.IOException(").
        @"new\s+(?:\w+\.)*(" + string.Join("|", typen.Distinct().Select(Regex.Escape)) + @")\s*\(",
        RegexOptions.Compiled);

    internal static IReadOnlyList<string> EigeneAusnahmeTypen(string wurzel)
    {
        var typen = new List<string>(StandardTypen);
        foreach (var schicht in EigeneSchichten)
            foreach (var datei in Directory.EnumerateFiles(Path.Combine(wurzel, "src", schicht), "*.cs", SearchOption.AllDirectories))
                foreach (Match m in AusnahmeKlasse.Matches(File.ReadAllText(datei)))
                    typen.Add(m.Groups[1].Value);
        return typen.Distinct().ToList();
    }

    /// <summary>Erstes Argument ist eine Zeichenkette, direkt (ggf. nach Leerraum/$) hinter der Klammer.</summary>
    private static readonly Regex ErstesArgumentAlsZeichenkette = new(
        "^\\s*\\$?\"((?:[^\"\\\\]|\\\\.)*)\"",
        RegexOptions.Compiled | RegexOptions.Singleline);

    /// <summary>
    /// Bewusst ausgenommene Fundstellen (Datei relativ zum Repo-Root mit "/", Zeilennummer der
    /// oeffnenden Klammer) samt Begruendung, warum der englische Text dort bleiben darf. Aktuell
    /// leer: alle bekannten Funde wurden uebersetzt statt ausgenommen.
    /// </summary>
    private static readonly Dictionary<(string Datei, int Zeile), string> Ausnahmen = new();

    [Fact]
    public void Eigene_InvalidOperationException_und_ArgumentException_sind_deutsch()
    {
        var wurzel = FindRepositoryRoot();
        var funde = new List<string>();
        var gepruefteDateien = 0;
        var typen = EigeneAusnahmeTypen(wurzel);
        Assert.Contains("SidecarInsufficientVramException", typen);
        Assert.Contains("SchachtProArchiveException", typen);
        // Fix-Runde 2: Klassen mit Primaerkonstruktor ("class X(string message) : …Exception").
        Assert.Contains("XtfQuelleFehltException", typen);
        Assert.Contains("CommitProcessInterruptedException", typen);
        var neueAusnahme = NeueEigeneAusnahme(typen);

        foreach (var schicht in EigeneSchichten)
        {
            var ordner = Path.Combine(wurzel, "src", schicht);
            Assert.True(Directory.Exists(ordner), $"Schicht-Ordner fehlt: {ordner}");

            foreach (var datei in Directory.EnumerateFiles(ordner, "*.cs", SearchOption.AllDirectories))
            {
                gepruefteDateien++;
                var text = File.ReadAllText(datei);
                var relativ = Path.GetRelativePath(wurzel, datei).Replace('\\', '/');

                foreach (Match treffer in neueAusnahme.Matches(text))
                {
                    var nachArgumentStart = treffer.Index + treffer.Length;
                    var fensterLaenge = Math.Min(400, text.Length - nachArgumentStart);
                    var fenster = text.Substring(nachArgumentStart, fensterLaenge);

                    var argument = ErstesArgumentAlsZeichenkette.Match(fenster);
                    if (!argument.Success)
                        continue; // erstes Argument ist keine direkte Zeichenkette (z. B. eine Variable)

                    var nachricht = argument.Groups[1].Value.Trim();
                    // Eigene Typen wie SchachtProArchiveException tragen zuerst einen Code
                    // ("INVALID_ARCHIVE"), die Meldung folgt als zweites Argument.
                    if (Regex.IsMatch(nachricht, "^[A-Z][A-Z0-9_]*$"))
                    {
                        var rest = fenster[(argument.Index + argument.Length)..].TrimStart();
                        if (!rest.StartsWith(','))
                            continue;
                        var zweites = ErstesArgumentAlsZeichenkette.Match(rest[1..]);
                        if (!zweites.Success)
                            continue;
                        nachricht = zweites.Groups[1].Value.Trim();
                    }
                    if (!EnglischerStart.IsMatch(nachricht))
                        continue;

                    var zeile = text[..treffer.Index].Count(c => c == '\n') + 1;
                    if (Ausnahmen.ContainsKey((relativ, zeile)))
                        continue;

                    funde.Add($"{relativ}:{zeile}: \"{nachricht}\"");
                }
            }
        }

        Assert.True(
            funde.Count == 0,
            "Englischer Text in eigener InvalidOperationException/ArgumentException aus " +
            "Domain/Application/Infrastructure - UserError.Describe zeigt ihn ab sofort " +
            "ungefiltert. Uebersetzen oder mit Begruendung in Ausnahmen eintragen:\n" +
            string.Join("\n", funde));

        // Der Waechter darf nicht leerlaufen: Er muss wirklich Dateien durchsucht haben, sonst
        // wuerde ein falscher Schichtenpfad still gruen bleiben.
        Assert.True(gepruefteDateien > 100, $"Nur {gepruefteDateien} Dateien durchsucht - zu wenig.");
    }

    /// <summary>
    /// Fix-Runde 1 zu 10c2, zweite Regel: Eine eigene Ausnahme darf den <c>.Message</c>-Text
    /// einer anderen Ausnahme nicht in ihre Meldung einbauen — <c>UserError</c> zeigt eigene
    /// Meldungen woertlich, und der eingebettete Text ist meist englisch (HTTP, ZIP, JSON,
    /// SQLite). Die aufgefangene Ausnahme gehoert als <c>innerException</c> dazu; sie steht dann
    /// im Programmlog. Zusaetzlich schneidet <c>UserError</c> einen eingebetteten Fremdtext ab.
    /// Bewusste Ausnahmen stehen mit Grund in <see cref="MessageEinbettungErlaubt"/>.
    /// </summary>
    private static readonly Dictionary<(string Datei, string Ausschnitt), string> MessageEinbettungErlaubt = new()
    {
        [("src/AuswertungPro.Next.Infrastructure/Ai/Pipeline/VisionPipelineClient.cs", "ist nicht verfügbar: {ex.Message}")] =
            "Der Text fliesst in Pipeline-Traces und Degraded-Gruende (Diagnose); UserError schneidet ihn fuer die Anzeige ab.",
        [("src/AuswertungPro.Next.Application/Ai/Training/ExportPlans/TrainingExportPlanService.cs", "nicht exportbereit: {ex.Message}")] =
            "Die eingebettete Meldung ist eine eigene deutsche TrainingYoloClassMapException (bleibt bei UserError stehen).",
        [("src/AuswertungPro.Next.Infrastructure/Ai/Teacher/VsaYoloClassMapFileStore.cs", "ist nicht lesbar oder ungültig: {ex.Message}")] =
            "Die innere Ausnahme ist meist die eigene deutsche Validierung (\"mehrfach\", \"lückenlos\"); UserError behaelt sie, schneidet aber fremde IO-/JSON-Texte ab.",
        [("src/AuswertungPro.Next.Infrastructure/Ai/Training/ExportPlans/TrainingExportRegistryFileStore.cs", "konnte nicht sicher gelesen werden: {ex.Message}")] =
            "Die innere Ausnahme ist meist die eigene deutsche Manifestpruefung (\"doppelte Feld\"); fremde Texte schneidet UserError ab.",
        [("src/AuswertungPro.Next.Infrastructure/Ai/Training/ClassMaps/TrainingYoloClassMapFileStore.cs", "konnte nicht sicher gelesen werden: {ex.Message}")] =
            "Die innere Ausnahme ist meist die eigene deutsche Klassenkartenpruefung; fremde Texte schneidet UserError ab.",
        [("src/AuswertungPro.Next.Infrastructure/Ai/OllamaClient.cs", "nicht gelesen werden: \" + ex.Message")] =
            "Diagnose der Modellantwort samt Rohtext; innere Ausnahme wird uebergeben, UserError schneidet ab dem Fremdtext ab.",
        [("src/AuswertungPro.Next.Application/Protocol/VsaKekCatalogBuilder.cs", "{result.Message}")] =
            "Ausgabe des Entpackprozesses beim Katalogaufbau, ohne innere Ausnahme; sonst ginge die Ursache verloren.",
        [("src/AuswertungPro.Next.Infrastructure/Import/Pdf/PdfTextExtractionService.cs", "{result.Message}")] =
            "pdftotext-Fehler wird intern aufgefangen und faellt auf PdfPig zurueck; nie eine Anzeige.",
        [("src/AuswertungPro.Next.Infrastructure/Import/Pdf/PdfFileSafetyService.cs", "check.Message")] =
            "Eigene deutsche Budgetmeldung (PdfImportSafetyPolicy), keine fremde Ausnahme.",
        [("src/AuswertungPro.Next.Infrastructure/Import/Pdf/PdfImportSafetyPolicy.cs", "check.Message")] =
            "Eigene deutsche Budgetmeldung, keine fremde Ausnahme.",
        [("src/AuswertungPro.Next.Infrastructure/Ai/Training/PdfReview/TrainingPdfReviewImportService.cs", "ex.Message")] =
            "Reicht bewusst die eigene deutsche Validierungsmeldung als UserFacingException weiter (RunUserVisibleValidation).",
    };

    // Fix-Runde 2: auch "ex?.Message", "Holen(x).Message" und "fehler[0].Message".
    private static readonly Regex FremdeMessage = new(@"(?:\b(?!this\b)\w+|[)\]])\??\.Message\b", RegexOptions.Compiled);

    [Fact]
    public void Eigene_Ausnahmen_betten_keinen_fremden_Message_Text_ein()
    {
        var wurzel = FindRepositoryRoot();
        var neueAusnahme = NeueEigeneAusnahme(EigeneAusnahmeTypen(wurzel));
        var funde = new List<string>();
        var genutzt = new HashSet<(string, string)>();

        foreach (var schicht in EigeneSchichten)
        {
            foreach (var datei in Directory.EnumerateFiles(Path.Combine(wurzel, "src", schicht), "*.cs", SearchOption.AllDirectories))
            {
                var text = File.ReadAllText(datei);
                var relativ = Path.GetRelativePath(wurzel, datei).Replace('\\', '/');
                if (relativ.Contains("WebGis", StringComparison.Ordinal))
                    continue; // geschuetzte WebGIS-Dateien; UserError nimmt ihre Typen ohnehin aus
                foreach (Match treffer in neueAusnahme.Matches(text))
                {
                    var argumente = Argumente(text, treffer.Index + treffer.Length);
                    if (!FremdeMessage.IsMatch(argumente))
                        continue;

                    var erlaubt = MessageEinbettungErlaubt.Keys.FirstOrDefault(
                        k => k.Datei == relativ && argumente.Contains(k.Ausschnitt, StringComparison.Ordinal));
                    if (erlaubt != default)
                    {
                        genutzt.Add(erlaubt);
                        continue;
                    }

                    var zeile = text[..treffer.Index].Count(c => c == '\n') + 1;
                    var kurz = argumente.Trim().Replace('\n', ' ').Replace('\r', ' ');
                    funde.Add($"{relativ}:{zeile}: {kurz[..Math.Min(160, kurz.Length)]}");
                }
            }
        }

        Assert.True(funde.Count == 0,
            "Eigene Ausnahme baut .Message einer anderen Ausnahme ein (UserError zeigt das woertlich). " +
            "Aufgefangene Ausnahme als innerException uebergeben oder begruendet eintragen:\n" + string.Join("\n", funde));
        Assert.True(genutzt.Count == MessageEinbettungErlaubt.Count,
            "Tote Eintraege in MessageEinbettungErlaubt: " +
            string.Join(", ", MessageEinbettungErlaubt.Keys.Where(k => !genutzt.Contains(k)).Select(k => k.Datei)));
    }

    /// <summary>
    /// Fix-Runde 2 zu 10c2: <c>UserError.Describe</c> haengt an jede eigene Meldung selbst
    /// "Technische Details stehen im Programmlog." an. Eine eigene Ausnahme, deren Text den
    /// Programmlog schon nennt, erschiene deshalb mit doppeltem Hinweis. Nur eine
    /// <c>UserFacingException</c> (woertlich, ohne Zusatz) darf ihn selbst tragen.
    /// </summary>
    [Fact]
    public void Eigene_Ausnahmen_nennen_den_Programmlog_nicht_selbst()
    {
        var wurzel = FindRepositoryRoot();
        var neueAusnahme = NeueEigeneAusnahme(EigeneAusnahmeTypen(wurzel).Where(t => t != "UserFacingException"));
        var funde = new List<string>();

        foreach (var schicht in EigeneSchichten)
        {
            foreach (var datei in Directory.EnumerateFiles(Path.Combine(wurzel, "src", schicht), "*.cs", SearchOption.AllDirectories))
            {
                var text = File.ReadAllText(datei);
                var relativ = Path.GetRelativePath(wurzel, datei).Replace('\\', '/');
                if (relativ.Contains("WebGis", StringComparison.Ordinal))
                    continue;
                foreach (Match treffer in neueAusnahme.Matches(text))
                {
                    if (!Argumente(text, treffer.Index + treffer.Length).Contains("Programmlog", StringComparison.Ordinal))
                        continue;
                    funde.Add($"{relativ}:{text[..treffer.Index].Count(c => c == '\n') + 1}");
                }
            }
        }

        Assert.True(funde.Count == 0,
            "Eigene Ausnahme nennt den Programmlog selbst (UserError haengt den Hinweis an, er stuende doppelt):\n"
            + string.Join("\n", funde));
    }

    /// <summary>Argumentliste bis zur passenden schliessenden Klammer (Zeichenketten uebersprungen).</summary>
    private static string Argumente(string text, int start)
    {
        var tiefe = 1;
        var i = start;
        while (i < text.Length && tiefe > 0)
        {
            var c = text[i];
            if (c == '"')
            {
                i++;
                while (i < text.Length && text[i] != '"')
                {
                    if (text[i] == '\\') i++;
                    i++;
                }
            }
            else if (c == '(') tiefe++;
            else if (c == ')') tiefe--;
            i++;
        }

        return text[start..Math.Min(text.Length, Math.Max(start, i - 1))];
    }

    /// <summary>
    /// Die Ausnahmen-Liste selbst darf keine toten Eintraege enthalten (Datei/Zeile nicht mehr
    /// vorhanden) - sonst verdeckt sie irgendwann eine andere, neue Fundstelle unbemerkt.
    /// </summary>
    [Fact]
    public void Ausnahmenliste_ist_leer_oder_zeigt_auf_echte_Stellen()
    {
        var wurzel = FindRepositoryRoot();
        foreach (var (datei, zeile) in Ausnahmen.Keys)
        {
            var pfad = Path.Combine(wurzel, datei.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(pfad), $"Ausnahme zeigt auf fehlende Datei: {datei}");

            var zeilen = File.ReadAllLines(pfad);
            Assert.True(zeile >= 1 && zeile <= zeilen.Length, $"Ausnahme zeigt auf ungueltige Zeile: {datei}:{zeile}");
        }
    }
}
