using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai.Sanierung;

namespace AuswertungPro.Next.UI.ViewModels.Windows;

public enum InitialFocusMode
{
    CostCalculator,
    AiOptimization
}

public sealed partial class SanierungsmassnahmenViewModel : ObservableObject
{
    public CostCalculatorViewModel CostCalcVm { get; }
    public SanierungOptimizationViewModel? OptimizationVm { get; }
    public InitialFocusMode InitialFocus { get; }

    // Context properties from HaltungRecord
    [ObservableProperty] private string _haltungName = "";
    [ObservableProperty] private string _dn = "";
    [ObservableProperty] private string _material = "";
    [ObservableProperty] private string _laenge = "";
    [ObservableProperty] private string _zustandsklasse = "";

    public string WindowTitle => string.IsNullOrWhiteSpace(HaltungName)
        ? "Sanierungsmassnahmen"
        : $"Sanierungsmassnahmen — {HaltungName}";

    // Footer bindings relayed from CostCalcVm
    public decimal Total => CostCalcVm.Total;
    public decimal MwstAmount => CostCalcVm.MwstAmount;
    public decimal TotalInclMwst => CostCalcVm.TotalInclMwst;
    public string MwstLabel => CostCalcVm.MwstLabel;

    // Consistency checking relayed from CostCalcVm
    public ObservableCollection<ConsistencyWarning> ConsistencyWarnings => CostCalcVm.ConsistencyWarnings;
    public int ErrorCount => CostCalcVm.ErrorCount;
    public int WarningCount => CostCalcVm.WarningCount;
    public int InfoCount => CostCalcVm.InfoCount;
    public bool HasWarnings => CostCalcVm.HasWarnings;

    public bool HasAi => OptimizationVm is not null;

    public event Action? CloseRequested;

    public SanierungsmassnahmenViewModel(
        CostCalculatorViewModel costCalcVm,
        SanierungOptimizationViewModel? optimizationVm,
        HaltungRecord record,
        InitialFocusMode initialFocus)
    {
        CostCalcVm = costCalcVm;
        OptimizationVm = optimizationVm;
        InitialFocus = initialFocus;

        HaltungName = record.GetFieldValue("Haltungsname") ?? record.Id.ToString();
        Dn = record.GetFieldValue("DN_mm") ?? "";
        Material = record.GetFieldValue("Rohrmaterial") ?? "";
        Laenge = record.GetFieldValue("Haltungslaenge_m") ?? "";
        Zustandsklasse = record.GetFieldValue("Zustandsklasse") ?? "";

        // Relay PropertyChanged from CostCalcVm for footer bindings
        CostCalcVm.PropertyChanged += CostCalcVm_PropertyChanged;

        // Wire optimization close to NOT close the unified window
        if (OptimizationVm is not null)
        {
            // Suppress the original CloseRequested from OptimizationVm
            // (the old window closed on TransferToPrimary, we keep it open now)
        }

        ApplyRuleToCalcCommand = new RelayCommand(ApplyRuleToCalc, CanApplyRule);
        ApplyAiToCalcCommand = new RelayCommand(ApplyAiToCalc, CanApplyAi);

        if (OptimizationVm is not null)
        {
            OptimizationVm.PropertyChanged += OptimizationVm_PropertyChanged;
        }
    }

    private void OptimizationVm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SanierungOptimizationViewModel.HasResult))
            ApplyAiToCalcCommand.NotifyCanExecuteChanged();
    }

    public IRelayCommand ApplyRuleToCalcCommand { get; }
    public IRelayCommand ApplyAiToCalcCommand { get; }

    private bool CanApplyRule()
    {
        if (OptimizationVm is null) return false;
        return !string.IsNullOrWhiteSpace(OptimizationVm.RuleMeasures)
            && OptimizationVm.RuleMeasures != "Keine Regelempfehlung verfügbar";
    }

    private void ApplyRuleToCalc()
    {
        if (OptimizationVm is null) return;
        // Parse rule measures and try to select them in the cost calculator
        var measures = OptimizationVm.RuleMeasures;
        if (string.IsNullOrWhiteSpace(measures)) return;

        var tokens = measures.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        SelectMeasuresInCalc(tokens);
    }

    private bool CanApplyAi()
    {
        return OptimizationVm is not null && OptimizationVm.HasResult && OptimizationVm.Result is not null;
    }

    private void ApplyAiToCalc()
    {
        if (OptimizationVm?.Result is null) return;
        var aiMeasure = OptimizationVm.AiMeasure;
        if (string.IsNullOrWhiteSpace(aiMeasure)) return;

        var tokens = aiMeasure.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        SelectMeasuresInCalc(tokens);
    }

    private void SelectMeasuresInCalc(string[] tokens)
    {
        // Try to find matching measures in the CostCalcVm's Measures list and select them
        var matched = new System.Collections.Generic.List<MeasureTemplateListItem>();
        foreach (var item in CostCalcVm.Measures)
        {
            if (item.Disabled) continue;
            foreach (var token in tokens)
            {
                if (item.DisplayName.Contains(token, StringComparison.OrdinalIgnoreCase)
                    || item.Id.Contains(token, StringComparison.OrdinalIgnoreCase))
                {
                    matched.Add(item);
                    break;
                }
            }
        }

        if (matched.Count > 0)
        {
            CostCalcVm.SetSelectedMeasures(matched);
            if (CostCalcVm.ApplyMeasuresCommand.CanExecute(null))
                CostCalcVm.ApplyMeasuresCommand.Execute(null);
        }
    }

    private void CostCalcVm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(CostCalculatorViewModel.Total):
                OnPropertyChanged(nameof(Total));
                break;
            case nameof(CostCalculatorViewModel.MwstAmount):
                OnPropertyChanged(nameof(MwstAmount));
                break;
            case nameof(CostCalculatorViewModel.TotalInclMwst):
                OnPropertyChanged(nameof(TotalInclMwst));
                break;
            case nameof(CostCalculatorViewModel.ConsistencyWarnings):
                OnPropertyChanged(nameof(ConsistencyWarnings));
                break;
            case nameof(CostCalculatorViewModel.ErrorCount):
                OnPropertyChanged(nameof(ErrorCount));
                break;
            case nameof(CostCalculatorViewModel.WarningCount):
                OnPropertyChanged(nameof(WarningCount));
                break;
            case nameof(CostCalculatorViewModel.InfoCount):
                OnPropertyChanged(nameof(InfoCount));
                break;
            case nameof(CostCalculatorViewModel.HasWarnings):
                OnPropertyChanged(nameof(HasWarnings));
                break;
        }
    }

    [RelayCommand]
    private void Close()
    {
        CostCalcVm.PropertyChanged -= CostCalcVm_PropertyChanged;
        if (OptimizationVm is not null)
            OptimizationVm.PropertyChanged -= OptimizationVm_PropertyChanged;
        CloseRequested?.Invoke();
    }
}
