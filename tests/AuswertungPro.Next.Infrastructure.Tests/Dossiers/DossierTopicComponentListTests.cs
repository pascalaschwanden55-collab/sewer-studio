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
    public void Nur_Schaeden_und_Sanierungskonzept_erhalten_die_Liste_automatisch(
        string titel,
        bool erwartet)
        => Assert.Equal(erwartet, DossierTopicEditing.IncludesComponentsAutomatically(titel));

    [Fact]
    public void Haltungen_stehen_vor_Schaechten_und_werden_durchgehend_nummeriert()
    {
        var (area, dossier, values) = BuildScenario();

        var rows = DossierWordTemplateExportService.BuildTopicRows(area, dossier, values);

        Assert.Equal(2, rows.Count);
        foreach (var row in rows)
        {
            var text = row["Text"];
            Assert.Contains("1. Haltung H-1", text, StringComparison.Ordinal);
            Assert.Contains("2. Haltung H-2", text, StringComparison.Ordinal);
            Assert.Contains("3. Schacht S-1", text, StringComparison.Ordinal);
            Assert.Contains("4. Schacht S-2", text, StringComparison.Ordinal);
            Assert.True(text.IndexOf("1. Haltung H-1", StringComparison.Ordinal)
                < text.IndexOf("3. Schacht S-1", StringComparison.Ordinal));
        }
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
    public void Eigener_Text_behaelt_seine_Formatierung_vor_der_automatischen_Liste()
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

        Assert.StartsWith("Dringend\n1. Haltung H-1", row["Text"], StringComparison.Ordinal);
        var range = Assert.Single(ranges);
        Assert.Equal(0, range.Start);
        Assert.Equal(8, range.Length);
        Assert.Equal("C00000", range.ColorHex);
        Assert.True(range.Bold);
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
