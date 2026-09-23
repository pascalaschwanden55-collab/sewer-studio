using AuswertungPro.Next.Application.Lookup;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Tests.WebGis;

/// <summary>GeoShop/QGIS liefern Normwerte; im Projekt steht danach der WebGIS-Begriff (sonst Scheinabweichungen im Vergleich).</summary>
public sealed class WebGisBegriffeKatasterTests
{
    [Fact]
    public void Kataster_normwerte_werden_webgis_begriffe()
    {
        var h = new QgisBauteil("H1", new Dictionary<string, string>
        {
            ["bw_status"] = "in_Betrieb", ["ha_lagebestimmung"] = "genau", ["ka_nutzungsart_ist"] = "Niederschlagsabwasser",
            ["ka_funktionhydraulisch"] = "Freispiegelleitung",
        });
        Assert.Equal("In Betrieb", QgisFeldKarte.Wert(h, FieldKeys.OperatingStatus, BauteilArt.Haltung));
        Assert.Equal("Genau", QgisFeldKarte.Wert(h, FieldKeys.PositionAccuracy, BauteilArt.Haltung));
        Assert.Equal("Regenabwasser", QgisFeldKarte.Wert(h, FieldKeys.UsageType, BauteilArt.Haltung));

        var s = new QgisBauteil("80461", new Dictionary<string, string> { ["ns_funktion"] = "Kontroll_Einsteigschacht", ["bw_status"] = "tot" });
        Assert.Equal("Kontrollschacht", QgisFeldKarte.Wert(s, "Funktion", BauteilArt.Schacht));
        Assert.Equal("Tot/Aufgehoben, verfüllt", QgisFeldKarte.Wert(s, FieldKeys.OperatingStatus, BauteilArt.Schacht));
    }
}
