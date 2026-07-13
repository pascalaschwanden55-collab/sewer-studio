using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.DataPage;
using CommunityToolkit.Mvvm.Input;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

public sealed partial class DataPageViewModel
{
    private DataPageProjectBindingController CreateProjectBindingController()
        => new(
            handler => _shell.PropertyChanged += handler,
            handler => _shell.PropertyChanged -= handler,
            handler => QgisBridge.QgisBridgeSelection.SelectionChanged += handler,
            handler => QgisBridge.QgisBridgeSelection.SelectionChanged -= handler,
            () => _shell.Project.Id,
            () => Records,
            () => Selected,
            value => Selected = value,
            QgisBridge.QgisBridgeSelection.CurrentFor,
            QgisBridge.QgisBridgeSelection.Set,
            action => { _ = System.Windows.Application.Current?.Dispatcher.InvokeAsync(action); },
            NotifyProjectReadinessChanged,
            NotifyProjectChanged,
            new IRelayCommand?[]
            {
                RemoveCommand,
                MoveUpCommand,
                MoveDownCommand,
                OpenCostsCommand,
                RestoreCostsCommand,
                SuggestMeasuresCommand,
                OptimizeSanierungKiCommand
            },
            NormalizeSelectedFindings,
            SyncSelectedProtocolFromFindings,
            RefreshSelectedProtocolEntries);

    private void NotifyProjectReadinessChanged()
    {
        OnPropertyChanged(nameof(IsProjectReady));
        OnPropertyChanged(nameof(IsDataGridReadOnly));
    }

    private void NotifyProjectChanged()
    {
        OnPropertyChanged(nameof(Project));
        OnPropertyChanged(nameof(Records));
        UpdateSearchResultInfo(Records.Count);
    }

    private void SyncSelectedProtocolFromFindings(HaltungRecord record)
        => _selectedProtocolController.SyncFromFindings(
            record,
            _protocols,
            ResolveCodeTitle,
            RefreshRecordInGrid,
            Selected?.Id == record.Id,
            _codeCatalog);

    private void RefreshSelectedProtocolEntries()
        => _selectedProtocolController.Refresh(Selected, _codeCatalog);

    private string? ResolveCodeTitle(string code)
        => _codeCatalog.TryGet(code, out var codeDef) && !string.IsNullOrWhiteSpace(codeDef.Title)
            ? codeDef.Title
            : null;

    private void NormalizeSelectedFindings(HaltungRecord record)
    {
        if (!VsaFindingNormalizer.Normalize(record))
            return;

        record.ModifiedAtUtc = DateTime.UtcNow;
        _shell.Project.ModifiedAtUtc = DateTime.UtcNow;
        _shell.Project.Dirty = true;
        RefreshRecordInGrid(record);
    }
}
