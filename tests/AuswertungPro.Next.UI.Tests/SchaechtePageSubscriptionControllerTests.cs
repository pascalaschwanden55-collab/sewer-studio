using System.Collections.ObjectModel;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Views.Pages;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SchaechtePageSubscriptionControllerTests
{
    [Fact]
    public void Switch_attaches_new_context_and_detaches_old_context()
    {
        var firstColumns = new ObservableCollection<string> { "A" };
        var firstRecords = new ObservableCollection<SchachtRecord> { new() };
        var secondColumns = new ObservableCollection<string> { "B" };
        var secondRecords = new ObservableCollection<SchachtRecord> { new() };
        var rebuildCount = 0;
        var searchRefreshCount = 0;
        var recordChangeCount = 0;

        var controller = new SchaechtePageSubscriptionController(
            rebuildColumns: () => rebuildCount++,
            refreshSearch: () => searchRefreshCount++,
            recordPropertyChanged: (_, __) => recordChangeCount++);

        controller.Switch(firstColumns, firstRecords, () => firstRecords);
        controller.Switch(secondColumns, secondRecords, () => secondRecords);

        firstColumns.Add("Ignored");
        firstRecords[0].SetFieldValue("Name", "ignored");
        secondColumns.Add("Visible");
        secondRecords[0].SetFieldValue("Name", "visible");

        Assert.Equal(3, rebuildCount);
        Assert.Equal(2, searchRefreshCount);
        Assert.Equal(3, recordChangeCount);
    }

    [Fact]
    public void Records_collection_changes_update_record_property_subscriptions()
    {
        var columns = new ObservableCollection<string> { "A" };
        var records = new ObservableCollection<SchachtRecord>();
        var removed = new SchachtRecord();
        var added = new SchachtRecord();
        var searchRefreshCount = 0;
        var recordChangeCount = 0;

        var controller = new SchaechtePageSubscriptionController(
            rebuildColumns: () => { },
            refreshSearch: () => searchRefreshCount++,
            recordPropertyChanged: (_, __) => recordChangeCount++);

        records.Add(removed);
        controller.Switch(columns, records, () => records);
        records.Remove(removed);
        records.Add(added);

        removed.SetFieldValue("Name", "ignored");
        added.SetFieldValue("Name", "visible");

        Assert.Equal(3, searchRefreshCount);
        Assert.Equal(3, recordChangeCount);
    }

    [Fact]
    public void Reset_resynchronizes_record_property_subscriptions_from_current_records()
    {
        var columns = new ObservableCollection<string> { "A" };
        var records = new ObservableCollection<SchachtRecord> { new() };
        var replacement = new SchachtRecord();
        var recordChangeCount = 0;

        var controller = new SchaechtePageSubscriptionController(
            rebuildColumns: () => { },
            refreshSearch: () => { },
            recordPropertyChanged: (_, __) => recordChangeCount++);

        var oldRecord = records[0];
        controller.Switch(columns, records, () => records);
        records.Clear();
        records.Add(replacement);

        oldRecord.SetFieldValue("Name", "ignored");
        replacement.SetFieldValue("Name", "visible");

        Assert.Equal(3, recordChangeCount);
    }

    [Fact]
    public void Detach_unsubscribes_columns_records_and_record_property_changes()
    {
        var columns = new ObservableCollection<string> { "A" };
        var record = new SchachtRecord();
        var records = new ObservableCollection<SchachtRecord> { record };
        var rebuildCount = 0;
        var searchRefreshCount = 0;
        var recordChangeCount = 0;

        var controller = new SchaechtePageSubscriptionController(
            rebuildColumns: () => rebuildCount++,
            refreshSearch: () => searchRefreshCount++,
            recordPropertyChanged: (_, __) => recordChangeCount++);

        controller.Switch(columns, records, () => records);
        controller.Detach();

        columns.Add("Ignored");
        records.Add(new SchachtRecord());
        record.SetFieldValue("Name", "ignored");

        Assert.Equal(1, rebuildCount);
        Assert.Equal(1, searchRefreshCount);
        Assert.Equal(0, recordChangeCount);
    }
}
