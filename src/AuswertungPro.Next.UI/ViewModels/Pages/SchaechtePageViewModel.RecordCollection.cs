using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.DataPage;
using CommunityToolkit.Mvvm.Input;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

public sealed partial class SchaechtePageViewModel
{
    private SchaechteRecordCollectionController? _recordCollectionController;

    private SchaechteRecordCollectionController RecordCollectionController
        => _recordCollectionController ??= new(
            () => Records,
            () => Columns,
            _shell.CollectionLock);

    private void Add()
    {
        var record = RecordCollectionController.Add();
        SetSelectedWithoutRequiredFieldWarning(record);
        UpdateSearchResultInfo(Records.Count);
        MarkRecordCollectionChanged();
    }

    private void Remove()
    {
        if (!RecordCollectionController.TryRemove(Selected, out var nextSelection))
            return;

        SetSelectedWithoutRequiredFieldWarning(nextSelection);
        UpdateNr();
        UpdateSearchResultInfo(Records.Count);
        MarkRecordCollectionChanged();
    }

    private bool CanMoveUp()
        => RecordCollectionController.CanMoveUp(Selected);

    private bool CanMoveDown()
        => RecordCollectionController.CanMoveDown(Selected);

    private void MoveUp()
    {
        if (!RecordCollectionController.TryMoveUp(Selected))
            return;

        CompleteRecordMove();
    }

    private void MoveDown()
    {
        if (!RecordCollectionController.TryMoveDown(Selected))
            return;

        CompleteRecordMove();
    }

    /// <summary>
    /// Verschiebt den ausgewaehlten Schacht auf die angegebene 1-basierte Position.
    /// </summary>
    public bool MoveToPosition(int targetPosition)
    {
        if (!RecordCollectionController.TryMoveToPosition(Selected, targetPosition))
            return false;

        CompleteRecordMove();
        return true;
    }

    private void UpdateNr()
        => RecordCollectionController.Renumber();

    private void CompleteRecordMove()
    {
        UpdateNr();
        MarkRecordCollectionChanged();
        (MoveUpCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (MoveDownCommand as RelayCommand)?.NotifyCanExecuteChanged();
    }

    private void MarkRecordCollectionChanged()
    {
        _shell.Project.ModifiedAtUtc = DateTime.UtcNow;
        _shell.Project.Dirty = true;
    }

    private void SetSelectedWithoutRequiredFieldWarning(SchachtRecord? record)
    {
        _suppressRequiredFieldWarning = true;
        try
        {
            Selected = record;
        }
        finally
        {
            _suppressRequiredFieldWarning = false;
        }
    }
}
