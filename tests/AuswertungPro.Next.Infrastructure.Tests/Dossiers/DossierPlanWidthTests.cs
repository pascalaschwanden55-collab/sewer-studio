using AuswertungPro.Next.Domain.Models.Dossiers;
using AuswertungPro.Next.Infrastructure.Dossiers;

using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Dossiers;

public sealed class DossierPlanWidthTests
{
    [Fact]
    public void Ohne_eigene_Angabe_gilt_die_Breite_der_Vorlage()
    {
        Assert.Equal(
            DossierWordTemplateExportService.PlanMaxWidthCm,
            DossierWordTemplateExportService.PlanWidthCm(new DossierDefinition()));
    }

    [Theory]
    [InlineData(8.0, 8.0)]
    [InlineData(30.0, 30.0)]
    [InlineData(80.0, 30.0)]
    public void Eine_eigene_Breite_gilt_bis_zur_Blattgrenze(double eingabe, double erwartet)
    {
        var dossier = new DossierDefinition { OverviewPlanWidthCm = eingabe };

        Assert.Equal(erwartet, DossierWordTemplateExportService.PlanWidthCm(dossier));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-5.0)]
    public void Eine_unsinnige_Breite_faellt_auf_die_Vorlage_zurueck(double eingabe)
    {
        var dossier = new DossierDefinition { OverviewPlanWidthCm = eingabe };

        Assert.Equal(
            DossierWordTemplateExportService.PlanMaxWidthCm,
            DossierWordTemplateExportService.PlanWidthCm(dossier));
    }

    [Fact]
    public void Planhoehe_verwendet_das_feste_Verhaeltnis_der_Referenz()
    {
        Assert.Equal(
            7_741_920d / 360_000d,
            DossierWordTemplateExportService.PlanHeightForWidth(5_402_580d / 360_000d),
            precision: 8);
    }
}
