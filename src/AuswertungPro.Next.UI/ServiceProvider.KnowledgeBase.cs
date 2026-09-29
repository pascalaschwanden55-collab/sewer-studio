using System.IO;
using System.Net.Http;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.KnowledgeBase;
using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;
using AuswertungPro.Next.Infrastructure.Ai.Ollama;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI;

public sealed partial class ServiceProvider
{
    /// <summary>
    /// Baut Retrieval, Diagnose und die Startwarnungen der Wissensdatenbank.
    /// Unveraendert aus dem Konstruktor ausgelagert, damit ServiceProvider.cs unter der
    /// 1000-Zeilen-Grenze bleibt (19.09.2026). Reihenfolge und Verhalten sind gleich.
    /// </summary>
    private IRetrievalService? InitialisiereWissensdatenbank(
        AppSettings settings,
        AiPlatformSettings aiPlatform,
        KnowledgeBasePaths.RootResolution knowledgeResolution,
        string? knowledgeConfigurationWarning)
    {
            // AP-06: Zustand der Wissensdatenbank VOR der Init erfassen (existiert die DB-Datei,
            // bevor der Context sie ggf. neu/leer anlegt?). Schuetzt gegen stillen Split-Brain,
            // wenn die Umgebungsvariable SEWERSTUDIO_KNOWLEDGE_ROOT verloren geht.
            var knowledgeHealth = KnowledgeBaseHealth.Inspect(KnowledgeDbPath);
            var knowledgeDbExisted = knowledgeHealth.DatabaseExists;
            var knowledgeSampleCount = 0;
            var knowledgeSampleCountRead = false;

            RetrievalService? retrieval = null;
            string? evalSchutzFehler = null;
            try
            {
                // Auditbefund 11: Der Pruefdaten-Root gilt fuer JEDE spaeter erzeugte Suche — auch fuer
                // die Vollprotokoll-Erstellung, die ihre eigene aufbaut. Audit A10: VOR der Pruefung setzen,
                // damit auch deren Suche an einem fehlenden Ordner scheitert statt ohne Sperrliste zu laufen.
                GuardedRetrievalFactory.ConfigureDefaultEvalSetRoot(settings.EvalSetRoot);

                if (!knowledgeHealth.IsHealthy)
                    throw new InvalidDataException(knowledgeHealth.Error ?? "SQLite quick_check fehlgeschlagen.");

                // Audit Fix #6a: Eval-Haltungs-Sperrliste auch leseseitig anwenden (Defense-in-Depth,
                // gleiche Quelle wie der Schreib-Guard) -> kontaminierte Samples kommen nie als Few-Shot.
                // Audit A10 (23.09.2026): streng — ein fehlender oder unlesbarer Pruefdaten-Ordner ist
                // ein Fehler. Dann keine Suche (ohne Vergleichswissen) und eine sichtbare Meldung.
                IReadOnlySet<string> evalHaltungKeys;
                try
                {
                    evalHaltungKeys = GuardedRetrievalFactory.Sperrliste(settings.EvalSetRoot);
                }
                catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
                {
                    evalSchutzFehler =
                        "Die Sperrliste der reservierten Prüfhaltungen fehlt. Die KI arbeitet vorerst ohne Vergleichswissen.\n" +
                        $"{ex.Message}\n" +
                        "Prüfdaten-Ordner in den Einstellungen prüfen (leer lassen schaltet den Schutz bewusst ab).";
                    throw;
                }

                var ollamaConfig = aiPlatform.ToOllamaConfig();
                var kbHttp = new HttpClient { Timeout = ollamaConfig.RequestTimeout };
                var kbCtx = new KnowledgeBaseContext(KnowledgeDbPath);
                var embedder = new EmbeddingService(kbHttp, ollamaConfig);
                retrieval = GuardedRetrievalFactory.Create(kbCtx, embedder, evalHaltungKeys);
                retrieval.CheckModelConsistency();
                if (retrieval.HasModelMismatch)
                    Logger.LogWarning(
                        "KB-Embedding-Modell '{StoredModel}' stimmt nicht mit aktuellem Modell '{CurrentModel}' überein. KB-Rebuild empfohlen.",
                        retrieval.StoredEmbedModel, ollamaConfig.EmbedModel);

                // AP-06: aktuelle Sample-Zahl fuer die Abweichungs-Warnung (best-effort).
                try
                {
                    knowledgeSampleCount = new KnowledgeBaseDiagnosticsService(kbCtx).ReadSummary(topCodes: 1).SampleCount;
                    knowledgeSampleCountRead = true;
                }
                catch { /* Sample-Zahl ist optional; 0 bleibt gueltig fuer die Pruefung. */ }
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "KnowledgeBase-Retrieval konnte nicht initialisiert werden. KI läuft ohne KB-Kontext.");
            }

            // AP-06: Warnen, wenn die App unbemerkt mit einer anderen oder leeren Wissensdatenbank laeuft.
            var knowledgeRootGuard = KnowledgeRootGuard.Evaluate(
                KnowledgeRoot,
                settings.LastKnownKnowledgeRoot,
                knowledgeDbExisted,
                knowledgeSampleCount,
                settings.LastKnownKnowledgeSampleCount);
            if (!knowledgeHealth.IsHealthy)
            {
                KnowledgeRootStartupWarning =
                    "Die Wissensdatenbank ist beschädigt oder nicht lesbar. Die App arbeitet vorerst ohne KB-Kontext.\n" +
                    $"Datei: {KnowledgeDbPath}\n" +
                    $"Fehler: {knowledgeHealth.Error}\n" +
                    "Bitte stelle die Datei aus einer Datensicherung wieder her.";
                Logger.LogError("Wissensdatenbank-Integritaetspruefung fehlgeschlagen: {Error}", knowledgeHealth.Error);
            }
            else if (knowledgeRootGuard.HatWarnung)
            {
                KnowledgeRootStartupWarning = knowledgeRootGuard.Meldung;
                Logger.LogWarning("Wissensdatenbank-Startwarnung ({Art}): {Meldung}",
                    knowledgeRootGuard.Art, knowledgeRootGuard.Meldung);
            }
            else if (knowledgeConfigurationWarning is not null)
            {
                KnowledgeRootStartupWarning = knowledgeConfigurationWarning;
                Logger.LogWarning("Wissensdatenbank-Pfadabweichung: {Meldung}", knowledgeConfigurationWarning);
            }
            if (evalSchutzFehler is not null)
            {
                KnowledgeRootStartupWarning = KnowledgeRootStartupWarning is null
                    ? evalSchutzFehler
                    : KnowledgeRootStartupWarning + "\n\n" + evalSchutzFehler;
                Logger.LogWarning("Pruefdaten-Sperrliste nicht geladen: {Meldung}", evalSchutzFehler);
            }
            settings.RecordKnowledgeRootStart(
                KnowledgeRoot,
                knowledgeSampleCountRead ? knowledgeSampleCount : null,
                knowledgeResolution.Source);
            settings.SaveImmediate();

            return retrieval;
    }
}
