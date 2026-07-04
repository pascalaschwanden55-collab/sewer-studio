using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AuswertungPro.Next.Application.Costs;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Costs;

public sealed record CostCalculatorTotals(decimal Total, decimal MwstAmount, decimal TotalInclMwst);

/// <summary>
/// Reine Kostenrechner-Logik ohne WPF-Abhaengigkeiten.
/// </summary>
public static class CostCalculatorLogicService
{
    /// <summary>MwSt-Standardsatz (Schweiz, 8.1%) — EINZIGE Stelle fuer den Fallback-Wert.</summary>
    public const decimal DefaultVatRate = 0.081m;

    public static CostCalculatorTotals CalculateTotals(decimal total, decimal vatRate)
    {
        // Kaufmaennisch runden (AwayFromZero) wie alle Export-Pfade — vorher Banker's
        // Rounding, das bis 1 Rappen vom PDF/Druckcenter abweichen konnte (Audit W12).
        var mwst = Math.Round(total * vatRate, 2, MidpointRounding.AwayFromZero);
        return new CostCalculatorTotals(
            Total: total,
            MwstAmount: mwst,
            TotalInclMwst: Math.Round(total + mwst, 2, MidpointRounding.AwayFromZero));
    }

    public static HoldingCost BuildHoldingCost(
        string holding,
        DateTime? date,
        IEnumerable<MeasureCost> measures,
        decimal vatRate)
    {
        var measureList = measures.ToList();
        var totals = CalculateTotals(measureList.Sum(m => m.Total), vatRate);

        return new HoldingCost
        {
            Holding = holding,
            Date = date,
            Measures = measureList,
            Total = totals.Total,
            MwstRate = vatRate,
            MwstAmount = totals.MwstAmount,
            TotalInclMwst = totals.TotalInclMwst
        };
    }

    public static List<string> ResolveMeasureIds(
        IReadOnlyList<string> tokens,
        IReadOnlyList<MeasureTemplate> templates,
        IReadOnlyDictionary<string, CostCatalogItem> catalogItems)
    {
        if (tokens.Count == 0 || templates.Count == 0)
            return new List<string>();

        var normalizedTokens = tokens
            .Select(NormalizeToken)
            .Where(t => t.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (normalizedTokens.Count == 0)
            return new List<string>();

        var scores = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var template in templates)
        {
            if (template.Disabled)
                continue;

            var templateId = template.Id?.Trim() ?? "";
            if (templateId.Length == 0)
                continue;

            var templateIdNorm = NormalizeToken(templateId);
            var templateNameNorm = NormalizeToken(template.Name);
            var templateScore = 0;

            foreach (var token in normalizedTokens)
            {
                if (templateIdNorm.Equals(token, StringComparison.OrdinalIgnoreCase) ||
                    templateNameNorm.Equals(token, StringComparison.OrdinalIgnoreCase))
                {
                    templateScore += 100;
                    continue;
                }

                if (ContainsToken(templateIdNorm, token) || ContainsToken(templateNameNorm, token))
                    templateScore += 25;

                foreach (var line in template.Lines)
                {
                    var keyNorm = NormalizeToken(line.ItemKey);
                    if (keyNorm.Length > 0)
                    {
                        if (keyNorm.Equals(token, StringComparison.OrdinalIgnoreCase))
                            templateScore += 40;
                        else if (ContainsToken(keyNorm, token))
                            templateScore += 12;
                    }

                    if (!catalogItems.TryGetValue(line.ItemKey, out var item))
                        continue;

                    var itemNameNorm = NormalizeToken(item.Name);
                    if (itemNameNorm.Length > 0)
                    {
                        if (itemNameNorm.Equals(token, StringComparison.OrdinalIgnoreCase))
                            templateScore += 60;
                        else if (ContainsToken(itemNameNorm, token))
                            templateScore += 18;
                    }

                    if (item.Aliases is null)
                        continue;

                    foreach (var alias in item.Aliases)
                    {
                        var aliasNorm = NormalizeToken(alias);
                        if (aliasNorm.Length == 0)
                            continue;

                        if (aliasNorm.Equals(token, StringComparison.OrdinalIgnoreCase))
                            templateScore += 45;
                        else if (ContainsToken(aliasNorm, token))
                            templateScore += 12;
                    }
                }
            }

            if (templateScore > 0)
                scores[templateId] = templateScore;
        }

        var ranked = scores
            .OrderByDescending(x => x.Value)
            .ThenBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (ranked.Count == 0)
            return new List<string>();

        var maxScore = ranked[0].Value;
        var minScore = Math.Max(25, (int)Math.Ceiling(maxScore * 0.4m));
        return ranked
            .Where(x => x.Value >= minScore)
            .Select(x => x.Key)
            .ToList();
    }

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

    public static int? ParseDn(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;
        return int.TryParse(raw.Trim(), out var dn) ? dn : null;
    }

    public static decimal? ParseDecimal(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var text = raw.Trim();
        if (TryParseDecimal(text, out var value))
            return value;

        var numericPrefix = new string(text
            .TakeWhile(ch => char.IsDigit(ch) || ch is '+' or '-' or '.' or ',')
            .ToArray());
        if (numericPrefix.Length > 0 && TryParseDecimal(numericPrefix, out value))
            return value;

        return null;
    }

    public static bool IsMeterUnit(string? unit)
        => UnitKinds.IsLength(unit);

    public static bool IsConnectionLine(string? itemKey, string? text)
    {
        if (!string.IsNullOrWhiteSpace(itemKey) &&
            itemKey.Contains("ANSCHLUSS", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(text) &&
            text.Contains("anschluss", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    public static bool IsInstallationLine(string? group, string? itemKey)
    {
        if (!string.IsNullOrWhiteSpace(group) &&
            group.Trim().Equals("Installation", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return IsInstallationItemKey(itemKey);
    }

    public static bool IsItemKey(string? itemKey, string key)
    {
        if (string.IsNullOrWhiteSpace(itemKey))
            return false;

        return itemKey.Trim().Equals(key, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsInstallationItemKey(string? itemKey)
    {
        if (string.IsNullOrWhiteSpace(itemKey))
            return false;

        var key = itemKey.Trim();
        return key.StartsWith("INSTALL_", StringComparison.OrdinalIgnoreCase)
               || key.StartsWith("HL_INSTALL_", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsToken(string text, string token)
    {
        if (text.Length == 0 || token.Length == 0)
            return false;
        if (text.Contains(token, StringComparison.OrdinalIgnoreCase))
            return true;

        // Reverse-Contains nur fuer laengere Werte, sonst entstehen zu viele Treffer.
        return text.Length >= 5 && token.Length >= 5 &&
               token.Contains(text, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeToken(string? value)
    {
        var text = (value ?? string.Empty).Trim();
        while (text.Length > 0 && (text[0] == '-' || text[0] == '*'))
            text = text[1..].TrimStart();
        return text;
    }

    private static bool TryParseDecimal(string raw, out decimal value)
    {
        if (decimal.TryParse(raw, NumberStyles.Number, CultureInfo.CurrentCulture, out value))
            return true;

        if (decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out value))
            return true;

        var normalized = raw.Contains(',')
            ? raw.Replace(',', '.')
            : raw.Replace('.', ',');

        if (!string.Equals(normalized, raw, StringComparison.Ordinal) &&
            decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out value))
            return true;

        value = 0;
        return false;
    }
}
