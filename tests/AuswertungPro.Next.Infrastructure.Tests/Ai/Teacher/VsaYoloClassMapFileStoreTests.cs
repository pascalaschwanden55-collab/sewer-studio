using System.Text.Json;
using AuswertungPro.Next.Application.Ai.Teacher;
using AuswertungPro.Next.Infrastructure.Ai.Teacher;

namespace AuswertungPro.Next.Infrastructure.Tests.Ai.Teacher;

public sealed class VsaYoloClassMapFileStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "VsaYoloClassMapFileStoreTests_" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Klassenkarte_bewahrt_feste_IDs_und_neue_Klasse_ueber_Dateirundlauf()
    {
        Directory.CreateDirectory(_root);
        var mapPath = Path.Combine(_root, "yolo_class_map.json");
        IVsaYoloClassMapStore first = new VsaYoloClassMapFileStore(mapPath);

        Assert.Equal(15, first.GetClassId("BAA"));
        var newId = first.GetClassId("BZZ-extra");
        Assert.Equal(16, newId);

        IVsaYoloClassMapStore reloaded = new VsaYoloClassMapFileStore(mapPath);
        Assert.Equal(newId, reloaded.GetClassId("BZZ"));

        var stored = JsonSerializer.Deserialize<Dictionary<string, int>>(
            File.ReadAllText(mapPath));
        Assert.Equal(newId, stored!["BZZ"]);
        Assert.Contains("BZZ", File.ReadAllLines(Path.Combine(_root, "classes.txt")));

        var exportPath = Path.Combine(_root, "export", "classes.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(exportPath)!);
        await reloaded.ExportClassesTxtAsync(exportPath);
        Assert.Contains("BAA", File.ReadAllLines(exportPath));
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }
        catch
        {
            // Test-Aufraeumen darf das eigentliche Ergebnis nicht verdecken.
        }
    }
}
