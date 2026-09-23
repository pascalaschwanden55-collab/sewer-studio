using AuswertungPro.Next.Application.DataPage;
using AuswertungPro.Next.Application.Hydraulik;
using AuswertungPro.Next.Domain.Models;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Hydraulik;

/// <summary>
/// Audit A13 (23.09.2026): SewerStudio speichert das Rohrmaterial als Begriff aus MaterialVokabular
/// («Normalbeton», «Polyvinylchlorid» …). Die Hydraulik erkannte davon nur «Beton», «Steinzeug», «GFK» und
/// «Guss» und rechnete sonst still mit der zuletzt gewaehlten Einstellung — nach einer PVC-Haltung also eine
/// Betonhaltung mit Kunststoff-Rauheit (Abfluss rund 15 % daneben).
/// </summary>
public sealed class HydraulikMaterialZuordnungTests
{
    [Theory]
    [InlineData("Normalbeton", "Beton")]
    [InlineData("Spezialbeton", "Beton")]
    [InlineData("Ortsbeton", "Beton")]
    [InlineData("Pressrohrbeton", "Beton")]
    [InlineData("Beton", "Beton")]
    [InlineData("Steinzeug", "Steinzeug")]
    [InlineData("Polyvinylchlorid", "PVC/PE")]
    [InlineData("PVC", "PVC/PE")]
    [InlineData("Hartpolyethylen", "PVC/PE")]
    [InlineData("Polyethylen", "PVC/PE")]
    [InlineData("Polypropylen", "PVC/PE")]
    [InlineData("Kunststoff unbekannt", "PVC/PE")]
    [InlineData("Kunststoff", "PVC/PE")]
    [InlineData("GFK", "GFK")]
    [InlineData("gfk", "GFK")]
    [InlineData("Polyester GUP", "GFK")]
    [InlineData("Guss", "Guss")]
    [InlineData("Guss duktil", "Guss")]
    [InlineData("Grauguss", "Guss")]
    [InlineData("Beton, Normalbeton (BN)", "Beton")]      // WebGIS-Schreibweise «Gruppe, Detail (Kuerzel)»
    [InlineData("Kunststoff, Polypropylen (PP)", "PVC/PE")]
    public void Bekannte_werkstoffe_finden_ihre_rauheit(string rohrmaterial, string erwarteterSchluessel)
        => Assert.Equal(erwarteterSchluessel, HydraulikMaterialCatalog.Zuordnen(rohrmaterial)?.Key);

    [Theory]
    [InlineData("Ton")]            // stand wegen «Beton».Contains("ton") bisher bei Beton
    [InlineData("Stahl")]
    [InlineData("Stahl rostfrei")]
    [InlineData("Zement")]
    [InlineData("Faserzement")]
    [InlineData("Asbestzement")]
    [InlineData("Gebrannte Steine")]
    [InlineData("Epoxydharz")]
    [InlineData("andere")]
    [InlineData("unbekannt")]
    [InlineData("")]
    public void Werkstoffe_ohne_rauheitswert_werden_nicht_geraten(string rohrmaterial)
        => Assert.Null(HydraulikMaterialCatalog.Zuordnen(rohrmaterial));

    [Fact]
    public void Jeder_waehlbare_werkstoff_ist_entweder_zugeordnet_oder_bewusst_offen()
    {
        var offen = new HashSet<string>(StringComparer.Ordinal)
        {
            "Asbestzement", "Epoxydharz", "Faserzement", "Gebrannte Steine", "Stahl", "Stahl rostfrei", "Ton", "Zement",
            "andere", "unbekannt",
        };
        foreach (var wert in MaterialVokabular.Auswahl.Where(w => w.Length > 0))
            Assert.True(HydraulikMaterialCatalog.Zuordnen(wert) is not null || offen.Contains(wert),
                $"Werkstoff «{wert}» hat weder eine Rauheit noch steht er auf der Liste der bewusst offenen Werte.");
    }

    private static HaltungRecord Haltung(string material)
    {
        var h = new HaltungRecord();
        h.SetFieldValue(FieldKeys.NominalDiameterMm, "300", FieldSource.Manual, true);
        h.SetFieldValue(FieldKeys.SlopePromille, "10", FieldSource.Manual, true);
        h.SetFieldValue(FieldKeys.PipeMaterial, material, FieldSource.Manual, true);
        return h;
    }

    [Fact]
    public void Nach_einer_pvc_haltung_rechnet_eine_normalbeton_haltung_mit_beton()
    {
        var panel = new HydraulikPanelSettings { MaterialKey = "Beton", IsNeuzustand = false };

        DataPageHydraulikReportCalculator.BuildReportCalculation(Haltung("Polyvinylchlorid"), panel);
        var beton = DataPageHydraulikReportCalculator.BuildReportCalculation(Haltung("Normalbeton"), panel);

        Assert.NotNull(beton);
        Assert.Equal("Beton", beton!.Material);
        Assert.Equal(0.0015, beton.Kb, 6);
    }

    [Fact]
    public void Werkstoff_ohne_rauheit_wird_im_bericht_sichtbar_gemeldet()
    {
        var panel = new HydraulikPanelSettings { MaterialKey = "Steinzeug", IsNeuzustand = false };

        var stahl = DataPageHydraulikReportCalculator.BuildReportCalculation(Haltung("Stahl"), panel);

        Assert.NotNull(stahl);
        Assert.Contains("Steinzeug", stahl!.Material);
        Assert.Contains("«Stahl»", stahl.Material);
        Assert.Contains("nicht zugeordnet", stahl.Material);
    }
}
