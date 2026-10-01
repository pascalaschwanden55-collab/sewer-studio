using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Application.DataPage;
using AuswertungPro.Next.Application.Hydraulik;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DataPageHydraulikReportCalculatorTests
{
    [Theory]
    [InlineData("300", "")]
    [InlineData("", "5")]
    [InlineData("300", "0")]
    [InlineData("300", "-2")]
    [InlineData("300", "NaN")]
    [InlineData("300", "Infinity")]
    public void Bericht_erfindet_keine_fehlenden_oder_ungueltigen_projektwerte(string dn, string slope)
    {
        var record = new HaltungRecord();
        record.SetFieldValue(FieldKeys.NominalDiameterMm, dn, FieldSource.Manual, true);
        record.SetFieldValue(FieldKeys.SlopePromille, slope, FieldSource.Manual, true);
        var saved = false;
        var result = DataPageHydraulikReportCalculator.BuildReportCalculation(record,
            new HydraulikPanelSettings { Dn = 300, Gefaelle = 5 },
            saveSettings: () => saved = true);
        Assert.Null(result);
        Assert.False(saved);
        Assert.False(DataPageHydraulikReportCalculator.ReadAvailability(record).IsAvailable);
    }

    [Fact]
    public void Bericht_verwendet_das_gefaelle_der_haltung_statt_des_letzten_panels()
    {
        var record = new HaltungRecord();
        record.SetFieldValue(FieldKeys.NominalDiameterMm, "300", FieldSource.Manual, true);
        record.SetFieldValue(FieldKeys.SlopePromille, "2,5", FieldSource.Manual, true);
        var result = DataPageHydraulikReportCalculator.BuildReportCalculation(record,
            new HydraulikPanelSettings { Gefaelle = 7 });
        Assert.NotNull(result);
        Assert.Equal(2.5, result.Gefaelle_Promille);
    }

    [Fact]
    public void Dn_parsing_lebt_nicht_mehr_in_der_ui()
        => Assert.Null(typeof(DataPageHydraulikReportCalculator).GetMethod("ParseDnMm"));

    [Theory]
    [InlineData("5", 5)]
    [InlineData("4,5", 4.5)]
    [InlineData(" 12.25 ", 12.25)]
    public void ParseGefaellePromille_liefert_positive_gefaelle_werte(string raw, double expected)
    {
        Assert.Equal(expected, DataPageHydraulikReportCalculator.ParseGefaellePromille(raw));
    }

    [Fact]
    public void ReadAvailability_ist_nur_mit_positiver_dn_und_positivem_gefaelle_verfuegbar()
    {
        var record = new HaltungRecord();
        record.SetFieldValue("DN_mm", "1'200", FieldSource.Manual, userEdited: true);
        record.SetFieldValue("Gefaelle_Promille", "3,5", FieldSource.Manual, userEdited: true);

        var availability = DataPageHydraulikReportCalculator.ReadAvailability(record);

        Assert.True(availability.IsAvailable);
        Assert.Equal(1200, availability.DnMm);
        Assert.Equal(3.5, availability.GefaellePromille);
    }

    [Fact]
    public void BuildReportCalculation_nutzt_record_dn_gefaelle_material_settings_und_halbfuellung()
    {
        var record = new HaltungRecord();
        record.SetFieldValue("DN_mm", "400", FieldSource.Manual, userEdited: true);
        record.SetFieldValue("Rohrmaterial", "PVC", FieldSource.Manual, userEdited: true);
        record.SetFieldValue(FieldKeys.SlopePromille, "7", FieldSource.Manual, userEdited: true);
        var settings = new HydraulikPanelSettings
        {
            Gefaelle = 7,
            Temperatur = 12,
            IsNeuzustand = false,
            MaterialKey = "Beton"
        };

        var calculation = DataPageHydraulikReportCalculator.BuildReportCalculation(
            record,
            settings,
            dnMm: 400);

        Assert.NotNull(calculation);
        Assert.Equal(400, calculation.DN_mm);
        Assert.Equal(200, calculation.Wasserstand_mm);
        Assert.Equal(7, calculation.Gefaelle_Promille);
        Assert.Equal("Kunststoff (PVC/PE)", calculation.Material);
        Assert.Equal(12, calculation.Temperatur_C);
        Assert.True(calculation.Q_T > 0);
    }

    [Fact]
    public void BuildReportCalculation_persistiert_dn_und_material_ohne_gefaelle_als_wasserstand()
    {
        var record = new HaltungRecord();
        record.SetFieldValue("DN_mm", "400", FieldSource.Manual, userEdited: true);
        record.SetFieldValue("Rohrmaterial", "PVC", FieldSource.Manual, userEdited: true);
        record.SetFieldValue(FieldKeys.SlopePromille, "5", FieldSource.Manual, userEdited: true);
        var settings = new HydraulikPanelSettings
        {
            Wasserstand = 90,
            MaterialKey = "Beton"
        };
        var saveCalls = 0;

        DataPageHydraulikReportCalculator.BuildReportCalculation(
            record,
            settings,
            dnMm: 400,
            saveSettings: () => saveCalls++);

        Assert.Equal(1, saveCalls);
        Assert.Equal(400, settings.Dn);
        Assert.Equal("PVC/PE", settings.MaterialKey);
        Assert.Equal(90, settings.Wasserstand);
    }
}
