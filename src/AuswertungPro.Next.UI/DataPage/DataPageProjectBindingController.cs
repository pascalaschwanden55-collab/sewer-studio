using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.ViewModels;
using AuswertungPro.Next.UI.ViewModels.Pages;
using CommunityToolkit.Mvvm.Input;

namespace AuswertungPro.Next.UI.DataPage;

/// <summary>
/// Haelt die Datenseite mit dem aktuellen Projekt, der Karten-Auswahl und den
/// transienten Zeilennummern synchron.
/// </summary>
internal sealed class DataPageProjectBindingController : IDisposable
{
    private readonly Action<PropertyChangedEventHandler> _subscribeProjectState;
    private readonly Action<PropertyChangedEventHandler> _unsubscribeProjectState;
    private readonly Action<Action> _subscribeMapSelection;
    private readonly Action<Action> _unsubscribeMapSelection;
    private readonly Func<Guid> _getProjectId;
    private readonly Func<ObservableCollection<HaltungRecord>> _getRecords;
    private readonly Func<HaltungRecord?> _getSelected;
    private readonly Action<HaltungRecord?> _setSelected;
    private readonly Func<Guid, string?> _getMapSelection;
    private readonly Action<string?> _setMapSelection;
    private readonly Action<Action> _dispatch;
    private readonly Action _projectReadinessChanged;
    private readonly Action _projectChanged;
    private readonly IReadOnlyList<IRelayCommand?> _selectionCommands;
    private readonly Action<HaltungRecord> _normalizeSelectedFindings;
    private readonly Action<HaltungRecord> _syncSelectedProtocolFromFindings;
    private readonly Action _refreshSelectedProtocolEntries;
    private INotifyCollectionChanged? _numberedRecords;
    private bool _applyingMapSelection;
    private bool _started;
    private bool _disposed;

    public DataPageProjectBindingController(
        Action<PropertyChangedEventHandler> subscribeProjectState,
        Action<PropertyChangedEventHandler> unsubscribeProjectState,
        Action<Action> subscribeMapSelection,
        Action<Action> unsubscribeMapSelection,
        Func<Guid> getProjectId,
        Func<ObservableCollection<HaltungRecord>> getRecords,
        Func<HaltungRecord?> getSelected,
        Action<HaltungRecord?> setSelected,
        Func<Guid, string?> getMapSelection,
        Action<string?> setMapSelection,
        Action<Action> dispatch,
        Action projectReadinessChanged,
        Action projectChanged,
        IReadOnlyList<IRelayCommand?> selectionCommands,
        Action<HaltungRecord> normalizeSelectedFindings,
        Action<HaltungRecord> syncSelectedProtocolFromFindings,
        Action refreshSelectedProtocolEntries)
    {
        _subscribeProjectState = subscribeProjectState;
        _unsubscribeProjectState = unsubscribeProjectState;
        _subscribeMapSelection = subscribeMapSelection;
        _unsubscribeMapSelection = unsubscribeMapSelection;
        _getProjectId = getProjectId;
        _getRecords = getRecords;
        _getSelected = getSelected;
        _setSelected = setSelected;
        _getMapSelection = getMapSelection;
        _setMapSelection = setMapSelection;
        _dispatch = dispatch;
        _projectReadinessChanged = projectReadinessChanged;
        _projectChanged = projectChanged;
        _selectionCommands = selectionCommands;
        _normalizeSelectedFindings = normalizeSelectedFindings;
        _syncSelectedProtocolFromFindings = syncSelectedProtocolFromFindings;
        _refreshSelectedProtocolEntries = refreshSelectedProtocolEntries;
    }

    public void Start()
    {
        if (_started)
            return;

        _started = true;
        _subscribeProjectState(OnProjectStateChanged);
        _subscribeMapSelection(OnMapSelectionChanged);
        HookRunningNumbers();
        OnMapSelectionChanged();
    }

    public void HandleSelectedChanged(HaltungRecord? selected)
    {
        if (!_applyingMapSelection)
            _setMapSelection(selected?.GetFieldValue("Haltungsname"));

        DataPageSelectionChangedController.Handle(
            selected,
            _selectionCommands,
            _normalizeSelectedFindings,
            _syncSelectedProtocolFromFindings,
            _refreshSelectedProtocolEntries);
    }

    private void OnProjectStateChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ShellViewModel.IsProjectReady))
        {
            _projectReadinessChanged();
        }
        else if (e.PropertyName == nameof(ShellViewModel.Project))
        {
            _projectChanged();
            HookRunningNumbers();
        }
    }

    private void OnMapSelectionChanged()
    {
        var name = _getMapSelection(_getProjectId());
        if (string.IsNullOrWhiteSpace(name))
            return;

        _dispatch(() => ApplyMapSelection(name));
    }

    private void ApplyMapSelection(string name)
    {
        if (_disposed)
            return;

        var record = FindRecordByName(_getRecords(), name);
        if (record is null || ReferenceEquals(record, _getSelected()))
            return;

        _applyingMapSelection = true;
        try
        {
            _setSelected(record);
        }
        finally
        {
            _applyingMapSelection = false;
        }
    }

    internal static HaltungRecord? FindRecordByName(IEnumerable<HaltungRecord> records, string name)
        => records.FirstOrDefault(record => string.Equals(
               record.GetFieldValue("Haltungsname"), name, StringComparison.OrdinalIgnoreCase))
           ?? records.FirstOrDefault(record => KarteHaltungNameMatcher.Matches(
               name,
               record.GetFieldValue("Haltungsname")));

    private void HookRunningNumbers()
    {
        if (_numberedRecords is not null)
            _numberedRecords.CollectionChanged -= OnRecordsCollectionChanged;

        var records = _getRecords();
        _numberedRecords = records;
        _numberedRecords.CollectionChanged += OnRecordsCollectionChanged;
        HaltungRunningNumberService.AssignNr(records);
    }

    private void OnRecordsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => HaltungRunningNumberService.AssignNr(_getRecords());

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        if (_started)
        {
            _unsubscribeProjectState(OnProjectStateChanged);
            _unsubscribeMapSelection(OnMapSelectionChanged);
        }

        if (_numberedRecords is not null)
            _numberedRecords.CollectionChanged -= OnRecordsCollectionChanged;
    }
}
