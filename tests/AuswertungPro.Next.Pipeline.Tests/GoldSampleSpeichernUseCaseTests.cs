using System.Security.Cryptography;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Teacher;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Ai.Workbench;
using AuswertungPro.Next.Application.UseCases.GoldSampleSpeichern;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Verhaltenstests fuer den Anwendungsfall Goldsample-Speichern ohne UI. Alle
/// Abhaengigkeiten sind handgeschriebene Fakes mit gemeinsamem Schreibprotokoll, damit
/// Phasenfolge und die Grenze "dauerhaft gespeichert" direkt sichtbar sind.
/// </summary>
public sealed class GoldSampleSpeichernUseCaseTests
{
    private static readonly BoundingBox TestBox = new(0.5, 0.5, 0.2, 0.2);

    // 1000x500, 100 Pixel in Zeile 250, Spalte 450-549: liegt in der TestBox.
    private const string MaskeInDerBox = "0,250450,100,249450";

    // Gleich gross, aber in Zeile 10: vollstaendig ausserhalb der TestBox.
    private const string MaskeAusserhalbDerBox = "0,10010,100,489890";

    private static readonly WorkbenchSegmentation Segmentierung =
        new(MaskeInDerBox, 1000, 500, 0.02, "Maske erstellt.", Degraded: false);

    private static WorkbenchItem Foto(string caseId = "case1")
        => new(@"C:\frames\f.jpg", caseId, 1.0, 1.0, null, null, 300);

    private static WorkbenchDecision Entscheid(string code = "BAB", string user = "Pascal", string text = "Riss quer im Scheitel")
        => new(code, false, text, null, null, user);

    [Fact]
    public async Task Gold_durchlaeuft_die_Phasen_in_fester_Reihenfolge()
    {
        var umgebung = new Umgebung();

        var result = await umgebung.UseCase().SaveAsync(
            new GoldSampleSpeichernAnfrage(Foto(), TestBox, Segmentierung, Entscheid()));

        Assert.True(result.Saved);
        Assert.True(result.GoldApproved);
        Assert.Null(result.RefusalReason);
        Assert.Equal("Indexed", result.KbIndexState);
        Assert.NotNull(result.TeacherAnnotationId);
        Assert.Equal(
            new[] { "frame.store", "file.read", "mask.check", "sample.tryadd", "kb.index", "sample.mergeorupdate", "teacher.export", "teacher.append" },
            umgebung.Log);
        Assert.Equal(Umgebung.GoldPfad, umgebung.MaskFramePath);
        var sample = Assert.Single(umgebung.Samples.Store);
        Assert.Equal(TrainingSampleStatus.Approved, sample.Status);
        Assert.Equal(KbIndexState.Indexed, sample.KbIndexState);
        Assert.Equal(result.SampleId, Assert.Single(umgebung.Teacher.Appended).SourceSampleId);
    }

    [Theory]
    [InlineData("   ", "Riss quer im Scheitel", "BAB", "Persönliche Bestätigung fehlt.")]
    [InlineData("Pascal", "zu kurz", "BAB", "Beschreibung zu kurz")]
    [InlineData("Pascal", "Riss quer — Ausmass ergaenzen", "BAB", "Platzhalter-Beschreibung")]
    [InlineData("Pascal", "Riss quer im Scheitel", "XYZ", "Unbekannter VSA-Code 'XYZ'.")]
    public async Task Ungueltige_Eingaben_werden_vor_jedem_Lesen_und_Schreiben_abgelehnt(
        string user, string text, string code, string erwartet)
    {
        var umgebung = new Umgebung();

        var result = await umgebung.UseCase().SaveAsync(
            new GoldSampleSpeichernAnfrage(Foto(), TestBox, Segmentierung, Entscheid(code, user, text)));

        Assert.False(result.Saved);
        Assert.Contains(erwartet, result.RefusalReason);
        Assert.Equal("-", result.KbIndexState);
        Assert.Empty(umgebung.Log);
    }

    [Fact]
    public async Task Ungueltige_Maske_speichert_nur_Entwurf_ohne_KB_und_Teacher()
    {
        var umgebung = new Umgebung { MaskeGueltig = false };

        var result = await umgebung.UseCase().SaveAsync(
            new GoldSampleSpeichernAnfrage(Foto(), TestBox, Segmentierung, Entscheid()));

        Assert.True(result.Saved);
        Assert.False(result.GoldApproved);
        Assert.Equal("Entwurf", result.KbIndexState);
        Assert.StartsWith("Entwurf gespeichert:", result.RefusalReason);
        Assert.Equal(new[] { "frame.store", "file.read", "mask.check", "sample.tryadd" }, umgebung.Log);
        var sample = Assert.Single(umgebung.Samples.Store);
        Assert.Equal(TrainingSampleStatus.Draft, sample.Status);
        Assert.Equal("Yellow", sample.QualityGateLevel);
        Assert.Null(sample.SamMaskRle);
    }

    [Fact]
    public async Task Gueltig_gemeldete_Maske_ausserhalb_der_Box_scheitert_an_der_zentralen_Goldschranke()
    {
        var umgebung = new Umgebung { AngewandteMaske = MaskeAusserhalbDerBox };

        var result = await umgebung.UseCase().SaveAsync(
            new GoldSampleSpeichernAnfrage(Foto(), TestBox, Segmentierung, Entscheid()));

        Assert.True(result.Saved);
        Assert.False(result.GoldApproved);
        Assert.Equal("Entwurf", result.KbIndexState);
        Assert.Equal(TrainingSampleStatus.Draft, Assert.Single(umgebung.Samples.Store).Status);
        Assert.DoesNotContain("kb.index", umgebung.Log);
        Assert.DoesNotContain("teacher.export", umgebung.Log);
    }

    [Fact]
    public async Task Eval_Haltung_wird_vor_der_Goldkopie_abgewiesen()
    {
        var umgebung = new Umgebung
        {
            EvalSchutz = new GoldSampleEvalSchutz(
                new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "287425-81162" }),
        };

        var result = await umgebung.UseCase().SaveAsync(
            new GoldSampleSpeichernAnfrage(Foto("287425-81162"), TestBox, Segmentierung, Entscheid()));

        Assert.False(result.Saved);
        Assert.Equal("Eval-Schutz: Bild gehört zum eingefrorenen Mess-Set (EvalHaltung). Nicht speicherbar.", result.RefusalReason);
        Assert.Empty(umgebung.Log);
    }

    [Fact]
    public async Task Eval_Bildhash_der_Momentaufnahme_wird_vor_der_Goldkopie_abgewiesen()
    {
        var bytes = new byte[] { 9, 8, 7, 6 };
        var umgebung = new Umgebung
        {
            EvalSchutz = new GoldSampleEvalSchutz(
                new HashSet<string>(StringComparer.OrdinalIgnoreCase) { Convert.ToHexStringLower(SHA256.HashData(bytes)) },
                new HashSet<string>(StringComparer.OrdinalIgnoreCase)),
        };

        var result = await umgebung.UseCase().SaveAsync(new GoldSampleSpeichernAnfrage(
            Foto(), TestBox, Segmentierung, Entscheid(), WorkbenchImageSnapshot.Create(bytes, ".jpg")));

        Assert.False(result.Saved);
        Assert.Contains("(EvalImageHash)", result.RefusalReason);
        Assert.Empty(umgebung.Log);
    }

    [Fact]
    public async Task Nicht_ladbarer_Eval_Schutz_sperrt_das_Speichern()
    {
        var umgebung = new Umgebung { EvalFehler = new DirectoryNotFoundException("Eval-Ordner fehlt (Test).") };

        var result = await umgebung.UseCase().SaveAsync(
            new GoldSampleSpeichernAnfrage(Foto(), TestBox, Segmentierung, Entscheid()));

        Assert.False(result.Saved);
        Assert.StartsWith("Eval-Schutz nicht verfügbar: ", result.RefusalReason);
        Assert.Empty(umgebung.Log);
    }

    [Fact]
    public async Task Geaenderter_gebundener_Bildstand_wird_vor_jedem_Schreiben_abgewiesen()
    {
        var umgebung = new Umgebung { QuellBytes = new byte[] { 4, 5, 6 } };
        var item = Foto() with
        {
            ExpectedImageSha256 = Convert.ToHexStringLower(SHA256.HashData(new byte[] { 1, 2, 3 })),
        };

        var result = await umgebung.UseCase().SaveAsync(
            new GoldSampleSpeichernAnfrage(item, TestBox, Segmentierung, Entscheid()));

        Assert.False(result.Saved);
        Assert.Equal(
            "Das Bild wurde seit dem Laden der Goldprüfung geändert. Bitte die Goldprüfung neu laden.",
            result.RefusalReason);
        Assert.Equal(new[] { "file.read" }, umgebung.Log);
    }

    [Fact]
    public async Task Momentaufnahme_wird_bytegenau_gespeichert_ohne_den_Quellpfad_zu_lesen()
    {
        var bytes = new byte[] { 1, 2, 3, 4 };
        var snapshot = WorkbenchImageSnapshot.Create(bytes, ".jpg");
        var umgebung = new Umgebung();

        var result = await umgebung.UseCase().SaveAsync(
            new GoldSampleSpeichernAnfrage(Foto(), TestBox, Segmentierung, Entscheid(), snapshot));

        Assert.True(result.GoldApproved);
        Assert.Equal(snapshot.Sha256, result.StoredImageSha256);
        Assert.Equal(bytes, umgebung.Frames.LastBytes);
        Assert.Equal("frame.bytes", umgebung.Log[0]);
        Assert.DoesNotContain("file.read", umgebung.Log);
    }

    [Fact]
    public async Task Fremd_geaenderte_Bestandsversion_wird_nach_dem_Lesen_ohne_Schreiben_abgewiesen()
    {
        var umgebung = new Umgebung();
        umgebung.Samples.Store.Add(new TrainingSample
        {
            SampleId = "wb_alt",
            CaseId = "case1",
            Code = "BAB",
            SourceType = SourceTypeNames.ManualCoding,
            ConfirmedAtUtc = new DateTime(2026, 8, 3, 12, 0, 0, DateTimeKind.Utc),
        });
        var item = Foto() with
        {
            ExistingSampleId = "wb_alt",
            ExistingCode = "BAB",
            ExpectedConfirmedAtUtc = new DateTimeOffset(2026, 8, 3, 11, 0, 0, TimeSpan.Zero),
        };

        var result = await umgebung.UseCase().SaveAsync(
            new GoldSampleSpeichernAnfrage(item, TestBox, Segmentierung, Entscheid()));

        Assert.False(result.Saved);
        Assert.Contains("inzwischen in einem anderen Arbeitsablauf geändert", result.RefusalReason);
        Assert.Equal(new[] { "sample.load" }, umgebung.Log);
    }

    [Fact]
    public async Task Doppeltes_Sample_wird_sichtbar_abgewiesen_ohne_KB_und_Teacher()
    {
        var umgebung = new Umgebung();
        umgebung.Samples.TryAddErgebnis = false;

        var result = await umgebung.UseCase().SaveAsync(
            new GoldSampleSpeichernAnfrage(Foto(), TestBox, Segmentierung, Entscheid()));

        Assert.False(result.Saved);
        Assert.StartsWith("Bereits als Goldsample vorhanden", result.RefusalReason);
        Assert.Null(result.SampleId);
        Assert.Equal(new[] { "frame.store", "file.read", "mask.check", "sample.tryadd" }, umgebung.Log);
    }

    [Fact]
    public async Task Abbruch_vor_der_dauerhaften_Speicherung_wird_weitergeworfen()
    {
        var umgebung = new Umgebung();
        umgebung.Frames.Fehler = new OperationCanceledException("Abbruch beim Goldbild (Test).");

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => umgebung.UseCase().SaveAsync(
            new GoldSampleSpeichernAnfrage(Foto(), TestBox, Segmentierung, Entscheid())));

        Assert.Equal(new[] { "frame.store" }, umgebung.Log);
        Assert.Empty(umgebung.Samples.Store);
    }

    [Fact]
    public async Task Schreibfehler_bei_Neuanlage_meldet_dieselbe_Ablehnung_wie_die_Reparaturwege()
    {
        // Neuanlage
        var neu = new Umgebung();
        neu.Samples.Schreibfehler = new IOException("Datei gesperrt (Test).");
        var neuErgebnis = await neu.UseCase().SaveAsync(
            new GoldSampleSpeichernAnfrage(Foto(), TestBox, Segmentierung, Entscheid()));

        // Reparatur mit gleichem Code (Nachlabeln) und mit geaendertem Code (Ersatz)
        var nachlabeln = UmgebungMitBestand();
        var nachlabelnErgebnis = await nachlabeln.UseCase().SaveAsync(new GoldSampleSpeichernAnfrage(
            Foto() with { ExistingSampleId = "wb_alt", ExistingCode = "BAB" }, TestBox, Segmentierung, Entscheid()));
        var ersatz = UmgebungMitBestand();
        var ersatzErgebnis = await ersatz.UseCase().SaveAsync(new GoldSampleSpeichernAnfrage(
            Foto() with { ExistingSampleId = "wb_alt", ExistingCode = "BAB" }, TestBox, Segmentierung,
            Entscheid("BBA", text: "Wurzeleinwuchs im Anschlussbereich")));

        Assert.False(neuErgebnis.Saved);
        Assert.Null(neuErgebnis.SampleId);
        Assert.Equal("-", neuErgebnis.KbIndexState);
        Assert.StartsWith("Goldsample konnte nicht gespeichert werden: ", neuErgebnis.RefusalReason);
        Assert.Equal(nachlabelnErgebnis, neuErgebnis);
        Assert.Equal(ersatzErgebnis, neuErgebnis);

        // Kein Sample, kein KB- oder Teacher-Nachlauf nach dem Fehler.
        Assert.Equal(new[] { "frame.store", "file.read", "mask.check", "sample.tryadd" }, neu.Log);
        Assert.Empty(neu.Samples.Store);
        Assert.Empty(neu.Teacher.Appended);

        static Umgebung UmgebungMitBestand()
        {
            var umgebung = new Umgebung();
            umgebung.Samples.Store.Add(new TrainingSample
            {
                SampleId = "wb_alt",
                CaseId = "case1",
                Code = "BAB",
                SourceType = SourceTypeNames.ManualCoding,
            });
            umgebung.Samples.Schreibfehler = new IOException("Datei gesperrt (Test).");
            return umgebung;
        }
    }

    [Fact]
    public async Task Abbruch_beim_Speichern_der_Neuanlage_wird_weitergeworfen()
    {
        var umgebung = new Umgebung();
        umgebung.Samples.Schreibfehler = new OperationCanceledException("Abbruch beim Speichern (Test).");

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => umgebung.UseCase().SaveAsync(
            new GoldSampleSpeichernAnfrage(Foto(), TestBox, Segmentierung, Entscheid())));

        Assert.Equal(new[] { "frame.store", "file.read", "mask.check", "sample.tryadd" }, umgebung.Log);
        Assert.Empty(umgebung.Samples.Store);
    }

    [Fact]
    public async Task PDF_Vorschlag_ohne_Bestand_ergibt_immer_PDF_Herkunft()
    {
        // Haelt fest, warum die Doppelsicherung "PDF-Herkunft konnte nicht eindeutig gebunden
        // werden" heute nie greift: Ohne Bestand und mit PDF-Vorschlag ist die Herkunft immer
        // PdfPhoto. Eine unvollstaendige Pruefspur scheitert deshalb an der PDF-Pruefung davor.
        static WorkbenchItem MitPdfVorschlag(string pdfBeschreibung) => Foto() with
        {
            SourceSuggestion = new WorkbenchSourceSuggestion(
                "BAB",
                pdfBeschreibung,
                "Haltung_123.pdf",
                new string('a', 64),
                PageNumber: 7,
                PhotoId: "IMG-0042",
                MatchKind: "time_meter_text"),
        };
        var samples = new SampleStore(new List<string>());

        var (gueltig, keineAblehnung) = await GoldSampleHerkunft.BestimmeAsync(
            samples, MitPdfVorschlag("Riss quer im Scheitel"), Entscheid(), "BAB");
        var (_, unvollstaendig) = await GoldSampleHerkunft.BestimmeAsync(
            samples, MitPdfVorschlag(""), Entscheid(), "BAB");

        Assert.Null(keineAblehnung);
        Assert.Equal(SourceTypeNames.PdfPhoto, gueltig!.SourceType);
        Assert.StartsWith("PDF-Goldsample kann nicht gespeichert werden", unvollstaendig!.RefusalReason);
    }

    [Fact]
    public async Task KB_Fehler_nach_der_Speicherung_meldet_gespeichert_mit_Warnung_ohne_zweite_Speicherung()
    {
        var umgebung = new Umgebung();
        umgebung.Indexer.Fehler = new IOException("KB-DB gesperrt (Test).");

        var result = await umgebung.UseCase().SaveAsync(
            new GoldSampleSpeichernAnfrage(Foto(), TestBox, Segmentierung, Entscheid()));

        Assert.True(result.Saved);
        Assert.True(result.GoldApproved);
        Assert.Equal("Error", result.KbIndexState);
        Assert.StartsWith("KB-Index nicht aktualisiert: ", result.RefusalReason);
        Assert.NotNull(result.TeacherAnnotationId);
        Assert.Equal(
            new[] { "frame.store", "file.read", "mask.check", "sample.tryadd", "kb.index", "teacher.export", "teacher.append" },
            umgebung.Log);
        Assert.Equal(TrainingSampleStatus.Approved, Assert.Single(umgebung.Samples.Store).Status);
    }

    [Fact]
    public async Task Teacher_Abbruch_nach_der_Speicherung_meldet_gespeichert_mit_Warnung()
    {
        var umgebung = new Umgebung();
        umgebung.Export.Fehler = new OperationCanceledException("Abbruch beim Teacher-Export (Test).");

        var result = await umgebung.UseCase().SaveAsync(
            new GoldSampleSpeichernAnfrage(Foto(), TestBox, Segmentierung, Entscheid()));

        Assert.True(result.Saved);
        Assert.True(result.GoldApproved);
        Assert.Equal("Indexed", result.KbIndexState);
        Assert.Null(result.TeacherAnnotationId);
        Assert.StartsWith("Teacher-Kandidat nicht gespeichert: ", result.RefusalReason);
        Assert.Empty(umgebung.Teacher.Appended);
        Assert.Single(umgebung.Samples.Store);
    }

    [Fact]
    public async Task Codekorrektur_ersetzt_den_Bestand_und_bereinigt_KB_und_Teacher_vor_dem_Nachlauf()
    {
        var umgebung = new Umgebung();
        umgebung.Samples.Store.Add(new TrainingSample
        {
            SampleId = "wb_alt",
            CaseId = "case1",
            Code = "BAB",
            SourceType = SourceTypeNames.ManualCoding,
        });
        umgebung.Teacher.Existing.Add(new TeacherAnnotation { AnnotationId = "t_alt", SourceSampleId = "wb_alt" });
        var item = Foto() with { ExistingSampleId = "wb_alt", ExistingCode = "BAB" };

        var result = await umgebung.UseCase().SaveAsync(new GoldSampleSpeichernAnfrage(
            item, TestBox, Segmentierung, Entscheid("BBA", text: "Wurzeleinwuchs im Anschlussbereich")));

        Assert.True(result.GoldApproved);
        Assert.Equal("wb_alt", result.SampleId);
        Assert.Equal(
            new[]
            {
                "sample.load", "frame.store", "file.read", "mask.check", "sample.replace", "kb.deindex",
                "teacher.load", "teacher.delete", "kb.index", "sample.mergeorupdate",
                "teacher.export", "teacher.append",
            },
            umgebung.Log);
        Assert.Equal("BBA", Assert.Single(umgebung.Samples.Store).Code);
    }

    [Fact]
    public void TeacherExportGrund_ordnet_eine_mitgelieferte_Framework_Ausnahme_deutsch_ein()
    {
        var grund = GoldSampleNachlauf.TeacherExportGrund(new TrainingAnnotationResult
        {
            Success = false,
            Error = "Access to the path is denied.",
            Failure = new UnauthorizedAccessException("Access to the path is denied."),
        });

        Assert.DoesNotContain("Access to the path", grund);
        Assert.False(string.IsNullOrWhiteSpace(grund));
    }

    // ── Testumgebung ───────────────────────────────────────────────────────

    private sealed class Umgebung
    {
        public const string GoldPfad = @"C:\KI_BRAIN\gold_frames\BAB - Riss\gold_test.jpg";

        public List<string> Log { get; } = new();
        public SampleStore Samples { get; }
        public FrameStore Frames { get; }
        public Indexer Indexer { get; }
        public TeacherStore Teacher { get; }
        public ExportService Export { get; }
        public bool MaskeGueltig { get; init; } = true;
        public string AngewandteMaske { get; init; } = MaskeInDerBox;
        public GoldSampleEvalSchutz EvalSchutz { get; init; } = new(
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        public Exception? EvalFehler { get; init; }
        public byte[] QuellBytes { get; init; } = new byte[] { 1, 2, 3 };
        public string? MaskFramePath { get; private set; }

        public Umgebung()
        {
            Samples = new SampleStore(Log);
            Frames = new FrameStore(Log);
            Indexer = new Indexer(Log);
            Teacher = new TeacherStore(Log);
            Export = new ExportService(Log);
        }

        public GoldSampleSpeichernUseCase UseCase()
            => new(
                Samples,
                Frames,
                () => @"C:\KI_BRAIN\gold_frames",
                Indexer,
                Teacher,
                new ClassMap(),
                () => Export,
                path =>
                {
                    Log.Add("file.read");
                    return QuellBytes;
                },
                () => null,
                _ => EvalFehler is null ? EvalSchutz : throw EvalFehler,
                code => code is "BAB" or "BBA",
                code => code == "BAB" ? "Riss" : null,
                (segmentation, box, storedFramePath) =>
                {
                    Log.Add("mask.check");
                    MaskFramePath = storedFramePath;
                    return new Maske(MaskeGueltig, AngewandteMaske);
                });
    }

    private sealed class Maske(bool isValid, string rle) : IGoldSampleMaske
    {
        public bool IsValid => isValid;

        public void ApplyTo(TrainingSample sample)
        {
            if (!isValid)
                return;
            sample.SamMaskRle = rle;
            sample.SamMaskImageWidth = 1000;
            sample.SamMaskImageHeight = 500;
            sample.SamMaskAreaPixels = 100;
        }
    }

    private sealed class SampleStore(List<string> log) : ITrainingSampleStore
    {
        public List<TrainingSample> Store { get; } = new();
        public bool TryAddErgebnis { get; set; } = true;

        // Schreibfehler fuer Neuanlage (TryAddNewAsync) und Ersatz (ReplaceBySampleIdAsync).
        public Exception? Schreibfehler { get; set; }

        public Task<List<TrainingSample>> LoadAsync()
        {
            log.Add("sample.load");
            return Task.FromResult(Store.ToList());
        }

        public Task SaveAsync(List<TrainingSample> samples) => throw new NotSupportedException();
        public Task MergeAndSaveAsync(List<TrainingSample> samples) => throw new NotSupportedException();
        public Task<bool> RemoveBySampleIdAsync(string sampleId) => throw new NotSupportedException();

        public Task MergeOrUpdateAsync(IEnumerable<TrainingSample> samples)
        {
            log.Add("sample.mergeorupdate");
            return Task.CompletedTask;
        }

        public Task<bool> TryAddNewAsync(TrainingSample sample, CancellationToken ct = default)
        {
            log.Add("sample.tryadd");
            if (Schreibfehler is not null) throw Schreibfehler;
            if (TryAddErgebnis)
                Store.Add(sample);
            return Task.FromResult(TryAddErgebnis);
        }

        public Task<bool> ReplaceBySampleIdAsync(TrainingSample sample)
        {
            log.Add("sample.replace");
            if (Schreibfehler is not null) throw Schreibfehler;
            var removed = Store.RemoveAll(existing => existing.SampleId == sample.SampleId) > 0;
            if (removed)
                Store.Add(sample);
            return Task.FromResult(removed);
        }
    }

    private sealed class FrameStore(List<string> log) : ITrainingFrameStore
    {
        public Exception? Fehler { get; set; }
        public byte[]? LastBytes { get; private set; }

        public string GetFramesDir(string? customDir = null) => customDir ?? string.Empty;

        public Task<string?> ExtractAndStoreAsync(
            string ffmpegPath, string videoPath, double timeSeconds, string sampleId,
            string? framesDir = null, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<string?> StoreExistingAsync(string sourcePath, string? framesDir = null, CancellationToken ct = default)
        {
            log.Add("frame.store");
            if (Fehler is not null) throw Fehler;
            return Task.FromResult<string?>(Umgebung.GoldPfad);
        }

        public Task<string?> StoreBytesAsync(
            byte[] imageBytes, string extension, string? framesDir = null, CancellationToken ct = default)
        {
            log.Add("frame.bytes");
            if (Fehler is not null) throw Fehler;
            LastBytes = (byte[])imageBytes.Clone();
            return Task.FromResult<string?>(Umgebung.GoldPfad);
        }
    }

    private sealed class Indexer(List<string> log) : IKnowledgeBaseIndexer
    {
        public Exception? Fehler { get; set; }

        public Task<KbIndexOutcome> IndexAsync(IReadOnlyList<TrainingSample> samples, CancellationToken ct)
        {
            log.Add("kb.index");
            if (Fehler is not null) throw Fehler;
            return Task.FromResult(new KbIndexOutcome(samples.Select(s => s.SampleId).ToList(), new List<string>()));
        }

        public void Deindex(string sampleId) => log.Add("kb.deindex");
    }

    private sealed class TeacherStore(List<string> log) : ITeacherAnnotationStore
    {
        public List<TeacherAnnotation> Existing { get; } = new();
        public List<TeacherAnnotation> Appended { get; } = new();

        public string StoragePath => string.Empty;
        public string GetImagesDir() => string.Empty;
        public string GetLabelsDir() => string.Empty;
        public Task<int> CountAsync() => Task.FromResult(Appended.Count);

        public Task<List<TeacherAnnotation>> LoadAsync()
        {
            log.Add("teacher.load");
            return Task.FromResult(Existing.ToList());
        }

        public Task AppendAsync(params TeacherAnnotation[] annotations)
        {
            log.Add("teacher.append");
            Appended.AddRange(annotations);
            return Task.CompletedTask;
        }

        public Task<bool> DeleteAsync(string annotationId)
        {
            log.Add("teacher.delete");
            return Task.FromResult(Existing.RemoveAll(a => a.AnnotationId == annotationId) > 0);
        }
    }

    private sealed class ExportService(List<string> log) : ITrainingAnnotationExportService
    {
        public Exception? Fehler { get; set; }

        public Task<TrainingAnnotationResult> ExportAsync(
            string sourceFramePath, NormalizedBoundingBox bbox, string vsaCode, int classId, string baseName,
            CancellationToken ct = default)
        {
            log.Add("teacher.export");
            if (Fehler is not null) throw Fehler;
            return Task.FromResult(new TrainingAnnotationResult
            {
                Success = true,
                FullFramePath = @"C:\teacher\images\wb.png",
                CroppedRegionPath = @"C:\teacher\crops\wb.png",
                YoloAnnotationPath = @"C:\teacher\labels\wb.txt",
            });
        }
    }

    private sealed class ClassMap : IVsaYoloClassMapStore
    {
        public int GetClassId(string vsaCode) => 0;
        public int GetOrAddClassId(string vsaCode) => 7;
        public Dictionary<string, int> GetFullMap() => new();
        public Task ExportClassesTxtAsync(string outputPath) => Task.CompletedTask;
    }
}
