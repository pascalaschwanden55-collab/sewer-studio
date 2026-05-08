using System;
using System.Globalization;
using System.Linq;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

// Kosten-Uebernahme aus dem CostCalculator + Helfer fuer Massnahmen-Texte,
// Liner/Manschette/Anschluss-Erkennung, Mengen-Aggregation und Formatter.
// Konsumiert von Sanierung-Partial (RestoreCosts/SuggestMeasures) und
// vom Main-Save (Learning ueber MeasureRecommendationService).
public sealed partial class DataPageViewModel
{
    private void ApplyCostsToRecord(HaltungRecord record, HoldingCost cost, bool learn = true, bool includeCosts = true)
    {
        if (includeCosts)
        {
            // Transfer net amount to table field "Kosten".
            var netTotal = ResolveNetTotal(cost);
            var totalText = netTotal.ToString("0.00", CultureInfo.InvariantCulture);
            record.SetFieldValue("Kosten", totalText, FieldSource.Manual, userEdited: true);
        }

        var massnahmenText = BuildMeasuresText(cost);
        record.SetFieldValue("Empfohlene_Sanierungsmassnahmen", massnahmenText, FieldSource.Manual, userEdited: true);

        var inlinerMeters = SumMeasureLengths(
            cost,
            "NADELFILZ",
            "GFK",
            "SCHLAUCHLINER_NADELFILZ",
            "SCHLAUCHLINER_NADELFILZ_OPENEND",
            "SCHLAUCHLINER_GFK");
        // Domain rule: if a liner is selected, count exactly 1 piece.
        var inlinerStk = HasSelectedLiner(cost) ? 1 : 0;
        var anschluesse = Math.Max(
            SumSelectedQty(cost, "ANSCHLUSS_EINBINDEN"),
            SumSelectedQty(cost, "ANSCHLUSS_AUFFRAESEN"));
        // LEM is not a repair manschette and must not fill Reparatur_Manschette.
        var manschette = SumSelectedQty(cost, "MANSCHETTE_PER_ST", "MANSCHETTE_EDELSTAHL");
        var lem = SumSelectedQty(cost, "LINERENDMANSCHETTE_LEM");
        var kurzliner = SumSelectedQty(cost, "KURZLINER_PER_ST", "QUICKLOCK_PER_ST", "KURZLINER_PARTLINER");

        record.SetFieldValue("Renovierung_Inliner_m", FormatDecimal(inlinerMeters), FieldSource.Manual, userEdited: true);
        record.SetFieldValue("Renovierung_Inliner_Stk", FormatInt(inlinerStk), FieldSource.Manual, userEdited: true);
        record.SetFieldValue("Anschluesse_verpressen", FormatNonNegativeInt(anschluesse), FieldSource.Manual, userEdited: true);
        record.SetFieldValue("Reparatur_Manschette", FormatInt(manschette), FieldSource.Manual, userEdited: true);
        record.SetFieldValue("Linerendmanschette_LEM", FormatInt(lem), FieldSource.Manual, userEdited: true);
        record.SetFieldValue("Reparatur_Kurzliner", FormatInt(kurzliner), FieldSource.Manual, userEdited: true);

        // Force a replace notification on the collection so dictionary-backed
        // grid cells refresh immediately without extra user clicks.
        RefreshRecordInGrid(record);

        if (learn)
        {
            var learnedNow = _measureRecommendationService.Learn(record);
            if (learnedNow)
                _measureRecommendationService.TrainModel(MinimumSamplesForModelTraining);
            UpdateLearningInfo();
        }

        _shell.Project.ModifiedAtUtc = DateTime.UtcNow;
        _shell.Project.Dirty = true;
    }

    private static decimal ResolveNetTotal(HoldingCost cost)
    {
        if (cost.Total > 0m)
            return cost.Total;

        if (cost.TotalInclMwst > 0m && cost.MwstRate > 0m)
            return Math.Round(cost.TotalInclMwst / (1m + cost.MwstRate), 2, MidpointRounding.AwayFromZero);

        return cost.TotalInclMwst;
    }

    private static string BuildMeasuresText(HoldingCost cost)
    {
        // Nur kanonische Massnahmen-Namen (Template-Level) schreiben,
        // KEINE einzelnen Kostenzeilen wie Verkehrsdienst oder Nebenarbeiten.
        // Grund: Dieses Feld wird vom MeasureRecommendationService als Lernlabel
        // eingelesen. Nebenpositionen wuerden das Learning vergiften.
        var measureNames = cost.Measures
            .Where(m => m.Lines.Any(l => l.Selected && IsHauptarbeitLine(l)))
            .Select(m => m.MeasureName ?? m.MeasureId ?? "")
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (measureNames.Count > 0)
            return string.Join(Environment.NewLine, measureNames);

        // Fallback: Falls keine Hauptarbeit erkannt, Transfer-markierte Zeilen
        // (legacy/manuelles Verhalten), aber nur Hauptarbeit-Zeilen, keine Nebenarbeiten.
        var markedHauptarbeit = cost.Measures
            .SelectMany(m => m.Lines)
            .Where(l => l.Selected && l.TransferMarked && IsHauptarbeitLine(l))
            .Select(l => FormatRecommendationBullet(l.Text))
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (markedHauptarbeit.Count > 0)
            return string.Join(Environment.NewLine, markedHauptarbeit);

        return "";
    }

    private static bool IsAutoIncludedRecommendationLine(CostLine line)
    {
        if (line is null)
            return false;

        return IsHauptarbeitLine(line) || IsVerkehrsdienstLine(line);
    }

    private static bool IsHauptarbeitLine(CostLine line)
    {
        if (line is null)
            return false;

        if (!string.IsNullOrWhiteSpace(line.Group) &&
            line.Group.Contains("hauptarbeit", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return IsHauptarbeitIdentifier(line.ItemKey)
            || IsHauptarbeitIdentifier(line.Text);
    }

    private static bool IsHauptarbeitIdentifier(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        return MatchesIdentifier(value, "SCHLAUCHLINER")
            || MatchesIdentifier(value, "LINERENDMANSCHETTE")
            || MatchesIdentifier(value, "KURZLINER")
            || MatchesIdentifier(value, "MANSCHETTE")
            || MatchesIdentifier(value, "ANSCHLUSS_AUFFRAESEN")
            || MatchesIdentifier(value, "ANSCHLUSS_EINBINDEN")
            || MatchesIdentifier(value, "HAUPTARBEIT");
    }

    private static bool IsVerkehrsdienstLine(CostLine line)
    {
        if (line is null)
            return false;

        if (MatchesIdentifier(line.ItemKey, "VORARBEIT_VD"))
            return true;

        var text = line.Text ?? "";
        return text.Contains("verkehrsdienst", StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatRecommendationBullet(string? value)
    {
        var normalized = NormalizeRecommendationEntry(value);
        return normalized.Length == 0 ? string.Empty : "- " + normalized;
    }

    private static string NormalizeRecommendationEntry(string? value)
    {
        var text = (value ?? string.Empty).Trim();
        while (text.Length > 0 && (text[0] == '-' || text[0] == '*'))
            text = text[1..].TrimStart();
        return text;
    }

    private static decimal SumMeasureLengths(HoldingCost cost, params string[] measureIds)
    {
        var sum = 0m;
        foreach (var measure in cost.Measures)
        {
            if (!measureIds.Any(id => MatchesIdentifier(measure.MeasureId, id)))
                continue;
            if (measure.LengthMeters is not null)
            {
                sum += measure.LengthMeters.Value;
                continue;
            }

            var fallback = measure.Lines
                .Where(l => l.Selected && string.Equals(l.Unit, "m", StringComparison.OrdinalIgnoreCase))
                .Select(l => l.Qty)
                .DefaultIfEmpty(0m)
                .Max();
            sum += fallback;
        }
        return sum;
    }

    private static bool HasSelectedLiner(HoldingCost cost)
    {
        foreach (var measure in cost.Measures)
        {
            var selectedLines = measure.Lines.Where(l => l.Selected).ToList();
            if (selectedLines.Count == 0)
                continue;

            if (selectedLines.Any(IsLinerLine))
                return true;

            // Fallback for legacy payloads where only measure id is reliable.
            if (IsLinerIdentifier(measure.MeasureId))
                return true;
        }

        return false;
    }

    private static bool IsLinerLine(CostLine line)
    {
        if (line is null)
            return false;

        if (IsLinerIdentifier(line.ItemKey))
            return true;

        var text = line.Text ?? "";
        return text.Contains("schlauchliner", StringComparison.OrdinalIgnoreCase)
            || text.Contains("nadelfilz", StringComparison.OrdinalIgnoreCase)
            || text.Contains("gfk", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLinerIdentifier(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        return MatchesIdentifier(value, "SCHLAUCHLINER_NADELFILZ")
            || MatchesIdentifier(value, "SCHLAUCHLINER_NADELFILZ_OPENEND")
            || MatchesIdentifier(value, "SCHLAUCHLINER_GFK")
            || MatchesIdentifier(value, "NADELFILZ_LINER_BIS_5M")
            || MatchesIdentifier(value, "SCHLAUCHLINER_NADELFILZ_BIS_5M")
            || MatchesIdentifier(value, "NADELFILZ")
            || MatchesIdentifier(value, "GFK");
    }

    private static bool MatchesIdentifier(string? value, string pattern)
    {
        if (string.IsNullOrWhiteSpace(value) || string.IsNullOrWhiteSpace(pattern))
            return false;

        var candidate = value.Trim();
        var token = pattern.Trim();
        if (string.Equals(candidate, token, StringComparison.OrdinalIgnoreCase))
            return true;

        // Legacy patterns like "NADELFILZ" or "GFK" should match newer IDs.
        if (token.IndexOf('_') >= 0 || token.IndexOf('-') >= 0)
            return false;

        return candidate.Contains(token, StringComparison.OrdinalIgnoreCase);
    }

    private static int SumSelectedQty(HoldingCost cost, params string[] itemKeys)
    {
        var total = 0m;
        foreach (var measure in cost.Measures)
        {
            foreach (var line in measure.Lines)
            {
                if (!line.Selected)
                    continue;
                if (!itemKeys.Any(key => string.Equals(line.ItemKey, key, StringComparison.OrdinalIgnoreCase)))
                    continue;
                total += line.Qty;
            }
        }
        return (int)Math.Round(total, 0, MidpointRounding.AwayFromZero);
    }

    private static string FormatDecimal(decimal value)
        => value <= 0m ? "" : value.ToString("0.00", CultureInfo.InvariantCulture);

    private static string FormatInt(int value)
        => value <= 0 ? "" : value.ToString(CultureInfo.InvariantCulture);

    private static string FormatNonNegativeInt(int value)
        => Math.Max(0, value).ToString(CultureInfo.InvariantCulture);
}
