using System.IO;
using AuswertungPro.Next.Application.UseCases.VsaFotos;
using AuswertungPro.Next.UI.Ai.Vsa;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Arbeitet bewusst mit echten Dateien: Der Zweck dieser Klasse ist, dass das
/// Bild den Temp-Ordner wirklich verlaesst.
/// </summary>
public sealed class VsaFotoAblageTests : IDisposable
{
    private readonly string _wurzel = Path.Combine(
        Path.GetTempPath(),
        "vsa_ablage_test_" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void Uebernehme_verschiebt_das_bild_aus_dem_temp_ordner_neben_das_video()
    {
        var quelle = Path.Combine(_wurzel, "coding_live_abc.png");
        var videoPfad = Path.Combine(_wurzel, "Haltung", "video.mp4");
        Directory.CreateDirectory(_wurzel);
        Directory.CreateDirectory(Path.GetDirectoryName(videoPfad)!);
        File.WriteAllBytes(quelle, [1, 2, 3]);

        var ziel = VsaFotoAblage.Uebernehme(quelle, videoPfad, photoIndex: 0);

        Assert.True(File.Exists(ziel), "Das Foto fehlt am neuen Ort.");
        Assert.False(File.Exists(quelle), "Das Foto liegt noch im Temp-Ordner.");
        Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(ziel));
        Assert.Equal(
            Path.Combine(_wurzel, "Haltung", "Fotos"),
            Path.GetDirectoryName(ziel));
    }

    [Fact]
    public void Uebernehme_behaelt_die_quelle_wenn_der_zielordner_nicht_erreichbar_ist()
    {
        var quelle = Path.Combine(_wurzel, "coding_live_def.png");
        Directory.CreateDirectory(_wurzel);
        File.WriteAllBytes(quelle, [9]);

        var ziel = VsaFotoAblage.Uebernehme(
            quelle,
            videoPath: Path.Combine(_wurzel, "nicht<>erlaubt", "video.mp4"),
            photoIndex: 0);

        Assert.Equal(quelle, ziel);
        Assert.True(File.Exists(quelle), "Das Foto darf bei einem Fehler nicht verloren gehen.");
    }


    [Fact]
    public async Task CaptureWithDefaults_laesst_kein_foto_im_temp_ordner_zurueck()
    {
        var haltungsordner = Path.Combine(_wurzel, "Haltung");
        var videoPfad = Path.Combine(haltungsordner, "video.mp4");
        var snapshot = Path.Combine(_wurzel, "coding_live_ghi.png");
        Directory.CreateDirectory(haltungsordner);
        File.WriteAllBytes(videoPfad, [0]);
        File.WriteAllBytes(snapshot, [4, 5, 6]);

        var fotoPfade = new List<string>();
        var originalPfade = new List<string>();

        var result = await VsaCodeExplorerPhotoCaptureWorkflow.CaptureWithDefaultsAsync(
            photoIndex: 0,
            photoPaths: fotoPfade,
            originalPhotoPaths: originalPfade,
            liveSnapshotProvider: () => snapshot,
            videoPath: videoPfad,
            currentVideoTime: null,
            timeText: null,
            cancellationToken: CancellationToken.None);

        Assert.Equal(VsaCodeExplorerPhotoCaptureOutcome.Captured, result.Outcome);
        Assert.NotNull(result.PhotoPath);
        Assert.False(File.Exists(snapshot), "Das Foto liegt noch im Temp-Ordner.");
        Assert.Equal(
            Path.Combine(haltungsordner, "Fotos"),
            Path.GetDirectoryName(result.PhotoPath));
        Assert.Equal([result.PhotoPath], fotoPfade);
        Assert.Equal([result.PhotoPath], originalPfade);
        Assert.True(File.Exists(result.PhotoPath));
    }

    /// <summary>
    /// Deepscan 02.10.2026, R3: Echte Dateien, echter Temp-Ordner. Ohne Video bleibt das
    /// Foto erhalten, aber das Ergebnis sagt deutlich, dass es nur vorlaeufig liegt.
    /// </summary>
    [Fact]
    public async Task CaptureWithDefaults_meldet_ohne_video_ein_nur_vorlaeufiges_foto()
    {
        var snapshot = Path.Combine(Path.GetTempPath(), "coding_live_" + Guid.NewGuid().ToString("N") + ".png");
        File.WriteAllBytes(snapshot, [7, 8, 9]);
        string? abgelegt = null;
        try
        {
            var result = await VsaCodeExplorerPhotoCaptureWorkflow.CaptureWithDefaultsAsync(
                photoIndex: 0,
                photoPaths: new List<string>(),
                originalPhotoPaths: new List<string>(),
                liveSnapshotProvider: () => snapshot,
                videoPath: null,
                currentVideoTime: null,
                timeText: null,
                cancellationToken: CancellationToken.None);
            abgelegt = result.PhotoPath;

            Assert.Equal(VsaCodeExplorerPhotoCaptureOutcome.Captured, result.Outcome);
            Assert.True(File.Exists(result.PhotoPath), "Das Foto darf nicht verloren gehen.");
            Assert.True(result.NurVorlaeufig);
            Assert.Contains("Temp-Ordner", result.Message);
        }
        finally
        {
            foreach (var datei in new[] { snapshot, abgelegt })
                if (datei is not null && File.Exists(datei))
                    File.Delete(datei);
        }
    }

    [Fact]
    public void TempHinweis_nennt_ein_video_das_selbst_im_temp_ordner_liegt()
    {
        static bool ImTemp(string pfad) => pfad.StartsWith(@"C:\Temp\", StringComparison.OrdinalIgnoreCase);

        var hinweis = VsaFotoTempHinweis.Fuer(
            @"C:\Temp\Haltung\Fotos\vsa_foto1.png",
            @"C:\Temp\Haltung\video.mp4",
            ImTemp);

        Assert.NotNull(hinweis);
        Assert.Contains("Das Video liegt selbst im Temp-Ordner", hinweis);
        Assert.Null(VsaFotoTempHinweis.Fuer(@"D:\Projekte\Fotos\vsa_foto1.png", @"D:\Projekte\video.mp4", ImTemp));
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_wurzel))
                Directory.Delete(_wurzel, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}
