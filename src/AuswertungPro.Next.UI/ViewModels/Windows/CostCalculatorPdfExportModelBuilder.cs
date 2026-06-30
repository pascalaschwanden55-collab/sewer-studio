using AuswertungPro.Next.Domain.Models;
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
        var context = new OfferPdfContext
        {
            ProjectTitle = "Abwasser Uri - Kostenzusammenstellung",
            VariantTitle = $"Auswertung ({entries.Count} Haltung(en))",
            CustomerBlock = "",
            ObjectBlock = OfferPdfModelFactory.BuildObjectBlock(holding, dn, lengthM, date),
            FilterSummaryText = "Eigentuemer: Alle",
            Currency = "CHF",
            OfferNo = "",
            TextBlocks = new List<string>
            {
                "Kosten je Massnahme: Nettobetraege fuer die aktuell ausgewaehlte Haltung.",
                "Kostenzusammenstellung nach Eigentuemer und Gesamtpositionen fuer diese Haltung.",
                "Diese Ausgabe ersetzt eine Offerte und dient als Kostenuebersicht."
            }
        };

        var model = OfferPdfModelFactory.CreateCostSummary(
            entries,
            context,
            now,
            includeOwnerSummary: true,
            includePositionSummary: true);

        return new CostCalculatorPdfExportModelBuildResult(entries, model);
    }

    private static (int? Dn, decimal? LengthM) ResolveFirstDnAndLength(
        IEnumerable<MeasureBlockVm> selectedMeasures)
    {
        int? dn = null;
        decimal? lengthM = null;

        foreach (var measure in selectedMeasures)
        {
            if (dn is null && int.TryParse(measure.DnText?.Trim(), out var parsedDn))
                dn = parsedDn;

            if (lengthM is null && decimal.TryParse(measure.LengthText?.Trim(), out var parsedLength))
                lengthM = parsedLength;

            if (dn.HasValue && lengthM.HasValue)
                break;
        }

        return (dn, lengthM);
    }
}
