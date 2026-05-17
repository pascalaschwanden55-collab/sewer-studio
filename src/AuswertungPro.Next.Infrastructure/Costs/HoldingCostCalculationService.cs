using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Costs;

public sealed record HoldingCostCalculationRequest
{
    public string Holding { get; init; } = "";
    public DateTime? Date { get; init; }
    public List<string> MeasureIds { get; init; } = new();
    public int? Dn { get; init; }
    public decimal? LengthMeters { get; init; }
    public int? Connections { get; init; }
    public decimal? VatRate { get; init; }
}

public sealed record HoldingCostCalculationResult
{
    public HoldingCost Cost { get; init; } = new();
    public List<string> Warnings { get; init; } = new();
}

public sealed record ProjectCostCalculationRequest
{
    public List<HoldingCostCalculationRequest> Holdings { get; init; } = new();
    public decimal? VatRate { get; init; }
}

public sealed record ProjectCostCalculationResult
{
    public ProjectCostStore Store { get; init; } = new();
    public HoldingCost TotalCost { get; init; } = new();
    public List<NpkCostSummaryLine> SummaryLines { get; init; } = new();
    public List<string> Warnings { get; init; } = new();
}

public sealed record NpkCostSummaryLine
{
    public string SubmissionPos { get; init; } = "";
    public string Group { get; init; } = "";
    public string Label { get; init; } = "";
    public string Unit { get; init; } = "";
    public decimal Qty { get; init; }
    public decimal UnitPrice { get; init; }
    public decimal Amount { get; init; }
    public string AllocationScope { get; init; } = "";
    public List<string> Holdings { get; init; } = new();
}

/// <summary>
/// Headless calculation core for the active CostCatalog / MeasureTemplate model.
/// The WPF calculator can stay interactive, while CLI/tests use this deterministic path.
/// </summary>
public sealed class HoldingCostCalculationService
{
    public HoldingCostCalculationResult Calculate(
        MeasureTemplateCatalog templates,
        CostCatalog catalog,
        HoldingCostCalculationRequest request)
    {
        ArgumentNullException.ThrowIfNull(templates);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(request);

        var warnings = new List<string>();
        var catalogByKey = (catalog.Items ?? new List<CostCatalogItem>())
            .Where(i => !string.IsNullOrWhiteSpace(i.Key))
            .ToDictionary(i => i.Key.Trim(), StringComparer.OrdinalIgnoreCase);

        var selectedTemplates = ResolveTemplates(templates, request.MeasureIds, warnings);
        var measures = selectedTemplates
            .Select(template => CalculateMeasure(template, catalogByKey, request, warnings, projectSplitCounts: null))
            .ToList();

        var total = Math.Round(measures.Sum(m => m.Total), 2, MidpointRounding.AwayFromZero);
        var vatRate = ResolveVatRate(request, catalog);
        var vat = Math.Round(total * vatRate, 2, MidpointRounding.AwayFromZero);

        var cost = new HoldingCost
        {
            Holding = request.Holding.Trim(),
            Date = request.Date,
            Measures = measures,
            Total = total,
            MwstRate = vatRate,
            MwstAmount = vat,
            TotalInclMwst = Math.Round(total + vat, 2, MidpointRounding.AwayFromZero)
        };

        return new HoldingCostCalculationResult
        {
            Cost = cost,
            Warnings = warnings
        };
    }

    public ProjectCostCalculationResult CalculateProject(
        MeasureTemplateCatalog templates,
        CostCatalog catalog,
        ProjectCostCalculationRequest request)
    {
        ArgumentNullException.ThrowIfNull(templates);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(request);

        var warnings = new List<string>();
        var catalogByKey = (catalog.Items ?? new List<CostCatalogItem>())
            .Where(i => !string.IsNullOrWhiteSpace(i.Key))
            .ToDictionary(i => i.Key.Trim(), StringComparer.OrdinalIgnoreCase);

        var rows = request.Holdings
            .Select(r => (Request: r, Templates: ResolveTemplates(templates, r.MeasureIds, warnings)))
            .ToList();

        var projectSplitCounts = BuildProjectSplitCounts(rows);
        var store = new ProjectCostStore();
        var vatRate = request.VatRate ?? (catalog.VatRate > 0m ? catalog.VatRate : 0.081m);

        foreach (var row in rows)
        {
            var rowRequest = row.Request with { VatRate = vatRate };
            var measures = row.Templates
                .Select(template => CalculateMeasure(template, catalogByKey, rowRequest, warnings, projectSplitCounts))
                .ToList();
            var total = Math.Round(measures.Sum(m => m.Total), 2, MidpointRounding.AwayFromZero);
            var vat = Math.Round(total * vatRate, 2, MidpointRounding.AwayFromZero);
            var holding = NormalizeHolding(rowRequest.Holding);

            store.ByHolding[holding] = new HoldingCost
            {
                Holding = holding,
                Date = rowRequest.Date,
                Measures = measures,
                Total = total,
                MwstRate = vatRate,
                MwstAmount = vat,
                TotalInclMwst = Math.Round(total + vat, 2, MidpointRounding.AwayFromZero)
            };
        }

        var summaryLines = BuildSummaryLines(store);
        var projectNet = Math.Round(summaryLines.Sum(l => l.Amount), 2, MidpointRounding.AwayFromZero);
        var projectVat = Math.Round(projectNet * vatRate, 2, MidpointRounding.AwayFromZero);

        return new ProjectCostCalculationResult
        {
            Store = store,
            SummaryLines = summaryLines,
            TotalCost = new HoldingCost
            {
                Holding = "Projekt",
                Total = projectNet,
                MwstRate = vatRate,
                MwstAmount = projectVat,
                TotalInclMwst = Math.Round(projectNet + projectVat, 2, MidpointRounding.AwayFromZero)
            },
            Warnings = warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList()
        };
    }

    private static List<MeasureTemplate> ResolveTemplates(
        MeasureTemplateCatalog templates,
        IReadOnlyList<string> requestedIds,
        List<string> warnings)
    {
        var activeTemplates = (templates.Measures ?? new List<MeasureTemplate>())
            .Where(t => !t.Disabled)
            .ToList();

        if (requestedIds.Count == 0)
            return new List<MeasureTemplate>();

        var resolved = new List<MeasureTemplate>();
        foreach (var raw in requestedIds)
        {
            var token = (raw ?? "").Trim();
            if (token.Length == 0)
                continue;

            var match = activeTemplates.FirstOrDefault(t =>
                string.Equals(t.Id, token, StringComparison.OrdinalIgnoreCase)
                || string.Equals(t.Name, token, StringComparison.OrdinalIgnoreCase));

            if (match is null)
            {
                warnings.Add($"Massnahme nicht gefunden: {token}");
                continue;
            }

            if (resolved.Any(t => string.Equals(t.Id, match.Id, StringComparison.OrdinalIgnoreCase)))
                continue;

            resolved.Add(match);
        }

        return resolved;
    }

    private static MeasureCost CalculateMeasure(
        MeasureTemplate template,
        IReadOnlyDictionary<string, CostCatalogItem> catalogByKey,
        HoldingCostCalculationRequest request,
        List<string> warnings,
        IReadOnlyDictionary<string, int>? projectSplitCounts)
    {
        var lines = new List<CostLine>();
        foreach (var templateLine in template.Lines ?? new List<MeasureLineTemplate>())
        {
            var itemKey = (templateLine.ItemKey ?? "").Trim();
            catalogByKey.TryGetValue(itemKey, out var item);

            var unit = item?.Unit ?? "";
            var text = item?.Name ?? itemKey;
            var qty = ResolveQuantity(templateLine.DefaultQty, unit, itemKey, text, request);
            var selected = templateLine.Enabled;

            if (IsConnectionLine(itemKey, text) && request.Connections.GetValueOrDefault() <= 0)
                selected = false;

            var unitPrice = ResolveUnitPrice(item, request.Dn, qty, itemKey, warnings);
            if (selected && IsProjectSplitLine(itemKey) && projectSplitCounts is not null)
            {
                var count = projectSplitCounts.TryGetValue(NormalizeSplitKey(itemKey), out var found)
                    ? Math.Max(1, found)
                    : 1;
                qty = Math.Round(1m / count, 6, MidpointRounding.AwayFromZero);
            }

            var line = new CostLine
            {
                Group = templateLine.Group ?? "",
                ItemKey = itemKey,
                Text = text,
                Unit = unit,
                Qty = qty,
                UnitPrice = unitPrice,
                Selected = selected,
                TransferMarked = false,
                IsPriceOverridden = false,
                IsQtyOverridden = false,
                SubmissionPos = ResolveSubmissionPos(itemKey)
            };

            lines.Add(line);
        }

        var total = Math.Round(
            lines.Where(l => l.Selected).Sum(l => l.Qty * l.UnitPrice),
            2,
            MidpointRounding.AwayFromZero);

        return new MeasureCost
        {
            MeasureId = template.Id,
            MeasureName = template.Name,
            Dn = request.Dn,
            LengthMeters = request.LengthMeters,
            Lines = lines,
            Total = total
        };
    }

    private static decimal ResolveQuantity(
        decimal defaultQty,
        string unit,
        string itemKey,
        string text,
        HoldingCostCalculationRequest request)
    {
        if (IsConnectionLine(itemKey, text))
            return Math.Max(0, request.Connections.GetValueOrDefault());

        if (IsMeterUnit(unit) && request.LengthMeters.HasValue)
            return Math.Max(0m, request.LengthMeters.Value);

        return Math.Max(0m, defaultQty);
    }

    private static Dictionary<string, int> BuildProjectSplitCounts(
        IReadOnlyList<(HoldingCostCalculationRequest Request, List<MeasureTemplate> Templates)> rows)
    {
        var holdingsBySplitKey = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            var holding = NormalizeHolding(row.Request.Holding);
            foreach (var template in row.Templates)
            {
                foreach (var line in template.Lines ?? new List<MeasureLineTemplate>())
                {
                    if (!line.Enabled)
                        continue;

                    var itemKey = (line.ItemKey ?? "").Trim();
                    if (!IsProjectSplitLine(itemKey))
                        continue;

                    var splitKey = NormalizeSplitKey(itemKey);
                    if (!holdingsBySplitKey.TryGetValue(splitKey, out var holdings))
                    {
                        holdings = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        holdingsBySplitKey[splitKey] = holdings;
                    }

                    holdings.Add(holding);
                }
            }
        }

        return holdingsBySplitKey.ToDictionary(k => k.Key, v => Math.Max(1, v.Value.Count), StringComparer.OrdinalIgnoreCase);
    }

    private static List<NpkCostSummaryLine> BuildSummaryLines(ProjectCostStore store)
    {
        var rows = store.ByHolding
            .SelectMany(kvp => kvp.Value.Measures
                .SelectMany(m => m.Lines
                    .Where(l => l.Selected)
                    .Select(line => new { Holding = kvp.Key, Line = line })))
            .ToList();

        return rows
            .GroupBy(x => new
            {
                x.Line.SubmissionPos,
                x.Line.Group,
                x.Line.Text,
                x.Line.Unit,
                x.Line.UnitPrice,
                Scope = IsProjectSplitLine(x.Line.ItemKey) ? "ProjectSplit" : "PerHolding"
            })
            .Select(g => new NpkCostSummaryLine
            {
                SubmissionPos = g.Key.SubmissionPos ?? "",
                Group = g.Key.Group,
                Label = g.Key.Text,
                Unit = g.Key.Unit,
                UnitPrice = g.Key.UnitPrice,
                Qty = Math.Round(g.Sum(x => x.Line.Qty), 3, MidpointRounding.AwayFromZero),
                Amount = Math.Round(g.Sum(x => x.Line.Qty * x.Line.UnitPrice), 2, MidpointRounding.AwayFromZero),
                AllocationScope = g.Key.Scope,
                Holdings = g.Select(x => x.Holding).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList()
            })
            .OrderBy(x => ResolveSubmissionSortKey(x.SubmissionPos))
            .ThenBy(x => x.Group, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static decimal ResolveUnitPrice(
        CostCatalogItem? item,
        int? dn,
        decimal qty,
        string itemKey,
        List<string> warnings)
    {
        if (item is null)
        {
            warnings.Add($"Katalogposition fehlt: {itemKey}");
            return 0m;
        }

        if (string.Equals(item.Type, "Fixed", StringComparison.OrdinalIgnoreCase))
        {
            if (item.Price.HasValue)
                return Math.Round(item.Price.Value, 2, MidpointRounding.AwayFromZero);

            warnings.Add($"Fixpreis fehlt: {item.Key}");
            return 0m;
        }

        if (!string.Equals(item.Type, "ByDN", StringComparison.OrdinalIgnoreCase))
        {
            warnings.Add($"Unbekannter Preistyp '{item.Type}' fuer {item.Key}");
            return 0m;
        }

        if (!dn.HasValue || dn.Value <= 0)
        {
            warnings.Add($"DN fehlt fuer DN-basierte Position: {item.Key}");
            return 0m;
        }

        var candidates = (item.DnPrices ?? new List<DnPrice>())
            .Where(p => dn.Value >= p.DnFrom && dn.Value <= p.DnTo)
            .ToList();

        if (candidates.Count == 0)
        {
            candidates = FindNearestDnCandidates(item.DnPrices ?? new List<DnPrice>(), dn.Value);
            if (candidates.Count == 0)
            {
                warnings.Add($"Kein DN-Preis gefunden: {item.Key} fuer DN {dn.Value.ToString(CultureInfo.InvariantCulture)}");
                return 0m;
            }

            var nearest = candidates[0];
            warnings.Add($"Kein exakter DN-Preis fuer {item.Key} DN {dn.Value}; verwende DN {nearest.DnFrom}-{nearest.DnTo}.");
        }

        var match = ResolveQtyAwarePrice(candidates, qty);
        return Math.Round(match.Price, 2, MidpointRounding.AwayFromZero);
    }

    private static DnPrice ResolveQtyAwarePrice(IReadOnlyList<DnPrice> candidates, decimal qty)
    {
        var hasQtyRules = candidates.Any(p => p.QtyFrom.HasValue || p.QtyTo.HasValue);
        if (!hasQtyRules)
            return candidates[0];

        return candidates.FirstOrDefault(p => QtyMatches(p, qty))
               ?? candidates.FirstOrDefault(p => !p.QtyFrom.HasValue && !p.QtyTo.HasValue)
               ?? candidates[0];
    }

    private static bool QtyMatches(DnPrice price, decimal qty)
    {
        var minOk = !price.QtyFrom.HasValue || qty >= price.QtyFrom.Value;
        var maxOk = !price.QtyTo.HasValue || qty <= price.QtyTo.Value;
        return minOk && maxOk;
    }

    private static List<DnPrice> FindNearestDnCandidates(IEnumerable<DnPrice> prices, int dn)
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

    private static decimal ResolveVatRate(HoldingCostCalculationRequest request, CostCatalog catalog)
    {
        if (request.VatRate.HasValue && request.VatRate.Value >= 0m)
            return request.VatRate.Value;

        return catalog.VatRate > 0m ? catalog.VatRate : 0.081m;
    }

    private static string ResolveSubmissionPos(string itemKey)
    {
        if (string.IsNullOrWhiteSpace(itemKey))
            return "";

        var key = itemKey.Trim();
        if (key.StartsWith("INSTALL_", StringComparison.OrdinalIgnoreCase)
            || key.StartsWith("HL_INSTALL_", StringComparison.OrdinalIgnoreCase))
            return "100.1";

        return key.ToUpperInvariant() switch
        {
            "VORARBEIT_REINIGUNG" => "2.1.1",
            "VORARBEIT_TV_VORKONTROLLE" => "2.1.2",
            "VORARBEIT_FRAESEN" => "2.1.5",
            "VORARBEIT_EINMESSUNG" => "2.1.x",
            "VORARBEIT_ANSCHLUSS_EINMESSEN" => "2.1.x",
            "VORARBEIT_WASSERHALTUNG" => "300.1",
            "SCHLAUCHLINER_PRELINER" => "600.1",
            "SCHLAUCHLINER_GFK" => "600.5",
            "SCHLAUCHLINER_NADELFILZ" => "600.2",
            "SCHLAUCHLINER_NADELFILZ_OPENEND" => "600.2",
            "ANSCHLUSS_AUFFRAESEN" => "600.6",
            "ANSCHLUSS_EINBINDEN" => "600.6",
            "LINERENDMANSCHETTE_LEM" => "600.7",
            "KURZLINER_PARTLINER" => "500.1",
            "MANSCHETTE_EDELSTAHL" => "500.2",
            "QK_DICHTHEITSPRUEFUNG" => "800.1",
            "QK_TV_ABNAHME" => "2.1.4",
            "QK_DOKUMENTATION" => "800.2",
            _ => ""
        };
    }

    private static decimal ResolveSubmissionSortKey(string? submissionPos)
    {
        if (string.IsNullOrWhiteSpace(submissionPos))
            return 99999m;

        var parts = submissionPos
            .Trim()
            .Replace('x', '9')
            .Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (parts.Length == 0 || !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var head))
            return 99999m;

        // Internal "2.1.x" positions belong to the NPK 200/Vorarbeiten block.
        if (head == 2 && parts.Length >= 2)
        {
            var minor = int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var m) ? m : 0;
            var detail = parts.Length >= 3 && int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var d) ? d : 0;
            return 200m + minor + (detail / 100m);
        }

        var tail = 0m;
        for (var i = 1; i < parts.Length; i++)
        {
            if (!int.TryParse(parts[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
                continue;
            tail += value / (decimal)Math.Pow(100, i);
        }

        return head + tail;
    }

    private static bool IsProjectSplitLine(string? itemKey)
    {
        if (string.IsNullOrWhiteSpace(itemKey))
            return false;

        var key = itemKey.Trim();
        return key.StartsWith("INSTALL_", StringComparison.OrdinalIgnoreCase)
            || key.StartsWith("HL_INSTALL_", StringComparison.OrdinalIgnoreCase)
            || key.Contains("WASSERHALTUNG", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeSplitKey(string itemKey)
        => itemKey.Trim().ToUpperInvariant();

    private static string NormalizeHolding(string? holding)
        => string.IsNullOrWhiteSpace(holding) ? "Unbekannt" : holding.Trim();

    private static bool IsMeterUnit(string? unit)
        => string.Equals(unit?.Trim(), "m", StringComparison.OrdinalIgnoreCase);

    private static bool IsConnectionLine(string? itemKey, string? text)
    {
        if (!string.IsNullOrWhiteSpace(itemKey)
            && itemKey.Contains("ANSCHLUSS", StringComparison.OrdinalIgnoreCase))
            return true;

        if (!string.IsNullOrWhiteSpace(text)
            && text.Contains("anschluss", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }
}
