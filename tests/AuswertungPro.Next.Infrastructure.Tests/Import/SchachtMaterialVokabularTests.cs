using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

public sealed class SchachtMaterialVokabularTests
{
    // Normschacht.Material kennt in der Modelldatei nur vier Werte - deutlich
    // weniger als Haltung.Material mit 24.
    [Theory]
    [InlineData("andere")]
    [InlineData("Beton")]
    [InlineData("Kunststoff")]
    [InlineData("unbekannt")]
    public void Jeder_Modellwert_kommt_unveraendert_zurueck(string norm)
    {
        Assert.Equal(norm, SchachtMaterialVokabular.NachNorm(SchachtMaterialVokabular.Normalisieren(norm)));
    }

    [Theory]
    [InlineData("Fertigbetonelement", "Beton")]
    [InlineData("Beton", "Beton")]
    [InlineData("Ortsbeton", "Beton")]
    [InlineData("Polyethylen", "Kunststoff")]
    [InlineData("Polypropylen", "Kunststoff")]
    [InlineData("GFK", "Kunststoff")]
    [InlineData("Gemauert", "andere")]
    public void Die_SchachtPro_Begriffe_finden_ihren_Normwert(string schachtPro, string norm)
    {
        Assert.Equal(norm, SchachtMaterialVokabular.NachNorm(SchachtMaterialVokabular.Normalisieren(schachtPro)));
    }

    [Theory]
    [InlineData("Fertigbetonelement")]
    [InlineData("Ortsbeton")]
    [InlineData("Gemauert")]
    public void Der_genaue_Begriff_bleibt_im_Programm_erhalten(string schachtPro)
    {
        // Die Norm kennt am Normschacht nur "Beton" - die Herstellungsart ist fuer
        // die Zustandsbeurteilung aber relevant und bleibt deshalb im Programm.
        Assert.Equal(schachtPro, SchachtMaterialVokabular.Normalisieren(schachtPro));
    }

    [Theory]
    [InlineData("Beton_unbekannt", "Beton")]
    [InlineData("Beton Normalbeton", "Beton")]
    [InlineData("Kunststoff_unbekannt", "Kunststoff")]
    public void Werte_aus_der_Haltungsliste_werden_auf_die_Schachtliste_gehoben(string gelesen, string norm)
    {
        // Der AWU-Export schreibt am Normschacht Beton_unbekannt (28080x) und
        // Kunststoff_unbekannt (526x). Beide stehen dort nicht in der Modelliste,
        // sondern in der von Haltung.Material. Gelesen werden muessen sie trotzdem.
        // "Beton Normalbeton" steht so in Zone 1.15 (10 Schaechte).
        Assert.Equal(norm, SchachtMaterialVokabular.NachNorm(SchachtMaterialVokabular.Normalisieren(gelesen)));
    }

    [Fact]
    public void Jeder_waehlbare_Eintrag_liefert_einen_gueltigen_Normwert_oder_gar_keinen()
    {
        var gueltig = new[] { "andere", "Beton", "Kunststoff", "unbekannt" };
        foreach (var eintrag in SchachtMaterialVokabular.Auswahl.Where(a => a.Length > 0))
        {
            var norm = SchachtMaterialVokabular.NachNorm(eintrag);
            Assert.True(norm is null || gueltig.Contains(norm),
                $"'{eintrag}' liefert '{norm}' - das ist kein Wert der Modelldatei.");
        }
    }
}
