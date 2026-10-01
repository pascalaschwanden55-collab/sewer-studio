using System.IO;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingStudioPdfProtectionLoaderTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(
        Path.GetTempPath(),
        "SewerStudioPdfProtection",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void LoadPdfProtectionSnapshot_configured_hash_only_root_fails_closed()
    {
        WriteManifest();

        var error = Assert.Throws<InvalidDataException>(
            () => TrainingStudioWindowDependencyFactory
                .LoadPdfProtectionSnapshot(_tempRoot));

        Assert.Contains("Haltungskennungen", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("nicht importiert", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LoadPdfProtectionSnapshot_configured_root_requires_real_holding_keys()
    {
        WriteManifest();
        File.WriteAllText(
            Path.Combine(_tempRoot, "_candidates.json"),
            """
            [
              { "haltung_key": "07.638910-1367" }
            ]
            """);

        var snapshot = TrainingStudioWindowDependencyFactory
            .LoadPdfProtectionSnapshot(_tempRoot);

        Assert.Contains(new string('a', 64), snapshot.ImageHashes);
        Assert.Contains("638910-1367", snapshot.HoldingKeys);
    }

    [Fact]
    public void LoadPdfProtectionSnapshot_empty_root_keeps_explicit_disable_compatible()
    {
        var snapshot = TrainingStudioWindowDependencyFactory
            .LoadPdfProtectionSnapshot(string.Empty);

        Assert.Empty(snapshot.ImageHashes);
        Assert.Empty(snapshot.HoldingKeys);
    }

    private void WriteManifest()
    {
        Directory.CreateDirectory(_tempRoot);
        File.WriteAllText(
            Path.Combine(_tempRoot, "_manifest.json"),
            $$"""
              {
                "hashes": {
                  "images/frame.png": { "sha256": "{{new string('a', 64)}}" }
                }
              }
              """);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, recursive: true);
    }
}
