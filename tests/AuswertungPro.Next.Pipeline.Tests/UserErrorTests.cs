using System.Net;
using System.Text.Json;
using AuswertungPro.Next.Application.Ai;
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
    public void Describe_zeigt_bei_ArgumentNullException_aus_eigener_Assembly_die_generische_Meldung()
    {
        // BestEffort.Try (AuswertungPro.Next.Application) wirft eine ArgumentNullException -
        // eine Unterklasse von ArgumentException, kein exakter Treffer. Fix-Runde 3: Vorher
        // fing die Erkennung per "is (InvalidOperationException or ArgumentException)" auch
        // Unterklassen und haette hier faelschlich "Value cannot be null. (Parameter 'action')"
        // (Framework-Text der Unterklasse selbst, nicht von uns formuliert) direkt gezeigt.
        ArgumentNullException geworfen;
        try
        {
            BestEffort.Try(null!, "Testkontext");
            throw new InvalidOperationException("Testaufbau fehlerhaft: keine Ausnahme geworfen.");
        }
        catch (ArgumentNullException ex)
        {
            geworfen = ex;
        }

        var message = UserError.Describe(geworfen);

        Assert.Equal(
            "Der Vorgang konnte nicht abgeschlossen werden. Technische Details stehen im Programmlog.",
            message);
        Assert.DoesNotContain("cannot be null", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Describe_zeigt_bei_ArgumentOutOfRangeException_aus_eigener_Assembly_die_generische_Meldung()
    {
        // PhotoMeasurementAnglePlanBuilder.BuildAngleGeometry (AuswertungPro.Next.Application)
        // wirft eine ArgumentOutOfRangeException mit dem englischen Text "Only LateralCircle
        // and PipeBend are supported." - ebenfalls eine ArgumentException-Unterklasse, real im
        // Code gefunden und der Auslöser dieser Fix-Runde.
        ArgumentOutOfRangeException geworfen;
        try
        {
            PhotoMeasurementAnglePlanBuilder.BuildAngleGeometry(
                OverlayToolType.Line,
                new NormalizedPoint(0, 0),
                normalizedDiameter: 1.0,
                positionDeg: 0.0,
                angleDeg: 0.0);
            throw new InvalidOperationException("Testaufbau fehlerhaft: keine Ausnahme geworfen.");
        }
        catch (ArgumentOutOfRangeException ex)
        {
            geworfen = ex;
        }

        var message = UserError.Describe(geworfen);

        Assert.Equal(
            "Der Vorgang konnte nicht abgeschlossen werden. Technische Details stehen im Programmlog.",
            message);
        Assert.DoesNotContain("LateralCircle", message, StringComparison.Ordinal);
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

    // Aufgabe 10c2: Die unteren Schichten werfen ihre deutschen Saetze auch als IOException,
    // InvalidDataException, JsonException und als eigene Ausnahmetypen. Diese Auskunft darf beim
    // Umstellen der Anzeigen auf UserError nicht verloren gehen — dieselben Typen aus dem Framework
    // bleiben aber beim generischen Satz.

    [Fact]
    public void Describe_zeigt_bei_IOException_aus_eigener_Assembly_die_eigene_Meldung()
    {
        var geworfen = Assert.Throws<IOException>(
            () => ProjectPathResolver.EnsureWritableProjectPath("Beilage.pdf", null));

        Assert.Equal(
            "Ohne gespeichertes Projekt dürfen Dateipfade nicht geändert werden. Technische Details stehen im Programmlog.",
            UserError.Describe(geworfen));
    }

    [Fact]
    public void Describe_zeigt_bei_InvalidDataException_aus_eigener_Assembly_die_eigene_Meldung()
    {
        var geworfen = Assert.Throws<InvalidDataException>(
            () => AuswertungPro.Next.Infrastructure.Import.SchachtPro.SchachtProQrPayload.Parse("kein-qr", CancellationToken.None));

        Assert.Equal(
            "Kein unterstützter SchachtPro-QR-Code (SPQR1). Technische Details stehen im Programmlog.",
            UserError.Describe(geworfen));
    }

    [Fact]
    public void Describe_zeigt_bei_eigenem_Ausnahmetyp_die_eigene_Meldung()
    {
        var pfad = Path.Combine(Path.GetTempPath(), $"usererror-{Guid.NewGuid():N}.json");
        File.WriteAllText(pfad, "{}");
        try
        {
            var geworfen = Assert.Throws<AuswertungPro.Next.Application.Ai.Training.ClassMaps.TrainingYoloClassMapException>(
                () => AuswertungPro.Next.Infrastructure.Ai.Training.ClassMaps.TrainingYoloClassMapJsonReader.ReadClassMap(pfad));

            var message = UserError.Describe(geworfen);

            Assert.StartsWith("Der Klassenkarte fehlt", message, StringComparison.Ordinal);
            Assert.EndsWith("Technische Details stehen im Programmlog.", message, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(pfad);
        }
    }

    [Fact]
    public void Describe_zeigt_bei_IOException_aus_dem_Framework_die_generische_Meldung()
    {
        var pfad = Path.Combine(Path.GetTempPath(), $"usererror-{Guid.NewGuid():N}.txt");
        File.WriteAllText(pfad, "vorhanden");
        try
        {
            var geworfen = Assert.Throws<IOException>(() => new FileStream(pfad, FileMode.CreateNew).Dispose());

            var message = UserError.Describe(geworfen);

            Assert.Equal(
                "Eine Datei oder ein Ordner ist momentan nicht verfügbar. Bitte schliessen Sie andere Zugriffe und versuchen Sie es erneut.",
                message);
            Assert.DoesNotContain(pfad, message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(pfad);
        }
    }

    [Fact]
    public void Describe_zeigt_bei_JsonException_aus_dem_Framework_die_generische_Meldung()
    {
        var geworfen = Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<int[]>("{"));

        Assert.Equal(
            "Die gelesenen Daten sind beschädigt oder nicht gültig. Bitte Original oder Datensicherung prüfen.",
            UserError.Describe(geworfen));
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
