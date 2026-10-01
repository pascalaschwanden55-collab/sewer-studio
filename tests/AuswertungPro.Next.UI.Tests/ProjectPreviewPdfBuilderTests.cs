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

    /// <summary>
    /// Detaillierteres Cockpit (2026-08-20): Der Ausdruck zeigt die Mengen der
    /// Sanierungsverfahren, den Schacht-Sanierungsentscheid und die Schachtkosten
    /// getrennt.
    /// </summary>
    [Fact]
    public void Build_zeigt_Verfahren_Schachtentscheid_und_Schachtkosten()
    {
        var project = new Project { Name = "Altdorf Zone 1.15" };
        project.Data.Add(Holding("H1", "1", "300", 12.5));

        var mitMassnahme = Schacht("S1", "1");
        mitMassnahme.SetFieldValue("Massnahmen", "Abdeckung ersetzen");
        project.SchaechteData.Add(mitMassnahme);
        project.SchaechteData.Add(Schacht("S2", "3"));

        var hCosts = new ProjectCostStore
        {
            ByHolding =
            {
                ["H1"] = new HoldingCost
                {
                    Holding = "H1",
                    Measures =
                    [
                        new MeasureCost
                        {
                            MeasureId = "M", MeasureName = "Renovierung",
                            Lines =
                            [
                                new CostLine { ItemKey = "SCHLAUCHLINER_GFK", Text = "Schlauchliner GFK", Unit = "m", Qty = 24m, UnitPrice = 200m, Selected = true },
                                new CostLine { ItemKey = "KURZLINER_PARTLINER", Text = "Kurzliner", Unit = "Stk", Qty = 3m, UnitPrice = 850m, Selected = true }
                            ]
                        }
                    ]
                }
            }
        };
        var sCosts = new ProjectCostStore { ByHolding = { ["S1"] = Cost("S1", 1100m) } };
        var preview = ProjectPreviewFactory.FromProject(project, @"C:\P\projekt.json", hCosts, sCosts);

        var pdf = ProjectPreviewPdfBuilder.Build(preview);

        using var document = PdfDocument.Open(new MemoryStream(pdf));
        var text = string.Join("\n", document.GetPages().Select(p => p.Text));

        Assert.Contains("Sanierungsverfahren", text);
        Assert.Contains("Inliner GFK", text);
        Assert.Contains("Kurzliner", text);
        Assert.Contains("Schächte sanieren", text);
        Assert.Contains("Kosten Schachtsanierung", text);
    }

    /// <summary>
    /// Der Ausdruck weist die Sanierungskosten nach Haltungen und Schächten
    /// gegliedert aus — und kennzeichnet sie als Nettobeträge ohne MWST.
    /// </summary>
    [Fact]
    public void Build_gliedert_die_Kosten_nach_Haltungen_und_Schaechten_ohne_Mwst()
    {
        var project = new Project { Name = "Altdorf Zone 1.15" };
        project.Data.Add(Holding("H1", "1", "300", 12.5));
        project.SchaechteData.Add(Schacht("S1", "1"));

        var preview = ProjectPreviewFactory.FromProject(
            project,
            @"C:\P\projekt.json",
            new ProjectCostStore { ByHolding = { ["H1"] = Cost("H1", 1200m) } },
            new ProjectCostStore { ByHolding = { ["S1"] = Cost("S1", 450m) } });

        var pdf = ProjectPreviewPdfBuilder.Build(preview);

        using var document = PdfDocument.Open(new MemoryStream(pdf));
        var text = string.Join("\n", document.GetPages().Select(p => p.Text));

        Assert.Contains("Sanierungskosten (ohne MWST)", text);
        Assert.Contains("1’200", text);   // Haltungen
        Assert.Contains("450", text);      // Schächte
        Assert.Contains("1’650", text);   // Total
        Assert.Contains("Total", text);
    }

    /// <summary>
    /// Bei einem vollen Projekt stand der Kostenblock zerrissen auf zwei Seiten
    /// ("Haltungen" unten auf Seite 1, "Schächte/Total" oben auf Seite 2, real
    /// passiert am 2026-08-20). Er muss immer komplett auf einer Seite stehen.
    /// </summary>
    [Fact]
    public void Kostenblock_bleibt_bei_vollem_Projekt_auf_einer_Seite()
    {
        var project = new Project { Name = "Volles Projekt" };
        var hCosts = new ProjectCostStore();

        // Realistische Fuelle: viele Haltungen, viele DN-Gruppen, viele Schadensarten.
        var dnWerte = new[] { "150", "200", "250", "300", "400", "500", "600", "800" };
        var codes = new[] { "BAB01", "BAF02", "BAC01", "BBA01", "BBC02", "BAJ01", "BBF01", "BAI01" };
        for (var i = 0; i < 65; i++)
        {
            var name = $"H{i:D3}";
            var record = Holding(name, (i % 5).ToString(), dnWerte[i % dnWerte.Length], 20 + i);
            record.Protocol = new AuswertungPro.Next.Domain.Protocol.ProtocolDocument
            {
                Current = new AuswertungPro.Next.Domain.Protocol.ProtocolRevision
                {
                    Entries =
                    [
                        new AuswertungPro.Next.Domain.Protocol.ProtocolEntry { Code = codes[i % codes.Length] },
                        new AuswertungPro.Next.Domain.Protocol.ProtocolEntry { Code = codes[(i + 3) % codes.Length] }
                    ]
                }
            };
            project.Data.Add(record);
            hCosts.ByHolding[name] = Cost(name, 1000m + i * 137m);
        }

        for (var i = 0; i < 87; i++)
            project.SchaechteData.Add(Schacht($"S{i:D3}", (i % 5).ToString()));

        var preview = ProjectPreviewFactory.FromProject(
            project, @"C:\P\projekt.json", hCosts, new ProjectCostStore());

        var pdf = ProjectPreviewPdfBuilder.Build(preview);

        using var document = PdfDocument.Open(new MemoryStream(pdf));
        var seiteMitBlock = document.GetPages()
            .Where(p => p.Text.Contains("Sanierungskosten (ohne MWST)", StringComparison.Ordinal))
            .ToList();

        var seite = Assert.Single(seiteMitBlock);
        Assert.Contains("Haltungen", seite.Text);
        Assert.Contains("Schächte", seite.Text);
        Assert.Contains("Total", seite.Text);
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
