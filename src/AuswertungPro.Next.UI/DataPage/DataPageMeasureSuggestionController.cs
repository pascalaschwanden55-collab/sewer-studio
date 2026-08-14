using System;
using System.Collections.Generic;
using System.Globalization;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.DataPage;

public sealed class DataPageMeasureSuggestionController
{
    /// <summary>Geldbetraege werden schweizerisch dargestellt, unabhaengig von der Rechnerkultur.</summary>
    private static readonly CultureInfo SchweizerZahl = CultureInfo.GetCultureInfo("de-CH");

    private readonly IDialogService _dialogs;
    private readonly IMeasureRecommendationService _recommendations;
    private readonly Func<HaltungRecord?> _getSelected;
    private readonly Func<IReadOnlyList<HaltungRecord>> _getRecords;
    private readonly Action<string> _addRecommendedOption;
    private readonly Action _markProjectDirty;
    private readonly Action<string> _setStatus;
    private readonly Action<int?, decimal?> _updateLearningInfo;

    public DataPageMeasureSuggestionController(
        IDialogService dialogs,
        IMeasureRecommendationService recommendations,
        Func<HaltungRecord?> getSelected,
        Func<IReadOnlyList<HaltungRecord>> getRecords,
        Action<string> addRecommendedOption,
        Action markProjectDirty,
        Action<string> setStatus,
        Action<int?, decimal?> updateLearningInfo)
    {
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _recommendations = recommendations ?? throw new ArgumentNullException(nameof(recommendations));
        _getSelected = getSelected ?? throw new ArgumentNullException(nameof(getSelected));
        _getRecords = getRecords ?? throw new ArgumentNullException(nameof(getRecords));
        _addRecommendedOption = addRecommendedOption ?? throw new ArgumentNullException(nameof(addRecommendedOption));
        _markProjectDirty = markProjectDirty ?? throw new ArgumentNullException(nameof(markProjectDirty));
        _setStatus = setStatus ?? throw new ArgumentNullException(nameof(setStatus));
        _updateLearningInfo = updateLearningInfo ?? throw new ArgumentNullException(nameof(updateLearningInfo));
    }

    public void Suggest(HaltungRecord? record)
    {
        record ??= _getSelected();
        if (record is null)
            return;

        var recommendation = _recommendations.Recommend(record, maxSuggestions: 5);
        if (recommendation.Measures.Count == 0)
        {
            _dialogs.Info(
                "Noch keine Vorschlaege verfuegbar. Bitte zuerst einige Haltungen mit Massnahmen bewerten.",
                "Massnahmen");
            return;
        }

        DataPageSanierungCostMapper.ApplyRecommendation(record, recommendation);
        foreach (var suggestion in recommendation.Measures)
            _addRecommendedOption(suggestion);

        _markProjectDirty();

        var sourceText = recommendation.UsedTrainedModel ? "KI-Modell" : "Lernlogik";
        _setStatus(recommendation.EstimatedTotalCost is null
            ? $"Maßnahmenvorschlag aus Schadenscodes gesetzt ({sourceText})"
            : $"Maßnahmenvorschlag mit Kostenschätzung gesetzt ({recommendation.EstimatedTotalCost.Value:0.00}, {sourceText})");
        _updateLearningInfo(recommendation.SimilarCasesCount, recommendation.EstimatedTotalCost);

        var summary = string.Join("\n", recommendation.Measures);
        if (recommendation.EstimatedTotalCost is not null)
        {
            // Geldbetrag ausdruecklich schweizerisch, nicht nach Rechnerkultur: Sonst zeigt
            // dieselbe Zahl je nach Windows-Einstellung 1'250.00 oder 1,250.00. Dieselbe
            // Festlegung wie in den PDF-Modellen und der ETA-Anzeige.
            summary += "\n\nGeschaetzte Kosten: "
                + recommendation.EstimatedTotalCost.Value.ToString("N2", SchweizerZahl);
        }
        summary += $"\n\nQuelle: {sourceText}";
        if (recommendation.SimilarCasesCount > 0)
            summary += $" ({recommendation.SimilarCasesCount} aehnliche Faelle)";
        _dialogs.Info(summary, "Empfohlene Sanierungsmassnahmen");
    }

    public void SuggestAll()
    {
        var records = _getRecords();
        if (records.Count == 0)
        {
            _dialogs.Info("Keine Haltungen vorhanden.", "Massnahmen");
            return;
        }

        var filled = 0;
        var skipped = 0;
        var noSuggestion = 0;

        foreach (var record in records)
        {
            var pruefung = (record.GetFieldValue("Pruefungsresultat") ?? "").Trim();
            var existingMeasures = (record.GetFieldValue("Empfohlene_Sanierungsmassnahmen") ?? "").Trim();
            var hasDamageCodes = record.VsaFindings is not null && record.VsaFindings.Count > 0
                || !string.IsNullOrWhiteSpace(record.GetFieldValue("Primaere_Schaeden"));

            if (!string.IsNullOrWhiteSpace(existingMeasures))
            {
                record.FieldMeta.TryGetValue("Empfohlene_Sanierungsmassnahmen", out var meta);
                if (meta is not null && meta.UserEdited)
                {
                    skipped++;
                    continue;
                }
            }

            if (!string.Equals(pruefung, "Sanierungsbedarf", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(pruefung, "beobachten", StringComparison.OrdinalIgnoreCase)
                && !hasDamageCodes)
            {
                skipped++;
                continue;
            }

            var recommendation = _recommendations.Recommend(record, maxSuggestions: 5);
            if (recommendation.Measures.Count == 0)
            {
                noSuggestion++;
                continue;
            }

            DataPageSanierungCostMapper.ApplyRecommendation(record, recommendation);
            foreach (var suggestion in recommendation.Measures)
                _addRecommendedOption(suggestion);

            filled++;
        }

        if (filled > 0)
            _markProjectDirty();

        _setStatus($"Maßnahmen: {filled} Haltungen befüllt, {skipped} übersprungen, {noSuggestion} ohne Vorschlag");
    }
}
