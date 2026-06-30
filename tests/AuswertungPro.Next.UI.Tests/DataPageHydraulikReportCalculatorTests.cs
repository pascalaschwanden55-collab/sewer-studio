using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI;
using AuswertungPro.Next.UI.DataPage;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DataPageHydraulikReportCalculatorTests
{
    [Theory]
    [InlineData("300", 300)]
    [InlineData("1'200", 1200)]
    [InlineData("1.200,5", 1200.5)]
    [InlineData(" 450 ", 450)]
    public void ParseDnMm_liefert_positive_dn_werte(string raw, double expected)
    {
        Assert.Equal(expected, DataPageHydraulikReportCalculator.ParseDnMm(raw));
    }

    [Theory]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("-300")]
    [InlineData("abc")]
    public void ParseDnMm_liefert_null_bei_ungueltigen_werten(string raw)
    {
        Assert.Null(DataPageHydraulikReportCalculator.ParseDnMm(raw));
    }

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
        record.SetFieldValue("DN_mm", "400", FieldSource.Manual, userEdited: true);
        record.SetFieldValue("Gefaelle_Promille", "3,5", FieldSource.Manual, userEdited: true);

        var availability = DataPageHydraulikReportCalculator.ReadAvailability(record);

        Assert.True(availability.IsAvailable);
        Assert.Equal(400, availability.DnMm);
        Assert.Equal(3.5, availability.GefaellePromille);
    }

    [Fact]
    public void BuildReportCalculation_nutzt_record_dn_material_settings_und_halbfuellung()
    {
        var record = new HaltungRecord();
        record.SetFieldValue("DN_mm", "400", FieldSource.Manual, userEdited: true);
        record.SetFieldValue("Rohrmaterial", "PVC", FieldSource.Manual, userEdited: true);
        var settings = new AppSettings
        {
            HydraulikPanel = new HydraulikPanelSettings
            {
                Gefaelle = 7,
                Temperatur = 12,
                IsNeuzustand = false,
                MaterialKey = "Beton"
            }
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
    public void BuildReportCalculation_persistiert_panel_defaults_wie_bisheriger_print_pfad()
    {
        var record = new HaltungRecord();
        record.SetFieldValue("DN_mm", "400", FieldSource.Manual, userEdited: true);
        record.SetFieldValue("Rohrmaterial", "PVC", FieldSource.Manual, userEdited: true);
        var settings = new AppSettings
        {
            HydraulikPanel = new HydraulikPanelSettings
            {
                Wasserstand = 90,
                MaterialKey = "Beton"
            }
        };
        var saveCalls = 0;

        DataPageHydraulikReportCalculator.BuildReportCalculation(
            record,
            settings,
            dnMm: 400,
            panelWasserstandMm: 3.5,
            saveSettings: () => saveCalls++);

        Assert.Equal(1, saveCalls);
        Assert.Equal(400, settings.HydraulikPanel.Dn);
        Assert.Equal("PVC/PE", settings.HydraulikPanel.MaterialKey);
        Assert.Equal(3.5, settings.HydraulikPanel.Wasserstand);
    }
}
