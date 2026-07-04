using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Costs;

namespace AuswertungPro.Next.Infrastructure.Output.Offers;

public static class NpkOfferPdfModelFactory
{
    private static readonly CultureInfo Ch = CultureInfo.GetCultureInfo("de-CH");

    public static NpkOfferPdfModel Create(
        IReadOnlyList<AggregatedPosition> positions,
        NpkOfferPdfContext ctx,
        DateTimeOffset now,
        decimal excludedPauschaleTotal = 0m,
        int excludedPauschaleHoldingCount = 0)
    {
        var currency = string.IsNullOrWhiteSpace(ctx.Currency) ? "CHF" : ctx.Currency.Trim();
        string Money(decimal value) => ChfFormat.Money(value, currency);

        var roundedPositions = (positions ?? Array.Empty<AggregatedPosition>())
            .Select(p => new PositionWithRoundedTotal(p, Math.Round(p.TotalNet, 2, MidpointRounding.AwayFromZero)))
            .ToList();
        var grossNet = roundedPositions.Sum(p => p.RoundedTotal);
        var discountAmount = PercentAmount(grossNet, ctx.DiscountPercent);
        var afterDiscount = grossNet - discountAmount;
        var skontoAmount = PercentAmount(afterDiscount, ctx.SkontoPercent);
        var net = afterDiscount - skontoAmount;
        var vat = Math.Round(net * ctx.VatRate, 2, MidpointRounding.AwayFromZero);
        var totalInclVat = Math.Round(net + vat, 2, MidpointRounding.AwayFromZero);

        var model = new NpkOfferPdfModel
        {
            OfferNo = string.IsNullOrWhiteSpace(ctx.OfferNo)
                ? $"NPK-{now:yyyyMMdd-HHmmss}"
                : ctx.OfferNo.Trim(),
            DateText = now.ToLocalTime().ToString("dd.MM.yyyy", Ch),
            ValidityText = string.IsNullOrWhiteSpace(ctx.ValidityText)
                ? "30 Tage"
                : ctx.ValidityText.Trim(),
            SenderBlock = OfferPdfModelFactory.BuildSenderBlockAbwasserUri(),
            CustomerBlock = ctx.CustomerBlock ?? "",
            ObjectBlock = ctx.ObjectBlock ?? "",
            ReferenceBlock = ctx.ReferenceBlock ?? "",
            ProjectTitle = string.IsNullOrWhiteSpace(ctx.ProjectTitle)
                ? "NPK-135-Offerte Kanalsanierung"
                : ctx.ProjectTitle.Trim(),
            VariantTitle = ctx.VariantTitle ?? "",
            FilterSummaryText = ctx.FilterSummaryText ?? "",
            IntroBlocks = (ctx.IntroBlocks is { Count: > 0 } ? ctx.IntroBlocks : DefaultIntroBlocks()).ToList(),
            ConditionLines = (ctx.ConditionLines is { Count: > 0 } ? ctx.ConditionLines : DefaultConditionLines()).ToList(),
            Totals = new NpkOfferTotalsModel
            {
                GrossNetText = Money(grossNet),
                DiscountText = ctx.DiscountPercent > 0m
                    ? $"{ctx.DiscountPercent:0.##} %: -{Money(discountAmount)}"
                    : Money(0m),
                SkontoText = ctx.SkontoPercent > 0m
                    ? $"{ctx.SkontoPercent:0.##} %: -{Money(skontoAmount)}"
                    : Money(0m),
                NetText = Money(net),
                VatText = $"{ctx.VatRate * 100m:0.0} %: {Money(vat)}",
                TotalInclVatText = Money(totalInclVat)
            }
        };

        foreach (var group in roundedPositions
                     .GroupBy(p => p.Position.Chapter ?? "")
                     .OrderBy(g => ProjectPositionAggregator.ChapterOrder(g.Key)))
        {
            var title = NpkLeistungsverzeichnisExporter.ChapterTitle(group.Key);
            var chapterTotal = group.Sum(p => p.RoundedTotal);
            model.ChapterSummaryLines.Add(new NpkOfferChapterSummaryLineModel
            {
                Chapter = string.IsNullOrWhiteSpace(group.Key) ? "-" : group.Key.Trim(),
                Title = title,
                TotalText = Money(chapterTotal)
            });

            foreach (var item in group
                         .OrderBy(p => p.Position.NpkCode, StringComparer.OrdinalIgnoreCase)
                         .ThenBy(p => p.Position.Text, StringComparer.OrdinalIgnoreCase)
                         .ThenBy(p => p.Position.Dn ?? 0))
            {
                model.PositionLines.Add(new NpkOfferPositionLineModel
                {
                    ChapterTitle = title,
                    NpkCode = item.Position.NpkCode ?? "",
                    Text = AppendPriceHint(item.Position.Text, item.Position.PriceHint),
                    DnText = item.Position.Dn?.ToString(CultureInfo.InvariantCulture) ?? "",
                    QtyText = item.Position.TotalQty.ToString("0.###", Ch),
                    Unit = item.Position.Unit ?? "",
                    UnitPriceText = item.Position.UnitPrice.HasValue ? Money(item.Position.UnitPrice.Value) : "variabel",
                    TotalText = Money(item.RoundedTotal),
                    HoldingCountText = item.Position.HoldingCount.ToString(CultureInfo.InvariantCulture)
                });
            }
        }

        foreach (var warning in BuildDuplicateNpkUnitWarnings(positions))
            model.Footnotes.Add(warning);
        if (excludedPauschaleTotal > 0m)
        {
            var countText = excludedPauschaleHoldingCount > 0
                ? $" ({excludedPauschaleHoldingCount} Haltung(en))"
                : "";
            model.Footnotes.Add(
                $"Nicht in NPK-Positionen enthaltene Pauschalkosten{countText}: {Money(excludedPauschaleTotal)}.");
        }

        return model;
    }

    private static decimal PercentAmount(decimal basis, decimal percent)
        => percent <= 0m
            ? 0m
            : Math.Round(basis * percent / 100m, 2, MidpointRounding.AwayFromZero);

    private static string AppendPriceHint(string? text, string? priceHint)
    {
        var t = (text ?? "").Trim();
        var h = (priceHint ?? "").Trim();
        if (h.Length == 0)
            return t;
        if (t.Length == 0)
            return h;
        return t.IndexOf(h, StringComparison.OrdinalIgnoreCase) >= 0 ? t : $"{t} ({h})";
    }

    private static IReadOnlyList<string> BuildDuplicateNpkUnitWarnings(IReadOnlyList<AggregatedPosition>? positions)
        => (positions ?? Array.Empty<AggregatedPosition>())
            .Where(p => !string.IsNullOrWhiteSpace(p.NpkCode))
            .GroupBy(p => p.NpkCode.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => new
            {
                Code = g.Key,
                Units = g.Select(p => (p.Unit ?? "").Trim())
                    .Where(u => u.Length > 0)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(u => u, StringComparer.OrdinalIgnoreCase)
                    .ToList()
            })
            .Where(x => x.Units.Count > 1)
            .Select(x => $"Warnung: NPK {x.Code} kommt mit unterschiedlichen Einheiten vor: {string.Join(", ", x.Units)}.")
            .ToList();

    private static List<string> DefaultIntroBlocks() =>
    [
        "Wir danken fuer Ihre Anfrage und unterbreiten Ihnen gerne unsere Offerte fuer die aufgefuehrten Kanalsanierungsarbeiten.",
        "Die Mengen basieren auf den im Projekt gespeicherten Sanierungskosten. Die Detailpositionen werden nach NPK-135-Kapiteln gegliedert und verstehen sich netto exkl. MwSt."
    ];

    private static List<NpkOfferConditionLineModel> DefaultConditionLines() =>
    [
        new() { Label = "Zahlungskonditionen", ValueText = "30 Tage netto" },
        new() { Label = "Gueltigkeit", ValueText = "30 Tage ab Ausstelldatum" },
        new() { Label = "Ausfuehrung", ValueText = "Nach Terminabsprache und technischer Freigabe" }
    ];

    private sealed record PositionWithRoundedTotal(AggregatedPosition Position, decimal RoundedTotal);
}
