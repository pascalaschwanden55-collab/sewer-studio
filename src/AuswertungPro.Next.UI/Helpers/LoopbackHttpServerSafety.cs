using System.Net.Sockets;
using System.Text;

namespace AuswertungPro.Next.UI.Helpers;

/// <summary>Gemeinsame Schutzregeln fuer die kleinen lokalen HTTP-Server.</summary>
internal static class LoopbackHttpServerSafety
{
    public static readonly TimeSpan RequestReadTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan BusyResponseTimeout = TimeSpan.FromSeconds(1);
    private static readonly byte[] BusyResponse = Encoding.UTF8.GetBytes(
        "HTTP/1.1 503 Service Unavailable\r\n" +
        "Content-Type: application/json; charset=utf-8\r\n" +
        "Content-Length: 42\r\n" +
        "Connection: close\r\n" +
        "Retry-After: 1\r\n\r\n" +
        "{\"ok\":false,\"error\":\"Server ausgelastet.\"}");

    public static async Task RejectBusyAsync(TcpClient client, CancellationToken serverToken)
    {
        using var ownedClient = client;
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(serverToken);
            timeout.CancelAfter(BusyResponseTimeout);
            await client.GetStream().WriteAsync(BusyResponse, timeout.Token).ConfigureAwait(false);
        }
        catch
        {
            // Der Client kann bereits getrennt sein. Die Annahmeschleife muss weiterlaufen.
        }
    }
}
