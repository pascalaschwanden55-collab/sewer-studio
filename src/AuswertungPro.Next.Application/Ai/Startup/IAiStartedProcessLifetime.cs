using System;
using System.Diagnostics;

namespace AuswertungPro.Next.Application.Ai.Startup;

/// <summary>
/// Verfolgt ausschliesslich KI-Prozesse, die SewerStudio selbst gestartet hat,
/// und beendet sie beim Programmende.
/// </summary>
public interface IAiStartedProcessLifetime
{
    bool TryTrack(Process process, out string? error);

    void StopAllStartedProcesses();

    /// <summary>
    /// True, wenn mindestens ein selbst gestarteter KI-Prozess verfolgt wird.
    /// Default false: nicht aktualisierte Implementierungen gelten konservativ als
    /// "nicht von der App gestartet" (kein Neustart, graceful Degraded). (Paket 3/A2)
    /// </summary>
    bool HasTrackedStartedProcesses => false;

    /// <summary>True, wenn die Prozess-ID einem selbst gestarteten Prozess gehoert.</summary>
    bool IsTrackedProcess(int processId) => false;

    /// <summary>
    /// Beendet einen einzelnen verfolgten Prozess samt Baum (No-op bei Fremdprozessen).
    /// </summary>
    void StopTrackedProcess(int processId)
    {
    }

    /// <summary>
    /// Registriert einen selbst gestarteten Prozess mit Art und erwarteter Programmdatei
    /// (Paket 2/A3). Kompatibilitaets-Fallback: nicht aktualisierte Implementierungen
    /// ignorieren Art/Pfad und verhalten sich wie <see cref="TryTrack(Process, out string?)"/>.
    /// </summary>
    bool TryTrack(Process process, AiStartedProcessKind kind, string? expectedImagePath, out string? error)
        => TryTrack(process, out error);

    /// <summary>
    /// True, wenn mindestens ein getrackter Prozess die Art <see cref="AiStartedProcessKind.Sidecar"/>
    /// traegt (Paket 2/A3). Grundlage gegen blinde Zweitstarts: ein Neustart ohne lesbare
    /// /health-PID ist nur mit einem eigenen Sidecar-Prozess erlaubt — nur Ollama reicht nicht.
    /// Default false = konservativ (kein Blindstart).
    /// </summary>
    bool HasTrackedSidecarProcess => false;

    /// <summary>
    /// Identitaetsdaten eines getrackten Prozesses (Startzeit, Art, Programmpfad) fuer die
    /// erneute Pruefung vor einem Kill (PID-Reuse-Schutz); null = nicht (mehr) getrackt.
    /// Default null: Implementierungen ohne Identitaetsdaten liefern keine Aussage.
    /// </summary>
    TrackedAiProcessInfo? GetTrackedProcessInfo(int processId) => null;

    /// <summary>
    /// PIDs der aktuell LEBENDEN getrackten Prozesse einer Art (veraltete Eintraege werden
    /// vor der Antwort entfernt). Grundlage gegen Zweitstarts neben einem laufenden
    /// Sidecar (Paket 2/B2). Default leer = konservativ.
    /// </summary>
    IReadOnlyList<int> GetLiveTrackedProcessPids(AiStartedProcessKind kind) => Array.Empty<int>();

    /// <summary>
    /// True, wenn in dieser Sitzung JEMALS ein eigener Prozess der Art Sidecar getrackt
    /// wurde — auch wenn er inzwischen beendet ist (z. B. nach einem Watchdog-Exit).
    /// Dient ausschliesslich als START-Berechtigung fuer den Wiederstart nach bestaetigtem
    /// Ende, NIE als Kill-Berechtigung (Kill-Entscheidungen brauchen die Live-Identitaet).
    /// Default false = konservativ (kein Blindstart).
    /// </summary>
    bool HadTrackedSidecarProcess => false;
}

/// <summary>Art des selbst gestarteten KI-Prozesses (Paket 2/A3).</summary>
public enum AiStartedProcessKind
{
    /// <summary>Unbekannt/aeltere Registrierung ohne Artangabe.</summary>
    Unknown = 0,

    /// <summary>Vision-Sidecar (PowerShell-Wrapper des Python-Sidecars).</summary>
    Sidecar = 1,

    /// <summary>Ollama-Server. Wird beim kontrollierten Sidecar-Neustart nie beendet.</summary>
    Ollama = 2,
}

/// <summary>Identitaetsdaten eines getrackten KI-Prozesses (Paket 2/A3).</summary>
/// <param name="ProcessId">Prozess-ID.</param>
/// <param name="StartTimeUtc">Startzeit (UTC) beim Registrieren — PID-Reuse-Anker.</param>
/// <param name="Kind">Prozessart (Sidecar/Ollama/Unbekannt).</param>
/// <param name="ExpectedImagePath">Erwartete Programmdatei (optional); null = keine Aussage.</param>
public sealed record TrackedAiProcessInfo(
    int ProcessId,
    DateTime StartTimeUtc,
    AiStartedProcessKind Kind,
    string? ExpectedImagePath);
