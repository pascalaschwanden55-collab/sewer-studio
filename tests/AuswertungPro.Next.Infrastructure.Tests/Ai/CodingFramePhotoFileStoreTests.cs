using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Infrastructure.Ai;

namespace AuswertungPro.Next.Infrastructure.Tests.Ai;

public sealed class CodingFramePhotoFileStoreTests
{
    [Fact]
    public void Dienst_speichert_Frame_und_verknuepft_das_Foto()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "CodingFramePhotoFileStoreTests_" + Guid.NewGuid().ToString("N"));
        try
        {
            ICodingFramePhotoStore store = new CodingFramePhotoFileStore();
            var entry = new ProtocolEntry
            {
                Code = "BAB",
                MeterStart = 2.5,
                Zeit = TimeSpan.FromSeconds(7)
            };
            byte[] frameBytes = [1, 2, 3, 4];

            var savedPath = store.AttachAnalyzedFramePhoto(
                entry,
                frameBytes,
                photoRoot: root);

            Assert.NotNull(savedPath);
            Assert.Equal(frameBytes, File.ReadAllBytes(savedPath!));
            Assert.Equal(savedPath, Assert.Single(entry.FotoPaths));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>
    /// Deepscan 02.10.2026, R3: Ohne Video und ohne Fotoordner schrieb der Dienst still
    /// nach %TEMP%\SewerStudio\coding_ai_frames und verknuepfte den Pfad mit dem Befund.
    /// Befundfotos gehoeren nie in den Temp-Ordner; dann entsteht lieber kein Foto.
    /// </summary>
    [Fact]
    public void Dienst_legt_ohne_video_kein_foto_im_temp_ordner_ab()
    {
        ICodingFramePhotoStore store = new CodingFramePhotoFileStore();
        var entry = new ProtocolEntry
        {
            Code = "BAB",
            MeterStart = 2.5,
            Zeit = TimeSpan.FromSeconds(7)
        };
        var tempOrdner = Path.Combine(Path.GetTempPath(), "SewerStudio", "coding_ai_frames");
        string[] Eigene() => Directory.Exists(tempOrdner)
            ? Directory.GetFiles(tempOrdner, $"*_{entry.EntryId:N}_ai*.png")
            : [];

        try
        {
            var savedPath = store.AttachAnalyzedFramePhoto(entry, [1, 2, 3, 4]);

            Assert.Null(savedPath);
            Assert.Empty(entry.FotoPaths);
            Assert.Empty(Eigene());
        }
        finally
        {
            foreach (var datei in Eigene())
                File.Delete(datei);
        }
    }
}
