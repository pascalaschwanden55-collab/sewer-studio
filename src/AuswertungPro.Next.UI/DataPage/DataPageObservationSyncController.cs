using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.UI.DataPage;

public sealed class DataPageObservationSyncController
{
    private readonly Action _markProjectDirty;
    private readonly Action<HaltungRecord> _refreshRecordInGrid;
    private readonly Func<HaltungRecord, bool> _isSelected;
    private readonly Action _refreshSelectedProtocolEntries;
    private readonly Action _scheduleAutoSave;
    private readonly Action<string> _setStatus;

    public DataPageObservationSyncController(
        Action markProjectDirty,
        Action<HaltungRecord> refreshRecordInGrid,
        Func<HaltungRecord, bool> isSelected,
        Action refreshSelectedProtocolEntries,
        Action scheduleAutoSave,
        Action<string> setStatus)
    {
        _markProjectDirty = markProjectDirty ?? throw new ArgumentNullException(nameof(markProjectDirty));
        _refreshRecordInGrid = refreshRecordInGrid ?? throw new ArgumentNullException(nameof(refreshRecordInGrid));
        _isSelected = isSelected ?? throw new ArgumentNullException(nameof(isSelected));
        _refreshSelectedProtocolEntries = refreshSelectedProtocolEntries ?? throw new ArgumentNullException(nameof(refreshSelectedProtocolEntries));
        _scheduleAutoSave = scheduleAutoSave ?? throw new ArgumentNullException(nameof(scheduleAutoSave));
        _setStatus = setStatus ?? throw new ArgumentNullException(nameof(setStatus));
    }

    public void Sync(HaltungRecord? record, bool showStatus = false)
    {
        if (record is null)
            return;

        var entries = CollectSyncEntries(record);
        if (entries is null)
            return;

        var changed = false;
        var mapped = DataPageProtocolObservationMapper.Build(entries, record.VsaFindings);
        var primaryText = mapped.PrimaryDamageText;
        var currentPrimary = record.GetFieldValue("Primaere_Schaeden") ?? string.Empty;
        if (!string.Equals(currentPrimary, primaryText, StringComparison.Ordinal))
        {
            record.SetFieldValue("Primaere_Schaeden", primaryText, FieldSource.Manual, userEdited: true);
            changed = true;
        }

        if (DataPageProtocolObservationMapper.HasFindingChanges(record.VsaFindings, mapped.Findings))
        {
            record.VsaFindings = mapped.Findings;
            changed = true;
        }

        if (!changed)
            return;

        _markProjectDirty();
        _refreshRecordInGrid(record);

        if (_isSelected(record))
            _refreshSelectedProtocolEntries();

        _scheduleAutoSave();
        if (showStatus)
            _setStatus("Beobachtungen in Haltungen-Feldern aktualisiert");
    }

    private static List<ProtocolEntry>? CollectSyncEntries(HaltungRecord record)
        => record.Protocol?.Current?.Entries?
            .Where(e => !e.IsDeleted && !string.IsNullOrWhiteSpace(e.Code))
            .ToList();
}
