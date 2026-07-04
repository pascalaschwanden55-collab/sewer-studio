using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Cost;

public enum CatalogPriceResolveMode
{
    Exact,
    WithNearestDnFallback
}

public readonly record struct CatalogPriceResolution(
    decimal UnitPrice,
    bool HasPrice,
    string PriceHint,
    bool UsedNearestDnFallback);

public static class CatalogPriceResolver
{
    public static CatalogPriceResolution Resolve(
        CostCatalogItem? item,
        int? dn,
        decimal qty,
        CatalogPriceResolveMode mode)
    {
        if (item is null || !item.Active)
            return Missing();

        if (string.Equals(item.Type, "Fixed", StringComparison.OrdinalIgnoreCase))
            return item.Price.HasValue
                ? new CatalogPriceResolution(item.Price.Value, true, "", false)
                : Missing();

        if (!string.Equals(item.Type, "ByDN", StringComparison.OrdinalIgnoreCase))
            return Missing();

        if (dn is not int d)
            return Missing();

        var candidates = (item.DnPrices ?? new List<DnPrice>())
            .Where(x => d >= x.DnFrom && d <= x.DnTo)
            .ToList();
        var usedNearestFallback = false;

        if (candidates.Count == 0)
        {
            if (mode != CatalogPriceResolveMode.WithNearestDnFallback)
                return Missing();

            candidates = FindNearestDnCandidates(item.DnPrices ?? new List<DnPrice>(), d);
            usedNearestFallback = candidates.Count > 0;
            if (candidates.Count == 0)
                return Missing();
        }

        var hasQtyRules = candidates.Any(p => p.QtyFrom.HasValue || p.QtyTo.HasValue);
        var match = hasQtyRules
            ? candidates.FirstOrDefault(x => QtyMatches(x, qty))
              ?? candidates.FirstOrDefault(x => !x.QtyFrom.HasValue && !x.QtyTo.HasValue)
            : candidates[0];
        if (match is null)
            return Missing();

        return new CatalogPriceResolution(
            match.Price,
            true,
            usedNearestFallback ? BuildNearestDnPriceHint(match) : "",
            usedNearestFallback);
    }

    public static int? ParseDn(string? raw)
        => int.TryParse((raw ?? "").Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var dn)
            ? dn
            : null;

    public static bool QtyMatches(DnPrice price, decimal qty)
    {
        var minOk = !price.QtyFrom.HasValue || qty >= price.QtyFrom.Value;
        var maxOk = !price.QtyTo.HasValue || qty <= price.QtyTo.Value;
        return minOk && maxOk;
    }

    public static List<DnPrice> FindNearestDnCandidates(IEnumerable<DnPrice> prices, int dn)
    {
        var withDistance = prices
            .Select(p => new
            {
                Price = p,
                Distance = dn < p.DnFrom
                    ? p.DnFrom - dn
                    : dn > p.DnTo
                        ? dn - p.DnTo
                        : 0
            })
            .ToList();

        if (withDistance.Count == 0)
            return new List<DnPrice>();

        var minDistance = withDistance.Min(x => x.Distance);
        return withDistance
            .Where(x => x.Distance == minDistance)
            .Select(x => x.Price)
            .OrderBy(x => x.DnFrom)
            .ThenBy(x => x.DnTo)
            .ToList();
    }

    public static string BuildNearestDnPriceHint(DnPrice price)
    {
        var dn = price.DnFrom == price.DnTo
            ? price.DnFrom.ToString(CultureInfo.InvariantCulture)
            : $"{price.DnFrom.ToString(CultureInfo.InvariantCulture)}-{price.DnTo.ToString(CultureInfo.InvariantCulture)}";
        return $"Preis von DN {dn} uebernommen";
    }

    private static CatalogPriceResolution Missing()
        => new(0m, false, "", false);
}
