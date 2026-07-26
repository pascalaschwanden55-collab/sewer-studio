using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Costs;
using AuswertungPro.Next.Infrastructure.Output.Offers;

namespace AuswertungPro.Next.UI.ViewModels.Windows;

public sealed record CostCalculatorPdfExportModelBuildResult(
    IReadOnlyList<CostSummaryEntry> Entries,
    OfferPdfModel Model);

public static class CostCalculatorPdfExportModelBuilder
{
    public static CostCalculatorPdfExportModelBuildResult? Build(
        string holding,
        DateTime? date,
        HoldingCost currentHoldingCost,
        IEnumerable<MeasureBlockVm> selectedMeasures,
        IReadOnlyDictionary<string, string> ownerByHolding,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(currentHoldingCost);
        ArgumentNullException.ThrowIfNull(selectedMeasures);
        ArgumentNullException.ThrowIfNull(ownerByHolding);

        var entries = CostCalculatorSummaryEntryBuilder.Build(currentHoldingCost, ownerByHolding);
        if (entries.Count == 0)
            return null;

        var (dn, lengthM) = ResolveFirstDnAndLength(selectedMeasures);
        var textBlocks = new List<string>
        {
            "Kosten je Massnahme: Nettobetraege fuer die aktuell ausgewaehlte Haltung.",
            "Kostenzusammenstellung nach Eigentuemer und Gesamtpositionen fuer diese Haltung.",
            "Diese Ausgabe ersetzt eine Offerte und dient als Kostenuebersicht."
        };
        var vatMismatchHint = BuildVatMismatchHint(currentHoldingCost.MwstRate, CostCalculatorLogicService.DefaultVatRate);
        if (vatMismatchHint.Length > 0)
            textBlocks.Insert(1, vatMismatchHint);

        var context = new OfferPdfContext
        {
            ProjectTitle = "Abwasser Uri - Kostenzusammenstellung",
            VariantTitle = $"Auswertung ({entries.Count} Haltung(en))",
            CustomerBlock = "",
            ObjectBlock = OfferPdfModelFactory.BuildObjectBlock(holding, dn, lengthM, date),
            FilterSummaryText = "Eigentuemer: Alle",
            Currency = "CHF",
            OfferNo = "",
            TextBlocks = textBlocks
        };

        var model = OfferPdfModelFactory.CreateCostSummary(
            entries,
            context,
            now,
            includeOwnerSummary: true,
            includePositionSummary: true);

        return new CostCalculatorPdfExportModelBuildResult(entries, model);
    }

    private static string BuildVatMismatchHint(decimal storedVatRate, decimal currentVatRate)
    {
        if (storedVatRate <= 0m || storedVatRate == currentVatRate)
            return "";

        return $"Hinweis: Diese Ausgabe nutzt den gespeicherten MwSt-Satz {FormatVatRate(storedVatRate)}; "
            + $"aktueller Katalogsatz ist {FormatVatRate(currentVatRate)}. "
            + "Kosten neu berechnen, wenn der aktuelle Satz gelten soll.";
    }

    private static string FormatVatRate(decimal rate)
        => $"{rate * 100m:0.0}%";

    private static (int? Dn, decimal? LengthM) ResolveFirstDnAndLength(
        IEnumerable<MeasureBlockVm> selectedMeasures)
    {
        int? dn = null;
        decimal? lengthM = null;

        foreach (var measure in selectedMeasures)
        {
            if (dn is null && int.TryParse(measure.DnText?.Trim(), out var parsedDn))
                dn = parsedDn;

            if (lengthM is null && FachzahlParser.TryParseMeasurement(measure.LengthText, out var parsedLength))
                lengthM = parsedLength;

            if (dn.HasValue && lengthM.HasValue)
                break;
        }

        return (dn, lengthM);
    }
}
