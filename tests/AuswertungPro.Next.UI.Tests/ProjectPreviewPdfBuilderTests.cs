using System.IO;
using System.Text;
using AuswertungPro.Next.Application.Dashboard;
using AuswertungPro.Next.Domain.Models;
using UglyToad.PdfPig;

namespace AuswertungPro.Next.UI.Tests;

public sealed class ProjectPreviewPdfBuilderTests
{
    [Fact]
    public void Build_erzeugt_pdf_aus_gesamter_projektvorschau()
    {
        var project = new Project { Name = "Altdorf Zone 1.15" };
        project.Metadata["Auftraggeber"] = "Gemeinde Altdorf";
        project.Metadata["Gemeinde"] = "Altdorf";
        project.Metadata["Zone"] = "1.15";
        project.Data.Add(Holding("H1", "0", "300", 12.5));
        project.Data.Add(Holding("H2", "3", "400", 8));
        project.SchaechteData.Add(Schacht("S1", "ohne"));
        var costs = new ProjectCostStore { ByHolding = { ["H1"] = Cost("H1", 1500m) } };
        var preview = ProjectPreviewFactory.FromProject(project, @"C:\P\projekt.json", costs, new ProjectCostStore());

        var pdf = ProjectPreviewPdfBuilder.Build(preview);

        Assert.True(pdf.Length > 1000);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(pdf, 0, 4));
        using var document = PdfDocument.Open(new MemoryStream(pdf));
        Assert.Equal(2, document.NumberOfPages);
    }

    private static HaltungRecord Holding(string name, string zustand, string dn, double length)
    {
        var record = new HaltungRecord();
        record.SetFieldValue("Haltungsname", name, FieldSource.Manual, false);
        record.SetFieldValue("Zustandsklasse", zustand, FieldSource.Manual, false);
        record.SetFieldValue("DN_mm", dn, FieldSource.Manual, false);
        record.SetFieldValue("Haltungslaenge_m", length.ToString(System.Globalization.CultureInfo.InvariantCulture), FieldSource.Manual, false);
        return record;
    }

    private static SchachtRecord Schacht(string nummer, string zustand)
    {
        var record = new SchachtRecord();
        record.SetFieldValue("Schachtnummer", nummer);
        record.SetFieldValue("Zustandsklasse", zustand);
        return record;
    }

    private static HoldingCost Cost(string key, decimal total)
        => new()
        {
            Holding = key,
            Total = total,
            Measures =
            [
                new MeasureCost
                {
                    MeasureId = "M",
                    MeasureName = "Massnahme",
                    Total = total,
                    Lines =
                    [
                        new CostLine
                        {
                            ItemKey = "M",
                            Text = "Massnahme",
                            Qty = 1m,
                            UnitPrice = total,
                            Selected = true
                        }
                    ]
                }
            ]
        };
}
