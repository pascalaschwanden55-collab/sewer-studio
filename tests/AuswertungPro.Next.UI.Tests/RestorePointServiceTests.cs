using System;
using System.IO;
using AuswertungPro.Next.UI.Services;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class RestorePointServiceTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(
        Path.GetTempPath(),
        $"RestorePointServiceTests_{Guid.NewGuid():N}");

    public RestorePointServiceTests()
    {
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_tempDir, recursive: true);
        }
        catch
        {
            // Test cleanup is best effort.
        }
    }

    [Fact]
    public void TryCreate_CopiesExistingSettingsIntoSanitizedScope()
    {
        var sourcePath = Path.Combine(_tempDir, "settings.json");
        var restoreRoot = Path.Combine(_tempDir, "restore-points");
        File.WriteAllText(sourcePath, "{\"language\":\"de\"}");

        RestorePointService.TryCreate(sourcePath, restoreRoot, "settings:local");

        var scopeDirectory = Path.Combine(restoreRoot, "settings_local");
        var snapshot = Assert.Single(Directory.GetFiles(scopeDirectory));
        Assert.EndsWith("_settings.json", snapshot, StringComparison.Ordinal);
        Assert.Equal(File.ReadAllText(sourcePath), File.ReadAllText(snapshot));
    }
}
