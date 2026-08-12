using System.Net;

namespace AuswertungPro.Next.Application.Ai;

/// <summary>
/// Gemeinsame Sicherheitsgrenze fuer Sidecar-Zugangsdaten.
/// Tokens duerfen nur an lokale Loopback-Endpunkte gesendet werden.
/// </summary>
public static class SidecarEndpointPolicy
{
    public static bool IsLoopback(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);

        if (uri.IsLoopback)
            return true;

        return IPAddress.TryParse(uri.Host, out var address)
               && IPAddress.IsLoopback(address);
    }
}
