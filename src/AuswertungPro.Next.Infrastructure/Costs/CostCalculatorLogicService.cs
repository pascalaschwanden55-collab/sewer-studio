using System;
using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Costs;

public sealed record CostCalculatorTotals(decimal Total, decimal MwstAmount, decimal TotalInclMwst);

/// <summary>
/// Reine Kostenrechner-Logik ohne WPF-Abhaengigkeiten.
/// </summary>
public static class CostCalculatorLogicService
{
    public static CostCalculatorTotals CalculateTotals(decimal total, decimal vatRate)
    {
        var mwst = Math.Round(total * vatRate, 2);
        return new CostCalculatorTotals(
            Total: total,
            MwstAmount: mwst,
            TotalInclMwst: Math.Round(total + mwst, 2));
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
}
