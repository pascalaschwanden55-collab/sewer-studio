using System.Collections.Generic;

namespace AuswertungPro.Next.Application.Cost;

/// <summary>
/// Unveraenderliches Datenobjekt, das IMeasureBlockView implementiert.
/// Wird vom CostConsistencyCheckService aus MeasureBlockVm gebaut
/// und dann an CostConsistencyChecker weitergegeben.
/// </summary>
public sealed class MeasureBlockView : IMeasureBlockView
{
    public required string MeasureId { get; init; }
    public required string MeasureName { get; init; }
    public string? DnText { get; init; }
    public string? LengthText { get; init; }
    public string? ConnectionsText { get; init; }
    public decimal Total { get; init; }
    public required IReadOnlyList<ICostLineView> Lines { get; init; }
}

/// <summary>
/// Unveraenderliches Datenobjekt, das ICostLineView implementiert.
/// </summary>
public sealed class CostLineView : ICostLineView
{
    public string? ItemKey { get; init; }
    public string? Text { get; init; }
    public string? Unit { get; init; }
    public decimal Qty { get; init; }
    public decimal UnitPrice { get; init; }
    public bool Selected { get; init; }
    public bool PriceMissing { get; init; }
    public bool IsPriceOverridden { get; init; }
}
