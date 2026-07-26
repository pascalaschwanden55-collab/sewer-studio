using System;
using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Infrastructure.Output.Offers;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

public sealed partial class BuilderPageViewModel
{
    private static string BuildVatMismatchHint(
        IReadOnlyList<CostSummaryEntry> entries,
        decimal currentVatRate)
    {
        var oldRates = entries
            .Select(e => e.Cost.MwstRate)
            .Where(rate => rate > 0m && rate != currentVatRate)
            .Distinct()
            .OrderBy(rate => rate)
            .Select(FormatVatRate)
            .ToList();
        if (oldRates.Count == 0)
            return "";

        return "Hinweis: Diese Ausgabe enthaelt gespeicherte MwSt-Saetze "
            + string.Join(", ", oldRates)
            + $"; aktueller Katalogsatz ist {FormatVatRate(currentVatRate)}. "
            + "Kosten neu berechnen, wenn der aktuelle Satz gelten soll.";
    }

    private static IReadOnlyList<decimal> FindMismatchedVatRates(
        IEnumerable<DruckcenterRowVm> rows,
        decimal currentVatRate)
        => rows
            .Select(row => row.StoredCost?.MwstRate ?? 0m)
            .Where(rate => rate > 0m && rate != currentVatRate)
            .Distinct()
            .OrderBy(rate => rate)
            .ToList();

    private static string BuildVatRecomputePrompt(
        IReadOnlyList<decimal> oldRates,
        decimal currentVatRate)
        => "Die gefilterte Ausgabe enthaelt gespeicherte Kosten mit altem MwSt-Satz "
           + string.Join(", ", oldRates.Select(FormatVatRate))
           + $".\nAktueller Katalogsatz ist {FormatVatRate(currentVatRate)}.\n\n"
           + "Ja = alle gespeicherten Sanierungskosten jetzt mit aktuellem Katalog/MwSt neu berechnen\n"
           + "Nein = Ausgabe mit gespeicherten Saetzen fortsetzen\n"
           + "Abbrechen = Export abbrechen\n\n"
           + "Manuelle Preis-Overrides bleiben unangetastet.";

    private static string FormatVatRate(decimal rate)
        => $"{rate * 100m:0.0}%";

    private static string BuildReferenceBlock(IReadOnlyDictionary<string, string> metadata)
    {
        var lines = new List<string>();
        AddReferenceLine(
            lines,
            "Ihre Referenz",
            metadata,
            "Referenz",
            "IhreReferenz",
            "AuftraggeberReferenz");
        AddReferenceLine(lines, "Unsere Referenz", metadata, "Bearbeiter", "UnsereReferenz");
        AddReferenceLine(lines, "Auftrag-Nr.", metadata, "AuftragNr");
        return string.Join("\n", lines);
    }

    private static void AddReferenceLine(
        List<string> lines,
        string label,
        IReadOnlyDictionary<string, string> metadata,
        params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!metadata.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
                continue;

            lines.Add($"{label}: {value.Trim()}");
            return;
        }
    }

    private static bool IsSanierenYes(string value)
    {
        var normalized = value.Trim();
        return normalized.Equals("ja", StringComparison.OrdinalIgnoreCase)
               || normalized.Equals("yes", StringComparison.OrdinalIgnoreCase)
               || normalized.Equals("true", StringComparison.OrdinalIgnoreCase)
               || normalized.Equals("1", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSanierenNo(string value)
    {
        var normalized = value.Trim();
        return normalized.Equals("nein", StringComparison.OrdinalIgnoreCase)
               || normalized.Equals("no", StringComparison.OrdinalIgnoreCase)
               || normalized.Equals("false", StringComparison.OrdinalIgnoreCase)
               || normalized.Equals("0", StringComparison.OrdinalIgnoreCase);
    }
}
