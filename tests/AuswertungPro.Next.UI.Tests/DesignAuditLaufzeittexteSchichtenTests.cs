using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Aufgabe 10c2 (Sprachbereinigung in Application, Infrastructure und Domain): Die in dieser
/// Aufgabe bereinigten Ersatzschreibweisen (Verknuepfung, ungueltig, pruefen, uebersprungen …)
/// duerfen in sichtbaren Meldungen, Berichten, Statustexten und Ausnahmetexten der drei unteren
/// Schichten nicht zurueckfallen. Schwester von <see cref="DesignAuditLaufzeittexteTests"/>
/// (dort das UI-Projekt, Aufgabe 10c1), gleiche Grundsaetze:
/// <list type="bullet">
/// <item>Geprueft wird nur ein Zeichenketten-Literal, das nach dem Entfernen von
/// <c>{...}</c>-Ausdruecken ein Leerzeichen enthaelt. Ein Einzelwort ist viel eher ein
/// Schluessel, Tag oder Datenwert (Lehre aus 10c1, Fix-Runde 1).</item>
/// <item>Ganzes Wort (Wortgrenze); Kommentar- und <c>[Obsolete(</c>-Zeilen sowie angehaengte
/// Zeilenkommentare zaehlen nicht.</item>
/// <item>Reine Protokoll-Anweisungen bleiben bewusst in Ersatzschreibweise (Konvention seit
/// 10b): <c>_logger.Log…</c>, <c>BestEffort.…</c> (auch der Kontext von <c>BestEffort.Try</c>),
/// <c>Trace.</c>/<c>Debug.</c>, <c>trace?.Invoke</c>, <c>TraceMessage:</c>, <c>.Log(</c>. Die
/// Anweisung wird dafuer ueber mehrere Zeilen bis zum Ende der vorigen Anweisung
/// zusammengesetzt.</item>
/// <item>Bewusst NICHT bereinigte Dateien und Literale (Schluessel, Parser-Muster, KI-Prompts,
/// Katalog/Vokabular, Markerinhalt, gespeicherte Datenwerte) stehen einzeln mit Grund in
/// <see cref="AusgenommeneDateien"/> und <see cref="GeschuetzteGanzeZeichenketten"/>.</item>
/// </list>
/// WICHTIG: <see cref="Sprachregeln"/> enthaelt die ALTE ASCII-Form; ein automatisches
/// Bereinigungsskript darf diese Datei nie anfassen (Lehre aus 10c1).
/// </summary>
public sealed class DesignAuditLaufzeittexteSchichtenTests
{
    private static readonly string[] Schichten =
    [
        "AuswertungPro.Next.Application",
        "AuswertungPro.Next.Infrastructure",
        "AuswertungPro.Next.Domain",
    ];

    /// <summary>In Aufgabe 10c2 bereinigte Wortformen (alte Schreibweise).</summary>
    private static readonly string[] BereinigteWoerter = Sprachregeln.WortformenFuerSchichten;

    /// <summary>Dateien, deren Zeichenketten bewusst unveraendert bleiben (Grund je Eintrag).</summary>
    private static readonly string[] AusgenommeneDateien =
        Sprachregeln.AusgenommeneSchichtDateien.Select(a => a.Datei).ToArray();

    /// <summary>Ganze Literale, die exakt so verglichen, zurueckgelesen oder gespeichert werden.</summary>
    private static readonly string[] GeschuetzteGanzeZeichenketten =
        Sprachregeln.GeschuetzteFuer(Sprachregeln.Bereich.Schichten);

    private static readonly Regex Literal = new("\"((?:[^\"\\\\]|\\\\.)*)\"", RegexOptions.Compiled);

    private static readonly Regex Protokollanweisung = new(
        @"BestEffort\.|trace\?\.Invoke|TraceMessage\s*:|\.Log\(|Log\?\.Invoke|\blog\(|\b_?[lL]ogger\??\.|\bLog(Warning|Error|Information|Debug|Trace|Critical)\s*\(|\bTrace\.|Debug\.Write|Console\.",
        RegexOptions.Compiled);

    private static readonly Regex WortRegex = new(
        @"\b(" + string.Join("|", BereinigteWoerter.Distinct().Select(Regex.Escape)) + @")\b",
        RegexOptions.Compiled);

    [Fact]
    public void Bereinigte_Ersatzschreibweisen_fallen_in_Application_Infrastructure_Domain_nicht_zurueck()
    {
        var treffer = new List<string>();
        var durchsucht = 0;

        foreach (var schicht in Schichten)
        {
            var wurzel = RepoFile("src", schicht);
            Assert.True(Directory.Exists(wurzel), $"Schicht fehlt: {wurzel}");

            foreach (var datei in Directory.EnumerateFiles(wurzel, "*.cs", SearchOption.AllDirectories))
            {
                if (IstAusgenommen(datei))
                    continue;

                durchsucht++;
                var zeilen = File.ReadAllLines(datei);
                for (var i = 0; i < zeilen.Length; i++)
                {
                    var getrimmt = zeilen[i].TrimStart();
                    if (getrimmt.StartsWith("//", StringComparison.Ordinal)
                        || getrimmt.StartsWith("*", StringComparison.Ordinal)
                        || getrimmt.StartsWith("[Obsolete(", StringComparison.Ordinal))
                        continue;

                    var zeile = OhneKommentar(zeilen[i]);
                    if (!Literal.IsMatch(zeile))
                        continue;

                    if (Protokollanweisung.IsMatch(Anweisung(zeilen, i)))
                        continue;

                    foreach (Match m in Literal.Matches(zeile))
                    {
                        var inhalt = m.Groups[1].Value;
                        if (GeschuetzteGanzeZeichenketten.Contains(inhalt))
                            continue;

                        var pruefbar = OhneAusdruecke(inhalt);
                        if (!pruefbar.Contains(' '))
                            continue;

                        foreach (Match wort in WortRegex.Matches(pruefbar))
                            treffer.Add($"{Path.GetFileName(datei)}:{i + 1}: \"{wort.Value}\" in \"{inhalt}\"");
                    }
                }
            }
        }

        Assert.True(durchsucht > 1000, $"Nur {durchsucht} Dateien durchsucht — Pfad pruefen.");
        Assert.True(treffer.Count == 0,
            "Ersatzschreibweise in Application/Infrastructure/Domain zurueckgefallen (Aufgabe 10c2):\n"
            + string.Join("\n", treffer));
    }

    [Fact]
    public void Jede_ausgenommene_Datei_existiert()
    {
        var alle = Schichten
            .SelectMany(s => Directory.EnumerateFiles(RepoFile("src", s), "*.cs", SearchOption.AllDirectories))
            .Select(Path.GetFileName)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var datei in AusgenommeneDateien)
            Assert.True(alle.Contains(datei), $"Ausgenommene Datei existiert nicht mehr: {datei}");
    }

    private static bool IstAusgenommen(string pfad)
    {
        var name = Path.GetFileName(pfad);
        var trenner = Path.DirectorySeparatorChar;
        return pfad.Contains($"{trenner}obj{trenner}", StringComparison.OrdinalIgnoreCase)
            || pfad.Contains($"{trenner}bin{trenner}", StringComparison.OrdinalIgnoreCase)
            || pfad.Contains($"{trenner}WebGis{trenner}", StringComparison.Ordinal)
            || name.Contains("WebGis", StringComparison.Ordinal)
            || AusgenommeneDateien.Contains(name);
    }

    /// <summary>Die Anweisung bis zum Ende der vorigen Anweisung (hoechstens 8 Zeilen zurueck).</summary>
    private static string Anweisung(string[] zeilen, int index)
    {
        var teile = new List<string> { zeilen[index] };
        for (var k = index - 1; k >= 0 && teile.Count < 8; k--)
        {
            var vorher = zeilen[k].TrimEnd();
            if (vorher.Length == 0 || vorher.EndsWith(';') || vorher.EndsWith('{') || vorher.EndsWith('}')
                || vorher.TrimStart().StartsWith("//", StringComparison.Ordinal))
                break;
            teile.Insert(0, vorher);
        }

        return string.Join(" ", teile.Select(t => t.Trim()));
    }

    /// <summary>Schneidet einen Zeilenkommentar ab, der ausserhalb eines Literals beginnt.</summary>
    private static string OhneKommentar(string zeile)
    {
        var imLiteral = false;
        for (var i = 0; i < zeile.Length - 1; i++)
        {
            if (imLiteral && zeile[i] == '\\')
            {
                i++;
                continue;
            }

            if (zeile[i] == '"')
                imLiteral = !imLiteral;
            else if (!imLiteral && zeile[i] == '/' && zeile[i + 1] == '/')
                return zeile[..i];
        }

        return zeile;
    }

    /// <summary>Ersetzt jeden <c>{...}</c>-Ausdruck (verschachtelt) durch Leerzeichen; <c>{{</c>/<c>}}</c> zaehlen nicht.</summary>
    private static string OhneAusdruecke(string inhalt)
    {
        var ergebnis = new StringBuilder(inhalt.Length);
        var i = 0;
        while (i < inhalt.Length)
        {
            if ((inhalt[i] == '{' || inhalt[i] == '}') && i + 1 < inhalt.Length && inhalt[i + 1] == inhalt[i])
            {
                ergebnis.Append("  ");
                i += 2;
                continue;
            }

            if (inhalt[i] == '{')
            {
                var tiefe = 1;
                var j = i + 1;
                while (j < inhalt.Length && tiefe > 0)
                {
                    if (inhalt[j] == '{') tiefe++;
                    else if (inhalt[j] == '}') tiefe--;
                    j++;
                }

                ergebnis.Append(' ', j - i);
                i = j;
                continue;
            }

            ergebnis.Append(inhalt[i]);
            i++;
        }

        return ergebnis.ToString();
    }
}
