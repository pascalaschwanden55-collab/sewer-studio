using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Costs;
using AuswertungPro.Next.Application.Dossiers;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Models.Dossiers;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

/// <summary>
/// Eine Zeile in der Dossier-Liste. Bewusst ein eigenes Anzeigemodell: die
/// Liste soll den Stand zeigen, ohne dass die Oberflaeche die Rechenlogik kennt.
/// </summary>
public sealed partial class DossierListItem : ObservableObject
{
    public DossierListItem(DossierDefinition definition, DossierSnapshot snapshot)
    {
        Definition = definition;
        Apply(snapshot);
    }

    public DossierDefinition Definition { get; }

    public Guid Id => Definition.Id;

    [ObservableProperty]
    private string _name = "";

    [ObservableProperty]
    private string _owner = "";

    [ObservableProperty]
    private string _summary = "";

    [ObservableProperty]
    private string _statusText = "";

    [ObservableProperty]
    private bool _hasMissingHoldings;

    public void Apply(DossierSnapshot snapshot)
    {
        Name = string.IsNullOrWhiteSpace(Definition.Name) ? "(ohne Name)" : Definition.Name;
        Owner = Definition.OwnerName;

        var parts = new List<string>
        {
            snapshot.HoldingCount == 1 ? "1 Leitung" : $"{snapshot.HoldingCount} Leitungen"
        };

        if (snapshot.LengthTotal > 0)
            parts.Add(snapshot.LengthTotal.ToString("0.0", DossiersPageViewModel.Ch) + " m");

        if (snapshot.NetCostTotal > 0m)
            parts.Add("CHF " + snapshot.NetCostTotal.ToString("#,##0", DossiersPageViewModel.Ch));

        Summary = string.Join(" · ", parts);
        StatusText = DossiersPageViewModel.DescribeStatus(Definition.Status);
        HasMissingHoldings = snapshot.HasMissingHoldings;
    }
}

/// <summary>
/// Der Dossier-Bereich: links die Liegenschaften des Gebiets, rechts das
/// Cockpit der gewaehlten Liegenschaft.
///
/// Die Seite haelt nur Anzeigezustand und Befehle. Auswahl, Kennzahlen,
/// Dateizugriff und Ausgabe liegen in den Diensten der Application- und
/// Infrastructure-Schicht.
/// </summary>
public sealed partial class DossiersPageViewModel : ObservableObject
{
    internal static readonly CultureInfo Ch = CultureInfo.GetCultureInfo("de-CH");

    private readonly Func<Project> _getProject;
    private readonly Func<string?> _getProjectFolder;
    private readonly Func<string?> _getProjectFilePath;
    private readonly IDossierStore _store;
    private readonly IDossierWordExportService _wordExport;
    private readonly IDossierAttachmentService _attachments;
    private readonly IDossierPdfAssemblyService _pdfAssembly;
    private readonly ICostStoreFactory _costStores;
    private readonly IDialogService _dialogs;
    private readonly ToastService _toasts;
    private readonly ISafeShellOpenService _shellOpen;
    private readonly IExplorerRevealService _explorerReveal;

    private DossierDocument _document = new();
    private bool _loaded;

    public DossiersPageViewModel(
        Func<Project> getProject,
        Func<string?> getProjectFolder,
        Func<string?> getProjectFilePath,
        IDossierStore store,
        IDossierWordExportService wordExport,
        IDossierAttachmentService attachments,
        IDossierPdfAssemblyService pdfAssembly,
        ICostStoreFactory costStores,
        IDialogService dialogs,
        ToastService toasts,
        ISafeShellOpenService shellOpen,
        IExplorerRevealService explorerReveal)
    {
        _getProject = getProject ?? throw new ArgumentNullException(nameof(getProject));
        _getProjectFolder = getProjectFolder ?? throw new ArgumentNullException(nameof(getProjectFolder));
        _getProjectFilePath = getProjectFilePath ?? throw new ArgumentNullException(nameof(getProjectFilePath));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _wordExport = wordExport ?? throw new ArgumentNullException(nameof(wordExport));
        _attachments = attachments ?? throw new ArgumentNullException(nameof(attachments));
        _pdfAssembly = pdfAssembly ?? throw new ArgumentNullException(nameof(pdfAssembly));
        _costStores = costStores ?? throw new ArgumentNullException(nameof(costStores));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _toasts = toasts ?? throw new ArgumentNullException(nameof(toasts));
        _shellOpen = shellOpen ?? throw new ArgumentNullException(nameof(shellOpen));
        _explorerReveal = explorerReveal ?? throw new ArgumentNullException(nameof(explorerReveal));

        NewDossierCommand = new AsyncRelayCommand(CreateDossierAsync);
        DeleteDossierCommand = new AsyncRelayCommand(DeleteDossierAsync, () => Selected is not null);
        EditHoldingsCommand = new AsyncRelayCommand(EditHoldingsAsync, () => Selected is not null);
        EditDossierCommand = new AsyncRelayCommand(EditDossierAsync, () => Selected is not null);
        EditAreaCommand = new AsyncRelayCommand(EditAreaAsync);
        CreateWordCommand = new AsyncRelayCommand(CreateWordAsync, () => Selected is not null);
        CollectAttachmentsCommand = new AsyncRelayCommand(
            CollectAttachmentsAsync, () => Selected is not null);
        AssemblePdfCommand = new AsyncRelayCommand(AssemblePdfAsync, () => Selected is not null);
        OpenFolderCommand = new RelayCommand(OpenFolder, () => Selected is not null);
        SetStatusCommand = new AsyncRelayCommand<DossierStatus?>(SetDossierStatusAsync);
        ResetTemplateCommand = new AsyncRelayCommand(ResetTemplateAsync);
        RefreshCommand = new AsyncRelayCommand(ReloadAsync);

        _ = ReloadAsync();
    }

    public ObservableCollection<DossierListItem> Dossiers { get; } = new();

    public ObservableCollection<DossierHoldingRow> HoldingRows { get; } = new();

    public ObservableCollection<DossierConditionRow> ConditionRows { get; } = new();

    public ObservableCollection<string> TopDamages { get; } = new();

    public IAsyncRelayCommand NewDossierCommand { get; }
    public IAsyncRelayCommand DeleteDossierCommand { get; }
    public IAsyncRelayCommand EditHoldingsCommand { get; }
    public IAsyncRelayCommand EditDossierCommand { get; }
    public IAsyncRelayCommand EditAreaCommand { get; }
    public IAsyncRelayCommand CreateWordCommand { get; }
    public IAsyncRelayCommand CollectAttachmentsCommand { get; }
    public IAsyncRelayCommand AssemblePdfCommand { get; }
    public IRelayCommand OpenFolderCommand { get; }
    public IAsyncRelayCommand<DossierStatus?> SetStatusCommand { get; }
    public IAsyncRelayCommand ResetTemplateCommand { get; }
    public IAsyncRelayCommand RefreshCommand { get; }

    [ObservableProperty]
    private DossierListItem? _selected;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusMessage = "";

    [ObservableProperty]
    private string _areaTitle = "";

    // ── Kennzahlen des gewaehlten Dossiers ────────────────────────────────

    [ObservableProperty]
    private string _detailTitle = "";

    [ObservableProperty]
    private string _detailSubtitle = "";

    [ObservableProperty]
    private string _holdingCountText = "0";

    [ObservableProperty]
    private string _lengthText = "—";

    [ObservableProperty]
    private string _costText = "—";

    [ObservableProperty]
    private string _urgentText = "0";

    [ObservableProperty]
    private string _missingWarning = "";

    public bool HasSelection => Selected is not null;

    public bool HasDossiers => Dossiers.Count > 0;

    public bool HasMissingWarning => !string.IsNullOrWhiteSpace(MissingWarning);

    partial void OnSelectedChanged(DossierListItem? value)
    {
        RefreshDetail();
        OnPropertyChanged(nameof(HasSelection));
        NotifyCommands();
    }

    partial void OnMissingWarningChanged(string value)
        => OnPropertyChanged(nameof(HasMissingWarning));

    // ── Laden ─────────────────────────────────────────────────────────────

    private async Task ReloadAsync()
    {
        var root = _getProjectFolder();
        if (string.IsNullOrWhiteSpace(root))
        {
            _document = new DossierDocument();
            Dossiers.Clear();
            Selected = null;
            StatusMessage = "Kein Projekt geöffnet.";
            _loaded = false;
            OnPropertyChanged(nameof(HasDossiers));
            return;
        }

        try
        {
            _document = await _store.LoadAsync(root);
            _loaded = true;
            StatusMessage = "";
        }
        catch (Exception ex)
        {
            // Fail-closed: bei unlesbarer Datei nichts anlegen und nichts
            // ueberschreiben, sondern den Grund zeigen.
            _document = new DossierDocument();
            _loaded = false;
            StatusMessage = ex.Message;
        }

        AreaTitle = _document.Area.AreaTitle;
        RebuildList();
    }

    private void RebuildList()
    {
        var previous = Selected?.Id;
        Dossiers.Clear();

        foreach (var definition in _document.Dossiers.OrderBy(d => d.Name, StringComparer.CurrentCultureIgnoreCase))
            Dossiers.Add(new DossierListItem(definition, BuildSnapshot(definition)));

        Selected = previous is null
            ? Dossiers.FirstOrDefault()
            : Dossiers.FirstOrDefault(d => d.Id == previous) ?? Dossiers.FirstOrDefault();

        OnPropertyChanged(nameof(HasDossiers));
        RefreshDetail();
    }

    private DossierSnapshot BuildSnapshot(DossierDefinition definition)
        => DossierSnapshotBuilder.Build(definition, _getProject(), LoadCosts());

    private ProjectCostStore LoadCosts()
    {
        try
        {
            var repository = _costStores.CreateProjectCostStore();
            return repository.Load(_getProjectFilePath());
        }
        catch
        {
            // Ohne Kostendaten bleiben die Kennzahlen ohne Geldwerte; das ist
            // besser als eine Seite, die gar nicht mehr aufgeht.
            return new ProjectCostStore();
        }
    }

    private void RefreshDetail()
    {
        HoldingRows.Clear();
        ConditionRows.Clear();
        TopDamages.Clear();

        if (Selected is null)
        {
            DetailTitle = "";
            DetailSubtitle = "";
            HoldingCountText = "0";
            LengthText = "—";
            CostText = "—";
            UrgentText = "0";
            MissingWarning = "";
            return;
        }

        var definition = Selected.Definition;
        var snapshot = BuildSnapshot(definition);
        Selected.Apply(snapshot);

        DetailTitle = Selected.Name;
        DetailSubtitle = BuildSubtitle(definition);

        HoldingCountText = snapshot.HoldingCount.ToString(CultureInfo.InvariantCulture);
        LengthText = snapshot.LengthTotal > 0
            ? snapshot.LengthTotal.ToString("0.00", Ch) + " m"
            : "—";
        CostText = snapshot.NetCostTotal > 0m
            ? snapshot.NetCostTotal.ToString("#,##0.00", Ch)
            : "—";

        var statistics = snapshot.Statistics;
        UrgentText = statistics.DringendCount.ToString(CultureInfo.InvariantCulture);

        foreach (var bucket in statistics.Haltungen.Buckets.Where(b => b.Count > 0))
            ConditionRows.Add(new DossierConditionRow(bucket.Label, bucket.Count, bucket.Percent));

        foreach (var damage in statistics.TopSchaeden.Take(6))
            TopDamages.Add($"{damage.Label} {damage.Count}×");

        foreach (var holding in snapshot.Holdings)
        {
            HoldingRows.Add(new DossierHoldingRow(
                holding.HoldingName,
                holding.LengthMeters is > 0
                    ? holding.LengthMeters.Value.ToString("0.00", Ch) + " m"
                    : "—",
                DescribeCondition(holding.ConditionClass),
                holding.Measures,
                holding.NetCost > 0m ? holding.NetCost.ToString("#,##0.00", Ch) : "—"));
        }

        MissingWarning = snapshot.HasMissingHoldings
            ? $"{snapshot.MissingHoldingIds.Count} zugeordnete Leitung(en) sind nicht mehr im Projekt. "
              + "Bitte die Auswahl prüfen."
            : "";
    }

    /// <summary>Internal statt private, damit der reine Textaufbau direkt testbar ist.</summary>
    internal static string BuildSubtitle(DossierDefinition d)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(d.ParcelNumbers))
            parts.Add("Parz. " + d.ParcelNumbers.Trim());

        var ownerName = d.OwnerName;
        if (string.IsNullOrWhiteSpace(ownerName))
        {
            // Wer nur die neue Tabelle fuellt, soll in der Uebersicht nicht
            // "Noch keine Stammdaten erfasst" lesen, waehrend im Word bereits
            // alle Namen stehen.
            ownerName = d.Owners.FirstOrDefault(o => !string.IsNullOrWhiteSpace(o.Name))?.Name;
        }

        if (!string.IsNullOrWhiteSpace(ownerName))
            parts.Add(ownerName.Trim());

        if (!string.IsNullOrWhiteSpace(d.Town))
            parts.Add(d.Town.Trim());

        return parts.Count == 0 ? "Noch keine Stammdaten erfasst" : string.Join(" · ", parts);
    }

    internal static string DescribeStatus(DossierStatus status) => status switch
    {
        DossierStatus.WordErzeugt => "Word erzeugt",
        DossierStatus.Versendet => "versendet",
        DossierStatus.Zurueck => "zurück / unterschrieben",
        _ => "offen"
    };

    private static string DescribeCondition(string conditionClass) => conditionClass switch
    {
        "0" => "Z0 – sofort",
        "1" => "Z1 – kurzfristig",
        "2" => "Z2 – mittelfristig",
        "3" => "Z3 – langfristig",
        "4" => "Z4 – kein Mangel",
        _ => "—"
    };

    private void NotifyCommands()
    {
        DeleteDossierCommand.NotifyCanExecuteChanged();
        EditHoldingsCommand.NotifyCanExecuteChanged();
        EditDossierCommand.NotifyCanExecuteChanged();
        CreateWordCommand.NotifyCanExecuteChanged();
        CollectAttachmentsCommand.NotifyCanExecuteChanged();
        AssemblePdfCommand.NotifyCanExecuteChanged();
        OpenFolderCommand.NotifyCanExecuteChanged();
    }
}

/// <summary>Eine Leitungszeile im Dossier-Cockpit.</summary>
public sealed record DossierHoldingRow(
    string Holding,
    string Length,
    string Condition,
    string Measures,
    string Cost);

/// <summary>Eine Zustandsklasse mit Anteil.</summary>
public sealed record DossierConditionRow(string Label, int Count, double Percent);
