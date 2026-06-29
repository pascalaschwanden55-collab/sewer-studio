using System;
using System.Collections.Generic;
using System.Linq;

namespace AuswertungPro.Next.Application.Ai.Startup;

// -----------------------------------------------------------------------
// Baut den Startplan fuer den KI-Startvorgang.
// Enthaelt reine Logik ohne UI-Abhaengigkeiten:
//   - ApplyRuntimeDefaults        : Einstellungen normalisieren/aktivieren
//   - BuildOllamaPreloadRequests  : Preload-Liste aus Platform-Settings bauen
//   - BuildModelLabel             : Bezeichnung fuer den Statusbalken
// -----------------------------------------------------------------------

public static class AiStartupPlanBuilder
{
    public const string DefaultOllamaUrl = "http://localhost:11434";
    public const string DefaultSidecarUrl = "http://localhost:8100";

    /// <summary>
    /// Setzt fehlende Pflichteinstellungen auf sinnvolle Standardwerte und
    /// aktiviert KI/Multi-Model, falls noch nicht aktiv.
    /// Gibt true zurueck, wenn mindestens eine Einstellung veraendert wurde.
    /// </summary>
    public static bool ApplyRuntimeDefaults(IAiStartupSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var changed = false;

        if (settings.AiEnabled != true)
        {
            settings.AiEnabled = true;
            changed = true;
        }

        if (settings.PipelineMultiModelEnabled != true)
        {
            settings.PipelineMultiModelEnabled = true;
            changed = true;
        }

        if (!string.Equals(settings.PipelineMode, "multimodel", StringComparison.OrdinalIgnoreCase))
        {
            settings.PipelineMode = "multimodel";
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(settings.AiOllamaUrl))
        {
            settings.AiOllamaUrl = DefaultOllamaUrl;
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(settings.PipelineSidecarUrl))
        {
            settings.PipelineSidecarUrl = DefaultSidecarUrl;
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(settings.AiOllamaKeepAlive))
        {
            settings.AiOllamaKeepAlive = "24h";
            changed = true;
        }

        return changed;
    }

    /// <summary>
    /// Erstellt die Liste der Ollama-Modelle, die beim Start vorab geladen werden sollen.
    /// Duplikate (gleicher Modellname, case-insensitive) werden entfernt.
    /// </summary>
    public static IReadOnlyList<AiStartupModelPreloadRequest> BuildOllamaPreloadRequests(
        string? visionModel,
        string? textModel,
        string? embedModel,
        string keepAlive)
    {
        var requests = new List<AiStartupModelPreloadRequest>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        Add(visionModel, AiStartupModelKind.Generate);
        Add(textModel, AiStartupModelKind.Generate);
        Add(embedModel, AiStartupModelKind.Embed);

        return requests;

        void Add(string? modelName, AiStartupModelKind kind)
        {
            if (string.IsNullOrWhiteSpace(modelName))
                return;

            var trimmed = modelName.Trim();
            if (!seen.Add(trimmed))
                return;

            requests.Add(new AiStartupModelPreloadRequest(trimmed, kind, keepAlive));
        }
    }

    /// <summary>
    /// Erstellt die Modell-Bezeichnung fuer den Statusbalken.
    /// Beispiel: "qwen3-vl:8b-q8 + Sidecar"
    /// </summary>
    public static string BuildModelLabel(IEnumerable<string> modelNames, bool multiModelEnabled)
    {
        var models = modelNames
            .Where(m => !string.IsNullOrWhiteSpace(m))
            .Select(m => m.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var label = models.Length == 0 ? "Qwen-VL" : string.Join(" + ", models);
        return multiModelEnabled ? $"{label} + Sidecar" : label;
    }
}

/// <summary>
/// Minimales Interface fuer die Laufzeit-Standardisierung der KI-Einstellungen.
/// Damit kann AiStartupPlanBuilder.ApplyRuntimeDefaults ohne Direktabhaengigkeit
/// auf AppSettings (UI-Layer) getestet werden.
/// </summary>
public interface IAiStartupSettings
{
    bool? AiEnabled { get; set; }
    bool? PipelineMultiModelEnabled { get; set; }
    string? PipelineMode { get; set; }
    string? AiOllamaUrl { get; set; }
    string? PipelineSidecarUrl { get; set; }
    string? AiOllamaKeepAlive { get; set; }
}
