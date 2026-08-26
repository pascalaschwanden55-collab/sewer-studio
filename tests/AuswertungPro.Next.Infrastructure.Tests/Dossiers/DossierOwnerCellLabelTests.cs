using AuswertungPro.Next.Application.Dossiers;
using AuswertungPro.Next.Domain.Models.Dossiers;
using AuswertungPro.Next.Infrastructure.Dossiers;

namespace AuswertungPro.Next.Infrastructure.Tests.Dossiers;

public sealed class DossierOwnerCellLabelTests
{
    [Fact]
    public void Beschriftungen_der_Eigentuemerzelle_sind_eigene_bearbeitbare_Werte()
    {
        var dossier = new DossierDefinition
        {
            Owners =
            [
                new DossierOwnerRow
                {
                    Phone = "041 123 45 67",
                    Mail = "person@example.ch",
                    Occupancy = "Mehrfamilienhaus"
                }
            ]
        };

        DossierOwnerCellLabels.SetText(
            dossier,
            DossierOwnerCellLabels.Phone,
            "Telefon:");
        DossierOwnerCellLabels.SetText(
            dossier,
            DossierOwnerCellLabels.Occupancy,
            string.Empty);

        var row = Assert.Single(DossierWordTemplateExportService.BuildOwnerRows(dossier));

        Assert.Contains("Telefon: 041 123 45 67", row["Eigentuemer_Zelle"]);
        Assert.Contains("Mail: person@example.ch", row["Eigentuemer_Zelle"]);
        Assert.Contains("Mehrfamilienhaus", row["Eigentuemer_Zelle"]);
        Assert.DoesNotContain("Objektbewohner:", row["Eigentuemer_Zelle"]);
        Assert.Equal("Telefon:", row[DossierOwnerCellLabels.Phone.CellKey]);
        Assert.Equal(string.Empty, row[DossierOwnerCellLabels.Occupancy.CellKey]);
    }

    [Fact]
    public void Formatierung_der_Beschriftung_und_des_Werts_bleibt_getrennt()
    {
        var dossier = new DossierDefinition
        {
            Owners =
            [
                new DossierOwnerRow
                {
                    Phone = "041",
                    FieldStyles =
                    {
                        ["Phone"] =
                        [
                            new DossierTextStyleRange
                            {
                                Start = 0,
                                Length = 3,
                                ColorHex = "C00000"
                            }
                        ]
                    }
                }
            ]
        };
        DossierOwnerCellLabels.SetFormatted(
            dossier,
            DossierOwnerCellLabels.Phone,
            "Telefon:",
            [new DossierTextStyleRange { Start = 0, Length = 8, Bold = true }]);

        var row = Assert.Single(DossierWordTemplateExportService.BuildOwnerRows(dossier));
        var ranges = DossierTopicTextFormatting.Decode(
            row["Eigentuemer_Zelle" + DossierTopicTextFormatting.StyleRangesSuffix]);

        Assert.Contains(ranges, range => range.Start == 0 && range.Length == 8 && range.Bold);
        Assert.Contains(ranges, range => range.Start == 9
            && range.Length == 3
            && range.ColorHex == "C00000");
    }
}
