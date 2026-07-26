using System.Collections.ObjectModel;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Common;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AuswertungPro.Next.UI.ViewModels;

public sealed record PersonalGoldAlbumCodeFilter(
    string? MainCode,
    string DisplayName,
    int TotalCount,
    int FullGoldCount);

/// <summary>Rein lesbares ViewModel fuer das persoenliche Goldstandard-Fotoalbum.</summary>
public sealed partial class PersonalGoldAlbumViewModel : ObservableObject
{
    private readonly IPersonalGoldAlbumService _albumService;
    private readonly string _confirmedByUser;
    private PersonalGoldAlbumSnapshot? _snapshot;

    public PersonalGoldAlbumViewModel(
        IPersonalGoldAlbumService albumService,
        string confirmedByUser)
    {
        _albumService = albumService ?? throw new ArgumentNullException(nameof(albumService));
        ArgumentException.ThrowIfNullOrWhiteSpace(confirmedByUser);
        _confirmedByUser = confirmedByUser;
    }

    [ObservableProperty]
    private ObservableCollection<PersonalGoldAlbumCodeFilter> _codeFilters = new();

    [ObservableProperty]
    private PersonalGoldAlbumCodeFilter? _selectedCodeFilter;

    [ObservableProperty]
    private ObservableCollection<PersonalGoldAlbumItem> _visibleItems = new();

    [ObservableProperty]
    private PersonalGoldAlbumItem? _selectedItem;

    [ObservableProperty]
    private string _summaryText = "Goldstandard-Fotoalbum wird geladen …";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoadCommand))]
    private bool _isBusy;

    partial void OnSelectedCodeFilterChanged(
        PersonalGoldAlbumCodeFilter? oldValue,
        PersonalGoldAlbumCodeFilter? newValue)
        => ApplyFilter(newValue?.MainCode);

    private bool CanLoad() => !IsBusy;

    [RelayCommand(CanExecute = nameof(CanLoad), AllowConcurrentExecutions = false)]
    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        var previousCode = SelectedCodeFilter?.MainCode;
        var previousSampleId = SelectedItem?.SampleId;
        IsBusy = true;
        try
        {
            _snapshot = await _albumService
                .LoadAsync(_confirmedByUser, cancellationToken)
                .ConfigureAwait(true);

            var filters = new List<PersonalGoldAlbumCodeFilter>
            {
                new(
                    MainCode: null,
                    DisplayName: "Alle Codes",
                    TotalCount: _snapshot.TotalSamples,
                    FullGoldCount: _snapshot.FullGoldSamples)
            };
            filters.AddRange(_snapshot.Groups.Select(group => new PersonalGoldAlbumCodeFilter(
                group.MainCode,
                group.MainCode,
                group.Items.Count,
                group.FullGoldCount)));
            CodeFilters = new ObservableCollection<PersonalGoldAlbumCodeFilter>(filters);
            SelectedCodeFilter = CodeFilters.FirstOrDefault(filter =>
                                     string.Equals(
                                         filter.MainCode,
                                         previousCode,
                                         StringComparison.OrdinalIgnoreCase))
                                 ?? CodeFilters.FirstOrDefault();
            ApplyFilter(SelectedCodeFilter?.MainCode, previousSampleId);
            SummaryText =
                $"{_snapshot.TotalSamples} persönliche Beispiele · " +
                $"{_snapshot.FullGoldSamples} vollständig · " +
                $"{_snapshot.IncompleteSamples} unvollständig · " +
                $"{_snapshot.MissingFiles} Bilddateien fehlen";
        }
        catch (OperationCanceledException)
        {
            SummaryText = "Laden abgebrochen.";
        }
        catch (Exception ex)
        {
            _snapshot = null;
            CodeFilters.Clear();
            VisibleItems.Clear();
            SelectedItem = null;
            SummaryText = "Goldstandard-Fotoalbum konnte nicht geladen werden: "
                + UserError.DescribeAndReport(ex, "Goldstandard-Fotoalbum");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyFilter(string? mainCode, string? preferredSampleId = null)
    {
        if (_snapshot is null)
        {
            VisibleItems.Clear();
            SelectedItem = null;
            return;
        }

        var items = string.IsNullOrWhiteSpace(mainCode)
            ? _snapshot.Groups.SelectMany(group => group.Items).ToArray()
            : _snapshot.Groups
                .FirstOrDefault(group => string.Equals(
                    group.MainCode,
                    mainCode,
                    StringComparison.OrdinalIgnoreCase))
                ?.Items
                .ToArray()
              ?? [];
        VisibleItems = new ObservableCollection<PersonalGoldAlbumItem>(items);
        SelectedItem = VisibleItems.FirstOrDefault(item =>
                           string.Equals(
                               item.SampleId,
                               preferredSampleId,
                               StringComparison.OrdinalIgnoreCase))
                       ?? VisibleItems.FirstOrDefault();
    }
}
