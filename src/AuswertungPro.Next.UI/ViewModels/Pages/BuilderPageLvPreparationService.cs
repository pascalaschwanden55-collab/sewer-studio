using AuswertungPro.Next.Application.Costs;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Costs;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

/// <summary>
/// Bereitet die AWU-Haltungs- und Schachtkosten fuer ein gemeinsames NPK-LV auf.
/// Dialoge und Dateizugriffe bleiben beim ViewModel.
/// </summary>
internal static class BuilderPageLvPreparationService
{
    public static BuilderPageLvHoldingSelection SelectAwuHoldings(
        IReadOnlyList<DruckcenterRowVm> rows,
        decimal vatRate)
    {
        var holdings = BuilderPageSummaryEntryBuilder.Build(rows, vatRate)
            .Where(entry => entry.Cost is not null && OwnershipAwuFilter.IsAwu(entry.Owner))
            .Select(entry => entry.Cost!)
            .ToList();
        var fallbackHoldings = holdings
            .Where(TablePauschaleCostHelper.IsFallbackPauschale)
            .ToList();

        return new BuilderPageLvHoldingSelection(holdings, fallbackHoldings);
    }

    public static BuilderPageLvPreparationResult Build(
        BuilderPageLvHoldingSelection selection,
        bool includeFallbackHoldings,
        IReadOnlyList<HoldingCost> shaftCosts,
        IReadOnlySet<string> awuShaftKeys,
        IReadOnlyDictionary<string, CostCatalogItem> catalog)
    {
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(shaftCosts);
        ArgumentNullException.ThrowIfNull(awuShaftKeys);
        ArgumentNullException.ThrowIfNull(catalog);

        var holdingsForLv = includeFallbackHoldings
            ? selection.Holdings
            : selection.Holdings
                .Where(holding => !TablePauschaleCostHelper.IsFallbackPauschale(holding))
                .ToList();
        var excludedTotal = includeFallbackHoldings
            ? 0m
            : selection.FallbackHoldings.Sum(holding => holding.Total);
        var excludedCount = includeFallbackHoldings ? 0 : selection.FallbackHoldings.Count;

        var awuShaftCosts = shaftCosts
            .Where(cost => awuShaftKeys.Contains(OwnershipAwuFilter.NormalizeSchacht(cost.Holding)))
            .ToList();
        var holdingsWithShafts = holdingsForLv.Concat(awuShaftCosts).ToList();
        var positions = ProjectPositionAggregator.Aggregate(holdingsWithShafts, catalog);

        return new BuilderPageLvPreparationResult(
            positions,
            excludedTotal,
            excludedCount,
            holdingsWithShafts.Count);
    }
}

internal sealed record BuilderPageLvHoldingSelection(
    IReadOnlyList<HoldingCost> Holdings,
    IReadOnlyList<HoldingCost> FallbackHoldings);

internal sealed record BuilderPageLvPreparationResult(
    IReadOnlyList<AggregatedPosition> Positions,
    decimal ExcludedTotal,
    int ExcludedCount,
    int HoldingCount);
