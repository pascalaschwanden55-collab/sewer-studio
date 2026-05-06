using System;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Vision;
using AuswertungPro.Next.Domain.Ai.Vision;
using AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;
using AuswertungPro.Next.Application.Ai.QualityGate;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels.Protocol;
using AuswertungPro.Next.Infrastructure.Ai.QualityGate;
using AuswertungPro.Next.Infrastructure.Ai.SelfImproving;

namespace AuswertungPro.Next.UI.Views.Windows;

// Phase 6.1.B: Feedback-Loop (Cluster B4) aus PlayerWindow.xaml.cs.
// Self-Improving-Pipeline: Positiv-/Negativ-Feedback wird in JSONL-Dateien
// und KB ValidationLog persistiert.
//
// Alle Methoden static und ohne UI-Zugriff — saubere Auslagerung.
// _feedbackHttpClient als static field bleibt geteilt mit dem Hauptpartial.
public partial class PlayerWindow
{
    // Shared HttpClient fuer Feedback-Ingest: vorher wurde bei JEDEM Accept/Reject
    // ein neuer HttpClient erzeugt und nicht disposed → 1-2 GB Socket-Pool-Leak
    // und TIME_WAIT-Port-Erschoepfung bei langen Codier-Sessions. 2 Minuten Timeout
    // deckt den Embedding-Call auf Ollama ab.
    private static readonly HttpClient _feedbackHttpClient =
        new() { Timeout = TimeSpan.FromMinutes(2) };

    /// <summary>Erzeugt FeedbackIngestionService mit optionalem KbManager fuer KB-Re-Indexierung.</summary>
    private static FeedbackIngestionService CreateFeedbackService(
        KnowledgeBaseContext db)
    {
        var logger = new ValidationLogger(db.Connection);
        var weights = new WeightLearningService(db.Connection);

        // KbManager optional - wenn Ollama offline, wird nur geloggt
        KnowledgeBaseManager? kbManager = null;
        try
        {
            var cfg = AuswertungPro.Next.Application.Ai.Ollama.OllamaConfigProvider.Load();
            var embedder = new EmbeddingService(_feedbackHttpClient, cfg);
            kbManager = new KnowledgeBaseManager(db, embedder);
        }
        catch { /* Ollama nicht verfuegbar - Feedback wird geloggt, KB-Update uebersprungen */ }

        return new FeedbackIngestionService(logger, weights, kbManager);
    }

    private static string ResolveFeedbackCode(string label, string? vsaCode)
    {
        if (!string.IsNullOrWhiteSpace(vsaCode))
            return vsaCode.Trim().ToUpperInvariant();

        var inferred = VsaCodeResolver.InferCodeFromLabel(label);
        if (!string.IsNullOrWhiteSpace(inferred))
            return inferred.Trim().ToUpperInvariant();

        return label?.Trim() ?? string.Empty;
    }

    private static MappedProtocolEntry BuildFeedbackMappedEntry(string label, string resolvedCode, double meter)
    {
        var safeMeter = double.IsFinite(meter) ? meter : 0d;
        var normalizedLabel = string.IsNullOrWhiteSpace(label) ? resolvedCode : label.Trim();
        var codeHint = string.IsNullOrWhiteSpace(resolvedCode) ? null : resolvedCode;

        var detection = new RawVideoDetection(
            FindingLabel: normalizedLabel,
            MeterStart: safeMeter,
            MeterEnd: safeMeter,
            Severity: "mid",
            VsaCodeHint: codeHint);

        return new MappedProtocolEntry(
            Detection: detection,
            SuggestedCode: codeHint,
            Confidence: 1.0,
            Reason: normalizedLabel,
            Warnings: Array.Empty<string>(),
            QualityGateResult: null,
            Uncertainty: null);
    }

    private static async Task IngestFeedbackAsync(
        string label,
        string? vsaCode,
        double meter,
        bool accepted)
    {
        try
        {
            var resolvedCode = ResolveFeedbackCode(label, vsaCode);
            var mapped = BuildFeedbackMappedEntry(label, resolvedCode, meter);
            var finalCode = accepted ? resolvedCode : string.Empty;

            using var db = new KnowledgeBaseContext();
            var feedback = CreateFeedbackService(db);
            await feedback.ProcessFeedbackAsync(mapped, finalCode, accepted).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Feedback] Ingestion-Fehler: {ex.Message}");
        }
    }

    // Schuetzt die JSONL-Append-Writes gegen parallele Task.Run-Calls (mehrere
    // User-Aktionen gleichzeitig wuerden sonst verschraenkte halb-geschriebene
    // Zeilen in die Feedback-Dateien schreiben).
    private static readonly SemaphoreSlim _positiveFeedbackLock = new(1, 1);
    private static readonly SemaphoreSlim _negativeFeedbackLock = new(1, 1);

    /// <summary>Positiv-Feedback: KI hat richtig erkannt.</summary>
    private static async Task SavePositiveFeedbackAsync(string label, string? vsaCode, double meter)
    {
        try
        {
            var feedbackPath = Path.Combine(
                KnowledgeRoot.GetRoot(), "positive_feedback.jsonl");

            var entry = new
            {
                timestamp = DateTime.UtcNow.ToString("o"),
                label,
                vsaCode = vsaCode ?? "",
                meter,
                action = "accepted_by_user"
            };

            var json = System.Text.Json.JsonSerializer.Serialize(entry);
            await _positiveFeedbackLock.WaitAsync().ConfigureAwait(false);
            try
            {
                await File.AppendAllTextAsync(feedbackPath, json + Environment.NewLine).ConfigureAwait(false);
            }
            finally
            {
                _positiveFeedbackLock.Release();
            }
            await IngestFeedbackAsync(label, vsaCode, meter, accepted: true).ConfigureAwait(false);
            KnowledgeMirrorService.Current?.NotifyChanged();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Feedback] Fehler: {ex.Message}");
        }
    }

    /// <summary>
    /// Speichert ein Negativ-Feedback in der KB (ValidationLog).
    /// Die KI lernt: bei diesem Label/Code war nichts - Fehlalarm.
    /// Wird asynchron im Hintergrund ausgefuehrt.
    /// </summary>
    private static async Task SaveNegativeFeedbackAsync(string label, string? vsaCode, double meter)
    {
        try
        {
            var feedbackPath = Path.Combine(
                KnowledgeRoot.GetRoot(), "negative_feedback.jsonl");

            var entry = new
            {
                timestamp = DateTime.UtcNow.ToString("o"),
                label,
                vsaCode = vsaCode ?? "",
                meter,
                action = "deleted_by_user",
                reason = "Fehlalarm - Benutzer hat Overlay geloescht"
            };

            var json = System.Text.Json.JsonSerializer.Serialize(entry);
            await _negativeFeedbackLock.WaitAsync().ConfigureAwait(false);
            try
            {
                await File.AppendAllTextAsync(feedbackPath, json + Environment.NewLine).ConfigureAwait(false);
            }
            finally
            {
                _negativeFeedbackLock.Release();
            }
            await IngestFeedbackAsync(label, vsaCode, meter, accepted: false).ConfigureAwait(false);
            KnowledgeMirrorService.Current?.NotifyChanged();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Feedback] Fehler beim Speichern: {ex.Message}");
        }
    }
}
