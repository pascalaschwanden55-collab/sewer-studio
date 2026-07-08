using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.DataPage;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.DataPage;

/// <summary>
/// Thin-Delegate: leitet alle Aufrufe an <see cref="SanierungCostFieldMapper"/> weiter.
/// Oeffentliche API und Signaturen bleiben unveraendert (Aufrufer unveraendert).
/// Reine Logik liegt in Application.DataPage (unit-testbar ohne WPF).
/// </summary>
public static class DataPageSanierungCostMapper
{
    /// <inheritdoc cref="SanierungCostFieldMapper.ApplyRecommendation"/>
    public static void ApplyRecommendation(HaltungRecord record, MeasureRecommendationResult recommendation)
        => SanierungCostFieldMapper.ApplyRecommendation(record, recommendation);

    /// <inheritdoc cref="SanierungCostFieldMapper.ApplyCosts"/>
    public static void ApplyCosts(HaltungRecord record, HoldingCost cost, bool includeCosts = true)
        => SanierungCostFieldMapper.ApplyCosts(record, cost, includeCosts);

    /// <inheritdoc cref="SanierungCostFieldMapper.ClearCosts"/>
    public static void ClearCosts(HaltungRecord record)
        => SanierungCostFieldMapper.ClearCosts(record);

    /// <inheritdoc cref="SanierungCostFieldMapper.SyncRecord"/>
    public static bool SyncRecord(HaltungRecord record, HoldingCost? cost)
        => SanierungCostFieldMapper.SyncRecord(record, cost);

    /// <inheritdoc cref="MeasuresTextBuilder.NormalizeRecommendationEntry"/>
    public static string NormalizeRecommendationEntry(string? value)
        => MeasuresTextBuilder.NormalizeRecommendationEntry(value);
}
