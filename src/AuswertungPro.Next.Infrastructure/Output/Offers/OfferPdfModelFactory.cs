using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Models.Costs;

namespace AuswertungPro.Next.Infrastructure.Output.Offers;

public static class OfferPdfModelFactory
{
    private static readonly CultureInfo Ch = CultureInfo.GetCultureInfo("de-CH");
    private const string MissingPriceText = "Preis fehlt";

    public static OfferPdfModel CreateFromHoldingCost(
        HoldingCost cost,
        OfferPdfContext ctx,
        DateTimeOffset now)
    {
        var currency = string.IsNullOrWhiteSpace(ctx.Currency) ? "CHF" : ctx.Currency;
        string Money(decimal v) => ChfFormat.Money(v, currency);
        string Qty(decimal v) => v.ToString("0.###", Ch);

        var model = new OfferPdfModel
        {
            DocumentKindLabel = "Offerte",
            OfferNo = string.IsNullOrWhiteSpace(ctx.OfferNo)
                ? $"OFF-{now:yyyyMMdd-HHmmss}"
                : ctx.OfferNo,
            DateText = now.ToLocalTime().ToString("dd.MM.yyyy", Ch),
            ValidityText = ctx.ValidityText ?? "",

            SenderBlock = BuildSenderBlockAbwasserUri(),
            CustomerBlock = ctx.CustomerBlock ?? "",
            ObjectBlock = ctx.ObjectBlock ?? "",

            ProjectTitle = ctx.ProjectTitle ?? "",
            VariantTitle = ctx.VariantTitle ?? "",
            FilterSummaryText = ctx.FilterSummaryText ?? "",

            Totals = new OfferPdfTotalsModel
            {
                NetText = Money(cost.Total),
                VatText = $"MwSt. {cost.MwstRate * 100:0.0}%: " + Money(cost.MwstAmount),
                GrossText = Money(cost.TotalInclMwst)
            },
            TextBlocks = (ctx.TextBlocks is { Count: > 0 } ? ctx.TextBlocks : DefaultTextBlocks()).ToList(),
        };

        foreach (var measure in cost.Measures)
        {
            foreach (var line in measure.Lines)
            {
                if (!line.Selected)
                    continue;

                var lineTotal = line.Qty * line.UnitPrice;
                var missingPrice = IsMissingPrice(line);

                model.Lines.Add(new OfferPdfLineModel
                {
                    GroupLabel = !string.IsNullOrWhiteSpace(line.Group) ? line.Group : measure.MeasureName,
                    Text = line.Text,
                    Note = "",
                    QtyText = Qty(line.Qty),
                    Unit = line.Unit,
                    UnitPriceText = missingPrice ? MissingPriceText : Money(line.UnitPrice),
                    TotalText = missingPrice ? MissingPriceText : Money(lineTotal),
                });
            }
        }

        return model;
    }

    // ---------- Cost summary: multi-holding, owner filter + global positions ----------
    public static OfferPdfModel CreateCostSummary(
        IReadOnlyList<CostSummaryEntry> entries,
        OfferPdfContext ctx,
        DateTimeOffset now,
        bool includeOwnerSummary = true,
        bool includePositionSummary = true,
        IReadOnlyList<OfferPdfHoldingDataLineModel>? holdingDataLines = null)
    {
        var currency = string.IsNullOrWhiteSpace(ctx.Currency) ? "CHF" : ctx.Currency;
        string Money(decimal v) => ChfFormat.Money(v, currency);
        string Qty(decimal v) => v.ToString("0.###", Ch);

        var list = (entries ?? Array.Empty<CostSummaryEntry>())
            .Where(e => e is not null && e.Cost is not null)
            .Select(e =>
            {
                var holding = string.IsNullOrWhiteSpace(e.Holding) ? (e.Cost.Holding ?? "") : e.Holding;
                return new CostSummaryEntry
                {
                    Holding = (holding ?? "").Trim(),
                    Owner = (e.Owner ?? "").Trim(),
                    ExecutedBy = (e.ExecutedBy ?? "").Trim(),
                    Cost = e.Cost
                };
            })
            .Where(e => !string.IsNullOrWhiteSpace(e.Holding))
            .ToList();

        var model = new OfferPdfModel
        {
            DocumentKindLabel = "Kostenzusammenstellung",
            OfferNo = string.IsNullOrWhiteSpace(ctx.OfferNo)
                ? $"KST-{now:yyyyMMdd-HHmmss}"
                : ctx.OfferNo,
            DateText = now.ToLocalTime().ToString("dd.MM.yyyy", Ch),
            ValidityText = ctx.ValidityText ?? "",

            SenderBlock = BuildSenderBlockAbwasserUri(),
            CustomerBlock = ctx.CustomerBlock ?? "",
            ObjectBlock = ctx.ObjectBlock ?? "",

            ProjectTitle = ctx.ProjectTitle ?? "",
            VariantTitle = ctx.VariantTitle ?? "",
            FilterSummaryText = ctx.FilterSummaryText ?? "",

            Totals = new OfferPdfTotalsModel(),
            HoldingDataLines = holdingDataLines?.ToList() ?? new List<OfferPdfHoldingDataLineModel>(),
            TextBlocks = (ctx.TextBlocks is { Count: > 0 } ? ctx.TextBlocks : DefaultSummaryTextBlocks()).ToList(),
        };

        if (list.Count == 0)
        {
            model.Totals.NetText = Money(0m);
            model.Totals.VatText = $"MwSt. 0.0%: {Money(0m)}";
            model.Totals.GrossText = Money(0m);
            return model;
        }

        var overallNet = 0m;
        var overallVat = 0m;
        var overallGross = 0m;

        var measureBuckets = new Dictionary<string, MeasureSummaryBucket>(StringComparer.OrdinalIgnoreCase);
        var ownerBuckets = new Dictionary<string, OwnerSummaryBucket>(StringComparer.OrdinalIgnoreCase);
        var executorBuckets = new Dictionary<string, ExecutorSummaryBucket>(StringComparer.OrdinalIgnoreCase);
        var positionBuckets = new Dictionary<string, PositionSummaryBucket>(StringComparer.OrdinalIgnoreCase);
        var specialStats = SpecialStatsClassifier.CreateSpecialStatsBuckets();

        foreach (var entry in list)
        {
            var owner = string.IsNullOrWhiteSpace(entry.Owner) ? "Unbekannt" : entry.Owner.Trim();
            var executedBy = string.IsNullOrWhiteSpace(entry.ExecutedBy) ? "Unbekannt" : entry.ExecutedBy.Trim();
            var holding = entry.Holding.Trim();
            var (net, vat, gross) = ResolveTotals(entry.Cost);

            overallNet += net;
            overallVat += vat;
            overallGross += gross;

            if (!ownerBuckets.TryGetValue(owner, out var ownerBucket))
            {
                ownerBucket = new OwnerSummaryBucket();
                ownerBuckets[owner] = ownerBucket;
            }

            ownerBucket.Net += net;
            ownerBucket.Vat += vat;
            ownerBucket.Gross += gross;
            ownerBucket.Holdings.Add(holding);

            if (!executorBuckets.TryGetValue(executedBy, out var executorBucket))
            {
                executorBucket = new ExecutorSummaryBucket();
                executorBuckets[executedBy] = executorBucket;
            }

            executorBucket.Net += net;
            executorBucket.Holdings.Add(holding);

            foreach (var measure in entry.Cost.Measures)
            {
                var selectedLines = measure.Lines.Where(l => l.Selected).ToList();
                if (selectedLines.Count == 0)
                    continue;

                var measureName = string.IsNullOrWhiteSpace(measure.MeasureName) ? measure.MeasureId : measure.MeasureName;
                if (string.IsNullOrWhiteSpace(measureName))
                    measureName = "Unbekannte Massnahme";

                var measureNet = selectedLines.Sum(l => l.Qty * l.UnitPrice);
                if (!measureBuckets.TryGetValue(measureName, out var measureBucket))
                {
                    measureBucket = new MeasureSummaryBucket();
                    measureBuckets[measureName] = measureBucket;
                }

                measureBucket.Net += measureNet;
                measureBucket.Holdings.Add(holding);

                foreach (var line in selectedLines)
                {
                    var lineTotal = line.Qty * line.UnitPrice;
                    var missingPrice = IsMissingPrice(line);

                    model.Lines.Add(new OfferPdfLineModel
                    {
                        GroupLabel = $"{holding} ({owner})",
                        Text = line.Text ?? "",
                        Note = measureName,
                        QtyText = Qty(line.Qty),
                        Unit = line.Unit ?? "",
                        UnitPriceText = missingPrice ? MissingPriceText : Money(line.UnitPrice),
                        TotalText = missingPrice ? MissingPriceText : Money(lineTotal),
                    });

                    var group = string.IsNullOrWhiteSpace(line.Group) ? "Sonstiges" : line.Group.Trim();
                    var text = string.IsNullOrWhiteSpace(line.Text) ? (line.ItemKey ?? "") : line.Text.Trim();
                    var unit = (line.Unit ?? "").Trim();
                    var key = $"{group}|{text}|{unit}";

                    if (!positionBuckets.TryGetValue(key, out var positionBucket))
                    {
                        positionBucket = new PositionSummaryBucket
                        {
                            Group = group,
                            Text = text,
                            Unit = unit
                        };
                        positionBuckets[key] = positionBucket;
                    }

                    positionBucket.TotalQty += line.Qty;
                    positionBucket.TotalNet += lineTotal;
                    positionBucket.Holdings.Add(holding);
                    positionBucket.UnitPrices.Add(line.UnitPrice);
                    if (missingPrice)
                        positionBucket.HasMissingPrice = true;

                    if (SpecialStatsClassifier.TryResolveSpecialStatsCategory(line, out var category) &&
                        specialStats.TryGetValue(category, out var statBucket))
                    {
                        statBucket.TotalQty += line.Qty;
                        statBucket.TotalNet += lineTotal;

                        var normalizedUnit = SpecialStatsClassifier.NormalizeUnit(line.Unit);
                        if (normalizedUnit.Length > 0)
                            statBucket.Units.Add(normalizedUnit);
                    }
                }
            }
        }

        var vatRate = overallNet > 0m
            ? Math.Round(overallVat / overallNet * 100m, 1, MidpointRounding.AwayFromZero)
            : 0m;

        model.Totals.NetText = Money(overallNet);
        model.Totals.VatText = $"MwSt. {vatRate:0.0}%: {Money(overallVat)}";
        model.Totals.GrossText = Money(overallGross);

        foreach (var pair in measureBuckets.OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase))
        {
            model.MeasureSummaryLines.Add(new OfferPdfMeasureSummaryLineModel
            {
                MeasureName = pair.Key,
                HoldingCountText = pair.Value.Holdings.Count.ToString(CultureInfo.InvariantCulture),
                NetText = Money(pair.Value.Net)
            });
        }

        foreach (var cfg in SpecialStatsClassifier.SpecialStatsConfigs)
        {
            specialStats.TryGetValue(cfg.Category, out var bucket);
            bucket ??= new SpecialStatsBucket { DefaultUnit = cfg.DefaultUnit };

            model.SpecialStatsLines.Add(new OfferPdfSpecialStatsLineModel
            {
                Category = cfg.Label,
                QtyText = Qty(bucket.TotalQty),
                Unit = SpecialStatsClassifier.ResolveDisplayUnit(bucket),
                NetText = Money(bucket.TotalNet)
            });
        }

        if (includeOwnerSummary)
        {
            foreach (var pair in ownerBuckets.OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase))
            {
                var ownerVatRate = pair.Value.Net > 0m
                    ? Math.Round(pair.Value.Vat / pair.Value.Net * 100m, 1, MidpointRounding.AwayFromZero)
                    : 0m;

                model.OwnerSummaryLines.Add(new OfferPdfOwnerSummaryLineModel
                {
                    Owner = pair.Key,
                    HoldingCountText = pair.Value.Holdings.Count.ToString(CultureInfo.InvariantCulture),
                    NetText = Money(pair.Value.Net),
                    VatText = $"{ownerVatRate:0.0}%",
                    GrossText = Money(pair.Value.Gross)
                });
            }
        }

        if (overallNet > 0m)
        {
            foreach (var pair in executorBuckets
                         .Where(p => p.Value.Net > 0m)
                         .OrderByDescending(p => p.Value.Net)
                         .ThenBy(p => p.Key, StringComparer.OrdinalIgnoreCase))
            {
                var share = pair.Value.Net * 100m / overallNet;
                var barWidth = Math.Clamp(share, 0m, 100m);

                model.ExecutorCostChartLines.Add(new OfferPdfExecutorCostChartLineModel
                {
                    Executor = pair.Key,
                    HoldingCountText = pair.Value.Holdings.Count.ToString(CultureInfo.InvariantCulture),
                    NetText = Money(pair.Value.Net),
                    ShareText = share.ToString("0.#", Ch) + " %",
                    BarWidthPercentText = barWidth.ToString("0.#", CultureInfo.InvariantCulture) + "%"
                });
            }
        }

        if (includePositionSummary)
        {
            foreach (var pair in positionBuckets
                         .OrderBy(p => p.Value.Group, StringComparer.OrdinalIgnoreCase)
                         .ThenBy(p => p.Value.Text, StringComparer.OrdinalIgnoreCase))
            {
                var unitPriceText = pair.Value.HasMissingPrice
                    ? MissingPriceText
                    : pair.Value.UnitPrices.Count == 1
                    ? Money(pair.Value.UnitPrices.First())
                    : "variabel";

                model.PositionSummaryLines.Add(new OfferPdfPositionSummaryLineModel
                {
                    GroupLabel = pair.Value.Group,
                    Position = pair.Value.Text,
                    QtyText = Qty(pair.Value.TotalQty),
                    Unit = pair.Value.Unit,
                    UnitPriceText = unitPriceText,
                    TotalText = pair.Value.HasMissingPrice ? MissingPriceText : Money(pair.Value.TotalNet),
                    HoldingCountText = pair.Value.Holdings.Count.ToString(CultureInfo.InvariantCulture)
                });
            }
        }

        return model;
    }

    private static bool IsMissingPrice(CostLine line)
        => line.UnitPrice == 0m;

    /// <summary>Builds object block text from pipe/holding metadata.</summary>
    public static string BuildObjectBlock(string holding, int? dn, decimal? lengthM, DateTime? date)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(holding))
            parts.Add($"Haltung: {holding}");
        if (dn.HasValue)
            parts.Add($"DN: {dn.Value} mm");
        if (lengthM.HasValue)
            parts.Add($"Laenge: {lengthM.Value:0.00} m");
        if (date.HasValue)
            parts.Add($"Inspektionsdatum: {date.Value:dd.MM.yyyy}");
        return string.Join("\n", parts);
    }

    public static string BuildSenderBlockAbwasserUri() =>
        "Abwasser Uri\n" +
        "Zentrale Dienste\n" +
        "Giessenstrasse 46\n" +
        "6460 Altdorf\n" +
        "info@abwasser-uri.ch\n" +
        "T 041 875 00 90";

    private static List<string> DefaultTextBlocks() =>
    [
        "Grundlage: Auswertung / Datentransfer. Mengen (m/h/stk) werden manuell erfasst.",
        "Gueltigkeit: 30 Tage. Ausfuehrung nach Terminabsprache.",
        "Zahlungsbedingungen: 30 Tage netto."
    ];

    private static List<string> DefaultSummaryTextBlocks() =>
    [
        "Grundlage: gespeicherte Kosten/Massnahmen aus dem Projekt.",
        "Betrag je Position = Menge x Einzelpreis (Netto).",
        "Diese Zusammenstellung dient als Kostenuebersicht, nicht als Offerte."
    ];

    private static (decimal Net, decimal Vat, decimal Gross) ResolveTotals(HoldingCost cost)
    {
        if (cost is null)
            return (0m, 0m, 0m);

        var net = cost.Total;
        if (net <= 0m)
        {
            net = cost.Measures
                .SelectMany(m => m.Lines)
                .Where(l => l.Selected)
                .Sum(l => l.Qty * l.UnitPrice);
        }

        var vat = cost.MwstAmount;
        if (vat <= 0m && cost.MwstRate > 0m && net > 0m)
            vat = Math.Round(net * cost.MwstRate, 2, MidpointRounding.AwayFromZero);

        var gross = cost.TotalInclMwst;
        if (gross <= 0m)
            gross = net + vat;

        return (net, vat, gross);
    }

    private sealed class MeasureSummaryBucket
    {
        public decimal Net { get; set; }
        public HashSet<string> Holdings { get; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private sealed class OwnerSummaryBucket
    {
        public decimal Net { get; set; }
        public decimal Vat { get; set; }
        public decimal Gross { get; set; }
        public HashSet<string> Holdings { get; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private sealed class ExecutorSummaryBucket
    {
        public decimal Net { get; set; }
        public HashSet<string> Holdings { get; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private sealed class PositionSummaryBucket
    {
        public string Group { get; set; } = "";
        public string Text { get; set; } = "";
        public string Unit { get; set; } = "";
        public decimal TotalQty { get; set; }
        public decimal TotalNet { get; set; }
        public HashSet<string> Holdings { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<decimal> UnitPrices { get; } = new();
        public bool HasMissingPrice { get; set; }
    }

}
