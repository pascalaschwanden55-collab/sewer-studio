using System.IO;
using System.Linq;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingSnapshotCaptureFactoryTests
{
    [Fact]
    public async Task CapturePngAsync_uses_service_to_capture_bytes()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "sewerstudio_snapshot_factory_tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var bytes = await CodingSnapshotCaptureFactory.CapturePngAsync(
                path =>
                {
                    File.WriteAllBytes(path, Enumerable.Range(0, 128).Select(i => (byte)i).ToArray());
                    return true;
                },
                tempDirectory: tempDir);

            Assert.NotNull(bytes);
            Assert.Equal(128, bytes.Length);
            Assert.Empty(Directory.GetFiles(tempDir));
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }
}
