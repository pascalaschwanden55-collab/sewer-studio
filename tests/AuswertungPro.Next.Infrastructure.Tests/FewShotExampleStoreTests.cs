using AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;
using AuswertungPro.Next.Infrastructure.Ai.Training;

namespace AuswertungPro.Next.Infrastructure.Tests;

[Collection("EnvironmentVars")]
public sealed class FewShotExampleStoreTests
{
    [Fact]
    public async Task SaveAsync_IsAtomic_AndKeepsBackup()
    {
        await WithTempKnowledgeRoot(async () =>
        {
            var store = new FewShotExampleStore();
            await store.AddExampleAsync(
                imageBytes: [1, 2, 3],
                imageExtension: ".png",
                vsaCode: "BAB",
                description: "Laengsriss sichtbar",
                clockPosition: "12",
                meterPosition: 1.2,
                material: "Beton",
                profile: "DN300",
                source: "test");

            await store.AddExampleAsync(
                imageBytes: [4, 5, 6],
                imageExtension: ".png",
                vsaCode: "BBA",
                description: "Ablagerung sichtbar",
                clockPosition: "6",
                meterPosition: 2.4,
                material: "Beton",
                profile: "DN300",
                source: "test");

            var path = Path.Combine(KnowledgeBasePaths.GetRoot(), "fewshot_examples.json");

            Assert.True(File.Exists(path));
            Assert.True(File.Exists(path + ".bak"));
            Assert.Empty(Directory.EnumerateFiles(KnowledgeBasePaths.GetRoot(), "*.tmp"));
        });
    }

    [Fact]
    public async Task RemoveAsync_WhenImageDeleteFails_ReportsBestEffortError()
    {
        if (!OperatingSystem.IsWindows())
            return;

        await WithTempKnowledgeRoot(async () =>
        {
            string? captured = null;
            var store = new FewShotExampleStore(message => captured = message);
            var example = await store.AddExampleAsync(
                imageBytes: [1, 2, 3],
                imageExtension: ".png",
                vsaCode: "BAB",
                description: "Laengsriss sichtbar",
                clockPosition: "12",
                meterPosition: 1.2,
                material: "Beton",
                profile: "DN300",
                source: "test");

            var imagePath = store.GetImagePath(example);
            await using var locked = new FileStream(
                imagePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.None);

            await store.RemoveAsync(example.Id);

            Assert.NotNull(captured);
            Assert.Contains("FewShot Bild loeschen", captured);
            Assert.Contains("BAB", captured);
            Assert.Empty(store.Examples);
        });
    }

    private static async Task WithTempKnowledgeRoot(Func<Task> body)
    {
        var previous = Environment.GetEnvironmentVariable("SEWERSTUDIO_KNOWLEDGE_ROOT");
        var temp = Path.Combine(Path.GetTempPath(), "sewer-fewshot-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        Environment.SetEnvironmentVariable("SEWERSTUDIO_KNOWLEDGE_ROOT", temp);
        KnowledgeBasePaths.InvalidateCache();
        try
        {
            await body();
        }
        finally
        {
            Environment.SetEnvironmentVariable("SEWERSTUDIO_KNOWLEDGE_ROOT", previous);
            KnowledgeBasePaths.InvalidateCache();
            try { Directory.Delete(temp, recursive: true); } catch { }
        }
    }
}
