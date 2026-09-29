using System.Net;
using System.Text.Json;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Ai.QualityGate;

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
    public void Describe_zeigt_bei_InvalidOperationException_aus_eigener_Assembly_die_eigene_Meldung()
    {
        // Project.AddRecord (AuswertungPro.Next.Domain) wirft eine bewusst formulierte deutsche
        // InvalidOperationException. Echtes Werfen+Fangen ist Pflicht: Exception.TargetSite bleibt
        // bei einer nur konstruierten, nie geworfenen Ausnahme leer (siehe KnownErrors unten).
        var project = new Project();
        var erste = new HaltungRecord();
        erste.SetFieldValue("Haltungsname", "H1", FieldSource.Manual, userEdited: false);
        project.AddRecord(erste);

        var zweite = new HaltungRecord();
        zweite.SetFieldValue("Haltungsname", "H1", FieldSource.Manual, userEdited: false);

        InvalidOperationException geworfen;
        try
        {
            project.AddRecord(zweite);
            throw new InvalidOperationException("Testaufbau fehlerhaft: keine Ausnahme geworfen.");
        }
        catch (InvalidOperationException ex)
        {
            geworfen = ex;
        }

        var message = UserError.Describe(geworfen);

        Assert.Equal(
            "Die Haltung 'H1' existiert bereits im Projekt. Technische Details stehen im Programmlog.",
            message);
    }

    [Fact]
    public void Describe_zeigt_bei_ArgumentException_aus_eigener_Assembly_die_eigene_Meldung()
    {
        // CategoryWeights.FromArray (AuswertungPro.Next.Infrastructure) wirft eine bewusst
        // formulierte deutsche ArgumentException.
        ArgumentException geworfen;
        try
        {
            CategoryWeights.Default().FromArray([1, 2, 3]);
            throw new InvalidOperationException("Testaufbau fehlerhaft: keine Ausnahme geworfen.");
        }
        catch (ArgumentException ex)
        {
            geworfen = ex;
        }

        var message = UserError.Describe(geworfen);

        Assert.Equal(
            "Es werden genau 8 Gewichte erwartet. Technische Details stehen im Programmlog.",
            message);
    }

    [Fact]
    public void Describe_zeigt_bei_InvalidOperationException_aus_dem_Framework_die_generische_Meldung()
    {
        // Enumerable.First() auf einer leeren Liste wirft aus System.Linq (Framework), nicht aus
        // einer eigenen SewerStudio-Assembly - der englische Originaltext darf nicht durchsickern.
        InvalidOperationException geworfen;
        try
        {
            new List<int>().First();
            throw new InvalidOperationException("Testaufbau fehlerhaft: keine Ausnahme geworfen.");
        }
        catch (InvalidOperationException ex)
        {
            geworfen = ex;
        }

        var message = UserError.Describe(geworfen);

        Assert.Equal(
            "Der Vorgang konnte nicht abgeschlossen werden. Technische Details stehen im Programmlog.",
            message);
        Assert.DoesNotContain("Sequence contains no elements", message, StringComparison.Ordinal);
    }

    [Fact]
    public void DescribeAndReport_haengt_bei_eigener_Meldung_den_Hinweis_an_und_loggt_trotzdem_voll()
    {
        string? logged = null;
        BestEffort.ConfigureDefaultErrorSink(message => logged = message);
        try
        {
            var project = new Project();
            var erste = new HaltungRecord();
            erste.SetFieldValue("Haltungsname", "H2", FieldSource.Manual, userEdited: false);
            project.AddRecord(erste);
            var zweite = new HaltungRecord();
            zweite.SetFieldValue("Haltungsname", "H2", FieldSource.Manual, userEdited: false);

            InvalidOperationException geworfen;
            try
            {
                project.AddRecord(zweite);
                throw new InvalidOperationException("Testaufbau fehlerhaft: keine Ausnahme geworfen.");
            }
            catch (InvalidOperationException ex)
            {
                geworfen = ex;
            }

            var shown = UserError.DescribeAndReport(geworfen, "Haltung hinzufuegen");

            Assert.Equal(
                "Die Haltung 'H2' existiert bereits im Projekt. Technische Details stehen im Programmlog.",
                shown);
            Assert.Contains("Haltung hinzufuegen", logged, StringComparison.Ordinal);
            Assert.Contains("existiert bereits im Projekt", logged, StringComparison.Ordinal);
        }
        finally
        {
            BestEffort.ConfigureDefaultErrorSink(null);
        }
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
        { new IOException("INTERN-GEHEIM"), "nicht verfügbar" },
        { new HttpRequestException("INTERN-GEHEIM", null, HttpStatusCode.ServiceUnavailable), "Dienst" },
        { new JsonException("INTERN-GEHEIM"), "Daten" },
        { new InvalidDataException("INTERN-GEHEIM"), "Daten" },
        { new OutOfMemoryException("INTERN-GEHEIM"), "Arbeitsspeicher" },
        { new NotSupportedException("INTERN-GEHEIM"), "nicht unterstützt" },
        // Nur konstruiert, nie geworfen: TargetSite bleibt leer, also generischer Satz -
        // deckt zugleich den Fall "Fixture-Ausnahme kommt aus dem Testcode" ab (siehe
        // Describe_zeigt_bei_InvalidOperationException_aus_eigener_Assembly_die_eigene_Meldung).
        { new InvalidOperationException("INTERN-GEHEIM"), "Programmlog" }
    };
}
