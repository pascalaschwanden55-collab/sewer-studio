using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AuswertungPro.Next.Application.Ai.Startup;

// -----------------------------------------------------------------------
// Datentypen und Schnittstellen fuer den KI-Startvorgang.
// Diese Typen leben in Application, damit Application.Ai.Startup
// und Infrastructure.Ai.Startup sie gemeinsam nutzen koennen – ohne
// Kreisabhaengigkeiten zur UI-Schicht.
// -----------------------------------------------------------------------

public enum AiStartupModelKind
{
    Generate,
    Embed
}

public sealed record AiStartupProcessRequest(
    string FileName,
    string Arguments,
    string? WorkingDirectory,
    bool Hidden)
{
    /// <summary>
    /// Gezielte Umgebung nur fuer den neuen Prozess. Der bestehende Konstruktor bleibt
    /// unveraendert; Aufrufer setzen Werte bei Bedarf per Objektinitialisierer.
    /// </summary>
    public IReadOnlyDictionary<string, string>? EnvironmentVariables { get; init; }
}

public sealed record AiStartupModelPreloadRequest(
    string ModelName,
    AiStartupModelKind Kind,
    string KeepAlive);

public sealed record AiStartupModelPreloadResult(
    bool Succeeded,
    string? Error);

public sealed record AiStartupWarmupResult(
    bool Succeeded,
    IReadOnlyList<string> LoadedModels,
    string? Error);

public sealed record AiStartupResult(
    bool SettingsChanged,
    bool OllamaReachable,
    bool OllamaStartAttempted,
    bool OllamaStartSucceeded,
    bool SidecarReachable,
    bool SidecarStartAttempted,
    bool SidecarStartSucceeded,
    IReadOnlyList<string> PreloadedModels,
    IReadOnlyList<string> Messages,
    IReadOnlyList<string> Warnings)
{
    public bool HasWarnings => Warnings.Count > 0;

    public string Summary
    {
        get
        {
            var lines = Messages.Concat(Warnings.Select(w => "Warnung: " + w)).ToArray();
            return lines.Length == 0 ? "Keine Aktion notwendig." : string.Join(System.Environment.NewLine, lines);
        }
    }
}

/// <summary>
/// Abstrahiert alle Systemoperationen (HTTP-Anfragen, Prozessstarts, Modell-Preloading),
/// damit der Orchestrator in Tests ohne echte Netzwerkaufrufe testbar ist.
/// </summary>
public interface IAiStartupLauncher
{
    Task<bool> IsReachableAsync(
        System.Uri baseUri,
        string relativePath,
        IReadOnlyDictionary<string, string>? headers,
        CancellationToken ct);

    bool TryStart(AiStartupProcessRequest request, out string? error);

    Task<AiStartupModelPreloadResult> PreloadOllamaModelAsync(
        System.Uri baseUri,
        AiStartupModelPreloadRequest request,
        CancellationToken ct);

    /// <summary>
    /// Prueft via /api/ps, ob das Modell wirklich im Speicher resident ist
    /// (nicht nur "Preload meldete ok"). null = Pruefung nicht moeglich/Fehler.
    /// </summary>
    Task<bool?> IsOllamaModelResidentAsync(
        System.Uri baseUri,
        string modelName,
        CancellationToken ct);

    Task<AiStartupWarmupResult> WarmupSidecarModelsAsync(
        System.Uri sidecarBaseUri,
        IReadOnlyDictionary<string, string>? headers,
        CancellationToken ct);
}
