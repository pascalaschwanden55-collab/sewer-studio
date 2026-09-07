using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Nova-Fixwelle 2b (P5): Sichtbare Beschriftungen schreiben echte Umlaute — auch die, die
/// nicht im XAML stehen, sondern in C# entstehen.
///
/// Der bestehende Umlaut-Waechter (<see cref="DesignAuditFeinschliffTests"/>) prueft nur
/// XAML-Attribute. In der Abnahme fielen deshalb zwei Stellen durch: „Lernbasis: 0 Faelle"
/// auf der Haltungsseite und die Gruppenbeschreibung „Bewertung, Schaeden und
/// Pruefresultate." in den Eingabefeldern der Schaechte.
///
/// Bewusst eine kurze Positivliste von Dateien statt eines Rundumschlags: Nur hier ist
/// belegt, dass JEDER mehrteilige Text eine sichtbare Beschriftung ist. Kommt eine weitere
/// Quelle sichtbarer Laufzeittexte dazu, gehoert sie in diese Liste.
///
/// Geprueft werden nur Zeichenketten MIT Leerzeichen. Feldnamen und Katalogschluessel
/// (<c>Gefaelle_Promille</c>, <c>VSA_Zustandsnote_D</c>) haben keine und bleiben unberuehrt —
/// sie sind Datenschluessel, keine Beschriftungen, und duerfen ihre Schreibweise nie aendern.
/// </summary>
public sealed class DesignAuditLaufzeittexteTests
{
    /// <summary>Dateien, deren mehrteilige Zeichenketten in sichtbare Beschriftungen fliessen.</summary>
    private static readonly string[][] Quellen =
    [
        ["src", "AuswertungPro.Next.Application", "DataPage", "LearningReadinessPresenter.cs"],
        ["src", "AuswertungPro.Next.UI", "DataPage", "SchaechteRecordDetailsBuilder.cs"],
        ["src", "AuswertungPro.Next.UI", "DataPage", "DataPageRecordDetailsBuilder.cs"]
    ];

    /// <summary>
    /// Deutsche Woerter in Ersatzschreibweise. Die Liste ist bewusst konkret: Ein blosses
    /// „ae/oe/ue irgendwo" traefe auch „Neu", „Quelle" oder „Muster".
    /// </summary>
    private static readonly string[] Ersatzschreibweisen =
    [
        "Faell", "Schaetz", "aehnlich", "Gruen", "Schaed", "Pruef", "Verknuepf",
        "Loesch", "Oeffn", "Groess", "Naechst", "Ueber", "Zustaend", "Maengel", "Bemuehung"
    ];

    [Fact]
    public void Sichtbare_Laufzeittexte_tragen_echte_Umlaute()
    {
        var funde = (from teile in Quellen
                     let pfad = RepoFile(teile)
                     from text in Zeichenketten(File.ReadAllText(pfad))
                     from wort in Ersatzschreibweisen
                     where text.Contains(wort, System.StringComparison.Ordinal)
                     select $"{Path.GetFileName(pfad)}: \"{text}\" ({wort})").ToList();

        Assert.True(funde.Count == 0, "Sichtbare Texte ohne Umlaut:\n" + string.Join("\n", funde));
    }

    /// <summary>
    /// Der Waechter darf nicht leerlaufen: Er muss in jeder gelisteten Datei wirklich Texte
    /// finden. Sonst wuerde eine umbenannte oder verschobene Datei still gruen bleiben.
    /// </summary>
    [Fact]
    public void Jede_gelistete_Datei_liefert_auch_wirklich_Texte()
    {
        foreach (var teile in Quellen)
        {
            var pfad = RepoFile(teile);
            Assert.True(File.Exists(pfad), $"{pfad} fehlt");
            Assert.NotEmpty(Zeichenketten(File.ReadAllText(pfad)));
        }
    }

    /// <summary>
    /// Alle Zeichenketten mit mindestens einem Leerzeichen, ohne Kommentarzeilen.
    /// Kommentare duerfen und sollen weiter in Ersatzschreibweise stehen (CLAUDE.md).
    /// </summary>
    private static string[] Zeichenketten(string quelle)
        => quelle.Split('\n')
            .Where(zeile => !zeile.TrimStart().StartsWith("//", System.StringComparison.Ordinal))
            .SelectMany(zeile => Regex.Matches(zeile, "\"([^\"\\\n]*)\"").Select(m => m.Groups[1].Value))
            .Where(text => text.Contains(' '))
            .ToArray();
}
