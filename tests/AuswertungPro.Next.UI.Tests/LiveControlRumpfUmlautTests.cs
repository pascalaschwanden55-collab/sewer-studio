using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.UI.Helpers;
using Xunit;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Auditbefund 18 (18.09.2026): Der LiveControl-Server las den Rumpf zeichenweise gegen
/// die in BYTES angegebene Content-Length. Ein Umlaut belegt in UTF-8 zwei Bytes, aber
/// nur ein Zeichen — die Schleife wartete deshalb auf Zeichen, die es nie gab. An einer
/// offenen Verbindung laeuft die Anfrage ins Leselimit, obwohl der Client alles gesendet
/// hat. Der gemeinsame <see cref="BoundedHttpRequestReader"/> kann das seit langem
/// richtig; der Server hatte eine eigene zweite Schleife daneben.
/// </summary>
public sealed class LiveControlRumpfUmlautTests
{
    [Theory]
    [InlineData("{\"farbe\":\"Grün\"}")]
    [InlineData("{\"text\":\"Schächte, Höhen und Strassen\"}")]
    [InlineData("{\"text\":\"ohne Sonderzeichen\"}")]
    public async Task Rumpf_wird_nach_Bytes_gelesen_und_bleibt_vollstaendig(string rumpf)
    {
        var bytes = Encoding.UTF8.GetByteCount(rumpf);
        using var strom = new MemoryStream(Encoding.UTF8.GetBytes(rumpf));
        using var leser = new StreamReader(strom, Encoding.UTF8);

        var gelesen = await new BoundedHttpRequestReader(leser)
            .ReadBodyAsync(bytes, 1024 * 1024, CancellationToken.None);

        Assert.Equal(rumpf, gelesen);
    }

    [Fact]
    public void LiveControl_verwendet_den_gemeinsamen_Rumpfleser()
    {
        var quelle = File.ReadAllText(
            RepoFile("src", "AuswertungPro.Next.UI", "LiveControl", "LiveControlServer.cs"));

        Assert.Contains("ReadBodyAsync(", quelle, StringComparison.Ordinal);
        // Keine zweite, zeichenzaehlende Rumpfschleife daneben.
        Assert.DoesNotContain("new char[contentLength]", quelle, StringComparison.Ordinal);
        Assert.DoesNotContain("read < contentLength", quelle, StringComparison.Ordinal);
    }
}
