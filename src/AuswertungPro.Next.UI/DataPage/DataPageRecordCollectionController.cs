using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.DataPage;

public sealed class DataPageRecordCollectionController
{
    private readonly Func<Project> _getProject;
    private readonly Func<HaltungRecord?> _getSelected;
    private readonly Action<HaltungRecord?> _setSelected;
    private readonly Func<string, string, bool> _confirmDelete;
    private readonly Action _notifyRecordsOrderChanged;
    private readonly Action _scheduleAutoSave;

    public DataPageRecordCollectionController(
        Func<Project> getProject,
        Func<HaltungRecord?> getSelected,
        Action<HaltungRecord?> setSelected,
        Func<string, string, bool> confirmDelete,
        Action notifyRecordsOrderChanged,
        Action scheduleAutoSave)
    {
        _getProject = getProject ?? throw new ArgumentNullException(nameof(getProject));
        _getSelected = getSelected ?? throw new ArgumentNullException(nameof(getSelected));
        _setSelected = setSelected ?? throw new ArgumentNullException(nameof(setSelected));
        _confirmDelete = confirmDelete ?? throw new ArgumentNullException(nameof(confirmDelete));
        _notifyRecordsOrderChanged = notifyRecordsOrderChanged ?? throw new ArgumentNullException(nameof(notifyRecordsOrderChanged));
        _scheduleAutoSave = scheduleAutoSave ?? throw new ArgumentNullException(nameof(scheduleAutoSave));
    }

    public bool CanMoveUp()
        => DataPageRecordOrderController.CanMoveByOffset(_getProject().Data, _getSelected(), -1);

    public bool CanMoveDown()
        => DataPageRecordOrderController.CanMoveByOffset(_getProject().Data, _getSelected(), 1);

    public void Add()
    {
        var project = _getProject();
        var record = project.CreateNewRecord();
        project.AddRecord(record);
        _setSelected(record);
        _scheduleAutoSave();
    }

    public void Remove()
    {
        var selected = _getSelected();
        if (selected is null)
            return;

        var name = selected.GetFieldValue("Haltungsname");
        var label = string.IsNullOrWhiteSpace(name) ? "diese Haltung" : $"die Haltung \"{name}\"";
        if (!_confirmDelete(
                $"Soll {label} wirklich geloescht werden?\n\nDie Zeile inkl. aller Daten wird entfernt.",
                "Haltung loeschen"))
        {
            return;
        }

        var project = _getProject();
        var records = project.Data;
        var idx = records.IndexOf(selected);
        var removed = project.RemoveRecord(selected.Id);
        if (!removed)
            return;

        if (records.Count == 0)
        {
            _setSelected(null);
            _scheduleAutoSave();
            return;
        }

        if (idx >= records.Count)
            idx = records.Count - 1;
        _setSelected(records[idx]);
        _scheduleAutoSave();
    }

    public void MoveUp()
    {
        if (!DataPageRecordOrderController.TryMoveByOffset(_getProject().Data, _getSelected(), -1))
            return;

        MarkRecordOrderChanged();
    }

    public void MoveDown()
    {
        if (!DataPageRecordOrderController.TryMoveByOffset(_getProject().Data, _getSelected(), 1))
            return;

        MarkRecordOrderChanged();
    }

    public bool MoveToPosition(int targetPosition)
    {
        if (!DataPageRecordOrderController.TryMoveToPosition(_getProject().Data, _getSelected(), targetPosition))
            return false;

        MarkRecordOrderChanged();
        return true;
    }

    private void MarkRecordOrderChanged()
    {
        var project = _getProject();
        project.ModifiedAtUtc = DateTime.UtcNow;
        project.Dirty = true;
        _notifyRecordsOrderChanged();
        _scheduleAutoSave();
    }
}
