using System;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.DataPage;

public enum DataPageComboBoxCommitStatus
{
    Noop,
    Committed
}

public sealed record DataPageComboBoxCommitResult(DataPageComboBoxCommitStatus Status);

/// <summary>
/// Entscheidungslogik fuer verwaltete ComboBox-Felder der Haltungsansicht.
/// WPF-Elementsuche und konkrete Dropdown-Listen bleiben in DataPage/DataPageViewModel.
/// </summary>
public static class DataPageComboBoxCommitController
{
    public static DataPageComboBoxCommitResult Commit(
        string? fieldName,
        bool isProjectReady,
        HaltungRecord? record,
        string? value,
        Action<string, string?> ensureOptionForField,
        Action scheduleAutoSave)
    {
        ArgumentNullException.ThrowIfNull(ensureOptionForField);
        ArgumentNullException.ThrowIfNull(scheduleAutoSave);

        if (fieldName is null || !isProjectReady)
            return new DataPageComboBoxCommitResult(DataPageComboBoxCommitStatus.Noop);

        if (record is not null)
        {
            if (string.IsNullOrWhiteSpace(value))
                return new DataPageComboBoxCommitResult(DataPageComboBoxCommitStatus.Noop);

            record.SetFieldValue(fieldName, value, FieldSource.Manual, userEdited: true);
        }

        ensureOptionForField(fieldName, value);
        scheduleAutoSave();
        return new DataPageComboBoxCommitResult(DataPageComboBoxCommitStatus.Committed);
    }
}
