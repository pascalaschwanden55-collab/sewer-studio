using System.Net;
using System.Text.Json;
using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.Pipeline.Tests;

[Collection("BestEffort global sink")]
public sealed class UserErrorTests
{
    [Theory]
    [MemberData(nameof(KnownErrors))]
    public void Describe_liefert_verstaendliche_Meldung_ohne_Rohtext(
        Exception exception,
        string expectedPart)
    {
        var message = UserError.Describe(exception);

        Assert.Contains(expectedPart, message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("INTERN-GEHEIM", message, StringComparison.Ordinal);
    }

    [Fact]
    public void Describe_unwrappt_einzelne_AggregateException()
    {
        var message = UserError.Describe(
            new AggregateException(new UnauthorizedAccessException("INTERN-GEHEIM")));

        Assert.Contains("Zugriff", message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("INTERN-GEHEIM", message, StringComparison.Ordinal);
    }

    [Fact]
    public void DescribeAndReport_zeigt_sichere_Meldung_und_loggt_vollen_Fehler()
    {
        string? logged = null;
        BestEffort.ConfigureDefaultErrorSink(message => logged = message);
        try
        {
            var shown = UserError.DescribeAndReport(
                new InvalidOperationException("INTERN-GEHEIM"),
                "Dossier erstellen");

            Assert.DoesNotContain("INTERN-GEHEIM", shown, StringComparison.Ordinal);
            Assert.Contains("Programmlog", shown, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Dossier erstellen", logged, StringComparison.Ordinal);
            Assert.Contains("INTERN-GEHEIM", logged, StringComparison.Ordinal);
        }
        finally
        {
            BestEffort.ConfigureDefaultErrorSink(null);
        }
    }

    public static TheoryData<Exception, string> KnownErrors => new()
    {
        { new UserFacingException("Sicherer Nutzerhinweis"), "Sicherer Nutzerhinweis" },
        { new OperationCanceledException("INTERN-GEHEIM"), "abgebrochen" },
        { new TimeoutException("INTERN-GEHEIM"), "zu lange" },
        { new UnauthorizedAccessException("INTERN-GEHEIM"), "Zugriff" },
        { new FileNotFoundException("INTERN-GEHEIM"), "Datei" },
        { new DirectoryNotFoundException("INTERN-GEHEIM"), "Ordner" },
        { new PathTooLongException("INTERN-GEHEIM"), "zu lang" },
        { new IOException("INTERN-GEHEIM"), "nicht verfuegbar" },
        { new HttpRequestException("INTERN-GEHEIM", null, HttpStatusCode.ServiceUnavailable), "Dienst" },
        { new JsonException("INTERN-GEHEIM"), "Daten" },
        { new InvalidDataException("INTERN-GEHEIM"), "Daten" },
        { new OutOfMemoryException("INTERN-GEHEIM"), "Arbeitsspeicher" },
        { new NotSupportedException("INTERN-GEHEIM"), "nicht unterstuetzt" },
        { new InvalidOperationException("INTERN-GEHEIM"), "Programmlog" }
    };
}
