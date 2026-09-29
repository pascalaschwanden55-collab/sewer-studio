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

    private static readonly Regex NeueEigeneAusnahme = new(
        // Seit Aufgabe 10c2 zeigt UserError auch eigene IOException/InvalidDataException/
        // JsonException woertlich — deren Texte muessen deshalb ebenso deutsch sein.
        @"new\s+(InvalidOperationException|ArgumentException|IOException|InvalidDataException|JsonException)\s*\(",
        RegexOptions.Compiled);

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

        foreach (var schicht in EigeneSchichten)
        {
            var ordner = Path.Combine(wurzel, "src", schicht);
            Assert.True(Directory.Exists(ordner), $"Schicht-Ordner fehlt: {ordner}");

            foreach (var datei in Directory.EnumerateFiles(ordner, "*.cs", SearchOption.AllDirectories))
            {
                gepruefteDateien++;
                var text = File.ReadAllText(datei);
                var relativ = Path.GetRelativePath(wurzel, datei).Replace('\\', '/');

                foreach (Match treffer in NeueEigeneAusnahme.Matches(text))
                {
                    var nachArgumentStart = treffer.Index + treffer.Length;
                    var fensterLaenge = Math.Min(400, text.Length - nachArgumentStart);
                    var fenster = text.Substring(nachArgumentStart, fensterLaenge);

                    var argument = ErstesArgumentAlsZeichenkette.Match(fenster);
                    if (!argument.Success)
                        continue; // erstes Argument ist keine direkte Zeichenkette (z. B. eine Variable)

                    var nachricht = argument.Groups[1].Value.Trim();
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
