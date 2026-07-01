using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.DataPage;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DataPageComboBoxCommitControllerTests
{
    [Fact]
    public void Commit_returns_noop_when_project_is_not_ready()
    {
        var result = DataPageComboBoxCommitController.Commit(
            "Sanieren_JaNein",
            isProjectReady: false,
            record: new HaltungRecord(),
            value: "Ja",
            ensureOptionForField: (_, _) => throw new InvalidOperationException("Soll keine Option sichern"),
            scheduleAutoSave: () => throw new InvalidOperationException("Soll nicht speichern"));

        Assert.Equal(DataPageComboBoxCommitStatus.Noop, result.Status);
    }

    [Fact]
    public void Commit_returns_noop_for_blank_record_value()
    {
        var record = new HaltungRecord();

        var result = DataPageComboBoxCommitController.Commit(
            "Sanieren_JaNein",
            isProjectReady: true,
            record,
            " ",
            ensureOptionForField: (_, _) => throw new InvalidOperationException("Soll keine Option sichern"),
            scheduleAutoSave: () => throw new InvalidOperationException("Soll nicht speichern"));

        Assert.Equal(DataPageComboBoxCommitStatus.Noop, result.Status);
        Assert.Equal(string.Empty, record.GetFieldValue("Sanieren_JaNein"));
    }

    [Fact]
    public void Commit_updates_record_option_and_schedules_autosave()
    {
        var record = new HaltungRecord();
        string? ensuredField = null;
        string? ensuredValue = null;
        var saveCount = 0;

        var result = DataPageComboBoxCommitController.Commit(
            "Sanieren_JaNein",
            isProjectReady: true,
            record,
            "Ja",
            ensureOptionForField: (field, value) =>
            {
                ensuredField = field;
                ensuredValue = value;
            },
            scheduleAutoSave: () => saveCount++);

        Assert.Equal(DataPageComboBoxCommitStatus.Committed, result.Status);
        Assert.Equal("Ja", record.GetFieldValue("Sanieren_JaNein"));
        Assert.Equal(FieldSource.Manual, record.FieldMeta["Sanieren_JaNein"].Source);
        Assert.True(record.FieldMeta["Sanieren_JaNein"].UserEdited);
        Assert.Equal("Sanieren_JaNein", ensuredField);
        Assert.Equal("Ja", ensuredValue);
        Assert.Equal(1, saveCount);
    }
}
