using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.DataPage;
using AuswertungPro.Next.UI.Views.Windows;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DataPageRecordDetailsBuilderTests
{
    [Theory]
    [InlineData("Haltungsname", "Stammdaten")]
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
    }
}
