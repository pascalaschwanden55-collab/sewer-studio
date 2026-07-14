using System.IO;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DropdownOptionsStoreTests
{
    [Fact]
    public void Instanz_speichert_und_laesst_sich_von_anderen_Pfaden_isolieren()
    {
        var root = Path.Combine(Path.GetTempPath(), $"SewerStudio_Dropdowns_{Guid.NewGuid():N}");
        var firstDir = Path.Combine(root, "first");
        var secondDir = Path.Combine(root, "second");
        try
        {
            var first = CreateStore(firstDir);
            var second = CreateStore(secondDir);

            first.SaveSanierenOptions(["Vielleicht"]);

            Assert.Equal(["Vielleicht"], first.LoadSanierenOptions());
            Assert.Equal(["Ja", "Nein"], second.LoadSanierenOptions());
            Assert.True(File.Exists(Path.Combine(firstDir, "sanieren.json")));
            Assert.False(File.Exists(Path.Combine(secondDir, "sanieren.json")));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Beschaedigte_Datei_faellt_auf_sichere_Standardwerte_zurueck()
    {
        var root = Path.Combine(Path.GetTempPath(), $"SewerStudio_Dropdowns_{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, "sanieren.json"), "{kaputt");
            var store = CreateStore(root);

            Assert.Equal(["Ja", "Nein"], store.LoadSanierenOptions());
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Parallele_Speicherungen_hinterlassen_eine_gueltige_Liste()
    {
        var root = Path.Combine(Path.GetTempPath(), $"SewerStudio_Dropdowns_{Guid.NewGuid():N}");
        try
        {
            var store = CreateStore(root);

            await Task.WhenAll(Enumerable.Range(0, 30).Select(index =>
                Task.Run(() => store.SaveSanierenOptions([$"Wert-{index}"]))));

            var saved = store.LoadSanierenOptions();
            Assert.Single(saved);
            Assert.StartsWith("Wert-", saved[0], StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    private static FileDropdownOptionsStore CreateStore(string optionsDir)
        => new(
            optionsDir,
            Path.Combine(optionsDir, "legacy-files"),
            Path.Combine(optionsDir, "legacy.json"));
}
