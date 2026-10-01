using AuswertungPro.Next.Application.Dashboard;
using AuswertungPro.Next.Application.Dossiers;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Models.Dossiers;
using AuswertungPro.Next.Infrastructure.Dossiers;

namespace AuswertungPro.Next.Infrastructure.Tests.Dossiers;

/// <summary>
/// Die „unbekannt"-Regel muss im echten Export ankommen, nicht nur in ihrer
/// eigenen Klasse. Geprueft werden die Zeilen, die der Fueller aus den
/// Wiederholtabellen erzeugt.
/// </summary>
public sealed class DossierUnbekanntImExportTests
{
    [Fact]
    public void Ein_eigenes_Thema_ohne_Text_erscheint_weiterhin_als_unbekannt()
    {
        var area = new DossierAreaSettings
        {
            Topics =
            [
                new DossierTopicRow { Title = "Eigener Zusatzpunkt", Text = "" },
                new DossierTopicRow { Title = "Unternehmer", Text = "Muster AG" }
            ]
        };

        var rows = DossierWordTemplateExportService.BuildTopicRows(area, new DossierDefinition());

        Assert.Equal("unbekannt", rows[0]["Text"]);
        Assert.Equal("Muster AG", rows[1]["Text"]);
    }

    [Fact]
    public void Leere_Standardthemen_bleiben_im_Kundendokument_leer()
    {
        var area = new DossierAreaSettings
        {
            Topics = DossierDocumentMigration.BuildDefaultTopics()
        };

        var rows = DossierWordTemplateExportService.BuildTopicRows(
            area,
            new DossierDefinition());

        Assert.Equal(11, rows.Count);
        Assert.Equal(7, rows.Count(row => string.IsNullOrEmpty(row["Text"])));
        Assert.DoesNotContain(rows, row => row["Text"] == DossierUnbekanntText.Unbekannt);
    }

    [Fact]
    public void Der_Thementitel_wird_nie_zu_unbekannt()
    {
        var area = new DossierAreaSettings
        {
            Topics = [new DossierTopicRow { Title = "Schäden", Text = "" }]
        };

        var rows = DossierWordTemplateExportService.BuildTopicRows(area, new DossierDefinition());

        Assert.Equal("Schäden", rows[0]["Thema"]);
    }

    [Fact]
    public void Die_Ausgangslage_setzt_den_Gebietsnamen_ein()
    {
        // Der stehende Text nennt keinen festen Ort mehr, sondern {{Gebiet_Ort}}.
        // Beim Fuellen muss daraus der echte Gebietsname werden.
        var area = new DossierAreaSettings
        {
            Topics =
            [
                new DossierTopicRow
                {
                    Title = "Ausgangslage",
                    Text = "Die Abwasseranlagen im Perimeter {{Gebiet_Ort}} wurden kontrolliert."
                }
            ]
        };

        var werte = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Gebiet_Ort"] = "der Linden und Lindenstrasse"
        };

        var rows = DossierWordTemplateExportService.BuildTopicRows(area, new DossierDefinition(), werte);

        Assert.Equal(
            "Die Abwasseranlagen im Perimeter der Linden und Lindenstrasse wurden kontrolliert.",
            rows[0]["Text"]);
    }

    [Fact]
    public void Die_Standard_Ausgangslage_bleibt_ohne_Gebietsort_vollstaendig()
    {
        var area = new DossierAreaSettings
        {
            AreaLocation = "",
            Topics = DossierDocumentMigration.BuildDefaultTopics()
        };
        var dossier = new DossierDefinition();

        var rows = DossierWordTemplateExportService.BuildTopicRows(
            area,
            dossier,
            DossierWordTemplateExportService.BuildValues(Request(area, dossier)));

        var text = rows.Single(row => row["Thema"] == "Ausgangslage")["Text"];
        Assert.Contains(
            "im öffentlichen Bereich sowie die angrenzenden privaten Liegenschaften",
            text,
            StringComparison.Ordinal);
        Assert.DoesNotContain("Perimeter ,", text, StringComparison.Ordinal);
        Assert.DoesNotContain("{{", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Die_Standard_Ausgangslage_setzt_einen_vorhandenen_Gebietsort_sinnvoll_ein()
    {
        var area = new DossierAreaSettings
        {
            AreaLocation = "der Linden und Lindenstrasse",
            Topics = DossierDocumentMigration.BuildDefaultTopics()
        };
        var dossier = new DossierDefinition();

        var rows = DossierWordTemplateExportService.BuildTopicRows(
            area,
            dossier,
            DossierWordTemplateExportService.BuildValues(Request(area, dossier)));

        var text = rows.Single(row => row["Thema"] == "Ausgangslage")["Text"];
        Assert.Contains(
            "im öffentlichen Bereich im Perimeter der Linden und Lindenstrasse sowie",
            text,
            StringComparison.Ordinal);
        Assert.DoesNotContain("{{", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Eine_Eigentuemerzeile_ohne_Namen_erscheint_als_unbekannt()
    {
        var dossier = new DossierDefinition
        {
            Owners = [new DossierOwnerRow { HouseNumber = "20", ParcelNumber = "844", Name = "" }]
        };

        var rows = DossierWordTemplateExportService.BuildOwnerRows(dossier);

        Assert.Equal("unbekannt", rows[0]["Eigentuemer_Zelle"]);
    }

    [Fact]
    public void Haus_und_Parzellennummer_bleiben_leer()
    {
        var dossier = new DossierDefinition
        {
            Owners = [new DossierOwnerRow { HouseNumber = "", ParcelNumber = "", Name = "Meier" }]
        };

        var rows = DossierWordTemplateExportService.BuildOwnerRows(dossier);

        Assert.Equal("", rows[0]["Haus_Nr"]);
        Assert.Equal("", rows[0]["Pz_Nr"]);
        Assert.Equal("Meier", rows[0]["Eigentuemer_Zelle"]);
    }

    private static DossierExportRequest Request(
        DossierAreaSettings area,
        DossierDefinition dossier)
    {
        var verteilung = new ZustandVerteilung(Array.Empty<ZustandBucket>());
        var statistik = new DashboardStatistics(
            0, 0, 0, 0,
            verteilung,
            verteilung,
            Array.Empty<DashboardBucket>(),
            Array.Empty<DashboardCostBucket>(),
            0, 0, 0, 0, 0);

        return new DossierExportRequest(
            new Project(),
            "",
            area,
            dossier,
            new DossierSnapshot(dossier.Id, dossier.Name, [], [], statistik),
            "");
    }
}
