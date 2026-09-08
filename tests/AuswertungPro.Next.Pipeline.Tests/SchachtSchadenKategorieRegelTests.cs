using AuswertungPro.Next.Application.Reports;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Nova, Aufklapp-Liste (Task 5, Review-Nacharbeit): Der PDF-Schachtprotokollimport traegt im
/// Code den Bauteilnamen ("Konus", "Bankett"), nicht einen VSA-Code — ohne Text-Fallback zeigten
/// alle Schaeden dasselbe generische Symbol (Riss, schadhafter Anschluss und Ablagerung sahen
/// im Bild gleich aus).
/// </summary>
public sealed class SchachtSchadenKategorieRegelTests
{
    [Theory]
    [InlineData("gerissen", "crack")]
    [InlineData("Riss", "crack")]
    [InlineData("mangelhaft eingebunden", "offset")]
    [InlineData("Ablagerung", "deposit")]
    [InlineData("Ablagerungen", "deposit")]
    [InlineData("ausgebrochen", "break")]
    [InlineData("Infiltration", "infiltration")]
    [InlineData("Verkalkungen", "incrustation")]
    [InlineData("korrodiert", "surface")]
    [InlineData("lose", "offset")]
    public void Bekannte_Schadenstexte_des_PDF_Imports_ergeben_die_passende_Kategorie(string text, string erwartet)
    {
        Assert.Equal(erwartet, SchachtSchadenKategorieRegel.Bestimme(code: "Konus", beschreibung: text));
    }

    /// <summary>
    /// Bewusste Grenze (kein stilles Wissen): Woerter ohne eindeutige Entsprechung bleiben
    /// generisch statt geraten zu werden.
    /// </summary>
    [Theory]
    [InlineData("klemmt")]
    [InlineData("Überdeckt")]
    [InlineData("fehlt")]
    [InlineData("zu kurz")]
    [InlineData("defekt")]
    [InlineData("Fugen mangelhaft verputzt")]
    [InlineData("Mangelhaft ausgebildet")]
    [InlineData("ein voellig freier Text ohne bekannte Formulierung")]
    public void Unbekannte_Schadenstexte_bleiben_generisch(string text)
    {
        Assert.Equal("default", SchachtSchadenKategorieRegel.Bestimme(code: "Konus", beschreibung: text));
    }

    /// <summary>Ein echter VSA-Code (VSA-KEK-Import) hat Vorrang vor dem Text-Fallback.</summary>
    [Fact]
    public void Ein_VSA_Code_hat_Vorrang_vor_dem_Text_Fallback()
    {
        // BAB (Riss) hat ueber DamageSymbolClassifier bereits die Kategorie "crack"; ein davon
        // abweichender Beschreibungstext darf das nicht ueberschreiben.
        Assert.Equal("crack", SchachtSchadenKategorieRegel.Bestimme(code: "BAB", beschreibung: "Ablagerung"));
    }

    [Fact]
    public void Ohne_Code_und_ohne_Beschreibung_bleibt_es_generisch()
    {
        Assert.Equal("default", SchachtSchadenKategorieRegel.Bestimme(code: null, beschreibung: null));
    }
}
