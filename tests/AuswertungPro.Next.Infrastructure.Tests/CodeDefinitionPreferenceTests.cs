using AuswertungPro.Next.Application.Protocol;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Charakterisierungstests fuer <see cref="CodeDefinitionPreference"/>.
/// Wichtigstes Ziel: die absichtliche Score-Divergenz zwischen Json- und Xml-Provider
/// (Json zaehlt Examples, Xml nicht) per Test festnageln.
/// </summary>
public sealed class CodeDefinitionPreferenceTests
{
    // ── Score-Divergenz ────────────────────────────────────────────────────

    [Fact]
    public void ScoreWithExamples_ZaehltExamples_ScoreWithoutExamples_Nicht()
    {
        // Definition mit zwei Examples, sonst identisch
        var def = new CodeDefinition
        {
            Code = "BAB",
            Title = "Riss",          // +3 (Title != Code)
            Examples = new List<string> { "Bsp1", "Bsp2" }  // +2 nur bei Json
        };

        var jsonScore = CodeDefinitionPreference.ScoreWithExamples(def);
        var xmlScore = CodeDefinitionPreference.ScoreWithoutExamples(def);

        Assert.Equal(jsonScore - 2, xmlScore);
        Assert.Equal(5, jsonScore);   // 3 (Title) + 2 (Examples)
        Assert.Equal(3, xmlScore);    // 3 (Title), kein Examples-Beitrag
    }

    [Fact]
    public void ScoreWithExamples_OhneExamples_GleichWieScoreWithoutExamples()
    {
        // Ohne Examples liefern beide Score-Varianten dasselbe Ergebnis
        var def = new CodeDefinition
        {
            Code = "BCD",
            Title = "Rohranfang",
            Description = "Kamera faehrt ein",
            Group = "Bestand"
        };

        Assert.Equal(
            CodeDefinitionPreference.ScoreWithoutExamples(def),
            CodeDefinitionPreference.ScoreWithExamples(def));
    }

    // ── Choose-Logik ───────────────────────────────────────────────────────

    [Fact]
    public void Choose_HoeheresScore_GewinntUnabhaengigVonReihenfolge()
    {
        var arm = new CodeDefinition { Code = "BCA", Title = "BCA" };   // Score = 0
        var reich = new CodeDefinition
        {
            Code = "BCA",
            Title = "Anschluss",      // +3
            Description = "Seitlich"  // +2
        };

        // reich gewinnt egal ob first oder second
        Assert.Same(reich, CodeDefinitionPreference.Choose(arm, reich, CodeDefinitionPreference.ScoreWithExamples));
        Assert.Same(reich, CodeDefinitionPreference.Choose(reich, arm, CodeDefinitionPreference.ScoreWithExamples));
    }

    [Fact]
    public void Choose_GleicherScore_LaengereBeschreibungGewinnt()
    {
        var kurz = new CodeDefinition { Code = "BAF", Title = "Schaden", Description = "Kurz" };
        var lang = new CodeDefinition { Code = "BAF", Title = "Schaden", Description = "Deutlich laengere Beschreibung" };

        Assert.Same(lang, CodeDefinitionPreference.Choose(kurz, lang, CodeDefinitionPreference.ScoreWithExamples));
        Assert.Same(lang, CodeDefinitionPreference.Choose(lang, kurz, CodeDefinitionPreference.ScoreWithExamples));
    }

    [Fact]
    public void Choose_VollstaendigerGleichstand_GewinntFirst()
    {
        var a = new CodeDefinition { Code = "BBC", Title = "Ablagerung" };
        var b = new CodeDefinition { Code = "BBC", Title = "Ablagerung" };

        // Bei vollstaendigem Gleichstand bleibt first
        Assert.Same(a, CodeDefinitionPreference.Choose(a, b, CodeDefinitionPreference.ScoreWithExamples));
    }

    // ── Json-Szenario: Examples kippen die Wahl ────────────────────────────

    [Fact]
    public void JsonScore_ExamplesKippenWahl_XmlScoreTut_Es_Nicht()
    {
        // Ohne Examples: 'mitBeschreibung' hat hoeheren BaseScore (Description +2)
        // Mit Examples: 'mitExamples' erhalt +2 und holt auf.
        // Szenario: mitBeschreibung (Score=2, kein Title-Bonus) vs mitExamples (Score=2 bei Xml, 4 bei Json)
        var mitBeschreibung = new CodeDefinition
        {
            Code = "BBA",
            Title = "BBA",             // Title == Code -> kein +3
            Description = "Wurzeln"   // +2
        };
        var mitExamples = new CodeDefinition
        {
            Code = "BBA",
            Title = "BBA",             // Title == Code -> kein +3
            Examples = new List<string> { "E1", "E2" }   // +2 nur bei Json
        };

        // Xml: beide Score=2, Gleichstand -> Description-Laenge entscheidet -> mitBeschreibung gewinnt
        var xmlWahl = CodeDefinitionPreference.Choose(mitBeschreibung, mitExamples, CodeDefinitionPreference.ScoreWithoutExamples);
        Assert.Same(mitBeschreibung, xmlWahl);

        // Json: mitExamples Score=2, mitBeschreibung Score=2 -> Gleichstand, aber Description-Laenge von mitBeschreibung
        // -> also gleicher Ausgang hier (first = mitBeschreibung gewinnt)
        // Anderes Szenario: mitExamples hat 2 Examples -> jsonScore=2; mitBeschreibung has Description -> jsonScore=2
        // -> Gleichstand, dann Description: mitBeschreibung hat Laenge > 0, mitExamples hat 0 -> mitBeschreibung gewinnt auch hier
        // Wir drehen die Reihenfolge (mitExamples first):
        var jsonWahl = CodeDefinitionPreference.Choose(mitExamples, mitBeschreibung, CodeDefinitionPreference.ScoreWithExamples);
        // Bei Json: mitExamples Score=2, mitBeschreibung Score=2 -> Gleichstand
        // Description: mitBeschreibung.Description.Length > mitExamples.Description.Length (0)
        // -> mitBeschreibung gewinnt
        Assert.Same(mitBeschreibung, jsonWahl);

        // Szenario wo Examples tatsaechlich kippen: Examples >= 2 und Gegner kein Description
        var ohneBeides = new CodeDefinition { Code = "BBA", Title = "BBA" };  // Score=0
        var nurExamples = new CodeDefinition
        {
            Code = "BBA",
            Title = "BBA",
            Examples = new List<string> { "E1", "E2" }  // +2 nur Json
        };

        // Xml: beide 0 -> Gleichstand -> first gewinnt
        Assert.Same(ohneBeides, CodeDefinitionPreference.Choose(ohneBeides, nurExamples, CodeDefinitionPreference.ScoreWithoutExamples));
        // Json: nurExamples Score=2 > ohneBeides Score=0 -> nurExamples gewinnt
        Assert.Same(nurExamples, CodeDefinitionPreference.Choose(ohneBeides, nurExamples, CodeDefinitionPreference.ScoreWithExamples));
    }
}
