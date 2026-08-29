using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

/// <summary>
/// Gemessen am AWU-Kantonsexport: 21 echte Materialwerte auf 109871 Haltungen.
/// Frueher fielen 14510 davon auf "Beton" oder "Guss" zusammen und waren damit
/// nicht mehr rueckfuehrbar.
/// </summary>
public sealed class MaterialVokabularTests
{
    // Alle 21 Werte, die im AWU-Kantonsexport wirklich vorkommen.
    [Theory]
    [InlineData("unbekannt")]
    [InlineData("Kunststoff_Hartpolyethylen")]
    [InlineData("Kunststoff_Polyvinilchlorid")]
    [InlineData("Kunststoff_Polypropylen")]
    [InlineData("Beton_Normalbeton")]
    [InlineData("Kunststoff_Polyethylen")]
    [InlineData("Beton_unbekannt")]
    [InlineData("Kunststoff_unbekannt")]
    [InlineData("Beton_Spezialbeton")]
    [InlineData("Kunststoff_Polyester_GUP")]
    [InlineData("Steinzeug")]
    [InlineData("andere")]
    [InlineData("Faserzement")]
    [InlineData("Beton_Ortsbeton")]
    [InlineData("Asbestzement")]
    [InlineData("Stahl")]
    [InlineData("Guss_duktil")]
    [InlineData("Guss_Grauguss")]
    [InlineData("Kunststoff_Epoxydharz")]
    [InlineData("Gebrannte_Steine")]
    [InlineData("Stahl_rostfrei")]
    public void Jeder_Kantonswert_kommt_unveraendert_zurueck(string norm)
    {
        // Der ganze Zweck: einlesen, anzeigen, unveraendert zurueckschreiben.
        var app = MaterialVokabular.Normalisieren(norm);
        Assert.Equal(norm, MaterialVokabular.NachNorm(app));
    }

    [Fact]
    public void Die_vier_Betonarten_bleiben_unterscheidbar()
    {
        var betonarten = new[] { "Beton_Normalbeton", "Beton_Spezialbeton", "Beton_Ortsbeton", "Beton_unbekannt" };
        var app = betonarten.Select(MaterialVokabular.Normalisieren).ToArray();

        Assert.Equal(4, app.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Die_zwei_Gussarten_bleiben_unterscheidbar()
    {
        Assert.NotEqual(
            MaterialVokabular.Normalisieren("Guss_duktil"),
            MaterialVokabular.Normalisieren("Guss_Grauguss"));
    }

    [Fact]
    public void Asbestzement_ist_kein_Zement()
    {
        Assert.NotEqual(
            MaterialVokabular.Normalisieren("Asbestzement"),
            MaterialVokabular.Normalisieren("Zement"));
    }

    [Theory]
    [InlineData("PVC", "Kunststoff_Polyvinilchlorid")]
    [InlineData("Polyvinylchlorid (PVC)", "Kunststoff_Polyvinilchlorid")]
    [InlineData("PE", "Kunststoff_Polyethylen")]
    [InlineData("Polyethylen (PE)", "Kunststoff_Polyethylen")]
    [InlineData("Hart-Polyethylen (HDPE)", "Kunststoff_Hartpolyethylen")]
    [InlineData("PE-HD", "Kunststoff_Hartpolyethylen")]
    [InlineData("PP", "Kunststoff_Polypropylen")]
    [InlineData("Beton", "Beton_unbekannt")]
    [InlineData("Normalbeton", "Beton_Normalbeton")]
    public void Altwerte_und_Fremdschreibweisen_finden_ihren_Normwert(string gelesen, string norm)
    {
        // Bestehende Projekte, WinCan und SchachtPro schreiben diese Formen.
        Assert.Equal(norm, MaterialVokabular.NachNorm(MaterialVokabular.Normalisieren(gelesen)));
    }

    [Theory]
    [InlineData("Guss")]
    [InlineData("GFK")]
    [InlineData("Glasfaser")]
    public void Ein_Altwert_ohne_sicheres_Gegenstueck_wird_nicht_geraten(string altwert)
    {
        // "Guss" allein sagt nicht, ob duktil oder Grauguss. "GFK" ist nicht dasselbe
        // wie Kunststoff_Polyester_GUP. Lieber nichts schreiben als raten - der Wert
        // bleibt im Programm sichtbar.
        //
        // Korrigiert 2026-08-29: "Ton" und "Zement" standen hier ebenfalls. Die
        // Modelldatei fuehrt beide als gueltige Werte - sie fehlten nur in der
        // Kantonsauszaehlung. Meine Annahme war falsch, nicht die Norm.
        Assert.Equal(altwert, MaterialVokabular.Normalisieren(altwert));
        Assert.Null(MaterialVokabular.NachNorm(altwert));
    }

    [Fact]
    public void Ein_unbekannter_Wert_bleibt_stehen_statt_verloren_zu_gehen()
    {
        Assert.Equal("Blaustein", MaterialVokabular.Normalisieren("Blaustein"));
        Assert.Null(MaterialVokabular.NachNorm("Blaustein"));
    }

    [Fact]
    public void Die_Auswahlliste_enthaelt_alle_Konzepte_und_die_Altwerte()
    {
        Assert.Contains("Normalbeton", MaterialVokabular.Auswahl);
        Assert.Contains("Asbestzement", MaterialVokabular.Auswahl);
        Assert.Contains("Guss", MaterialVokabular.Auswahl);      // Altwert, bleibt waehlbar
        Assert.Contains("", MaterialVokabular.Auswahl);          // leer erlaubt
    }

    [Fact]
    public void Jeder_Begriff_der_Auswahlliste_ist_auch_wieder_lesbar()
    {
        // Waechter gegen die Falle von oben: ein Wert der Liste, den das Vokabular
        // selbst nicht mehr wiederfindet. Kurzformen wie "PVC" duerfen dabei auf
        // ihren Langbegriff zeigen - sie muessen nur in der Liste bleiben.
        foreach (var app in MaterialVokabular.Auswahl.Where(a => a.Length > 0))
            Assert.Contains(MaterialVokabular.Normalisieren(app), MaterialVokabular.Auswahl);
    }

    // Alle 24 Werte der Modelldatei SIA405_Abwasser_2020_2_d_LV95 fuer Haltung.Material.
    // Die Kantonsauszaehlung zeigte nur 21 - sie sagt, was vorkommt, nicht was erlaubt ist.
    [Theory]
    [InlineData("andere")]
    [InlineData("Asbestzement")]
    [InlineData("Beton_Normalbeton")]
    [InlineData("Beton_Ortsbeton")]
    [InlineData("Beton_Pressrohrbeton")]
    [InlineData("Beton_Spezialbeton")]
    [InlineData("Beton_unbekannt")]
    [InlineData("Faserzement")]
    [InlineData("Gebrannte_Steine")]
    [InlineData("Guss_duktil")]
    [InlineData("Guss_Grauguss")]
    [InlineData("Kunststoff_Epoxydharz")]
    [InlineData("Kunststoff_Hartpolyethylen")]
    [InlineData("Kunststoff_Polyester_GUP")]
    [InlineData("Kunststoff_Polyethylen")]
    [InlineData("Kunststoff_Polypropylen")]
    [InlineData("Kunststoff_Polyvinilchlorid")]
    [InlineData("Kunststoff_unbekannt")]
    [InlineData("Stahl")]
    [InlineData("Stahl_rostfrei")]
    [InlineData("Steinzeug")]
    [InlineData("Ton")]
    [InlineData("unbekannt")]
    [InlineData("Zement")]
    public void Jeder_Modellwert_kommt_unveraendert_zurueck(string norm)
    {
        var app = MaterialVokabular.Normalisieren(norm);
        Assert.Equal(norm, MaterialVokabular.NachNorm(app));
    }
}
