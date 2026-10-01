using System;
using System.Globalization;

namespace AuswertungPro.Next.Application.Ai;

/// <summary>
/// Kapazitaetsfehler des Vision-Sidecars (Paket 2/A4): Der Sidecar meldet per
/// HTTP 503 mit Fehlercode "insufficient_vram", dass fuer das angeforderte Modell
/// nicht genuegend freier VRAM vorhanden ist, das 29-GB-Budget erreicht ist oder
/// die CUDA-Messung fehlschlug. Das ist KEIN Transport- und kein
/// Ausfallfehler: kein HTTP-Retry, kein Outage-Zaehler, kein Sidecar-Neustart —
/// der Frame wird wie bei einem Modellfehler uebersprungen (Skip-Quote/Review).
/// Die Meldung nennt nur VRAM-Zahlen und den Endpunkt, keine Tokens/Kundendaten.
/// </summary>
public sealed class SidecarInsufficientVramException : Exception
{
    public SidecarInsufficientVramException(
        string endpoint,
        double? freeGb,
        double? requiredGb,
        double? reservedGb,
        string? reason = null,
        double? usedGb = null,
        double? budgetGb = null)
        : base(BuildMessage(endpoint, freeGb, requiredGb, reservedGb, reason, usedGb, budgetGb))
    {
        Endpoint = endpoint;
        FreeGb = freeGb;
        RequiredGb = requiredGb;
        ReservedGb = reservedGb;
    }

    /// <summary>Endpunkt, dessen Modell nicht geladen/inferiert werden konnte (z. B. "/detect/dino").</summary>
    public string Endpoint { get; }

    /// <summary>Freier VRAM in GB laut Sidecar; null = nicht gemeldet.</summary>
    public double? FreeGb { get; }

    /// <summary>Benoetigter VRAM in GB laut Sidecar; null = nicht gemeldet.</summary>
    public double? RequiredGb { get; }

    /// <summary>Bereits reservierter VRAM in GB laut Sidecar; null = nicht gemeldet.</summary>
    public double? ReservedGb { get; }

    private static string BuildMessage(
        string endpoint, double? freeGb, double? requiredGb, double? reservedGb,
        string? reason, double? usedGb, double? budgetGb)
    {
        if (reason == "budget" && usedGb is not null && budgetGb is not null)
            return $"Sidecar {endpoint}: VRAM-Budget überschritten – nach Ladung " +
                   $"geschätzt {FormatGb(usedGb)}, Grenze {FormatGb(budgetGb)}.";
        if (reason == "measurement")
            return $"Sidecar {endpoint}: VRAM-Messung fehlgeschlagen; Modell nicht geladen.";
        return $"Sidecar {endpoint}: VRAM unzureichend – frei {FormatGb(freeGb)}, " +
               $"benötigt {FormatGb(requiredGb)}, reserviert {FormatGb(reservedGb)}.";
    }

    private static string FormatGb(double? gb)
        => gb is { } value
            ? value.ToString("0.0##", CultureInfo.InvariantCulture) + " GB"
            : "unbekannt";
}
