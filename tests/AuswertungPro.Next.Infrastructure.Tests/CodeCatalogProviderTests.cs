using AuswertungPro.Next.Application.Protocol;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class CodeCatalogProviderTests
{
    [Fact]
    public void CreatesCatalogFile_WhenMissing()
    {
        var root = Path.Combine(Path.GetTempPath(), "AuswertungProTests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(root, "Data", "vsa_codes.json");

        try
        {
            var provider = new JsonCodeCatalogProvider(path);

            Assert.True(File.Exists(path));
            Assert.Empty(provider.GetAll());
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void SavesAndReloadsCodes_AndDetectsDuplicates()
    {
        var root = Path.Combine(Path.GetTempPath(), "AuswertungProTests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(root, "Data", "vsa_codes.json");

        try
        {
            var provider = new JsonCodeCatalogProvider(path);

            provider.Save(new List<CodeDefinition>
            {
                new() { Code = " bab ", Title = "Riss", Group = "Risse" },
                new() { Code = "BCA", Title = "Einlauf", Group = "" }
            });

            var reloaded = new JsonCodeCatalogProvider(path);
            Assert.Equal(2, reloaded.GetAll().Count);
            Assert.True(reloaded.TryGet("BAB", out var bab));
            Assert.Equal("Riss", bab.Title);
            Assert.Contains("BCA", reloaded.AllowedCodes());

            var validation = reloaded.Validate(new List<CodeDefinition>
            {
                new() { Code = "ABC", Title = "A", Group = "G1" },
                new() { Code = "abc", Title = "B", Group = "G1" }
            });

            Assert.Contains(validation, msg => msg.Contains("Duplikat-Code", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }
}
