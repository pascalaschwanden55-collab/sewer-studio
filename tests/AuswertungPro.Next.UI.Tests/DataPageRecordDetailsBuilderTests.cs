using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.DataPage;
using AuswertungPro.Next.UI.Views.Windows;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DataPageRecordDetailsBuilderTests
{
    // Anfangs- und Endschacht gehoeren fachlich zu den Stammdaten der Haltung.
    // Sie stehen nicht im Feldkatalog, sondern kommen als freie Projektfelder herein -
    // frueher landeten sie deshalb ungefragt in "Weitere Angaben".
    [Fact]
    public void Build_stellt_Anfangs_und_Endschacht_zu_den_Stammdaten()
    {
        var record = new HaltungRecord();
        record.Fields["Schacht_oben"] = "36262";
        record.Fields["Schacht_unten"] = "36275";

        var groups = DataPageRecordDetailsBuilder.Build(
            record,
            fieldName => new RecordDetailItem(fieldName, fieldName, _ => { }));

        var stammdaten = groups.Single(g => g.Title == "Stammdaten");
        Assert.Contains(stammdaten.Items, item => item.Label == "Schacht_oben");
        Assert.Contains(stammdaten.Items, item => item.Label == "Schacht_unten");

        var weitere = groups.SingleOrDefault(g => g.Title == "Weitere Angaben");
        if (weitere is not null)
        {
            Assert.DoesNotContain(weitere.Items, item => item.Label == "Schacht_oben");
            Assert.DoesNotContain(weitere.Items, item => item.Label == "Schacht_unten");
        }
    }

    [Theory]
    [InlineData("Haltungsname", "Stammdaten")]
    [InlineData("Schacht_oben", "Stammdaten")]
    [InlineData("Schacht_unten", "Stammdaten")]
    [InlineData("Primaere_Schaeden", "Zustand & Inspektion")]
    [InlineData("Kosten", "Sanierung & Kosten")]
    [InlineData("Link", "Dokumente & Medien")]
    [InlineData("Feld_Das_Nicht_Im_Katalog_Ist", "Weitere Angaben")]
    public void ResolveGroup_routes_known_fields(string fieldName, string expectedGroup)
    {
        Assert.Equal(expectedGroup, DataPageRecordDetailsBuilder.ResolveGroup(fieldName));
    }

    [Fact]
    public void Build_keeps_extra_fields_in_additional_group()
    {
        var record = new HaltungRecord();
        record.Fields["Z_Extra"] = "42";

        var groups = DataPageRecordDetailsBuilder.Build(
            record,
            fieldName => new RecordDetailItem(fieldName, fieldName, _ => { }));

        var additional = groups.Single(g => g.Title == "Weitere Angaben");

        Assert.Contains(additional.Items, item => item.Label == "Z_Extra");
    }

    [Fact]
    public void Build_emits_groups_in_stable_ui_order()
    {
        var record = new HaltungRecord();

        var groups = DataPageRecordDetailsBuilder.Build(
            record,
            fieldName => new RecordDetailItem(fieldName, fieldName, _ => { }));

        Assert.Equal(new[]
        {
            "Stammdaten",
            "Zustand & Inspektion",
            "Sanierung & Kosten",
            "Dokumente & Medien",
            "Weitere Angaben"
        }, groups.Select(g => g.Title));
        Assert.Equal(new[]
        {
            RecordDetailGroupKind.MasterData,
            RecordDetailGroupKind.Condition,
            RecordDetailGroupKind.RenovationCosts,
            RecordDetailGroupKind.Documents,
            RecordDetailGroupKind.Additional
        }, groups.Select(g => g.Kind));
    }

    [Fact]
    public void Build_covers_every_catalog_field_exactly_once()
    {
        var record = new HaltungRecord();

        var groups = DataPageRecordDetailsBuilder.Build(
            record,
            fieldName => new RecordDetailItem(fieldName, fieldName, _ => { }));

        // Label == Feldname (durch die Test-Factory oben).
        var renderedFields = groups
            .SelectMany(g => g.Items)
            .Select(item => item.Label)
            .ToList();

        // Jedes Katalogfeld erscheint genau einmal (faengt fehlend=0 und doppelt>1 ab).
        foreach (var field in FieldCatalog.ColumnOrder)
        {
            var count = renderedFields.Count(f => f == field);
            Assert.True(count == 1,
                $"Feld '{field}' sollte genau einmal editierbar erscheinen, war aber {count}x vorhanden.");
        }
    }
}
