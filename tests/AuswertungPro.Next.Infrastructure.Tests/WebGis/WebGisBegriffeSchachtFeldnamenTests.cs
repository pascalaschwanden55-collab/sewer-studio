using AuswertungPro.Next.Application.WebGis;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Tests.WebGis;

/// <summary>
/// Audit A14 (23.09.2026): Schachtfelder heissen nach der Kopfzeile der Excel-Vorlage, nicht nach dem
/// Katalog. «STATUS» oder «Status » fand die WebGIS-Umwandlung nicht: <c>in_Betrieb</c> blieb stehen, und
/// beim Senden fiel das Feld aus der Regel «nur zeichengenaue WebGIS-Begriffe» heraus.
/// </summary>
public sealed class WebGisBegriffeSchachtFeldnamenTests
{
    private const string SchachtStatusRef = "576d3dff-e216-c25f-3e55-16d93027ca3d";

    [Theory]
    [InlineData("Status")]
    [InlineData("STATUS")]
    [InlineData("Status ")]
    [InlineData("status")]
    public void Status_wird_unter_jeder_vorlagenschreibweise_zum_webgis_begriff(string feld)
    {
        var s = new SchachtRecord();
        s.SetFieldValue(feld, "in_Betrieb", FieldSource.Xtf405, userEdited: false);
        Assert.Equal("In Betrieb", s.GetFieldValue(feld));
    }

    [Fact]
    public void Auch_der_kompatibilitaetsweg_und_das_nachfuellen_wandeln_um()
    {
        var s = new SchachtRecord();
        s.SetFieldValue("NUTZUNGSART", "Niederschlagsabwasser");
        Assert.True(s.FuelleLeeresFeld("LAGEBESTIMMUNG", "genau", FieldSource.Kataster));

        Assert.Equal("Regenabwasser", s.GetFieldValue("NUTZUNGSART"));
        Assert.Equal("Genau", s.GetFieldValue("LAGEBESTIMMUNG"));
    }

    [Fact]
    public void Funktion_in_grossbuchstaben_wird_beim_normschacht_umgewandelt()
    {
        var s = new SchachtRecord();
        s.SetFieldValue("BAUWERKSART", "Normschacht", FieldSource.Xtf405, userEdited: false);
        s.SetFieldValue("FUNKTION", "Pumpwerk", FieldSource.Xtf405, userEdited: false);
        Assert.Equal("Pumpenschacht", s.GetFieldValue("FUNKTION"));
    }

    [Fact]
    public void Ein_fremdes_feld_mit_aehnlichem_namen_bleibt_unberuehrt()
    {
        var s = new SchachtRecord();
        s.SetFieldValue("Status\noffen/abgeschlossen", "in_Betrieb", FieldSource.Manual, userEdited: true);
        Assert.Equal("in_Betrieb", s.GetFieldValue("Status\noffen/abgeschlossen"));
    }

    [Fact]
    public void Beim_senden_gilt_die_begriffsregel_auch_fuer_grossgeschriebene_feldnamen()
    {
        var stand = new WebGisLesestand { GlobalId = "G1", Bezeichnung = "80409" };
        stand.Felder[SchachtStatusRef] = "0";
        stand.Kataloge[SchachtStatusRef] = new List<(string, string)> { ("0", "Unbekannt"), ("1", "In Betrieb") };
        var e = new WebGisObjektEingabe
        {
            Objektart = WebGisObjektart.Schacht, Bezeichnung = "80409", Handwerte = { ["STATUS"] = "in_Betrieb" },
        };

        var pos = WebGisExportPlanBuilder.Baue(e, stand);

        Assert.DoesNotContain(pos.Aenderungen, a => a.RefId == SchachtStatusRef);
        Assert.Contains(pos.Hinweise, h => h.Contains("kein WebGIS-Begriff"));
    }
}
