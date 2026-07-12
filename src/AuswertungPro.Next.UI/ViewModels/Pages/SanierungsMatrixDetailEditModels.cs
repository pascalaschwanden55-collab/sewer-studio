using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Costs;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

public sealed partial class SanierungsMatrixDetailEditLineVm : ObservableObject
{
    private readonly Action _changed;
    private readonly bool _initialized;

    public string Group { get; }
    public string ItemKey { get; }
    public string Text { get; }
    public string Unit { get; }
    public string PriceHint { get; private set; }
    public bool IsPriceOverridden { get; private set; }
    public bool IsQtyOverridden { get; private set; }

    [ObservableProperty] private bool _selected;
    [ObservableProperty] private bool _transferMarked;
    [ObservableProperty] private decimal _qty;
    [ObservableProperty] private decimal _unitPrice;

    public decimal LineTotal => Selected ? Qty * UnitPrice : 0m;

    public SanierungsMatrixDetailEditLineVm(CostLine line, Action changed)
    {
        _changed = changed;
        Group = line.Group;
        ItemKey = line.ItemKey;
        Text = line.Text;
        Unit = line.Unit;
        PriceHint = line.PriceHint;
        Selected = line.Selected;
        TransferMarked = line.TransferMarked;
        Qty = line.Qty;
        UnitPrice = line.UnitPrice;
        IsPriceOverridden = line.IsPriceOverridden;
        IsQtyOverridden = line.IsQtyOverridden;
        _initialized = true;
    }

    public CostLine ToModel()
        => new()
        {
            Group = Group,
            ItemKey = ItemKey,
            Text = Text,
            Unit = Unit,
            Qty = Qty,
            UnitPrice = UnitPrice,
            Selected = Selected,
            TransferMarked = TransferMarked,
            IsPriceOverridden = IsPriceOverridden,
            IsQtyOverridden = IsQtyOverridden,
            PriceHint = PriceHint
        };

    partial void OnSelectedChanged(bool value) => NotifyChanged();
    partial void OnTransferMarkedChanged(bool value) => NotifyChanged();

    partial void OnQtyChanged(decimal value)
    {
        if (_initialized)
            IsQtyOverridden = true;
        NotifyChanged();
    }

    partial void OnUnitPriceChanged(decimal value)
    {
        if (_initialized)
        {
            IsPriceOverridden = true;
            PriceHint = "";
        }
        NotifyChanged();
    }

    private void NotifyChanged()
    {
        OnPropertyChanged(nameof(LineTotal));
        _changed();
    }
}

public sealed partial class SanierungsMatrixDetailEditMeasureVm : ObservableObject
{
    private readonly Action _changed;

    public string MeasureName { get; }
    public string MeasureId { get; }
    public int? Dn { get; }
    public decimal? LengthMeters { get; }
    public ObservableCollection<SanierungsMatrixDetailEditLineVm> Lines { get; } = [];

    [ObservableProperty] private decimal _total;

    public SanierungsMatrixDetailEditMeasureVm(MeasureCost measure, Action changed)
    {
        _changed = changed;
        MeasureName = string.IsNullOrWhiteSpace(measure.MeasureName) ? measure.MeasureId : measure.MeasureName;
        MeasureId = measure.MeasureId;
        Dn = measure.Dn;
        LengthMeters = measure.LengthMeters;

        foreach (var line in measure.Lines)
            Lines.Add(new SanierungsMatrixDetailEditLineVm(line, LineChanged));

        Recalculate(markDirty: false);
    }

    public MeasureCost ToModel()
        => new()
        {
            MeasureId = MeasureId,
            MeasureName = MeasureName,
            Dn = Dn,
            LengthMeters = LengthMeters,
            Lines = Lines.Select(line => line.ToModel()).ToList(),
            Total = Total
        };

    private void LineChanged() => Recalculate(markDirty: true);

    private void Recalculate(bool markDirty)
    {
        Total = Lines.Sum(line => line.LineTotal);
        if (markDirty)
            _changed();
    }
}

public sealed partial class SanierungsMatrixDetailEditSession : ObservableObject
{
    private readonly decimal _vatRate;

    public ObservableCollection<SanierungsMatrixDetailEditMeasureVm> Measures { get; } = [];

    [ObservableProperty] private decimal _total;
    [ObservableProperty] private decimal _mwstAmount;
    [ObservableProperty] private decimal _totalInclMwst;
    [ObservableProperty] private bool _isDirty;

    private SanierungsMatrixDetailEditSession(decimal vatRate) => _vatRate = vatRate;

    public static SanierungsMatrixDetailEditSession FromCost(HoldingCost? cost, decimal vatRate)
    {
        var session = new SanierungsMatrixDetailEditSession(vatRate);
        if (cost is not null)
        {
            foreach (var measure in cost.Measures)
                session.Measures.Add(new SanierungsMatrixDetailEditMeasureVm(CloneMeasure(measure), session.MeasureChanged));
        }

        session.Recalculate();
        session.MarkClean();
        return session;
    }

    public HoldingCost ToHoldingCost(string holding, DateTime? date, decimal vatRate)
        => CostCalculatorLogicService.BuildHoldingCost(
            holding,
            date,
            Measures.Select(measure => measure.ToModel()).ToList(),
            vatRate);

    public void MarkClean() => IsDirty = false;

    private void MeasureChanged()
    {
        Recalculate();
        IsDirty = true;
    }

    private void Recalculate()
    {
        var totals = CostCalculatorLogicService.CalculateTotals(Measures.Sum(measure => measure.Total), _vatRate);
        Total = totals.Total;
        MwstAmount = totals.MwstAmount;
        TotalInclMwst = totals.TotalInclMwst;
    }

    private static MeasureCost CloneMeasure(MeasureCost measure)
        => new()
        {
            MeasureId = measure.MeasureId,
            MeasureName = measure.MeasureName,
            Dn = measure.Dn,
            LengthMeters = measure.LengthMeters,
            Total = measure.Total,
            Lines = measure.Lines.Select(CloneLine).ToList()
        };

    private static CostLine CloneLine(CostLine line)
        => new()
        {
            Group = line.Group,
            ItemKey = line.ItemKey,
            Text = line.Text,
            Unit = line.Unit,
            Qty = line.Qty,
            UnitPrice = line.UnitPrice,
            Selected = line.Selected,
            TransferMarked = line.TransferMarked,
            IsPriceOverridden = line.IsPriceOverridden,
            IsQtyOverridden = line.IsQtyOverridden,
            PriceHint = line.PriceHint
        };
}

public static class SanierungsMatrixDetailOverrideMerger
{
    public static void ApplyManualOverrides(HoldingCost target, HoldingCost? source)
    {
        if (source is null)
            return;

        var sourceLines = source.Measures
            .SelectMany(measure => measure.Lines)
            .Where(line => !string.IsNullOrWhiteSpace(line.ItemKey))
            .GroupBy(line => line.ItemKey.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var targetLine in target.Measures.SelectMany(measure => measure.Lines))
        {
            var key = (targetLine.ItemKey ?? "").Trim();
            if (key.Length == 0 || !sourceLines.TryGetValue(key, out var oldLine))
                continue;

            if (oldLine.IsPriceOverridden)
            {
                targetLine.UnitPrice = oldLine.UnitPrice;
                targetLine.IsPriceOverridden = true;
                targetLine.PriceHint = "";
            }

            if (oldLine.IsQtyOverridden)
            {
                targetLine.Qty = oldLine.Qty;
                targetLine.IsQtyOverridden = true;
            }
        }

        foreach (var measure in target.Measures)
            measure.Total = measure.Lines.Where(line => line.Selected).Sum(line => line.Qty * line.UnitPrice);

        var totals = CostCalculatorLogicService.CalculateTotals(
            target.Measures.Sum(measure => measure.Total),
            target.MwstRate);
        target.Total = totals.Total;
        target.MwstAmount = totals.MwstAmount;
        target.TotalInclMwst = totals.TotalInclMwst;
    }
}
