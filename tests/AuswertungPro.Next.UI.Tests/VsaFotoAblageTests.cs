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
        Assert.False(File.Exists(snapshot), "Das Foto liegt noch im Temp-Ordner.");
        Assert.Equal(
            Path.Combine(haltungsordner, "Fotos"),
            Path.GetDirectoryName(result.PhotoPath));
        Assert.Equal([result.PhotoPath], fotoPfade);
        Assert.Equal([result.PhotoPath], originalPfade);
        Assert.True(File.Exists(result.PhotoPath));
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
