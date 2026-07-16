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

    [Fact]
    public void Store_reports_missing_file_and_continues_with_remaining_files()
    {
        Directory.CreateDirectory(_tempRoot);
        var projectPath = Path.Combine(_tempRoot, "projekt.json");
        File.WriteAllText(projectPath, "{}");
        var missing = Path.Combine(_tempRoot, "fehlt.pdf");
        var valid = Path.Combine(_tempRoot, "vorhanden.pdf");
        File.WriteAllText(valid, "PDF-Inhalt");
        var metadata = new Dictionary<string, string>();
        IStoredImportFileService service = new StoredImportFileService();

        var result = service.Store(projectPath, metadata, "PDF", [missing, valid]);

        var error = Assert.Single(result.Errors);
        Assert.Equal(missing, error.SourcePath);
        Assert.Contains("nicht gefunden", error.Message, StringComparison.OrdinalIgnoreCase);
        var relativePath = Assert.Single(result.StoredRelativePaths);
        Assert.True(File.Exists(Path.Combine(_tempRoot, relativePath)));
        Assert.Equal(
            [relativePath],
            StoredImportFileRegistry.Load(metadata, "PDF_StoredFiles"));
    }

    [Fact]
    public void Store_reports_copy_error_and_continues_with_next_file()
    {
        Directory.CreateDirectory(_tempRoot);
        var projectPath = Path.Combine(_tempRoot, "projekt.json");
        File.WriteAllText(projectPath, "{}");
        var locked = Path.Combine(_tempRoot, "gesperrt.pdf");
        var valid = Path.Combine(_tempRoot, "vorhanden.pdf");
        File.WriteAllText(locked, "gesperrt");
        File.WriteAllText(valid, "vorhanden");
        var metadata = new Dictionary<string, string>();
        IStoredImportFileService service = new StoredImportFileService();

        using var lockStream = new FileStream(
            locked,
            FileMode.Open,
            FileAccess.ReadWrite,
            FileShare.None);
        var result = service.Store(projectPath, metadata, "PDF", [locked, valid]);

        var error = Assert.Single(result.Errors);
        Assert.Equal(locked, error.SourcePath);
        var relativePath = Assert.Single(result.StoredRelativePaths);
        Assert.Equal(Path.Combine("Imports", "PDF", "vorhanden.pdf"), relativePath);
        Assert.True(File.Exists(Path.Combine(_tempRoot, relativePath)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("..")]
    [InlineData("PDF\\Unterordner")]
    public void Store_rejects_unsafe_import_folder_names(string importKind)
    {
        IStoredImportFileService service = new StoredImportFileService();

        Assert.Throws<ArgumentException>(() => service.Store(
            Path.Combine(_tempRoot, "projekt.json"),
            new Dictionary<string, string>(),
            importKind,
            Array.Empty<string>()));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, recursive: true);
    }
}
