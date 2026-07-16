using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using AuswertungPro.Next.Application.Costs;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Costs;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

/// <summary>Eine Haltungs-Zeile in der Sanierungs-Matrix.</summary>
public sealed partial class SanierungMatrixRowVm : ObservableObject
{
    private readonly Action<SanierungMatrixRowVm>? _onChanged;
    private bool _suppress;

    public HaltungRecord Record { get; }
    public string Holding { get; }
    public string Dn { get; }
    public string Laenge { get; }
    public int Anschluesse { get; }
    public HoldingCost? StoredCost { get; private set; }

    [ObservableProperty] private MeasureOption? _selectedMeasure;
    [ObservableProperty] private decimal _menge;
    [ObservableProperty] private bool _isMengeEditierbar;

    /// <summary>Mengen-Zelle nur bei Stk-Hauptarbeit editierbar; bei Metern gesperrt.</summary>
    public bool IsMengeReadOnly => !IsMengeEditierbar;
    [ObservableProperty] private bool _optVerkehrsdienst;
    [ObservableProperty] private bool _optWasserhaltung;
    [ObservableProperty] private bool _optFraesen;
    [ObservableProperty] private bool _optDichtheit;
    [ObservableProperty] private bool _optDokumentation;
    [ObservableProperty] private decimal _total;
    [ObservableProperty] private string _hinweis = "";
    [ObservableProperty] private bool _hasMultipleStoredMeasures;
    [ObservableProperty] private string _measuresSummary = SanierungsMatrixMeasureSummaryFormatter.EmptySummary;

    public bool IsMatrixEditable => !HasMultipleStoredMeasures;

    public SanierungMatrixRowVm(
        HaltungRecord record,
        string holding,
        string dn,
        string laenge,
        int anschluesse,
        Action<SanierungMatrixRowVm>? onChanged)
    {
        Record = record;
        Holding = holding;
        Dn = dn;
        Laenge = laenge;
        Anschluesse = anschluesse;
        _onChanged = onChanged;
    }

    /// <summary>Vorbelegen aus gespeicherten Kosten ohne Neuberechnung.</summary>
    public void InitFrom(
        MeasureOption? option,
        decimal total,
        decimal menge,
        bool vd,
        bool wasser,
        bool fraesen,
        bool dichtheit,
        bool doku)
    {
        _suppress = true;
        SelectedMeasure = option;
        IsMengeEditierbar = option?.ManuelleMenge == true;
        Menge = menge;
        OptVerkehrsdienst = vd;
        OptWasserhaltung = wasser;
        OptFraesen = fraesen;
        OptDichtheit = dichtheit;
        OptDokumentation = doku;
        Total = total;
        _suppress = false;
    }

    public void SetStoredCost(HoldingCost? cost)
    {
        StoredCost = cost;
        MeasuresSummary = SanierungsMatrixMeasureSummaryFormatter.FormatSummary(cost);
    }

    /// <summary>
    /// Spiegelt die Zusatzoptionen aus dem Detail-Editor, ohne eine Neuberechnung auszuloesen.
    /// </summary>
    public void SetOptionFlags(bool vd, bool wasser, bool fraesen, bool dichtheit, bool doku)
    {
        _suppress = true;
        OptVerkehrsdienst = vd;
        OptWasserhaltung = wasser;
        OptFraesen = fraesen;
        OptDichtheit = dichtheit;
        OptDokumentation = doku;
        _suppress = false;
    }

    public void MarkMultipleStoredMeasures()
    {
        HasMultipleStoredMeasures = true;
        Hinweis = "Mehrfach-Massnahme: im Detail bearbeiten";
    }

    partial void OnSelectedMeasureChanged(MeasureOption? value)
    {
        if (_suppress)
            return;

        _suppress = true;
        IsMengeEditierbar = value?.ManuelleMenge == true;
        if (value?.ManuelleMenge == true)
        {
            if (Menge <= 0m)
                Menge = 1m;
        }
        else
        {
            Menge = decimal.TryParse(
                Laenge?.Trim().Replace(',', '.'),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var length)
                ? length
                : 0m;
        }
        _suppress = false;

        _onChanged?.Invoke(this);
    }

    partial void OnIsMengeEditierbarChanged(bool value) => OnPropertyChanged(nameof(IsMengeReadOnly));
    partial void OnHasMultipleStoredMeasuresChanged(bool value) => OnPropertyChanged(nameof(IsMatrixEditable));
    partial void OnMengeChanged(decimal value) => Recalculate();
    partial void OnOptVerkehrsdienstChanged(bool value) => Recalculate();
    partial void OnOptWasserhaltungChanged(bool value) => Recalculate();
    partial void OnOptFraesenChanged(bool value) => Recalculate();
    partial void OnOptDichtheitChanged(bool value) => Recalculate();
    partial void OnOptDokumentationChanged(bool value) => Recalculate();

    private void Recalculate()
    {
        if (!_suppress)
            _onChanged?.Invoke(this);
    }
}
