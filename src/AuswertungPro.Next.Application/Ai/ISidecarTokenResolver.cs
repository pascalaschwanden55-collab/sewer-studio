namespace AuswertungPro.Next.Application.Ai;

/// <summary>Löst den Schlüssel für die lokale Sidecar-Verbindung auf.</summary>
public interface ISidecarTokenResolver
{
    string? Resolve(string? configuredToken = null);
}
