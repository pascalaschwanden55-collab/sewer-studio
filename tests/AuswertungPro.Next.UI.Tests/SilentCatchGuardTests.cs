using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SilentCatchGuardTests
{
    // Eine runde Klammer samt Inhalt, auch verschachtelt (Balancing Group): ein Filter wie
    // when (File.Exists(x)) oder when (e.InnerExceptions.All(i => i is IOException)) endet nicht am ersten ")".
    private const string Klammer = @"\((?:[^()]|(?<o>\()|(?<-o>\)))*(?(o)(?!))\)";

    private static readonly Regex EmptyCatchPattern = new(
        @"catch(?:\s*" + Klammer + @")?(?:\s+when\s*" + Klammer + @")?\s*\{\s*\}",
        RegexOptions.CultureInvariant);

    // Ein catch-Block, in dem nur Kommentare stehen (kein Code).
    private static readonly Regex CommentOnlyCatchPattern = new(
        @"catch(?:\s*" + Klammer + @")?(?:\s+when\s*" + Klammer + @")?\s*\{((?:\s|//[^\r\n]*|/\*.*?\*/)*)\}",
        RegexOptions.CultureInvariant | RegexOptions.Singleline);

    // Obergrenze der Kommentar-catch-Bloecke (Sperrklinke, Deepscan 02.10.2026 R7, mit verschachtelten when-Filtern gezaehlt): darf nur sinken.
    // Wer einen Block entfernt oder durch sichtbare Fehlermeldung ersetzt, zieht den Wert nach.
    private const int MaxKommentarCatchBloecke = 236;

    // Woerter, die allein noch keinen Grund nennen ("ignore", "non-fatal", "best effort cleanup" ...).
    private static readonly HashSet<string> Floskelwoerter = new(StringComparer.OrdinalIgnoreCase)
    {
        "ignore", "ignored", "ignoring", "ignorieren", "ignoriert", "swallow", "swallowed",
        "skip", "skipped", "skipping", "next", "non", "fatal", "nonfatal", "best", "effort", "besteffort",
        "cleanup", "errors", "error", "exceptions", "exception", "only", "fehler", "weiter", "still", "catch",
        "folder", "folders", "access", "search", "fallback", "layout", "directories", "directory", "files", "io",
        "ueberspringen", "ueberspringt", "egal", "ok", "nothing", "nichts", "noop", "none", "expected", "erwartet"
    };

    // Ausnahmen mit Verweis: Datei (relativ zu src, mit /) -> Floskeltext. Codex arbeitet dort noch (Welle 2,
    // «nach Codex»); der Eintrag faellt weg, sobald der Kommentar eine echte Begruendung traegt.
    private static readonly (string Datei, string Kommentar)[] FloskelAusnahmen =
    {
        ("AuswertungPro.Next.Infrastructure/HoldingDistribution/ParsedHoldingDistributionController.cs", "Best-effort cleanup.")
    };

    [Fact]
    public void Produktivcode_enthaelt_keine_vollstaendig_leeren_Catch_Bloecke()
    {
        var sourceRoot = Path.Combine(TestRepoPaths.FindRepositoryRoot(), "src");
        var findings = Directory
            .EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
            .Where(ContainsCodeMatch)
            .Select(path => Path.GetRelativePath(sourceRoot, path))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.True(
            findings.Length == 0,
            "Vollstaendig leere catch-Bloecke verschlucken Fehler ohne jede Spur:\n" +
            string.Join("\n", findings));
    }

    [Fact]
    public void Kommentar_Catch_Bloecke_tragen_eine_Begruendung_statt_einer_Floskel()
    {
        var sourceRoot = Path.Combine(TestRepoPaths.FindRepositoryRoot(), "src");
        var floskeln = new List<string>();
        foreach (var path in Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories))
        {
            var relativ = Path.GetRelativePath(sourceRoot, path).Replace('\\', '/');
            foreach (var (zeile, kommentar) in FindeKommentarCatches(File.ReadAllText(path)))
            {
                if (!IstFloskel(kommentar))
                    continue;
                if (FloskelAusnahmen.Any(a => a.Datei == relativ && a.Kommentar == OhneKommentarzeichen(kommentar)))
                    continue;
                floskeln.Add($"{relativ}:{zeile}: \"{kommentar}\"");
            }
        }

        Assert.True(
            floskeln.Count == 0,
            "Ein leerer catch braucht einen Grund (welcher Fehler, warum harmlos, was gilt stattdessen), " +
            "keine Floskel wie \"ignore\" oder \"best effort\":\n" + string.Join("\n", floskeln.OrderBy(f => f, StringComparer.OrdinalIgnoreCase)));
    }

    [Fact]
    public void Anzahl_der_Kommentar_Catch_Bloecke_kann_nicht_steigen()
    {
        var sourceRoot = Path.Combine(TestRepoPaths.FindRepositoryRoot(), "src");
        var anzahl = Directory
            .EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
            .Sum(path => FindeKommentarCatches(File.ReadAllText(path)).Count());

        Assert.True(
            anzahl <= MaxKommentarCatchBloecke,
            $"Kommentar-catch-Bloecke: {anzahl} > Obergrenze {MaxKommentarCatchBloecke}. " +
            "Fehler melden (BestEffort.Run / UserError) statt verschlucken.");
        Assert.True(
            anzahl >= MaxKommentarCatchBloecke,
            $"Kommentar-catch-Bloecke: {anzahl} < Obergrenze {MaxKommentarCatchBloecke}. " +
            "Sperrklinke nachziehen (MaxKommentarCatchBloecke senken).");
    }

    [Theory]
    [InlineData("ignore")]
    [InlineData("/* next */")]
    [InlineData("// Non-fatal.")]
    [InlineData("Best effort only.")]
    [InlineData("// Best-effort cleanup.")]
    [InlineData("ignore folder errors")]
    [InlineData("Swallow layout exceptions")]
    [InlineData("/* ignore */")]
    public void Floskeln_werden_erkannt(string kommentar)
        => Assert.True(IstFloskel(kommentar), kommentar);

    [Theory]
    [InlineData("// Nur vollstaendig gleicher Inhalt gilt als vorhanden.")]
    [InlineData("/* Cache defekt -> neu bauen */")]
    [InlineData("// Normaler Programmabschluss.")]
    [InlineData("// Das Fenster wurde geschlossen.")]
    [InlineData("// Eine gesperrte Temp-Datei bleibt liegen; das Ergebnis steht schon fest.")]
    public void Begruendungen_sind_keine_Floskeln(string kommentar)
        => Assert.False(IstFloskel(kommentar), kommentar);

    [Theory]
    [InlineData("catch (IOException) when (File.Exists(target)) { // ignore\n }")]
    [InlineData("catch (AggregateException e) when (e.InnerExceptions.All(i => i is IOException)) { // ignore\n }")]
    [InlineData("catch when (A(B(C()))) { // ignore\n }")]
    public void Catch_mit_verschachteltem_when_Filter_wird_erfasst(string quelle)
    {
        var treffer = FindeKommentarCatches("void F() { try { X(); } " + quelle + " }").ToArray();

        Assert.Single(treffer);
        Assert.True(IstFloskel(treffer[0].Kommentar));
    }

    [Fact]
    public void Ein_neuer_catch_mit_Floskelkommentar_wird_gefunden()
    {
        const string quelle = "void F() { try { X(); } catch (Exception) { // ignore\n } }";

        var treffer = FindeKommentarCatches(quelle).ToArray();

        Assert.Single(treffer);
        Assert.True(IstFloskel(treffer[0].Kommentar));
    }

    private static IEnumerable<(int Zeile, string Kommentar)> FindeKommentarCatches(string source)
    {
        foreach (Match match in CommentOnlyCatchPattern.Matches(source))
        {
            var lineStart = source.LastIndexOf('\n', Math.Max(0, match.Index - 1)) + 1;
            var prefix = source[lineStart..match.Index].TrimStart();
            if (prefix.StartsWith("//", StringComparison.Ordinal) || prefix.StartsWith("*", StringComparison.Ordinal))
                continue;
            var zeile = source.AsSpan(0, match.Index).Count('\n') + 1;
            yield return (zeile, match.Groups[1].Value.Trim());
        }
    }

    private static string OhneKommentarzeichen(string kommentar)
        => Regex.Replace(kommentar, @"/\*|\*/|//+", " ").Trim();

    // Floskel: nach Abzug der Kommentarzeichen bestehen alle Woerter aus der Floskelliste.
    // Ein Kommentar ohne jedes Wort (leerer Block) zaehlt hier nicht; den prueft der Test auf leere Bloecke.
    private static bool IstFloskel(string kommentar)
    {
        var text = Regex.Replace(kommentar, @"/\*|\*/|//+", " ");
        var woerter = Regex.Matches(text, @"[A-Za-zÄÖÜäöüß]+").Select(m => m.Value).ToArray();
        return woerter.Length > 0 && woerter.All(w => Floskelwoerter.Contains(w));
    }

    private static bool ContainsCodeMatch(string path)
    {
        var source = File.ReadAllText(path);
        return EmptyCatchPattern.Matches(source).Any(match =>
        {
            var lineStart = source.LastIndexOf('\n', Math.Max(0, match.Index - 1)) + 1;
            var prefix = source[lineStart..match.Index].TrimStart();
            return !prefix.StartsWith("//", StringComparison.Ordinal)
                && !prefix.StartsWith("*", StringComparison.Ordinal);
        });
    }
}
