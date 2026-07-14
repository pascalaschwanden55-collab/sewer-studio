using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Infrastructure.Import;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

public sealed class StoredImportFileServiceInstanceTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(
        Path.GetTempPath(),
        $"StoredImportFileServiceInstanceTests_{Guid.NewGuid():N}");

    [Fact]
    public void Store_copies_source_and_registers_relative_project_path_through_contract()
    {
        Directory.CreateDirectory(_tempRoot);
        var projectPath = Path.Combine(_tempRoot, "projekt.json");
        File.WriteAllText(projectPath, "{}");
        var source = Path.Combine(_tempRoot, "quelle.pdf");
        File.WriteAllText(source, "PDF-Inhalt");
        var metadata = new Dictionary<string, string>();
        IStoredImportFileService service = new StoredImportFileService();

        var result = service.Store(projectPath, metadata, "PDF", [source]);

        Assert.False(result.MissingProjectPath);
        var relativePath = Assert.Single(result.StoredRelativePaths);
        Assert.Equal(Path.Combine("Imports", "PDF", "quelle.pdf"), relativePath);
        Assert.Equal("PDF-Inhalt", File.ReadAllText(Path.Combine(_tempRoot, relativePath)));
        Assert.Equal(
            [relativePath],
            StoredImportFileRegistry.Load(metadata, "PDF_StoredFiles"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, recursive: true);
    }
}
