using System;
using System.Linq;

using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Domain.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.Training;
using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.ViewModels.Windows;

// TrainingCenterViewModel Pipeline-Visualisierung: SelfTraining-Step-Tracker
// (Pipeline-Schritt 0..5), Match-Rate (Exact/Partial/Mismatch/NoFindings),
// Code-Distribution-Update, Live-Log + Reset-Visuals. Aus dem Hauptdatei
// extrahiert (Slice 13c). Felder _totalExact/_totalPartial/_totalMismatch/
// _totalNoFindings bleiben in der Hauptdatei.
public partial class TrainingCenterViewModel
{
    private void RefreshMatchRatePercents()
    {
        var total = _totalExact + _totalPartial + _totalMismatch + _totalNoFindings;
        if (total == 0) { ExactPercent = PartialPercent = MismatchPercent = NoFindingsPercent = 0; return; }
        ExactPercent = (double)_totalExact / total;
        PartialPercent = (double)_totalPartial / total;
        MismatchPercent = (double)_totalMismatch / total;
        NoFindingsPercent = (double)_totalNoFindings / total;
    }

    private void AddSelfTrainingLog(string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] {message}";
        void Apply()
        {
            SelfTrainingLogEntries.Add(line);
            while (SelfTrainingLogEntries.Count > 100)
                SelfTrainingLogEntries.RemoveAt(0);
            // TextBox-Binding aktualisieren (weisse Schrift auf dunkel)
            EchtzeitLogText = string.Join("\n", SelfTrainingLogEntries);
        }
        if (System.Windows.Application.Current?.Dispatcher is { } d && !d.CheckAccess())
            d.Invoke(Apply);
        else
            Apply();
    }

    private void UpdateCodeDistribution(string code, MatchLevel level)
    {
        void Apply()
        {
            var entry = CodeDistribution.FirstOrDefault(e => e.Code == code);
            if (entry is null)
            {
                entry = new CodeDistributionEntry { Code = code };
                CodeDistribution.Add(entry);
            }
            entry.Total++;
            switch (level)
            {
                case MatchLevel.ExactMatch: entry.Exact++; break;
                case MatchLevel.PartialMatch: entry.Partial++; break;
                case MatchLevel.Mismatch: entry.Mismatch++; break;
                case MatchLevel.NoFindings: entry.NoFindings++; break;
            }
        }
        if (System.Windows.Application.Current?.Dispatcher is { } d && !d.CheckAccess())
            d.Invoke(Apply);
        else
            Apply();
    }

    /// <summary>Wird vom SelfTrainingOrchestrator bei jedem Schritt aufgerufen.</summary>
    public void OnSelfTrainingStep(SelfTrainingStep step)
    {
        void Apply()
        {
            PipelineActiveStep = (int)step.Stage;
            CurrentEntryCode = step.VsaCode;
            CurrentEntryMeter = step.MeterPosition;
            ProgressValue = step.EntryIndex + 1;
            ProgressMax = step.TotalEntries;

            // Aktives Modell je Stage anzeigen
            (ActiveModelName, IsModelActive) = step.Stage switch
            {
                SelfTrainingStage.BuildingTimeline => ("PdfPig (CPU)", true),
                SelfTrainingStage.ExtractingFrame  => ("ffmpeg (CPU)", true),
                SelfTrainingStage.Analyzing        => ($"{_activeVisionModel} (GPU)", true),
                SelfTrainingStage.Comparing        => ("Deterministisch (CPU)", true),
                SelfTrainingStage.AssessingTechnique => ($"{_activeVisionModel} (GPU)", true),
                SelfTrainingStage.Completed        => ("", false),
                _ => ("", false)
            };

            // Stage-spezifisches Logging
            switch (step.Stage)
            {
                case SelfTrainingStage.BuildingTimeline:
                    if (step.ErrorMessage is not null)
                        AddSelfTrainingLog(step.ErrorMessage);
                    break;
                case SelfTrainingStage.ExtractingFrame:
                    AddSelfTrainingLog($"Frame extrahieren: {step.VsaCode} @ {step.MeterPosition:F1}m");
                    if (step.FramePath is not null) SetLiveFrameThrottled(step.FramePath);
                    break;
                case SelfTrainingStage.Analyzing:
                    AddSelfTrainingLog($"KI-Analyse [{_activeVisionModel}]: {step.VsaCode}");
                    break;
                case SelfTrainingStage.Comparing:
                    AddSelfTrainingLog($"Vergleich: {step.VsaCode}");
                    break;
                case SelfTrainingStage.AssessingTechnique:
                    if (step.Technique is { } tech)
                    {
                        CurrentTechniqueGrade = tech.OverallGrade;
                        CurrentTechniqueDetails = $"Licht: {tech.LightingQuality} | Schaerfe: {tech.SharpnessQuality}";
                        AddSelfTrainingLog($"Technik: {tech.OverallGrade} (Licht={tech.LightingQuality}, Schaerfe={tech.SharpnessQuality})");
                    }
                    break;
                case SelfTrainingStage.Completed:
                    if (step.Comparison is { } cmp)
                    {
                        CurrentComparisonText = $"{cmp.Level} ({cmp.ConfidenceScore:P0})";
                        var levelStr = cmp.Level switch
                        {
                            MatchLevel.ExactMatch => "EXACT",
                            MatchLevel.PartialMatch => "PARTIAL",
                            MatchLevel.Mismatch => "MISMATCH",
                            _ => "NO_FINDINGS"
                        };
                        AddSelfTrainingLog($"Ergebnis: {step.VsaCode} → {levelStr} ({cmp.ConfidenceScore:P0}) {cmp.Explanation}");

                        // Zaehler aktualisieren
                        switch (cmp.Level)
                        {
                            case MatchLevel.ExactMatch: _totalExact++; break;
                            case MatchLevel.PartialMatch: _totalPartial++; break;
                            case MatchLevel.Mismatch: _totalMismatch++; break;
                            case MatchLevel.NoFindings: _totalNoFindings++; break;
                        }
                        RefreshMatchRatePercents();

                        // Ergebnis-Eintrag hinzufuegen
                        SelfTrainingResults.Add(new SelfTrainingEntryResult
                        {
                            Index = step.EntryIndex + 1,
                            VsaCode = step.VsaCode,
                            Meter = step.MeterPosition,
                            Level = cmp.Level,
                            Summary = cmp.Explanation
                        });

                        UpdateCodeDistribution(step.VsaCode, cmp.Level);
                    }
                    break;
            }

            if (step.ErrorMessage is not null)
                AddSelfTrainingLog($"FEHLER: {step.ErrorMessage}");
        }

        if (System.Windows.Application.Current?.Dispatcher is { } d && !d.CheckAccess())
            d.Invoke(Apply);
        else
            Apply();
    }

    /// <summary>Setzt alle Selbsttraining-Visualisierungen zurueck.</summary>
    /// <param name="resetMatchRate">Match-Rate auf 0 setzen (nur bei echtem Selbsttraining, nicht bei Batch-Import).</param>
    private void ResetSelfTrainingVisuals(bool resetMatchRate = false)
    {
        SelfTrainingResults.Clear();
        // CodeDistribution NICHT leeren — Gesamtstand wird beibehalten
        // und im Lauf inkrementell erweitert
        SelfTrainingLogEntries.Clear();
        PipelineActiveStep = 0;
        CurrentEntryCode = "";
        CurrentEntryMeter = 0;
        CurrentComparisonText = "";
        CurrentTechniqueGrade = "";
        CurrentTechniqueDetails = "";
        if (resetMatchRate)
        {
            _totalExact = _totalPartial = _totalMismatch = _totalNoFindings = 0;
            RefreshMatchRatePercents();
        }
    }
}
