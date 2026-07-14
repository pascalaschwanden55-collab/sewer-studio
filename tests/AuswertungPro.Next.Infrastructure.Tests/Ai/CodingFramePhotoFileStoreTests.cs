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
}
