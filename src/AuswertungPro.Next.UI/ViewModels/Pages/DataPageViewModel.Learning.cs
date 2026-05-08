using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using AuswertungPro.Next.Application.Ai.Training;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

// Lernbasis-Anzeige + Trainings-Status: Ampel (rot/gelb/gruen) gemaess
// Anzahl Lernfaelle, Modell-Status-Dialog, Sync der Trained-Haltungen
// aus dem Training Center.
public sealed partial class DataPageViewModel
{
    private void ShowModelStatus()
    {
        var stats = _measureRecommendationService.GetStats();
        var status = stats.TrainedModelAvailable ? "Aktiv" : "Noch nicht trainiert";
        var trainedAt = stats.TrainedAtUtc is null
            ? "-"
            : stats.TrainedAtUtc.Value.ToLocalTime().ToString("dd.MM.yyyy HH:mm:ss", CultureInfo.InvariantCulture);
        var modelSamples = stats.TrainedModelSamples?.ToString(CultureInfo.InvariantCulture) ?? "0";

        var message =
            $"Lernfaelle gesamt: {stats.TotalSamples}\n" +
            $"Schadenscodes: {stats.DistinctDamageCodes}\n" +
            $"Code-Signaturen: {stats.CodeSignatures}\n" +
            $"KI-Modell: {status}\n" +
            $"Modell-Faelle: {modelSamples}\n" +
            $"Letztes Training: {trainedAt}\n" +
            $"Modell-Datei:\n{stats.ModelPath}";

        _dialogs.ShowMessage(message, "KI-Modell Status", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void UpdateLearningInfo(int? similarCases = null, decimal? estimatedCost = null)
    {
        var stats = _measureRecommendationService.GetStats();
        if (stats.TotalSamples <= 0)
        {
            LearningInfo = "Lernbasis: 0 Faelle";
            UpdateLearningTrafficLight(0);
            IsLearningInfoVisible = true;
            return;
        }

        var suffix = string.Empty;
        if (similarCases is not null && similarCases.Value > 0)
        {
            suffix = estimatedCost is null
                ? $" / letzte Schaetzung aus {similarCases.Value} aehnlichen Haltungen"
                : $" / letzte Kostenschaetzung {estimatedCost.Value:0.00} aus {similarCases.Value} aehnlichen Haltungen";
        }

        var modelText = stats.TrainedModelAvailable
            ? $" / KI-Modell aktiv ({stats.TrainedModelSamples ?? 0} Faelle)"
            : $" / KI-Modell ab {MinimumSamplesForModelTraining} Faellen";

        LearningInfo = $"Lernbasis: {stats.TotalSamples} Faelle{suffix}{modelText}";
        UpdateLearningTrafficLight(stats.TotalSamples);
        IsLearningInfoVisible = true;
    }

    private void UpdateLearningTrafficLight(int totalSamples)
    {
        if (totalSamples >= StrongModelThreshold)
        {
            LearningTrafficLightColor = "#2E7D32";
            LearningTrafficLightText = "Gruen";
            return;
        }

        if (totalSamples >= MinimumSamplesForModelTraining)
        {
            LearningTrafficLightColor = "#F9A825";
            LearningTrafficLightText = "Gelb";
            return;
        }

        LearningTrafficLightColor = "#C62828";
        LearningTrafficLightText = "Rot";
    }

    /// <summary>
    /// Lädt die CaseIds aus dem Training Center und normalisiert sie zu Haltungsnamen.
    /// </summary>
    private async Task LoadTrainedHaltungenAsync()
    {
        try
        {
            var store = new TrainingCenterStore();
            var state = await store.LoadAsync();
            TrainedHaltungen.Clear();
            foreach (var tc in state.Cases)
            {
                var name = NormalizeTrainingCaseId(tc.CaseId);
                if (!string.IsNullOrWhiteSpace(name))
                    TrainedHaltungen.Add(name);
            }
        }
        catch
        {
            // Training-Daten nicht verfügbar – kein Fehler
        }
    }

    /// <summary>
    /// Normalisiert eine Training-CaseId zu einem Haltungsnamen.
    /// Entfernt Datums-Prefixe wie "20250602_" und Knoten-Prefixe wie "07.", "10.".
    /// </summary>
    private static string NormalizeTrainingCaseId(string caseId)
    {
        var v = (caseId ?? "").Trim();
        // Datums-Prefix entfernen (z.B. "20250602_06.24341-35625" → "06.24341-35625")
        v = Regex.Replace(v, @"^\d{8}_", "");
        return v;
    }

    /// <summary>
    /// Prüft ob eine Haltung im Training Center erfasst ist.
    /// </summary>
    public bool IsTrainedCase(string? haltungsname)
    {
        if (string.IsNullOrWhiteSpace(haltungsname) || TrainedHaltungen.Count == 0)
            return false;
        // Exakter Match
        if (TrainedHaltungen.Contains(haltungsname))
            return true;
        // Ohne Knoten-Prefixe vergleichen (z.B. "07.1028055" → "1028055")
        var stripped = StripNodePrefixes(haltungsname);
        foreach (var trained in TrainedHaltungen)
        {
            if (string.Equals(StripNodePrefixes(trained), stripped, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static readonly Regex NodePrefixRx = new(@"^\d{1,2}\.", RegexOptions.Compiled);

    private static string StripNodePrefixes(string holdingKey)
    {
        var dashIdx = holdingKey.IndexOf('-');
        if (dashIdx < 0)
            return NodePrefixRx.Replace(holdingKey, "");
        var left = holdingKey[..dashIdx];
        var right = holdingKey[(dashIdx + 1)..];
        return $"{NodePrefixRx.Replace(left, "")}-{NodePrefixRx.Replace(right, "")}";
    }
}
