namespace AuswertungPro.Next.Infrastructure.Costs;

public readonly record struct ConnectionQuantityUpdate(
    bool IsApplicable,
    bool ShouldDisable,
    bool ShouldReactivate,
    decimal? SuggestedQuantity);

/// <summary>
/// Decides how a connection count changes an existing cost line without applying mutations.
/// Positive manual quantities remain untouched; non-positive counts always disable the line.
/// </summary>
public static class ConnectionQuantityPolicy
{
    public static ConnectionQuantityUpdate Evaluate(
        string? itemKey,
        string? text,
        decimal currentQuantity,
        bool selected,
        decimal connectionCount)
    {
        if (!CostCalculatorLogicService.IsConnectionLine(itemKey, text))
            return default;

        if (connectionCount <= 0m)
        {
            return new ConnectionQuantityUpdate(
                IsApplicable: true,
                ShouldDisable: true,
                ShouldReactivate: false,
                SuggestedQuantity: 0m);
        }

        return new ConnectionQuantityUpdate(
            IsApplicable: true,
            ShouldDisable: false,
            ShouldReactivate: !selected && currentQuantity == 0m,
            SuggestedQuantity: connectionCount);
    }

    public static decimal? ResolveSuggestedQuantity(
        ConnectionQuantityUpdate update,
        bool isQuantityOverridden)
    {
        if (!update.IsApplicable)
            return null;
        if (update.ShouldDisable)
            return update.SuggestedQuantity;

        return isQuantityOverridden ? null : update.SuggestedQuantity;
    }
}
