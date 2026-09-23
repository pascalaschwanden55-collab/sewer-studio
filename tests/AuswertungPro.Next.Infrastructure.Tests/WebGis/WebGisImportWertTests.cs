using AuswertungPro.Next.Application.WebGis;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.WebGis;

/// <summary>
/// WebGIS-Klartext -> SewerStudio-Wert beim Holen (23.09.2026). Das WebGIS schreibt «Sammelkanal»,
/// «In Betrieb», «Normalbeton (NB)»; SewerStudio fuehrt «PAA.Sammelkanal», «in_Betrieb», seine eigenen
/// Materialbegriffe. Ein Wert, der zu keinem SewerStudio-Wert passt, kommt nicht hinein — nie raten.
/// </summary>
public sealed class WebGisImportWertTests
{
    [Theory]
    [InlineData("FunktionHierarchisch", "Sammelkanal", "PAA.Sammelkanal")]
    // Seit 23.09.2026 (Schritt A) bleibt der WebGIS-Text woertlich; FunktionHierarchisch folgt in Schritt C.
    [InlineData("Status", "In Betrieb", "In Betrieb")]
    [InlineData("Profiltyp", "Kreisprofil (K)", "Kreisprofil (K)")]
    [InlineData("Nutzungsart", "Mischabwasser", "Mischabwasser")]
    [InlineData("Lagebestimmung", "Genau", "Genau")]
    public void Haltung_auswahlwerte_werden_auf_den_sewerstudio_begriff_gebracht(string feld, string webgis, string erwartet)
    {
        Assert.Equal(erwartet, WebGisImportWert.Zuordne(WebGisObjektart.Haltung, feld, webgis, out var hinweis));
        Assert.Null(hinweis);
    }

    [Theory]
    [InlineData("Material", "Polypropylen", "Polypropylen")]
    [InlineData("Funktion", "Kontrollschacht", "Kontrollschacht")]
    [InlineData("Schachtform", "rund", "Rund")]
    public void Schacht_verwendet_seine_eigenen_listen(string feld, string webgis, string erwartet)
        => Assert.Equal(erwartet, WebGisImportWert.Zuordne(WebGisObjektart.Schacht, feld, webgis, out _));

    [Fact]
    public void Mehrdeutiger_blattname_wird_nicht_geraten()
    {
        // «Strassenentwaesserung» gibt es unter PAA UND SAA.
        Assert.Null(WebGisImportWert.Zuordne(WebGisObjektart.Haltung, "FunktionHierarchisch", "Strassenentwässerung", out var hinweis));
        Assert.Contains("Strassenentwässerung", hinweis);
    }

    [Fact]
    public void Unbekannter_wert_kommt_nicht_hinein_und_wird_gemeldet()
    {
        Assert.Null(WebGisImportWert.Zuordne(WebGisObjektart.Haltung, "Status", "Im Bau erfunden", out var hinweis));
        Assert.Contains("Im Bau erfunden", hinweis);
    }

    [Theory]
    [InlineData("unbekannt")]
    [InlineData("Unbekannt")]
    [InlineData("  ")]
    public void Unbekannt_und_leer_fuellen_nichts_und_melden_nichts(string webgis)
    {
        Assert.Null(WebGisImportWert.Zuordne(WebGisObjektart.Haltung, "Status", webgis, out var hinweis));
        Assert.Null(hinweis);
    }

    [Theory]
    [InlineData(WebGisObjektart.Schacht, "Tiefe", "1,96", "1.96")]
    [InlineData(WebGisObjektart.Schacht, "Dimension 1 mm", "800", "800")]
    [InlineData(WebGisObjektart.Haltung, "DN_mm", "300.0", "300")]
    public void Zahlen_werden_mit_punkt_uebernommen(WebGisObjektart art, string feld, string webgis, string erwartet)
        => Assert.Equal(erwartet, WebGisImportWert.Zuordne(art, feld, webgis, out _));

    [Fact]
    public void Keine_zahl_in_einem_zahlenfeld_wird_gemeldet()
    {
        Assert.Null(WebGisImportWert.Zuordne(WebGisObjektart.Schacht, "Tiefe", "tief", out var hinweis));
        Assert.Contains("tief", hinweis);
    }

    // Zone 1.15, 23.09.2026: das WebGIS schreibt Materialdetails als «Gruppe, Detail (Kuerzel)».
    [Theory]
    [InlineData(WebGisObjektart.Haltung, "Rohrmaterial", "Beton, unbekannt (BU)", "Beton")]
    [InlineData(WebGisObjektart.Haltung, "Rohrmaterial", "Kunststoff, unbekannt (KUU)", "Kunststoff unbekannt")]
    [InlineData(WebGisObjektart.Schacht, "Material", "Beton, Fertigteil", "Fertigbetonelement")]
    public void Materialdetail_in_webgis_schreibweise_wird_erkannt(WebGisObjektart art, string feld, string webgis, string erwartet)
        => Assert.Equal(erwartet, WebGisImportWert.Zuordne(art, feld, webgis, out _));

    [Fact]
    public void Material_ohne_sewerstudio_begriff_bleibt_hinweis()
    {
        Assert.Null(WebGisImportWert.Zuordne(WebGisObjektart.Haltung, "Rohrmaterial", "Schleuderbeton (SBR)", out var hinweis));
        Assert.Contains("Schleuderbeton", hinweis);
    }

    [Theory]
    [InlineData("PAA", "PAA.Liegenschaftsentwaesserung")]
    [InlineData("SAA", "SAA.Liegenschaftsentwaesserung")]
    public void Typ_aa_entscheidet_zwischen_paa_und_saa(string typAa, string erwartet)
        => Assert.Equal(erwartet, WebGisImportWert.Zuordne(WebGisObjektart.Haltung, "FunktionHierarchisch",
            "Liegenschaftsentwässerung", out _, typAa));

    [Theory]
    [InlineData(WebGisObjektart.Haltung, "Status", "In Betrieb")]
    [InlineData(WebGisObjektart.Haltung, "Profiltyp", "Kreisprofil (K)")]
    [InlineData(WebGisObjektart.Haltung, "Nutzungsart", "Regenabwasser")]
    [InlineData(WebGisObjektart.Haltung, "FunktionHydraulisch", "Dükerleitung")]
    [InlineData(WebGisObjektart.Schacht, "Status", "Tot/Aufgehoben, verfüllt")]
    [InlineData(WebGisObjektart.Schacht, "Funktion", "Absturzschacht")]
    public void Webgis_begriff_wird_woertlich_uebernommen(WebGisObjektart art, string feld, string text)
    {
        Assert.Equal(text, WebGisImportWert.Zuordne(art, feld, text, out var hinweis));
        Assert.Null(hinweis);
    }

    [Fact]
    public void Text_ausserhalb_der_bekannten_webgis_liste_wird_gemeldet_nicht_eingetragen()
    {
        Assert.Null(WebGisImportWert.Zuordne(WebGisObjektart.Haltung, "Status", "Im Bau", out var hinweis));
        Assert.Contains("nicht in der bekannten WebGIS-Liste", hinweis);
    }

    [Fact]
    public void Unbekannt_fuellt_weiterhin_nichts()
    {
        Assert.Null(WebGisImportWert.Zuordne(WebGisObjektart.Haltung, "Status", "Unbekannt", out var hinweis));
        Assert.Null(hinweis);
    }
}
