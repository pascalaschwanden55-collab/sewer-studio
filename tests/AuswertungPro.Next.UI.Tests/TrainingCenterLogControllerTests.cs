using System.Collections.ObjectModel;
using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingCenterLogControllerTests
{
    [Fact]
    public void FormatEntry_prefixes_message_with_time()
    {
        var entry = TrainingCenterLogController.FormatEntry(
            "Sample gespeichert",
            new DateTime(2026, 7, 3, 22, 15, 4));

        Assert.Equal("[22:15:04] Sample gespeichert", entry);
    }

    [Fact]
    public void AppendLogText_appends_entry_with_newline()
    {
        var text = TrainingCenterLogController.AppendLogText(
            "alt\n",
            "[22:15:04] neu");

        Assert.Equal("alt\n[22:15:04] neu\n", text);
    }

    [Fact]
    public void AppendSelfTrainingEntry_trims_oldest_entries_to_limit()
    {
        var entries = new ObservableCollection<string>(
            Enumerable.Range(1, 100).Select(i => "alt-" + i));

        TrainingCenterLogController.AppendSelfTrainingEntry(entries, "neu");

        Assert.Equal(100, entries.Count);
        Assert.Equal("alt-2", entries[0]);
        Assert.Equal("neu", entries[^1]);
    }

    [Fact]
    public void AppendSelfTrainingLog_dispatches_formatted_entry_to_self_training_list()
    {
        var entries = new ObservableCollection<string>();
        var dispatchCount = 0;

        TrainingCenterLogController.AppendSelfTrainingLog(
            "Analyse gestartet",
            new DateTime(2026, 7, 4, 9, 8, 7),
            action =>
            {
                dispatchCount++;
                action();
            },
            entries);

        Assert.Equal(1, dispatchCount);
        Assert.Equal(["[09:08:07] Analyse gestartet"], entries);
    }

    [Fact]
    public void AppendSelfTrainingLog_ohne_timestamp_nutzt_controller_systemzeit()
    {
        var entries = new ObservableCollection<string>();

        TrainingCenterLogController.AppendSelfTrainingLog(
            "Analyse gestartet",
            action => action(),
            entries);

        var entry = Assert.Single(entries);
        Assert.Matches(@"^\[\d{2}:\d{2}:\d{2}\] Analyse gestartet$", entry);
    }

    [Fact]
    public void AppendLog_dispatches_log_text_and_self_training_entry()
    {
        var entries = new ObservableCollection<string>();
        var logText = "alt\n";
        var dispatchCount = 0;

        TrainingCenterLogController.AppendLog(
            "Analyse fertig",
            new DateTime(2026, 7, 4, 9, 8, 7),
            action =>
            {
                dispatchCount++;
                action();
            },
            () => logText,
            value => logText = value,
            entries);

        Assert.Equal(1, dispatchCount);
        Assert.Equal("alt\n[09:08:07] Analyse fertig\n", logText);
        Assert.Equal(["[09:08:07] Analyse fertig"], entries);
    }

    [Fact]
    public void AppendLog_ohne_timestamp_nutzt_controller_systemzeit()
    {
        var entries = new ObservableCollection<string>();
        var logText = "";

        TrainingCenterLogController.AppendLog(
            "Analyse fertig",
            action => action(),
            () => logText,
            value => logText = value,
            entries);

        Assert.Matches(@"^\[\d{2}:\d{2}:\d{2}\] Analyse fertig\n$", logText);
        Assert.Matches(@"^\[\d{2}:\d{2}:\d{2}\] Analyse fertig$", Assert.Single(entries));
    }
}
