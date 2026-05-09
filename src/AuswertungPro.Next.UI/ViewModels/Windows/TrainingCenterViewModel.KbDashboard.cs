using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Domain.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;
using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.ViewModels.Windows;

// TrainingCenterViewModel KB-Dashboard: KB-Status (Sample-Counts, Embedding-
// Coverage, Top-Codes), KB-Qualitaet (Coverage-Luecken, Accuracy aus
// ValidationLog, Stale Samples, Selbsttraining-Trend). Aus dem Hauptdatei
// extrahiert (Slice 13a).
public partial class TrainingCenterViewModel
{
    private async Task RefreshKbStatusAsync()
    {
        try
        {
            var (summary, totalDistinctCodes, errorCount, newCount, codeCounts) = await Task.Run(() =>
            {
                using var db = new KnowledgeBaseContext();
                var diag = new KnowledgeBaseDiagnosticsService(db);
                var s = diag.ReadSummary(20);
                var allCodes = diag.ReadAllCodeCounts().Count;

                // Sample-Statistik aus JSON fuer Diagnose-Anzeige
                int errors = 0, news = 0;
                Dictionary<string, int> codeCounts = new();
                try
                {
                    var samples = TrainingSamplesStore.LoadAsync().GetAwaiter().GetResult();
                    foreach (var sample in samples)
                    {
                        if (sample.KbIndexState == KbIndexState.Error) errors++;
                        else if (sample.Status == TrainingSampleStatus.New) news++;

                        // Code-Verteilung aus allen Samples (Gesamtstand)
                        if (!string.IsNullOrEmpty(sample.Code))
                        {
                            if (!codeCounts.TryGetValue(sample.Code, out var cnt))
                                codeCounts[sample.Code] = 1;
                            else
                                codeCounts[sample.Code] = cnt + 1;
                        }
                    }
                }
                catch { /* optional */ }

                return (s, allCodes, errors, news, codeCounts);
            });

            void Apply()
            {
                KbSampleCount = summary.SampleCount;
                KbErrorCount = errorCount;
                KbNewCount = newCount;
                KbEmbeddingCount = summary.EmbeddingCount;
                KbCodesCovered = totalDistinctCodes;
                KbLastUpdate = summary.LatestVersionAtUtc?.ToLocalTime().ToString("dd.MM.yyyy HH:mm") ?? "\u2014";

                static System.Windows.Media.SolidColorBrush Rgb(byte r, byte g, byte b)
                    => new(System.Windows.Media.Color.FromRgb(r, g, b));

                (KbReadinessLabel, KbReadinessBrush) = summary.SampleCount switch
                {
                    >= 100 => ("KI-Modell einsatzbereit", Rgb(0x4A, 0xDE, 0x80)),
                    >= 25  => ("Lernbasis grundlegend",   Rgb(0xFA, 0xCC, 0x15)),
                    > 0    => ("Lernbasis unzureichend",  Rgb(0xF8, 0x71, 0x71)),
                    _      => ("Keine Trainingsdaten",    Rgb(0x94, 0xA3, 0xB8))
                };

                KbTopCodesText = string.Join("\n", summary.TopCodes
                    .Select(c => $"{c.VsaCode}: {c.Count} Samples"));

                // Code-Verteilung aus Gesamtstand befuellen (wenn leer)
                if (CodeDistribution.Count == 0 && codeCounts.Count > 0)
                {
                    foreach (var (code, count) in codeCounts.OrderByDescending(kv => kv.Value))
                    {
                        CodeDistribution.Add(new CodeDistributionEntry
                        {
                            Code = code,
                            Total = count
                        });
                    }
                }
            }

            if (System.Windows.Application.Current?.Dispatcher is { } d && !d.CheckAccess())
                d.Invoke(Apply);
            else
                Apply();

            // KB-Qualitaet ebenfalls aktualisieren
            await RefreshKbQualityAsync();
        }
        catch
        {
            // KB might not exist yet — silently ignore
        }
    }

    /// <summary>
    /// Laedt KB-Qualitaetsmetriken: Coverage-Luecken, Accuracy, Stale Samples, Trend.
    /// Eigener KnowledgeBaseContext (unabhaengig von RefreshKbStatusAsync).
    /// </summary>
    private async Task RefreshKbQualityAsync()
    {
        try
        {
            var (gaps, gapCount, accuracy, stale) = await Task.Run(() =>
            {
                // Leere KB abfangen: DB existiert evtl. noch nicht
                var dbPath = KnowledgeBaseContext.DefaultDbPath;
                if (!System.IO.File.Exists(dbPath))
                    return ("KB noch nicht erstellt", 0, "Noch keine Validierungsdaten", 0);

                using var db = new KnowledgeBaseContext();
                var diag = new KnowledgeBaseDiagnosticsService(db);

                // Coverage: ALLE Codes abfragen, nicht nur Top-N
                var allCodes = diag.ReadAllCodeCounts();
                var underRep = allCodes.Where(c => c.Count < 3).ToList();
                var gapsText = allCodes.Count == 0
                    ? "KB leer — noch keine Samples indexiert"
                    : underRep.Count > 0
                        ? string.Join("\n", underRep.Select(c => $"{c.VsaCode}: {c.Count} Samples"))
                        : "Keine Luecken (alle Codes >= 3 Samples)";

                // Accuracy (aus ValidationLog)
                string accText;
                try
                {
                    var accSvc = new AuswertungPro.Next.Infrastructure.Ai.Monitoring.AccuracyDashboardService(db.Connection);
                    var metrics = accSvc.ComputeMetrics();
                    accText = metrics.Count > 0
                        ? string.Join("\n", metrics
                            .OrderByDescending(m => m.TruePositives + m.FalsePositives + m.FalseNegatives)
                            .Take(8)
                            .Select(m =>
                                $"{m.VsaCode}: F1={m.F1Score:F2}  P={m.Precision:F2}  R={m.Recall:F2}  (n={m.TruePositives + m.FalsePositives + m.FalseNegatives})"))
                        : "Noch keine Validierungsdaten";
                }
                catch { accText = "Validierungsdaten nicht verfuegbar"; }

                // Stale Samples — STAB-H6 (Audit 2026-04-23): SQLite-Lock- oder
                // Schema-Fehler aus FindStaleCandidates ist diagnostisch wertvoll
                // (verraet KB-Korruption) und darf nicht stumm verschluckt werden.
                int staleCount = 0;
                try
                {
                    var kbq = new AuswertungPro.Next.Infrastructure.Ai.SelfImproving.KbQualityService(db.Connection);
                    staleCount = kbq.FindStaleCandidates().Count;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[KbQuality] FindStaleCandidates fehlgeschlagen: {ex.GetType().Name}: {ex.Message}");
                }

                return (gapsText, underRep.Count, accText, staleCount);
            });

            // Trend (aus JSON, kein DB-Zugriff)
            var runs = await AuswertungPro.Next.Application.Ai.Training.SelfTrainingHistoryStore.LoadAsync();
            var last5 = runs.TakeLast(5).ToList();
            var trendText = last5.Count > 0
                ? string.Join("\n", last5.Select(r =>
                    $"{r.TimestampUtc.ToLocalTime():dd.MM. HH:mm} — " +
                    $"Exact: {r.ExactPercent:P0} | Partial: {r.PartialPercent:P0} | " +
                    $"Miss: {r.MismatchPercent:P0} | Leer: {r.NoFindingsPercent:P0}"))
                : "Noch keine Selbsttraining-Laeufe";

            var direction = "";
            if (last5.Count >= 2)
            {
                var delta = last5[^1].ExactPercent - last5[^2].ExactPercent;
                direction = delta > 0.02 ? "\u2191" : delta < -0.02 ? "\u2193" : "\u2192";
            }

            void Apply()
            {
                KbCoverageGapsText = gaps;
                KbCoverageGapsCount = gapCount;
                KbAccuracyText = accuracy;
                KbStaleSampleCount = stale;
                KbTrendText = trendText;
                KbTrendDirection = direction;

                // Stale-Sample Warnung im Log (E1)
                if (stale > 0)
                    Log($"KB-Qualitaet: {stale} veraltete Samples erkannt (manuell pruefen im Tab 'Samples')");
            }
            if (System.Windows.Application.Current?.Dispatcher is { } d && !d.CheckAccess())
                d.Invoke(Apply);
            else
                Apply();
        }
        catch { /* KB evtl. noch nicht vorhanden */ }
    }
}
