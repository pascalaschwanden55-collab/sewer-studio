using System;
using AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using AuswertungPro.Next.Application.Ai.KnowledgeBase;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.KnowledgeBase;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Modules;

/// <summary>
/// Phase 5.2.F: KnowledgeBase-/Retrieval-/BrainMirror-Setup aus ServiceProvider extrahiert.
///
/// Verantwortlich fuer:
///   1. KB-HttpClient + KnowledgeBaseContext + EmbeddingService + RetrievalService
///      (mit Modell-Konsistenz-Pruefung).
///   2. BrainMirror — Spiegel der KI-Lerndaten auf E:\Brain (oder konfiguriertes Laufwerk).
///
/// HttpClient wird zurueckgeliefert, damit ServiceProvider ihn fuer
/// Dispose-Cleanup festhalten kann (Audit B9 / Phase 0.2).
/// </summary>
internal static class KnowledgeBaseModule
{
    public sealed record Services(HttpClient? KbHttp, IRetrievalService? Retrieval);

    /// <summary>
    /// Erzeugt KbHttp + Retrieval. Bei Init-Fehler wird der HttpClient sauber
    /// disposed und null zurueckgegeben (KI laeuft dann ohne KB-Kontext).
    /// </summary>
    public static Services ConfigureRetrieval(
        AiPlatformConfig aiPlatform,
        AppSettings settings,
        ILogger logger)
    {
        HttpClient? kbHttp = null;
        IRetrievalService? retrieval = null;

        try
        {
            var ollamaConfig = aiPlatform.ToOllamaConfig();
            kbHttp = new HttpClient { Timeout = ollamaConfig.RequestTimeout };
            var kbCtx = new KnowledgeBaseContext();
            var embedder = new EmbeddingService(kbHttp, ollamaConfig);
            var retrievalService = new RetrievalService(kbCtx, embedder, settings);
            retrievalService.CheckModelConsistency();
            if (retrievalService.HasModelMismatch)
                logger.LogWarning(
                    "KB-Embedding-Modell '{StoredModel}' stimmt nicht mit aktuellem Modell '{CurrentModel}' überein. KB-Rebuild empfohlen.",
                    retrievalService.StoredEmbedModel, ollamaConfig.EmbedModel);
            retrieval = retrievalService;
        }
        catch (Exception ex)
        {
            // Phase 0.2: HttpClient bei Init-Fehler explizit freigeben.
            kbHttp?.Dispose();
            kbHttp = null;
            logger.LogWarning(ex, "KnowledgeBase-Retrieval konnte nicht initialisiert werden. KI läuft ohne KB-Kontext.");
        }

        return new Services(kbHttp, retrieval);
    }

    /// <summary>
    /// Startet den BrainMirror-Service (spiegelt KI-Lerndaten auf konfiguriertes Laufwerk).
    /// Initialer Sync laeuft als Background-Task — blockiert nicht.
    ///
    /// Wenn das Ziel-Laufwerk nicht verfuegbar ist (z.B. externe Platte abgesteckt),
    /// wird BrainMirror still uebersprungen mit Warnung.
    /// </summary>
    public static void StartBrainMirror(AppSettings settings, ILogger logger)
    {
        var brainPath = settings.BrainMirrorPath ?? KnowledgeRoot.ResolveBrainMirrorPath();
        var brainDrive = Path.GetPathRoot(brainPath);
        if (brainDrive is null || !Directory.Exists(brainDrive))
        {
            logger.LogWarning("Brain-Mirror deaktiviert: Laufwerk {Drive} nicht verfuegbar", brainDrive);
            return;
        }

        _ = new KnowledgeMirrorService(KnowledgeRoot.GetRoot(), brainPath, logger);

        _ = Task.Run(async () =>
        {
            try
            {
                await KnowledgeMirrorService.Current!.SyncNowAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[BrainMirror] Initialer Sync fehlgeschlagen: {ex.Message}");
            }
        });
    }
}
