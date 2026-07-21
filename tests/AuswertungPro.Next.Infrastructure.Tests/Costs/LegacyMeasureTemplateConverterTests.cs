using System.Text.Json;
using AuswertungPro.Next.Infrastructure.Costs;
using LegacyMeasureTemplate = AuswertungPro.Next.Domain.Models.Costs.MeasureTemplate;
using LegacyMeasureTemplates = AuswertungPro.Next.Domain.Models.Costs.MeasureTemplates;
using LegacyTemplateLine = AuswertungPro.Next.Domain.Models.Costs.TemplateLine;

namespace AuswertungPro.Next.Infrastructure.Tests.Costs;

public sealed class LegacyMeasureTemplateConverterTests
{
    public static TheoryData<int, int> Versions => new()
    {
        { -2, 1 },
        { 0, 1 },
        { 1, 1 },
        { 3, 3 }
    };

    public static TheoryData<string, decimal> Quantities => new()
    {
        { "2.5", 2.5m },
        { "2.5e1", 25m },
        { "\"2.5\"", 2.5m },
        { "\"2,5\"", 2.5m },
        { "\" 2,5 \"", 2.5m },
        { "\"2e2\"", 200m },
        { "\"\"", 1m },
        { "\"   \"", 1m },
        { "\"length_m\"", 1m },
        { "null", 1m },
        { "true", 1m },
        { "{\"value\":2}", 1m },
        { "[2]", 1m },
        { "1e1000", 1m },
        { "0", 0m },
        { "-2", -2m }
    };

    [Theory]
    [MemberData(nameof(Versions))]
    public void Convert_haelt_version_mindestens_eins(int sourceVersion, int expectedVersion)
    {
        var source = new LegacyMeasureTemplates { SchemaVersion = sourceVersion };

        var result = LegacyMeasureTemplateConverter.Convert(source);

        Assert.Equal(expectedVersion, result.Version);
    }

    [Fact]
    public void Convert_trimmt_filtert_und_behaelt_reihenfolge_sowie_duplikate()
    {
        var source = new LegacyMeasureTemplates
        {
            Templates =
            [
                new LegacyMeasureTemplate { Id = " A ", Name = " Alpha ", Description = "wird ignoriert" },
                new LegacyMeasureTemplate { Id = "  ", Name = "nicht uebernehmen" },
                new LegacyMeasureTemplate { Id = "A", Name = "  " }
            ]
        };

        var result = LegacyMeasureTemplateConverter.Convert(source);

        Assert.Collection(
            result.Measures,
            first =>
            {
                Assert.Equal("A", first.Id);
                Assert.Equal("Alpha", first.Name);
                Assert.False(first.Disabled);
            },
            second =>
            {
                Assert.Equal("A", second.Id);
                Assert.Equal("A", second.Name);
                Assert.False(second.Disabled);
            });
    }

    [Fact]
    public void Convert_filtert_leere_positionen_und_behaelt_reihenfolge_sowie_aktivierung()
    {
        var source = CreateSourceWithLines(
            new LegacyTemplateLine
            {
                Group = " Gruppe 1 ",
                ItemRef = " POS_1 ",
                Qty = ParseElement("2"),
                When = "wird ignoriert"
            },
            new LegacyTemplateLine { ItemRef = "   ", Qty = ParseElement("9") },
            new LegacyTemplateLine
            {
                Group = null!,
                ItemRef = "POS_1",
                Qty = ParseElement("3")
            });

        var result = LegacyMeasureTemplateConverter.Convert(source);

        Assert.Collection(
            Assert.Single(result.Measures).Lines,
            first =>
            {
                Assert.Equal("Gruppe 1", first.Group);
                Assert.Equal("POS_1", first.ItemKey);
                Assert.Equal(2m, first.DefaultQty);
                Assert.True(first.Enabled);
            },
            second =>
            {
                Assert.Equal("", second.Group);
                Assert.Equal("POS_1", second.ItemKey);
                Assert.Equal(3m, second.DefaultQty);
                Assert.True(second.Enabled);
            });
    }

    [Theory]
    [MemberData(nameof(Quantities))]
    public void Convert_verarbeitet_legacy_mengen_wie_bisher(string json, decimal expected)
    {
        var source = CreateSourceWithLines(new LegacyTemplateLine
        {
            ItemRef = "position",
            Qty = ParseElement(json)
        });

        var result = LegacyMeasureTemplateConverter.Convert(source);

        Assert.Equal(expected, Assert.Single(Assert.Single(result.Measures).Lines).DefaultQty);
    }

    [Fact]
    public void Convert_nutzt_eins_fuer_nicht_gesetzte_menge()
    {
        var source = CreateSourceWithLines(new LegacyTemplateLine { ItemRef = "position" });

        var result = LegacyMeasureTemplateConverter.Convert(source);

        Assert.Equal(1m, Assert.Single(Assert.Single(result.Measures).Lines).DefaultQty);
    }

    [Fact]
    public void Convert_erlaubt_fehlende_vorlagen_und_positionslisten()
    {
        var sourceWithoutTemplates = new LegacyMeasureTemplates { Templates = null! };
        var sourceWithoutLines = new LegacyMeasureTemplates
        {
            Templates = [new LegacyMeasureTemplate { Id = "template", Lines = null! }]
        };

        var emptyCatalog = LegacyMeasureTemplateConverter.Convert(sourceWithoutTemplates);
        var emptyTemplate = Assert.Single(LegacyMeasureTemplateConverter.Convert(sourceWithoutLines).Measures);

        Assert.Empty(emptyCatalog.Measures);
        Assert.Empty(emptyTemplate.Lines);
    }

    [Fact]
    public void Convert_verarbeitet_auch_leere_vorlagen_id_vor_dem_filter()
    {
        var source = new LegacyMeasureTemplates
        {
            Templates =
            [
                new LegacyMeasureTemplate
                {
                    Id = " ",
                    Lines = [null!]
                }
            ]
        };

        Assert.Throws<NullReferenceException>(() => LegacyMeasureTemplateConverter.Convert(source));
    }

    [Fact]
    public void Convert_bricht_bei_null_eintrag_in_vorlagenliste_wie_bisher_ab()
    {
        var source = new LegacyMeasureTemplates { Templates = [null!] };

        Assert.Throws<NullReferenceException>(() => LegacyMeasureTemplateConverter.Convert(source));
    }

    private static LegacyMeasureTemplates CreateSourceWithLines(params LegacyTemplateLine[] lines)
        => new()
        {
            Templates =
            [
                new LegacyMeasureTemplate
                {
                    Id = "template",
                    Name = "Template",
                    Lines = [.. lines]
                }
            ]
        };

    private static JsonElement ParseElement(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
