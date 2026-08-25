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
using AuswertungPro.Next.Application.Dossiers.Lookup;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Models.Dossiers;
using AuswertungPro.Next.UI.Dossiers;
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

    /// <summary>
    /// Wahr, wenn dem Dossier eine Leitung ODER ein Schacht fehlt. Beides ist
    /// derselbe Fall: ein Bauteil, das die Ausgabe nicht mehr findet.
    /// </summary>
    [ObservableProperty]
    private bool _hasMissingParts;

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
        HasMissingParts = snapshot.HasMissingHoldings || snapshot.HasMissingShafts;
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
    private readonly IDossierDialogs _dialogWindows;
    private readonly ICostStoreFactory _costStores;
    private readonly IDialogService _dialogs;
    private readonly ToastService _toasts;
    private readonly ISafeShellOpenService _shellOpen;
    private readonly IExplorerRevealService _explorerReveal;
    private readonly DossierHoldingActionController _holdingActions;
    private readonly DossierShaftActionController _shaftActions;
    private readonly AppSettings _settings;

    private readonly DossierCostCache _costs;

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
        IDossierDialogs dialogWindows,
        ICostStoreFactory costStores,
        IDialogService dialogs,
        ToastService toasts,
        ISafeShellOpenService shellOpen,
        IExplorerRevealService explorerReveal,
        DossierHoldingActionController holdingActions,
        DossierShaftActionController shaftActions,
        AppSettings settings)
    {
        _getProject = getProject ?? throw new ArgumentNullException(nameof(getProject));
        _getProjectFolder = getProjectFolder ?? throw new ArgumentNullException(nameof(getProjectFolder));
        _getProjectFilePath = getProjectFilePath ?? throw new ArgumentNullException(nameof(getProjectFilePath));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _wordExport = wordExport ?? throw new ArgumentNullException(nameof(wordExport));
        _attachments = attachments ?? throw new ArgumentNullException(nameof(attachments));
        _pdfAssembly = pdfAssembly ?? throw new ArgumentNullException(nameof(pdfAssembly));
        _dialogWindows = dialogWindows ?? throw new ArgumentNullException(nameof(dialogWindows));
        _costStores = costStores ?? throw new ArgumentNullException(nameof(costStores));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _toasts = toasts ?? throw new ArgumentNullException(nameof(toasts));
        _shellOpen = shellOpen ?? throw new ArgumentNullException(nameof(shellOpen));
        _explorerReveal = explorerReveal ?? throw new ArgumentNullException(nameof(explorerReveal));
        _holdingActions = holdingActions ?? throw new ArgumentNullException(nameof(holdingActions));
        _shaftActions = shaftActions ?? throw new ArgumentNullException(nameof(shaftActions));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));

        // Einmal zuklappen soll reichen — nicht bei jedem Seitenwechsel erneut.
        _isSummaryCollapsed = _settings.DossierSummaryCollapsed;

        _costs = new DossierCostCache(() => new DossierCostSnapshot(
            LoadCosts(), LoadSchachtCosts()));

        NewDossierCommand = new AsyncRelayCommand(CreateDossierAsync);
        DeleteDossierCommand = new AsyncRelayCommand(DeleteDossierAsync, () => Selected is not null);
        MoveDossierUpCommand = new AsyncRelayCommand(
            () => MoveSelectedDossierAsync(-1),
            CanMoveSelectedDossierUp);
        MoveDossierDownCommand = new AsyncRelayCommand(
            () => MoveSelectedDossierAsync(1),
            CanMoveSelectedDossierDown);
        EditHoldingsCommand = new AsyncRelayCommand(EditHoldingsAsync, () => Selected is not null);
        EditShaftsCommand = new AsyncRelayCommand(EditShaftsAsync, () => Selected is not null);
        EditDossierCommand = new AsyncRelayCommand(EditDossierAsync, () => Selected is not null);
        EditAreaCommand = new AsyncRelayCommand(EditAreaAsync);
        CreateWordCommand = new AsyncRelayCommand(CreateWordAsync, () => Selected is not null);
        PreviewCommand = new AsyncRelayCommand(PreviewAsync, () => Selected is not null);
        CollectAttachmentsCommand = new AsyncRelayCommand(
            CollectAttachmentsAsync, () => Selected is not null);
        AssemblePdfCommand = new AsyncRelayCommand(AssemblePdfAsync, () => Selected is not null);
        OpenFolderCommand = new RelayCommand(OpenFolder, () => Selected is not null);
        SetStatusCommand = new AsyncRelayCommand<DossierStatus?>(SetDossierStatusAsync);
        OpenTemplateCommand = new AsyncRelayCommand(OpenTemplateAsync);
        RefreshCommand = new AsyncRelayCommand(ReloadAsync);
        SaveCommand = new AsyncRelayCommand(SaveNowAsync);
        RefreshDossierCommand = new AsyncRelayCommand(
            RefreshDossierAsync, () => Selected is not null);
        CreateFromProjectCommand = new AsyncRelayCommand(CreateFromProjectAsync);
        PlayHoldingVideoCommand = new RelayCommand<DossierHoldingRow?>(
            PlayHoldingVideo,
            row => row is not null);
        OpenHoldingProtocolCommand = new RelayCommand<DossierHoldingRow?>(
            OpenHoldingProtocol,
            row => row is not null);
        NavigateToHoldingCommand = new RelayCommand<DossierHoldingRow?>(
            NavigateToHolding,
            row => row is not null);
        OpenShaftProtocolCommand = new RelayCommand<DossierShaftRow?>(
            OpenShaftProtocol,
            row => row is not null);
        NavigateToShaftCommand = new RelayCommand<DossierShaftRow?>(
            NavigateToShaft,
            row => row is not null);

        _ = ReloadAsync();
    }

    public ObservableCollection<DossierListItem> Dossiers { get; } = new();

    public ObservableCollection<DossierHoldingRow> HoldingRows { get; } = new();

    /// <summary>Die Schaechte der gewaehlten Liegenschaft.</summary>
    public ObservableCollection<DossierShaftRow> ShaftRows { get; } = new();

    public ObservableCollection<DossierConditionRow> ConditionRows { get; } = new();

    public ObservableCollection<string> TopDamages { get; } = new();

    public IAsyncRelayCommand NewDossierCommand { get; }
    public IAsyncRelayCommand DeleteDossierCommand { get; }

    /// <summary>Verschiebt die gewaehlte Liegenschaft in der gespeicherten Reihenfolge.</summary>
    public IAsyncRelayCommand MoveDossierUpCommand { get; }
    public IAsyncRelayCommand MoveDossierDownCommand { get; }

    public IAsyncRelayCommand EditHoldingsCommand { get; }

    /// <summary>Oeffnet die Auswahl der Schaechte dieser Liegenschaft.</summary>
    public IAsyncRelayCommand EditShaftsCommand { get; }
    public IAsyncRelayCommand EditDossierCommand { get; }
    public IAsyncRelayCommand EditAreaCommand { get; }
    public IAsyncRelayCommand CreateWordCommand { get; }

    public IAsyncRelayCommand PreviewCommand { get; }
    public IAsyncRelayCommand CollectAttachmentsCommand { get; }
    public IAsyncRelayCommand AssemblePdfCommand { get; }
    public IRelayCommand OpenFolderCommand { get; }
    public IAsyncRelayCommand<DossierStatus?> SetStatusCommand { get; }
    public IAsyncRelayCommand OpenTemplateCommand { get; }
    public IAsyncRelayCommand RefreshCommand { get; }

    /// <summary>Speichert den aktuellen Stand der Dossiers von Hand.</summary>
    public IAsyncRelayCommand SaveCommand { get; }

    /// <summary>
    /// Ergaenzt das gewaehlte Dossier um Leitungen und Schaechte, die das
    /// Projekt inzwischen kennt.
    /// </summary>
    public IAsyncRelayCommand RefreshDossierCommand { get; }
    public IAsyncRelayCommand CreateFromProjectCommand { get; }
    public IRelayCommand<DossierHoldingRow?> PlayHoldingVideoCommand { get; }
    public IRelayCommand<DossierHoldingRow?> OpenHoldingProtocolCommand { get; }
    public IRelayCommand<DossierHoldingRow?> NavigateToHoldingCommand { get; }
    public IRelayCommand<DossierShaftRow?> OpenShaftProtocolCommand { get; }
    public IRelayCommand<DossierShaftRow?> NavigateToShaftCommand { get; }

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
    private string _shaftCountText = "0";

    /// <summary>Der Kopfblock mit Kacheln, Zustand und Schaeden ist zugeklappt.</summary>
    [ObservableProperty]
    private bool _isSummaryCollapsed;

    /// <summary>Die eine Zeile, die im zugeklappten Zustand stehen bleibt.</summary>
    [ObservableProperty]
    private string _summaryLine = "";

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

    partial void OnIsSummaryCollapsedChanged(bool value)
    {
        _settings.DossierSummaryCollapsed = value;
        _settings.Save();
    }

    partial void OnMissingWarningChanged(string value)
        => OnPropertyChanged(nameof(HasMissingWarning));

    // ── Laden ─────────────────────────────────────────────────────────────

    private async Task ReloadAsync()
    {
        // „Aktualisieren" heisst auch: die Kostendateien erneut lesen.
        _costs.Invalidate();

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

        // Die Reihenfolge in dossiers.json ist die vom Benutzer festgelegte
        // Reihenfolge. Eine alphabetische Anzeige wuerde das Verschieben nach
        // jedem erneuten Laden scheinbar rueckgaengig machen.
        foreach (var definition in _document.Dossiers)
            Dossiers.Add(new DossierListItem(definition, BuildSnapshot(definition)));

        Selected = previous is null
            ? Dossiers.FirstOrDefault()
            : Dossiers.FirstOrDefault(d => d.Id == previous) ?? Dossiers.FirstOrDefault();

        OnPropertyChanged(nameof(HasDossiers));
        RefreshDetail();
    }

    private DossierSnapshot BuildSnapshot(DossierDefinition definition)
    {
        var kosten = _costs.Get();

        return DossierSnapshotBuilder.Build(
            definition, _getProject(), kosten.Haltungen, kosten.Schaechte);
    }

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

    /// <summary>
    /// Die Schachtkosten aus BEIDEN gepflegten Quellen: der Schacht-Matrix
    /// (<c>schacht_costs.json</c>) und dem Massnahmen-Dialog
    /// (<c>schacht_empfehlungen.json</c>).
    ///
    /// Die Matrix hat Vorrang, die Empfehlung ist der Rueckfall; addiert wird
    /// NIE, sonst stuende derselbe Schacht doppelt und zu teuer im Dossier.
    /// Dieselbe Regel wie in Projektuebersicht und Druckcenter — eine eigene
    /// zweite Regel wuerde dem Eigentuemer einen anderen Betrag nennen als der
    /// Ausdruck.
    /// </summary>
    private ProjectCostStore LoadSchachtCosts()
    {
        var matrix = LoadCostFile("schacht_costs.json");
        var empfehlungen = LoadCostFile("schacht_empfehlungen.json");

        return SchachtCostStoreMerger.Merge(matrix, empfehlungen);
    }

    private ProjectCostStore LoadCostFile(string fileName)
    {
        try
        {
            return _costStores.CreateProjectCostStore(fileName).Load(_getProjectFilePath());
        }
        catch
        {
            return new ProjectCostStore();
        }
    }

    private void RefreshDetail()
    {
        HoldingRows.Clear();
        ShaftRows.Clear();
        ConditionRows.Clear();
        TopDamages.Clear();

        if (Selected is null)
        {
            DetailTitle = "";
            DetailSubtitle = "";
            HoldingCountText = "0";
            ShaftCountText = "0";
            SummaryLine = "";
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
        ShaftCountText = snapshot.ShaftCount.ToString(CultureInfo.InvariantCulture);
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
                holding.HoldingId,
                holding.HoldingName,
                holding.LengthMeters is > 0
                    ? holding.LengthMeters.Value.ToString("0.00", Ch) + " m"
                    : "—",
                DescribeCondition(holding.ConditionClass),
                holding.Measures,
                holding.NetCost > 0m ? holding.NetCost.ToString("#,##0.00", Ch) : "—"));
        }

        foreach (var shaft in snapshot.Shafts)
            ShaftRows.Add(BuildShaftRow(shaft));

        SummaryLine = BuildSummaryLine(snapshot);

        MissingWarning = BuildMissingWarning(snapshot);
    }

    /// <summary>
    /// Der Warnhinweis ueber fehlende Bauteile.
    ///
    /// Auch Schaechte werden genannt: eine Nummer ohne Datensatz verschwand
    /// bisher spurlos aus Tabelle und Word-Datei, und niemand erfuhr davon.
    /// Die Nummern stehen mit dabei, sonst muesste man sie suchen.
    /// </summary>
    public static string BuildMissingWarning(DossierSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var teile = new List<string>();

        if (snapshot.HasMissingHoldings)
        {
            var anzahl = snapshot.MissingHoldingIds.Count;
            teile.Add(anzahl == 1
                ? "1 zugeordnete Leitung ist nicht mehr im Projekt"
                : $"{anzahl} zugeordnete Leitungen sind nicht mehr im Projekt");
        }

        if (snapshot.HasMissingShafts)
        {
            var nummern = string.Join(", ", snapshot.MissingShaftNumbers);
            teile.Add(snapshot.MissingShaftNumbers.Count == 1
                ? $"Schacht {nummern} ist nicht mehr im Projekt"
                : $"Die Schächte {nummern} sind nicht mehr im Projekt");
        }

        return teile.Count == 0
            ? ""
            : string.Join(". ", teile) + ". Bitte die Auswahl prüfen.";
    }

    /// <summary>
    /// Der Zusammenzug, der sichtbar bleibt, wenn der Kopfblock zugeklappt ist.
    ///
    /// Kennzahlen und Zustandsblock brauchen rund ein Drittel der Hoehe, obwohl
    /// man sie beim Arbeiten selten liest — von fuenf Leitungen blieben drei
    /// sichtbar. Zugeklappt verschwinden nicht die Zahlen, nur ihr Platz.
    /// </summary>
    public static string BuildSummaryLine(DossierSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var teile = new List<string>();

        if (snapshot.HoldingCount > 0)
        {
            teile.Add(snapshot.HoldingCount == 1
                ? "1 Leitung"
                : $"{snapshot.HoldingCount} Leitungen");
        }

        // Ohne Schaechte steht dort nichts: „0 Schaechte" waere Rauschen, und
        // die meisten Liegenschaften haben keine.
        if (snapshot.ShaftCount > 0)
        {
            teile.Add(snapshot.ShaftCount == 1
                ? "1 Schacht"
                : $"{snapshot.ShaftCount} Schächte");
        }

        if (snapshot.LengthTotal > 0)
            teile.Add(snapshot.LengthTotal.ToString("0.00", Ch) + " m");

        if (snapshot.NetCostTotal > 0m)
            teile.Add("CHF " + snapshot.NetCostTotal.ToString("#,##0.00", Ch));

        var dringend = snapshot.Statistics.DringendCount;
        if (dringend > 0)
            teile.Add($"{dringend} dringend (Z0/Z1)");

        return teile.Count == 0 ? "Noch nichts zugeordnet" : string.Join(" · ", teile);
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

    /// <summary>
    /// Eine Schachtzeile fuer die Anzeige. Fehlende Angaben werden zum Strich:
    /// eine leere Zelle liesse offen, ob nichts erfasst oder nichts noetig ist,
    /// und "0.00" waere eine Zahl, die niemand geprueft hat.
    /// </summary>
    public static DossierShaftRow BuildShaftRow(DossierShaftLine line)
    {
        ArgumentNullException.ThrowIfNull(line);

        return new DossierShaftRow(
            line.ShaftId,
            OderStrich(line.Number),
            OderStrich(line.Funktion),
            OderStrich(line.Measures),
            line.NetCost > 0m ? line.NetCost.ToString("#,##0.00", Ch) : "—");
    }

    /// <summary>Die Rueckmeldung nach der Schachtauswahl.</summary>
    public static string SchaechteZugeordnet(int anzahl) => anzahl switch
    {
        <= 0 => "Kein Schacht zugeordnet.",
        1 => "1 Schacht zugeordnet.",
        _ => $"{anzahl} Schächte zugeordnet."
    };

    private static string OderStrich(string? value)
        => string.IsNullOrWhiteSpace(value) ? "—" : value.Trim();

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
        MoveDossierUpCommand.NotifyCanExecuteChanged();
        MoveDossierDownCommand.NotifyCanExecuteChanged();
        EditHoldingsCommand.NotifyCanExecuteChanged();
        EditShaftsCommand.NotifyCanExecuteChanged();
        EditDossierCommand.NotifyCanExecuteChanged();
        CreateWordCommand.NotifyCanExecuteChanged();
        PreviewCommand.NotifyCanExecuteChanged();
        CollectAttachmentsCommand.NotifyCanExecuteChanged();
        AssemblePdfCommand.NotifyCanExecuteChanged();
        OpenFolderCommand.NotifyCanExecuteChanged();
        RefreshDossierCommand.NotifyCanExecuteChanged();
    }

    private void PlayHoldingVideo(DossierHoldingRow? row)
    {
        if (row is not null)
            _holdingActions.PlayVideo(row.HoldingId);
    }

    private void OpenHoldingProtocol(DossierHoldingRow? row)
    {
        if (row is not null)
            _holdingActions.OpenProtocol(row.HoldingId);
    }

    private void NavigateToHolding(DossierHoldingRow? row)
    {
        if (row is not null)
            _holdingActions.NavigateToHolding(row.HoldingId);
    }

    private void OpenShaftProtocol(DossierShaftRow? row)
    {
        if (row is not null)
            _shaftActions.OpenProtocol(row.ShaftId);
    }

    private void NavigateToShaft(DossierShaftRow? row)
    {
        if (row is not null)
            _shaftActions.NavigateToShaft(row.ShaftId);
    }
}

/// <summary>Eine Leitungszeile im Dossier-Cockpit.</summary>
public sealed record DossierHoldingRow(
    Guid HoldingId,
    string Holding,
    string Length,
    string Condition,
    string Measures,
    string Cost);

/// <summary>
/// Eine Schachtzeile im Dossier-Cockpit.
///
/// Bewusst nicht dieselbe Zeile wie bei den Leitungen: ein Schacht hat keine
/// Laenge, dafuer eine Funktion, und seine Massnahme steht nicht im
/// Projektdatensatz, sondern in den Kostendateien.
/// </summary>
public sealed record DossierShaftRow(
    Guid ShaftId,
    string Shaft,
    string Funktion,
    string Measures,
    string Cost);

/// <summary>Eine Zustandsklasse mit Anteil.</summary>
public sealed record DossierConditionRow(string Label, int Count, double Percent);
