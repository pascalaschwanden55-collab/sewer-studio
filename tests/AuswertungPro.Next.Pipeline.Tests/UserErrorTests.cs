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

    // Fix-Runde 1 zu 10c2 (A1): Eine eigene Meldung, die den englischen Text ihrer inneren
    // Framework-Ausnahme einbettet, wird an dieser Stelle abgeschnitten — der deutsche Vorspann
    // bleibt, der Fremdtext (und alles danach, hier die Rohantwort) nicht.
    [Fact]
    public async Task Describe_schneidet_eingebetteten_Fremdtext_der_inneren_Ausnahme_ab()
    {
        using var http = new HttpClient(new FesteOllamaAntwort("{\"message\":{\"content\":\"kein json {\"}}"));
        using var client = new AuswertungPro.Next.Infrastructure.Ai.OllamaClient(new Uri("http://localhost:11434"), http);
        using var schema = JsonDocument.Parse("{}");

        var geworfen = await Assert.ThrowsAsync<InvalidOperationException>(() => client.ChatStructuredAsync<int[]>(
            "modell",
            [new AuswertungPro.Next.Infrastructure.Ai.OllamaClient.ChatMessage("user", "x")],
            schema.RootElement,
            CancellationToken.None));

        Assert.NotNull(geworfen.InnerException);
        Assert.Contains(geworfen.InnerException!.Message, geworfen.Message, StringComparison.Ordinal);

        var message = UserError.Describe(geworfen);

        Assert.Equal(
            "Die strukturierte JSON-Antwort des Modells konnte nicht gelesen werden. Technische Details stehen im Programmlog.",
            message);
        Assert.DoesNotContain("kein json", message, StringComparison.Ordinal);
    }

    // Fix-Runde 1 zu 10c2: Einfache Eingabehinweise tragen keinen Log-Zusatz und schreiben
    // keinen Stacktrace ins Log; eine fremde Ausnahme bleibt generisch und wird protokolliert.
    [Fact]
    public void DescribeInputHint_zeigt_eigene_Meldung_ohne_Loghinweis_und_ohne_Log()
    {
        string? logged = null;
        BestEffort.ConfigureDefaultErrorSink(message => logged = message);
        try
        {
            var geworfen = Assert.Throws<IOException>(
                () => ProjectPathResolver.EnsureWritableProjectPath("Beilage.pdf", null));

            Assert.Equal(
                "Ohne gespeichertes Projekt dürfen Dateipfade nicht geändert werden.",
                UserError.DescribeInputHint(geworfen, "Eingabe"));
            Assert.Null(logged);

            var fremd = UserError.DescribeInputHint(new InvalidOperationException("INTERN-GEHEIM"), "Eingabe");
            Assert.DoesNotContain("INTERN-GEHEIM", fremd, StringComparison.Ordinal);
            Assert.Contains("INTERN-GEHEIM", logged, StringComparison.Ordinal);
        }
        finally
        {
            BestEffort.ConfigureDefaultErrorSink(null);
        }
    }

    // Fix-Runde 2 zu 10c2: Randfaelle der Schnittregel.
    [Fact]
    public void SchneideFremdtext_laesst_bei_leerem_Fremdtext_alles_stehen()
    {
        const string text = "Die Datei konnte nicht gelesen werden: ";
        Assert.Equal(text, UserError.SchneideFremdtext(text, ["", "   "]));
    }

    [Fact]
    public void SchneideFremdtext_meldet_eine_Meldung_die_mit_dem_Fremdtext_beginnt_als_fremd()
    {
        const string fremd = "The process cannot access the file 'x.png'.";
        Assert.Null(UserError.SchneideFremdtext(fremd, [fremd]));
        Assert.Null(UserError.SchneideFremdtext(fremd + " (Lauf 3)", [fremd]));
    }

    [Fact]
    public void SchneideFremdtext_schneidet_bei_kurzem_Fremdtext_nicht()
    {
        // "Timeout" (7 Zeichen) steht hier zufaellig im deutschen Text; ein Schnitt zerstoerte ihn.
        const string text = "Der Timeout des Dienstes wurde erreicht: Timeout";
        Assert.Equal(text, UserError.SchneideFremdtext(text, ["Timeout"]));
    }

    [Fact]
    public void SchneideFremdtext_bevorzugt_das_Vorkommen_hinter_einem_Trenner()
    {
        // Der Fremdtext kommt zweimal vor: frueh im deutschen Satz (ohne Trenner) und am Ende
        // hinter ": ". Geschnitten wird am angehaengten Fremdtext.
        const string fremd = "Datei gesperrt durch Prozess";
        var text = $"Die Meldung {fremd}er wurde gemeldet, Export abgebrochen: {fremd}";
        Assert.Equal($"Die Meldung {fremd}er wurde gemeldet, Export abgebrochen.", UserError.SchneideFremdtext(text, [fremd]));
    }

    [Fact]
    public void OhneFremdtext_behaelt_eine_eingebettete_eigene_deutsche_Meldung()
    {
        var eigene = Assert.Throws<IOException>(
            () => ProjectPathResolver.EnsureWritableProjectPath("Beilage.pdf", null));
        var aussen = new InvalidOperationException($"Speichern gesperrt: {eigene.Message}", eigene);

        Assert.Equal(aussen.Message, UserError.OhneFremdtext(aussen));
    }

    [Fact]
    public void OhneFremdtext_schneidet_in_der_Kette_eigen_nach_fremd_nur_den_Fremdtext()
    {
        // Echte Kette: TrainingYoloClassMapFileStore (eigene Meldung) bettet den englischen
        // Sperr-Text der IOException ein (Klassenkarte exklusiv geoeffnet); aussen haengt eine
        // weitere eigene Meldung daran.
        var wurzel = Path.Combine(Path.GetTempPath(), $"usererror-{Guid.NewGuid():N}");
        Directory.CreateDirectory(wurzel);
        var karte = Path.Combine(wurzel, "class_map.json");
        File.WriteAllText(karte, "{}");
        try
        {
            AuswertungPro.Next.Application.Ai.Training.ClassMaps.TrainingYoloClassMapException eigene;
            using (new FileStream(karte, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                var store = new AuswertungPro.Next.Infrastructure.Ai.Training.ClassMaps.TrainingYoloClassMapFileStore(
                    karte, Path.Combine(wurzel, "migration.json"), Path.Combine(wurzel, "manifest.json"));
                eigene = Assert.Throws<AuswertungPro.Next.Application.Ai.Training.ClassMaps.TrainingYoloClassMapException>(
                    () => store.ReadSnapshot());
            }
            Assert.IsAssignableFrom<IOException>(eigene.InnerException);
            Assert.Contains(eigene.InnerException!.Message, eigene.Message, StringComparison.Ordinal);
            var aussen = new InvalidOperationException($"Export gesperrt: {eigene.Message}", eigene);

            var ergebnis = UserError.OhneFremdtext(aussen);

            Assert.Equal("Export gesperrt: Die YOLO-Detect-Klassenkonfiguration konnte nicht sicher gelesen werden.", ergebnis);
            Assert.Equal(
                "Die YOLO-Detect-Klassenkonfiguration konnte nicht sicher gelesen werden. Technische Details stehen im Programmlog.",
                UserError.Describe(eigene));
        }
        finally
        {
            Directory.Delete(wurzel, recursive: true);
        }
    }

    private sealed class FesteOllamaAntwort(string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) });
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
