using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AuswertungPro.Next.Application.Ai.Startup;

/// <summary>
/// Kontrollierter Neustart des Vision-Sidecars nach einem Ausfall (Paket 3/A2).
/// Ein Neustart ist nur erlaubt, wenn die App den Sidecar selbst gestartet hat;
/// ein fremd gestarteter Sidecar wird nie beendet (graceful Degraded).
/// </summary>
public interface ISidecarRestartService
{
    /// <summary>
    /// Versucht genau einen kontrollierten Neustart: stale Prozessbaum beenden,
    /// Sidecar über den bestehenden Startweg starten, auf /health warten.
    /// </summary>
    Task<SidecarRestartResult> TryRestartAsync(
        IProgress<string>? progress = null,
        CancellationToken ct = default);
}

/// <summary>Ergebnis eines Neustartversuchs.</summary>
/// <param name="Attempted">
/// true, wenn ein Neustart tatsächlich versucht wurde. false = abgelehnt
/// (fremd gestarteter Sidecar oder bereits laufender Neustart) → graceful Degraded.
/// </param>
/// <param name="Succeeded">true, wenn der Sidecar danach wieder auf /health antwortet.</param>
/// <param name="Reason">Kurzbegründung für Protokoll/Fortschritt.</param>
public sealed record SidecarRestartResult(bool Attempted, bool Succeeded, string? Reason);

/// <summary>
/// Zielwerte für einen Neustart (werden pro Versuch frisch aufgelöst, weil sich
/// Sidecar-Url/Token über die Einstellungen ändern können).
/// </summary>
public sealed record SidecarRestartTarget(
    Uri SidecarUrl,
    IReadOnlyDictionary<string, string>? Headers,
    string? ScriptPath,
    string PowerShellExe,
    IReadOnlyDictionary<string, string>? EnvironmentVariables);
