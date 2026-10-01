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

    /// <summary>
    /// Ein echter VSA-Code (VSA-KEK-Import) hat Vorrang vor dem Text-Fallback — der Text-Fallback
    /// laeuft dabei ueberhaupt nicht (sonst koennte ein zufaellig passender Beschreibungstext die
    /// per Code bereits sichere Kategorie verfaelschen).
    /// </summary>
    [Fact]
    public void Ein_VSA_Code_hat_Vorrang_der_Text_Fallback_greift_gar_nicht()
    {
        // BAB (Riss) hat ueber DamageSymbolClassifier bereits die Kategorie "crack"; ein davon
        // abweichender Beschreibungstext darf das nicht ueberschreiben.
        Assert.Equal("crack", SchachtSchadenKategorieRegel.Bestimme(code: "BAB", beschreibung: "Ablagerung"));
    }

    /// <summary>
    /// Fix-Runde 2 (Review-Befund "hoch"): Der Text-Fallback darf NUR laufen, wenn der Code
    /// selbst ein bekannter Bauteilname des PDF-Schachtprotokollimports ist. Bei einem anderen,
    /// unbekannten Code bleibt es generisch, auch wenn die Beschreibung ein bekanntes Wort enthaelt.
    /// </summary>
    [Theory]
    [InlineData("XYZ")]
    [InlineData("")]
    [InlineData(null)]
    public void Ohne_bekannten_Bauteilnamen_als_Code_laeuft_der_Text_Fallback_nicht(string? code)
    {
        Assert.Equal("default", SchachtSchadenKategorieRegel.Bestimme(code, beschreibung: "Riss"));
    }

    /// <summary>
    /// Fix-Runde 2 (Review-Befund "hoch"): Ein Negationswort unmittelbar vor dem Begriff
    /// verhindert den Treffer — "kein Riss festgestellt" ist kein Riss.
    /// </summary>
    [Theory]
    [InlineData("kein Riss festgestellt")]
    [InlineData("keine Ablagerung")]
    [InlineData("nicht lose")]
    [InlineData("ohne Riss")]
    public void Eine_Verneinung_unmittelbar_vor_dem_Begriff_verhindert_den_Treffer(string text)
    {
        Assert.Equal("default", SchachtSchadenKategorieRegel.Bestimme(code: "Konus", beschreibung: text));
    }

    /// <summary>
    /// Fix-Runde 2 (Review-Befund "hoch"): Ohne unmittelbare Verneinung bleibt der Treffer
    /// bestehen — eine Verneinung an anderer Stelle im Satz darf den Begriff nicht entwerten.
    /// </summary>
    [Fact]
    public void Eine_Verneinung_an_anderer_Stelle_verhindert_den_Treffer_nicht()
    {
        Assert.Equal("crack", SchachtSchadenKategorieRegel.Bestimme(code: "Konus", beschreibung: "Riss, keine Sanierung noetig"));
    }

    /// <summary>
    /// Fix-Runde 2 (Review-Befund "hoch"): Eine Wortgrenze VOR dem Begriff statt blossem
    /// Contains — "Xriss" (der Begriff mitten in einem fremden Wort ohne Grenze davor) darf
    /// nicht als Riss zaehlen. Eine Pluralform DANACH ("Ablagerungen") bleibt dagegen bewusst
    /// erlaubt (siehe <c>Bekannte_Schadenstexte_des_PDF_Imports_ergeben_die_passende_Kategorie</c>).
    /// </summary>
    [Fact]
    public void Ein_Begriff_mitten_in_einem_fremden_Wort_ohne_Grenze_davor_zaehlt_nicht()
    {
        Assert.Equal("default", SchachtSchadenKategorieRegel.Bestimme(code: "Konus", beschreibung: "Xriss notiert"));
    }

    [Fact]
    public void Ohne_Code_und_ohne_Beschreibung_bleibt_es_generisch()
    {
        Assert.Equal("default", SchachtSchadenKategorieRegel.Bestimme(code: null, beschreibung: null));
    }
}
