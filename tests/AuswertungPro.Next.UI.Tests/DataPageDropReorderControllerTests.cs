using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.DataPage;
using Xunit;

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

    private static HaltungRecord Record(string nr)
    {
        var record = new HaltungRecord();
        record.SetFieldValue("NR", nr, FieldSource.Manual, userEdited: true);
        return record;
    }

    private static string RepoFile(params string[] segments)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "AuswertungPro.sln");
            if (File.Exists(candidate))
                return Path.Combine(new[] { directory.FullName }.Concat(segments).ToArray());

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root not found.");
    }
}
