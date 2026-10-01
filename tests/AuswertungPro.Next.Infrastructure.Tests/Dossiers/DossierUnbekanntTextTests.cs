using AuswertungPro.Next.Application.Dossiers;

namespace AuswertungPro.Next.Infrastructure.Tests.Dossiers;

/// <summary>
/// Eine leere Zelle im fertigen Dossier war bisher einfach leer. Der Eigentuemer
/// sieht dann nicht, ob die Angabe fehlt oder ob sie vergessen wurde. Fehlende
/// Angaben stehen deshalb als „unbekannt" im Blatt - genau wie in der Wordvorlage.
/// </summary>
public sealed class DossierUnbekanntTextTests
{
    [Theory]
    [InlineData("Text")]
    [InlineData("Eigentuemer_Zelle")]
    [InlineData("Aktennotiz")]
    public void Ein_leeres_Feld_wird_zu_unbekannt(string feld)
    {
        var werte = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [feld] = ""
        };

        var ergebnis = DossierUnbekanntText.Anwenden(werte);

        Assert.Equal("unbekannt", ergebnis[feld]);
    }

    [Fact]
    public void Nur_Leerraum_zaehlt_ebenfalls_als_leer()
    {
        var werte = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Aktennotiz"] = "   \t "
        };

        Assert.Equal("unbekannt", DossierUnbekanntText.Anwenden(werte)["Aktennotiz"]);
    }

    [Fact]
    public void Ein_gefuelltes_Feld_bleibt_unveraendert()
    {
        var werte = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Text"] = "Sanierung im Frühjahr",
            ["Eigentuemer_Zelle"] = "Heinz Meier"
        };

        var ergebnis = DossierUnbekanntText.Anwenden(werte);

        Assert.Equal("Sanierung im Frühjahr", ergebnis["Text"]);
        Assert.Equal("Heinz Meier", ergebnis["Eigentuemer_Zelle"]);
    }

    [Theory]
    [InlineData("Gebiet_Ort")]
    [InlineData("Gebietstitel")]
    public void Die_Deckblattfelder_bleiben_leer(string feld)
    {
        // Auf dem Deckblatt stand daraufhin zweimal "unbekannt" in 40 Punkt - als
        // Titel eines Briefs an den Eigentuemer. Ein leerer Titel ist besser als ein
        // falscher. Ausserdem tragen dort mehrere freie Felder denselben Text, und
        // die Klickzuordnung erkennt ein Feld am Text: gleiche Texte verschiedener
        // Felder bleiben dort bewusst ohne Treffer, das Feld waere unanklickbar.
        // In den Tabellen ist "unbekannt" dagegen sicher - dort loest der
        // Tabellenmapper die Zelle ueber Spalte und Zeile auf.
        var werte = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [feld] = ""
        };

        Assert.Equal("", DossierUnbekanntText.Anwenden(werte)[feld]);
    }

    [Theory]
    [InlineData("Haus_Nr")]
    [InlineData("Pz_Nr")]
    public void Die_schmalen_Zahlenspalten_bleiben_leer(string feld)
    {
        // "unbekannt" sprengt die schmale Spalte und wuerde die Zeile umbrechen.
        var werte = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [feld] = ""
        };

        Assert.Equal("", DossierUnbekanntText.Anwenden(werte)[feld]);
    }

    [Theory]
    [InlineData("Datum")]
    [InlineData("Autoren")]
    [InlineData("Fusszeile")]
    [InlineData("Rueckmeldung")]
    [InlineData("Thema")]
    public void Felder_ohne_Fachaussage_bleiben_unveraendert(string feld)
    {
        // Ein leeres Datum oder eine leere Fusszeile ist keine fehlende Angabe
        // des Eigentuemers - dort waere "unbekannt" nur Laerm.
        var werte = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [feld] = ""
        };

        Assert.Equal("", DossierUnbekanntText.Anwenden(werte)[feld]);
    }

    [Fact]
    public void Ein_fehlendes_Feld_wird_nicht_erfunden()
    {
        var ergebnis = DossierUnbekanntText.Anwenden(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

        Assert.Empty(ergebnis);
    }
}
