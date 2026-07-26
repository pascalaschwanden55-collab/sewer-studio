using System;

namespace AuswertungPro.Next.Application.Ai;

/// <summary>
/// Per-Request-Timeout eines Sidecar-Inferenzaufrufs (Paket 3/C). Der geteilte
/// HttpClient darf bis zu 5 min laufen; ein einzelner Inferenz-Request wird früher
/// abgebrochen und zählt dadurch als Transportfehler im Sidecar-Ausfallschutz.
/// Paket 2/A6: Die Meldung nennt optional das betroffene Modell (z. B. "YOLO"),
/// damit Logs/Fortschritt den Ausfall ohne Endpunkt-Kenntnis einordnen koennen.
/// </summary>
public sealed class SidecarRequestTimeoutException : TimeoutException
{
    public SidecarRequestTimeoutException(string endpoint, TimeSpan timeout, string? model = null)
        : base(string.IsNullOrWhiteSpace(model)
            ? $"Sidecar {endpoint} antwortete nicht innerhalb von {timeout.TotalSeconds:0}s (Per-Request-Timeout)."
            : $"Sidecar-Modell {model}, {endpoint}, antwortete nicht innerhalb von {timeout.TotalSeconds:0}s (Per-Request-Timeout).")
    {
        Endpoint = endpoint;
        Model = string.IsNullOrWhiteSpace(model) ? null : model;
    }

    public string Endpoint { get; }

    /// <summary>Modell-Label des Endpunkts (z. B. "YOLO", "DINO", "SAM", "YOLO-cls"); null = unbekannt.</summary>
    public string? Model { get; }
}
