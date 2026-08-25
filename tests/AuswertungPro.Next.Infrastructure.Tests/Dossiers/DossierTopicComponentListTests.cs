using System;
using System.Collections.Generic;

using AuswertungPro.Next.Application.Dossiers;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Models.Dossiers;
using AuswertungPro.Next.Infrastructure.Dossiers;

using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Dossiers;

public sealed class DossierTopicComponentListTests
{
    [Theory]
    [InlineData("Schäden", true)]
    [InlineData("Schäden Pz. 30", true)]
    [InlineData("Sanierungskonzept", true)]
    [InlineData("Sanierungskonzept Parzelle 30", true)]
    [InlineData("Kostenschätzung", false)]
    [InlineData("Ausgangslage", false)]
    public void Nur_Schaeden_und_Sanierungskonzept_erhalten_den_Listenimport(
        string titel,
        bool erwartet)
        => Assert.Equal(erwartet, DossierTopicEditing.SupportsComponentListImport(titel));

    [Fact]
    public void Ohne_Import_bleibt_der_Thementext_frei_von_der_Bauteilliste()
    {
        var (area, dossier, values) = BuildScenario();

        var rows = DossierWordTemplateExportService.BuildTopicRows(area, dossier, values);

        Assert.Equal(2, rows.Count);
        Assert.DoesNotContain("H-1", rows[0]["Text"], StringComparison.Ordinal);
        Assert.DoesNotContain("S-1", rows[0]["Text"], StringComparison.Ordinal);
        Assert.DoesNotContain("H-1", rows[1]["Text"], StringComparison.Ordinal);
        Assert.DoesNotContain("S-1", rows[1]["Text"], StringComparison.Ordinal);
    }

    [Fact]
    public void Import_kopiert_Haltungen_vor_Schaechten_als_frei_bearbeitbaren_Text()
    {
        var (area, dossier, values) = BuildScenario();

        var imported = DossierTopicEditing.ImportComponentListForDossier(
            dossier, DossierTopicTitles.Schaeden, values);

        Assert.Contains("1. Haltung H-1", imported, StringComparison.Ordinal);
        Assert.Contains("2. Haltung H-2", imported, StringComparison.Ordinal);
        Assert.Contains("3. Schacht S-1", imported, StringComparison.Ordinal);
        Assert.Contains("4. Schacht S-2", imported, StringComparison.Ordinal);
        Assert.DoesNotContain("{{", imported, StringComparison.Ordinal);

        var row = DossierWordTemplateExportService.BuildTopicRows(area, dossier, values)[0];
        Assert.Equal(imported, row["Text"]);
        Assert.Equal(2, dossier.HoldingIds.Count);
        Assert.Equal(2, dossier.ShaftNumbers.Count);
    }

    [Fact]
    public void Loeschen_im_importierten_Text_loescht_keine_Haltung_und_keinen_Schacht()
    {
        var (area, dossier, values) = BuildScenario();
        DossierTopicEditing.ImportComponentListForDossier(
            dossier, DossierTopicTitles.Schaeden, values);

        DossierTopicEditing.SetForDossier(
            dossier, DossierTopicTitles.Schaeden, "Nur noch meine eigene Bemerkung");

        var row = DossierWordTemplateExportService.BuildTopicRows(area, dossier, values)[0];
        Assert.Equal("Nur noch meine eigene Bemerkung", row["Text"]);
        Assert.Equal(2, dossier.HoldingIds.Count);
        Assert.Equal(2, dossier.ShaftNumbers.Count);
    }

    [Fact]
    public void Alte_Listenmarken_werden_nicht_doppelt_oder_in_falscher_Reihenfolge_ausgegeben()
    {
        var (area, dossier, values) = BuildScenario();
        dossier.Topics.Add(new DossierTopicRow
        {
            Title = "Schäden",
            Text = "{{Schaechte_Text}}\n{{Haltungen_Text}}"
        });

        var row = DossierWordTemplateExportService.BuildTopicRows(area, dossier, values)[0];
        var text = row["Text"];

        Assert.StartsWith("1. Haltung H-1", text, StringComparison.Ordinal);
        Assert.Equal(1, Count(text, "H-1"));
        Assert.Equal(1, Count(text, "S-1"));
        Assert.DoesNotContain("{{", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Eigener_Text_bleibt_ohne_Import_unveraendert_formatiert()
    {
        var (area, dossier, values) = BuildScenario();
        dossier.Topics.Add(new DossierTopicRow
        {
            Title = "Schäden",
            Text = "Dringend",
            StyleRanges =
            {
                new DossierTextStyleRange
                {
                    Start = 0,
                    Length = 8,
                    ColorHex = "C00000",
                    Bold = true
                }
            }
        });

        var row = DossierWordTemplateExportService.BuildTopicRows(area, dossier, values)[0];
        var ranges = DossierTopicTextFormatting.Decode(
            row["Text" + DossierTopicTextFormatting.StyleRangesSuffix]);

        Assert.Equal("Dringend", row["Text"]);
        var range = Assert.Single(ranges);
        Assert.Equal(0, range.Start);
        Assert.Equal(8, range.Length);
        Assert.Equal("C00000", range.ColorHex);
        Assert.True(range.Bold);
        Assert.Equal(2, dossier.HoldingIds.Count);
        Assert.Equal(2, dossier.ShaftNumbers.Count);
    }

    [Fact]
    public void Andere_Themen_bleiben_unveraendert()
    {
        var (area, dossier, values) = BuildScenario();
        area.Topics.Clear();
        area.Topics.Add(new DossierTopicRow { Title = "Ausgangslage", Text = "Bestehender Text" });

        var row = Assert.Single(
            DossierWordTemplateExportService.BuildTopicRows(area, dossier, values));

        Assert.Equal("Bestehender Text", row["Text"]);
        Assert.DoesNotContain("H-1", row["Text"], StringComparison.Ordinal);
        Assert.DoesNotContain("S-1", row["Text"], StringComparison.Ordinal);
    }

    private static (DossierAreaSettings Area, DossierDefinition Dossier,
        IReadOnlyDictionary<string, string> Values) BuildScenario()
    {
        var project = new Project();
        var dossier = new DossierDefinition();

        foreach (var name in new[] { "H-1", "H-2" })
        {
            var holding = new HaltungRecord();
            holding.Fields[FieldKeys.HoldingName] = name;
            project.Data.Add(holding);
            dossier.HoldingIds.Add(holding.Id);
        }

        foreach (var number in new[] { "S-1", "S-2" })
        {
            var shaft = new SchachtRecord();
            shaft.SetFieldValue("Schachtnummer", number);
            project.SchaechteData.Add(shaft);
            dossier.ShaftNumbers.Add(number);
        }

        var area = new DossierAreaSettings
        {
            Topics =
            {
                new DossierTopicRow { Title = "Schäden", Text = "Festgestellte Schäden:" },
                new DossierTopicRow { Title = "Sanierungskonzept", Text = "Vorgesehene Arbeiten:" }
            }
        };

        var snapshot = DossierSnapshotBuilder.Build(dossier, project, null);
        var request = new DossierExportRequest(project, string.Empty, area, dossier, snapshot, string.Empty);

        return (area, dossier, DossierWordTemplateExportService.BuildValues(request));
    }

    private static int Count(string text, string value)
        => (text.Length - text.Replace(value, string.Empty, StringComparison.Ordinal).Length)
            / value.Length;
}
