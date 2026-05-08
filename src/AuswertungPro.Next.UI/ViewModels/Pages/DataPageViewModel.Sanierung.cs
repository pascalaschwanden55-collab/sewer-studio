using System;
using System.Globalization;
using System.IO;
using System.Windows;
using AuswertungPro.Next.Application.Ai.Sanierung;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Costs;
using AuswertungPro.Next.UI.Dialogs;
using AuswertungPro.Next.UI.ViewModels.Windows;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

// Sanierungs-Workflow: Kosten wiederherstellen, Massnahmen vorschlagen
// (einzeln + Batch), Sanierungsmassnahmen-Window oeffnen mit optionaler
// KI-Optimierung (AiSanierungOptimizationService).
public sealed partial class DataPageViewModel
{
    private bool CanOpenCosts(HaltungRecord? record)
    {
        if (record is not null)
            return true;
        return Selected is not null;
    }

    private bool CanRestoreCosts(HaltungRecord? record)
    {
        if (record is not null)
            return true;
        return Selected is not null;
    }

    private bool CanSuggestMeasures(HaltungRecord? record)
    {
        if (record is not null)
            return true;
        return Selected is not null;
    }

    private void RestoreCosts(HaltungRecord? record)
    {
        record ??= Selected;
        if (record is null)
            return;

        var holding = (record.GetFieldValue("Haltungsname") ?? "").Trim();
        if (string.IsNullOrWhiteSpace(holding))
        {
            _dialogs.ShowMessage("Haltungsname fehlt in der Zeile.", "Kosten/Massnahmen",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var projectPath = App.Resolve<AppSettings>().LastProjectPath;
        if (string.IsNullOrWhiteSpace(projectPath))
        {
            _dialogs.ShowMessage("Projekt bitte zuerst speichern/oeffnen, um Kosten wiederherzustellen.", "Kosten/Massnahmen",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var store = new ProjectCostStoreRepository().Load(projectPath);
        if (!store.ByHolding.TryGetValue(holding, out var cost))
        {
            var dir = Path.GetDirectoryName(projectPath);
            var storePath = string.IsNullOrWhiteSpace(dir) ? "" : ProjectCostStoreRepository.GetStorePath(dir);
            _dialogs.ShowMessage($"Keine gespeicherten Kosten/Massnahmen gefunden fuer:\n{holding}\n\nDatei:\n{storePath}",
                "Kosten/Massnahmen", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        ApplyCostsToRecord(record, cost, learn: false);
        _shell.SetStatus($"Kosten/Massnahmen wiederhergestellt: {holding}");
    }

    private void OpenCosts(HaltungRecord? record)
    {
        OpenSanierungsmassnahmenWindow(record, InitialFocusMode.CostCalculator);
    }

    private void SuggestMeasures(HaltungRecord? record)
    {
        record ??= Selected;
        if (record is null)
            return;

        var recommendation = _measureRecommendationService.Recommend(record, maxSuggestions: 5);
        if (recommendation.Measures.Count == 0)
        {
            _dialogs.ShowMessage(
                "Noch keine Vorschlaege verfuegbar. Bitte zuerst einige Haltungen mit Massnahmen bewerten.",
                "Massnahmen",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var value = string.Join(Environment.NewLine, recommendation.Measures);
        record.SetFieldValue("Empfohlene_Sanierungsmassnahmen", value, FieldSource.Unknown, userEdited: false);
        foreach (var suggestion in recommendation.Measures)
            AddOptionIfMissing(EmpfohleneSanierungsmassnahmenOptions, suggestion);

        if (recommendation.EstimatedTotalCost is not null)
            record.SetFieldValue("Kosten", recommendation.EstimatedTotalCost.Value.ToString("0.00", CultureInfo.InvariantCulture), FieldSource.Unknown, userEdited: false);
        if (recommendation.RenovierungInlinerM is not null)
            record.SetFieldValue("Renovierung_Inliner_m", FormatDecimal(recommendation.RenovierungInlinerM.Value), FieldSource.Unknown, userEdited: false);
        if (recommendation.RenovierungInlinerStk is not null)
            record.SetFieldValue("Renovierung_Inliner_Stk", FormatInt(recommendation.RenovierungInlinerStk.Value), FieldSource.Unknown, userEdited: false);
        if (recommendation.AnschluesseVerpressen is not null)
            record.SetFieldValue("Anschluesse_verpressen", FormatInt(recommendation.AnschluesseVerpressen.Value), FieldSource.Unknown, userEdited: false);
        if (recommendation.ReparaturManschette is not null)
            record.SetFieldValue("Reparatur_Manschette", FormatInt(recommendation.ReparaturManschette.Value), FieldSource.Unknown, userEdited: false);
        if (recommendation.ReparaturKurzliner is not null)
            record.SetFieldValue("Reparatur_Kurzliner", FormatInt(recommendation.ReparaturKurzliner.Value), FieldSource.Unknown, userEdited: false);

        _shell.Project.ModifiedAtUtc = DateTime.UtcNow;
        _shell.Project.Dirty = true;
        var sourceText = recommendation.UsedTrainedModel ? "KI-Modell" : "Lernlogik";
        _shell.SetStatus(recommendation.EstimatedTotalCost is null
            ? $"Massnahmenvorschlag aus Schadenscodes gesetzt ({sourceText})"
            : $"Massnahmenvorschlag mit Kostenschaetzung gesetzt ({recommendation.EstimatedTotalCost.Value:0.00}, {sourceText})");
        UpdateLearningInfo(recommendation.SimilarCasesCount, recommendation.EstimatedTotalCost);

        // Show result dialog so user sees the suggested measures
        var summary = string.Join("\n", recommendation.Measures);
        if (recommendation.EstimatedTotalCost is not null)
            summary += $"\n\nGeschaetzte Kosten: {recommendation.EstimatedTotalCost.Value:N2}";
        summary += $"\n\nQuelle: {sourceText}";
        if (recommendation.SimilarCasesCount > 0)
            summary += $" ({recommendation.SimilarCasesCount} aehnliche Faelle)";
        _dialogs.ShowMessage(summary, "Empfohlene Sanierungsmassnahmen",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    /// <summary>
    /// Batch: Fuer alle Haltungen mit Sanierungsbedarf (oder fehlenden Massnahmen)
    /// automatisch Sanierungsmassnahmen vorschlagen.
    /// </summary>
    public void SuggestAllMeasures()
    {
        var records = _shell.Project.Data;
        if (records.Count == 0)
        {
            _dialogs.ShowMessage("Keine Haltungen vorhanden.", "Massnahmen",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var filled = 0;
        var skipped = 0;
        var noSuggestion = 0;

        foreach (var record in records)
        {
            // Nur Records mit Sanierungsbedarf oder schlechter Zustandsnote beruecksichtigen
            var pruefung = (record.GetFieldValue("Pruefungsresultat") ?? "").Trim();
            var existingMeasures = (record.GetFieldValue("Empfohlene_Sanierungsmassnahmen") ?? "").Trim();
            var hasDamageCodes = record.VsaFindings is not null && record.VsaFindings.Count > 0
                || !string.IsNullOrWhiteSpace(record.GetFieldValue("Primaere_Schaeden"));

            // Ueberspringe Records die bereits manuell bearbeitete Massnahmen haben
            if (!string.IsNullOrWhiteSpace(existingMeasures))
            {
                var meta = record.FieldMeta.GetValueOrDefault("Empfohlene_Sanierungsmassnahmen");
                if (meta is not null && meta.UserEdited)
                {
                    skipped++;
                    continue;
                }
            }

            // Nur Records mit Sanierungsbedarf oder Schadenscodes verarbeiten
            if (!string.Equals(pruefung, "Sanierungsbedarf", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(pruefung, "beobachten", StringComparison.OrdinalIgnoreCase)
                && !hasDamageCodes)
            {
                skipped++;
                continue;
            }

            var recommendation = _measureRecommendationService.Recommend(record, maxSuggestions: 5);
            if (recommendation.Measures.Count == 0)
            {
                noSuggestion++;
                continue;
            }

            var value = string.Join(Environment.NewLine, recommendation.Measures);
            record.SetFieldValue("Empfohlene_Sanierungsmassnahmen", value, FieldSource.Unknown, userEdited: false);
            foreach (var suggestion in recommendation.Measures)
                AddOptionIfMissing(EmpfohleneSanierungsmassnahmenOptions, suggestion);

            if (recommendation.EstimatedTotalCost is not null)
                record.SetFieldValue("Kosten", recommendation.EstimatedTotalCost.Value.ToString("0.00", CultureInfo.InvariantCulture), FieldSource.Unknown, userEdited: false);
            if (recommendation.RenovierungInlinerM is not null)
                record.SetFieldValue("Renovierung_Inliner_m", FormatDecimal(recommendation.RenovierungInlinerM.Value), FieldSource.Unknown, userEdited: false);
            if (recommendation.RenovierungInlinerStk is not null)
                record.SetFieldValue("Renovierung_Inliner_Stk", FormatInt(recommendation.RenovierungInlinerStk.Value), FieldSource.Unknown, userEdited: false);
            if (recommendation.AnschluesseVerpressen is not null)
                record.SetFieldValue("Anschluesse_verpressen", FormatInt(recommendation.AnschluesseVerpressen.Value), FieldSource.Unknown, userEdited: false);
            if (recommendation.ReparaturManschette is not null)
                record.SetFieldValue("Reparatur_Manschette", FormatInt(recommendation.ReparaturManschette.Value), FieldSource.Unknown, userEdited: false);
            if (recommendation.ReparaturKurzliner is not null)
                record.SetFieldValue("Reparatur_Kurzliner", FormatInt(recommendation.ReparaturKurzliner.Value), FieldSource.Unknown, userEdited: false);

            filled++;
        }

        if (filled > 0)
        {
            _shell.Project.ModifiedAtUtc = DateTime.UtcNow;
            _shell.Project.Dirty = true;
        }

        _shell.SetStatus($"Massnahmen: {filled} Haltungen befuellt, {skipped} uebersprungen, {noSuggestion} ohne Vorschlag");
    }

    private void OpenSanierungOptimizationWindow(HaltungRecord? record)
    {
        OpenSanierungsmassnahmenWindow(record, InitialFocusMode.AiOptimization);
    }

    private void OpenSanierungsmassnahmenWindow(HaltungRecord? record, InitialFocusMode focus)
    {
        record ??= Selected;
        if (record is null) return;

        var holding = (record.GetFieldValue("Haltungsname") ?? "").Trim();
        if (string.IsNullOrWhiteSpace(holding))
        {
            _dialogs.ShowMessage("Haltungsname fehlt in der Zeile.", "Sanierungsmassnahmen",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Build CostCalculatorViewModel
        var recommended = ParseRecommendedTemplates(record.GetFieldValue("Empfohlene_Sanierungsmassnahmen"));
        var costCalcVm = new CostCalculatorViewModel(
            holding,
            null,
            recommended,
            App.Resolve<AppSettings>().LastProjectPath,
            cost => ApplyCostsToRecord(record, cost),
            haltungRecord: record,
            projectRecords: Records);

        // Build SanierungOptimizationViewModel (nullable when AI disabled)
        SanierungOptimizationViewModel? optimizationVm = null;
        var cfg = AuswertungPro.Next.Application.Ai.AiRuntimeConfigProvider.Load();
        if (cfg.Enabled)
        {
            var ruleResult = _measureRecommendationService.Recommend(record, maxSuggestions: 5);
            RuleRecommendationDto? ruleDto = null;
            if (ruleResult.Measures.Count > 0)
            {
                ruleDto = new RuleRecommendationDto
                {
                    Measures         = ruleResult.Measures,
                    EstimatedCost    = ruleResult.EstimatedTotalCost,
                    UsedTrainedModel = ruleResult.UsedTrainedModel
                };
            }

            // Phase 5.1.B Etappe 3.L: Direkt new() statt Bundle-Methode.
            var aiService = new AuswertungPro.Next.Infrastructure.Ai.Sanierung.AiSanierungOptimizationService(cfg);
            optimizationVm = new SanierungOptimizationViewModel(record, aiService, ruleDto);

            optimizationVm.TransferredToPrimary += _ =>
            {
                _shell.Project.ModifiedAtUtc = DateTime.UtcNow;
                _shell.Project.Dirty         = true;
                RefreshRecordInGrid(record);
                ScheduleAutoSave();
                _shell.SetStatus($"KI-Sanierungsvorschlag uebertragen: {holding}");
            };
        }

        var vm = new SanierungsmassnahmenViewModel(costCalcVm, optimizationVm, record, focus);
        var win = new SanierungsmassnahmenWindow(vm)
        {
            Owner = System.Windows.Application.Current?.MainWindow
        };
        App.Resolve<IDialogService>().ShowDialog(win);
    }
}
