using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using AuswertungPro.Next.UI.Helpers;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Grenzen fuer Anfragezeile und Kopfteil der lokalen HTTP-Server.
///
/// Geprueft wird das Verhalten, nicht der Quelltext: einmal direkt am Leser, einmal ueber
/// eine echte Loopback-Verbindung. Der frueher unbegrenzte <c>ReadLineAsync</c> liess vor
/// der Anmeldung beliebig grosse Kopfzeilen zu.
/// </summary>
public sealed class BoundedHttpRequestReaderTests
{
    private static BoundedHttpRequestReader Leser(string anfrage)
        => new(new StringReader(anfrage));

    [Fact]
    public async Task Eine_normale_Anfrage_wird_vollstaendig_gelesen()
    {
        var leser = Leser("GET /status HTTP/1.1\r\nHost: localhost\r\nX-Token: abc\r\n\r\n");

        Assert.Equal("GET /status HTTP/1.1", await leser.ReadRequestLineAsync(default));
        var kopf = await leser.ReadHeaderLinesAsync(default);

        Assert.NotNull(kopf);
        Assert.Equal(["Host: localhost", "X-Token: abc"], kopf);
    }

    [Fact]
    public async Task Eine_Anfrage_ganz_ohne_Kopfzeilen_ist_gueltig()
    {
        var leser = Leser("GET / HTTP/1.1\r\n\r\n");

        Assert.NotNull(await leser.ReadRequestLineAsync(default));
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<string>>(await leser.ReadHeaderLinesAsync(default)));
    }

    [Fact]
    public async Task Eine_zu_lange_Anfragezeile_wird_abgewiesen()
    {
        var leser = Leser("GET /" + new string('a', BoundedHttpRequestReader.MaxRequestLineChars) + " HTTP/1.1\r\n\r\n");

        Assert.Null(await leser.ReadRequestLineAsync(default));
    }

    [Fact]
    public async Task Eine_zu_lange_Kopfzeile_wird_abgewiesen()
    {
        var leser = Leser(
            "GET / HTTP/1.1\r\nX-Gross: "
            + new string('a', BoundedHttpRequestReader.MaxHeaderLineChars) + "\r\n\r\n");

        Assert.NotNull(await leser.ReadRequestLineAsync(default));
        Assert.Null(await leser.ReadHeaderLinesAsync(default));
    }

    [Fact]
    public async Task Zu_viele_Kopfzeilen_werden_abgewiesen()
    {
        var kopf = new StringBuilder("GET / HTTP/1.1\r\n");
        for (var i = 0; i <= BoundedHttpRequestReader.MaxHeaderCount; i++)
            kopf.Append($"X-{i}: v\r\n");
        kopf.Append("\r\n");

        var leser = Leser(kopf.ToString());
        Assert.NotNull(await leser.ReadRequestLineAsync(default));
        Assert.Null(await leser.ReadHeaderLinesAsync(default));
    }

    // Jede Zeile fuer sich ist erlaubt, die Summe nicht. Ohne diese Grenze koennte man
    // die Einzelgrenzen umgehen, indem man viele mittelgrosse Zeilen schickt.
    [Fact]
    public async Task Ein_insgesamt_zu_grosser_Kopfteil_wird_abgewiesen()
    {
        var wert = new string('a', 4000);
        var kopf = new StringBuilder("GET / HTTP/1.1\r\n");
        for (var i = 0; i < 16; i++)
            kopf.Append($"X-{i}: {wert}\r\n");
        kopf.Append("\r\n");

        var leser = Leser(kopf.ToString());
        Assert.NotNull(await leser.ReadRequestLineAsync(default));
        Assert.Null(await leser.ReadHeaderLinesAsync(default));
    }

    [Fact]
    public async Task Eine_abgebrochene_Verbindung_liefert_nichts_Halbes()
    {
        // Kein Zeilenende: Der Absender hat mittendrin aufgehoert.
        var leser = Leser("GET /status HTTP/1.1");

        Assert.Null(await leser.ReadRequestLineAsync(default));
    }

    // Echter Socket: beweist, dass die Grenze am Netzwerkstrom greift und nicht nur
    // an einem StringReader.
    [Fact]
    public async Task Ueber_eine_echte_Loopback_Verbindung_greift_die_Grenze()
    {
        var horcher = new TcpListener(IPAddress.Loopback, 0);
        horcher.Start();
        try
        {
            var port = ((IPEndPoint)horcher.LocalEndpoint).Port;
            var annahme = horcher.AcceptTcpClientAsync();

            using var absender = new TcpClient();
            await absender.ConnectAsync(IPAddress.Loopback, port);

            using var bedient = await annahme;
            using var strom = bedient.GetStream();
            using var reader = new StreamReader(strom, Encoding.UTF8, false, leaveOpen: true);
            var leser = new BoundedHttpRequestReader(reader);

            var schreiben = Task.Run(async () =>
            {
                var daten = Encoding.UTF8.GetBytes(
                    "GET / HTTP/1.1\r\nX-Gross: "
                    + new string('a', BoundedHttpRequestReader.MaxHeaderLineChars + 100) + "\r\n\r\n");
                try
                {
                    await absender.GetStream().WriteAsync(daten);
                }
                catch
                {
                    // Der Leser darf die Verbindung schliessen, bevor alles geschrieben ist.
                }
            });

            Assert.Equal("GET / HTTP/1.1", await leser.ReadRequestLineAsync(default));
            Assert.Null(await leser.ReadHeaderLinesAsync(default));
            await schreiben;
        }
        finally
        {
            horcher.Stop();
        }
    }
}
