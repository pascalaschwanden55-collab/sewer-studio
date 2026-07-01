using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.DataPage;
using Xunit;
using static AuswertungPro.Next.UI.Tests.SourceTextTestHelpers;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DataPageDropReorderControllerTests
{
    [Fact]
    public void DataPage_drop_handler_uses_controller_instead_of_reflecting_private_update_method()
    {
        var source = File.ReadAllText(RepoFile(
            "src",
            "AuswertungPro.Next.UI",
            "Views",
            "Pages",
            "DataPage.xaml.cs"));

        Assert.DoesNotContain("GetMethod(\"UpdateNr\"", source);
        Assert.Contains("DataPageDropReorderController.TryMoveAndRenumber", source);
    }

    [Fact]
    public void TryMoveAndRenumber_moves_dropped_record_to_target_index_and_updates_nr_fields()
    {
        var first = Record("alt-1");
        var second = Record("alt-2");
        var third = Record("alt-3");
        var records = new ObservableCollection<HaltungRecord> { first, second, third };

        var moved = DataPageDropReorderController.TryMoveAndRenumber(records, third, first);

        Assert.True(moved);
        Assert.Equal(new[] { third, first, second }, records);
        Assert.Equal("1", third.GetFieldValue("NR"));
        Assert.Equal("2", first.GetFieldValue("NR"));
        Assert.Equal("3", second.GetFieldValue("NR"));
    }

    [Fact]
    public void TryMoveAndRenumber_ignores_missing_same_or_equal_position_records()
    {
        var first = Record("1");
        var second = Record("2");
        var records = new ObservableCollection<HaltungRecord> { first, second };
        var original = records.ToArray();

        Assert.False(DataPageDropReorderController.TryMoveAndRenumber(records, first, first));
        Assert.False(DataPageDropReorderController.TryMoveAndRenumber(records, new HaltungRecord(), second));
        Assert.False(DataPageDropReorderController.TryMoveAndRenumber(records, first, new HaltungRecord()));

        Assert.Equal(original, records);
        Assert.Equal("1", first.GetFieldValue("NR"));
        Assert.Equal("2", second.GetFieldValue("NR"));
    }

    [Fact]
    public void CanMoveByOffset_erkennt_bewegliche_auswahl()
    {
        var first = Record("1");
        var second = Record("2");
        var third = Record("3");
        var records = new ObservableCollection<HaltungRecord> { first, second, third };

        Assert.False(DataPageRecordOrderController.CanMoveByOffset(records, null, -1));
        Assert.False(DataPageRecordOrderController.CanMoveByOffset(records, first, -1));
        Assert.True(DataPageRecordOrderController.CanMoveByOffset(records, second, -1));
        Assert.True(DataPageRecordOrderController.CanMoveByOffset(records, second, 1));
        Assert.False(DataPageRecordOrderController.CanMoveByOffset(records, third, 1));
        Assert.False(DataPageRecordOrderController.CanMoveByOffset(records, new HaltungRecord(), 1));
    }

    [Fact]
    public void TryMoveByOffset_verschiebt_auswahl_und_aktualisiert_nr()
    {
        var first = Record("10");
        var second = Record("20");
        var third = Record("30");
        var records = new ObservableCollection<HaltungRecord> { first, second, third };

        var moved = DataPageRecordOrderController.TryMoveByOffset(records, second, -1);

        Assert.True(moved);
        Assert.Equal(new[] { second, first, third }, records);
        Assert.Equal("1", second.GetFieldValue("NR"));
        Assert.Equal("2", first.GetFieldValue("NR"));
        Assert.Equal("3", third.GetFieldValue("NR"));
    }

    [Theory]
    [InlineData(0, new[] { "B", "A", "C" })]
    [InlineData(1, new[] { "B", "A", "C" })]
    [InlineData(3, new[] { "A", "C", "B" })]
    [InlineData(99, new[] { "A", "C", "B" })]
    public void TryMoveToPosition_nutzt_eins_basierte_position_mit_grenzen(int targetPosition, string[] expectedOrder)
    {
        var first = Record("1", "A");
        var second = Record("2", "B");
        var third = Record("3", "C");
        var records = new ObservableCollection<HaltungRecord> { first, second, third };

        var moved = DataPageRecordOrderController.TryMoveToPosition(records, second, targetPosition);

        Assert.True(moved);
        Assert.Equal(expectedOrder, records.Select(r => r.GetFieldValue("Haltungsname")).ToArray());
        Assert.Equal(new[] { "1", "2", "3" }, records.Select(r => r.GetFieldValue("NR")).ToArray());
    }

    [Fact]
    public void TryMoveToPosition_ignoriert_fehlende_oder_unveraenderte_position()
    {
        var first = Record("1");
        var second = Record("2");
        var records = new ObservableCollection<HaltungRecord> { first, second };
        var original = records.ToArray();

        Assert.False(DataPageRecordOrderController.TryMoveToPosition(records, null, 1));
        Assert.False(DataPageRecordOrderController.TryMoveToPosition(records, new HaltungRecord(), 1));
        Assert.False(DataPageRecordOrderController.TryMoveToPosition(records, first, 1));

        Assert.Equal(original, records);
        Assert.Equal(new[] { "1", "2" }, records.Select(r => r.GetFieldValue("NR")).ToArray());
    }

    private static HaltungRecord Record(string nr, string? name = null)
    {
        var record = new HaltungRecord();
        record.SetFieldValue("NR", nr, FieldSource.Manual, userEdited: true);
        if (name is not null)
            record.SetFieldValue("Haltungsname", name, FieldSource.Manual, userEdited: true);
        return record;
    }
}
