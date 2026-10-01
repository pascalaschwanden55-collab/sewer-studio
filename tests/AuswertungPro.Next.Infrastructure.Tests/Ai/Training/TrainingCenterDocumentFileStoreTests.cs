using System.Text.Json;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.Training;

namespace AuswertungPro.Next.Infrastructure.Tests.Ai.Training;

public sealed class TrainingCenterDocumentFileStoreTests
{
    [Fact]
    public async Task Bestehendes_Json_bleibt_lesbar_und_Status_bleibt_numerisch()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "training_center.json");
        await File.WriteAllTextAsync(path,
            """
            {
              "Cases": [
                {
                  "CaseId": "Fall-1",
                  "FolderPath": "C:\\Training\\Fall1",
                  "VideoPath": "video.mp4",
                  "ProtocolPath": "protokoll.pdf",
                  "InspectionDate": "2026-08-14T00:00:00Z",
                  "Status": 2,
                  "CreatedUtc": "2026-08-13T00:00:00Z"
                }
              ],
              "RootFolders": ["C:\\Training"],
              "UpdatedUtc": "2026-08-14T01:00:00Z"
            }
            """);
        ITrainingCenterDocumentStore store = new TrainingCenterDocumentFileStore(path);

        var loaded = await store.LoadAsync();
        var trainingCase = Assert.Single(loaded.Cases);
        Assert.Equal("Fall-1", trainingCase.CaseId);
        Assert.Equal(2, trainingCase.Status);

        await store.SaveAsync(loaded);

        using var json = JsonDocument.Parse(await File.ReadAllTextAsync(path));
        var storedCase = json.RootElement.GetProperty("Cases")[0];
        Assert.Equal(JsonValueKind.Number, storedCase.GetProperty("Status").ValueKind);
        Assert.Equal(2, storedCase.GetProperty("Status").GetInt32());
        Assert.True(File.Exists(path + ".bak"));
        Assert.Empty(Directory.EnumerateFiles(temp.Path, "*.tmp"));
    }

    [Fact]
    public async Task Korrupte_Hauptdatei_wird_gesichert_und_Bak_geladen()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "training_center.json");
        await File.WriteAllTextAsync(path, "{ungueltig");
        await File.WriteAllTextAsync(path + ".bak",
            """
            {
              "Cases": [{ "CaseId": "aus-backup", "Status": 1 }],
              "RootFolders": [],
              "UpdatedUtc": "2026-08-14T01:00:00Z"
            }
            """);
        ITrainingCenterDocumentStore store = new TrainingCenterDocumentFileStore(path);

        var loaded = await store.LoadAsync();

        Assert.Equal("aus-backup", Assert.Single(loaded.Cases).CaseId);
        Assert.Single(Directory.EnumerateFiles(temp.Path, "training_center.json.bad_*"));
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "training-center-document-" + Guid.NewGuid().ToString("N"));

        public TempDirectory()
            => Directory.CreateDirectory(Path);

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path))
                    Directory.Delete(Path, recursive: true);
            }
            catch
            {
                // Testaufraeumen ist best effort.
            }
        }
    }
}
