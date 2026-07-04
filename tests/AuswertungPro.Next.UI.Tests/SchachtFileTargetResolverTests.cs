using System.IO;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.DataPage;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SchachtFileTargetResolverTests
{
    [Fact]
    public void ResolvePdfPath_prefers_pdf_path_field()
    {
        using var temp = new TempDir();
        var pdf = temp.CreateFile("Schaechte_Verteilt", "22149", "22149.pdf");
        var link = temp.CreateFile("Schaechte_Verteilt", "22149", "link.pdf");
        var projectFile = temp.CreateFile("Projektdateien", "projekt.json");

        var record = new SchachtRecord();
        record.SetFieldValue(FieldKeys.PdfPath, Path.GetRelativePath(temp.Path, pdf));
        record.SetFieldValue(FieldKeys.Link, Path.GetRelativePath(temp.Path, link));

        var target = SchachtFileTargetResolver.ResolvePdfPath(record, projectFile);

        Assert.Equal(Path.GetFullPath(pdf), target);
    }

    [Fact]
    public void ResolvePdfPath_uses_pdf_link_when_pdf_path_is_missing()
    {
        using var temp = new TempDir();
        var link = temp.CreateFile("Schaechte_Verteilt", "22149", "link.pdf");
        var projectFile = temp.CreateFile("Projektdateien", "projekt.json");

        var record = new SchachtRecord();
        record.SetFieldValue(FieldKeys.Link, Path.GetRelativePath(temp.Path, link));

        var target = SchachtFileTargetResolver.ResolvePdfPath(record, projectFile);

        Assert.Equal(Path.GetFullPath(link), target);
    }

    [Fact]
    public void ResolveExplorerTarget_uses_directory_candidate_when_no_pdf_exists()
    {
        using var temp = new TempDir();
        var directory = temp.CreateDirectory("Schaechte_Verteilt", "22149");
        var projectFile = temp.CreateFile("Projektdateien", "projekt.json");

        var record = new SchachtRecord();
        record.SetFieldValue(FieldKeys.Link, Path.GetRelativePath(temp.Path, directory));

        var target = SchachtFileTargetResolver.ResolveExplorerTarget(record, projectFile);

        Assert.Equal(Path.GetFullPath(directory), target);
    }

    [Fact]
    public void ResolveExplorerTarget_splits_semicolon_pdf_all_candidates()
    {
        using var temp = new TempDir();
        var pdf = temp.CreateFile("Schaechte_Verteilt", "22149", "22149.pdf");
        var projectFile = temp.CreateFile("Projektdateien", "projekt.json");

        var record = new SchachtRecord();
        record.SetFieldValue(FieldKeys.PdfAll, "fehlt.pdf;" + Path.GetRelativePath(temp.Path, pdf));

        var target = SchachtFileTargetResolver.ResolveExplorerTarget(record, projectFile);

        Assert.Equal(Path.GetFullPath(pdf), target);
    }

    [Fact]
    public void ResolveExplorerTarget_returns_null_for_missing_paths()
    {
        using var temp = new TempDir();
        var projectFile = temp.CreateFile("Projektdateien", "projekt.json");

        var record = new SchachtRecord();
        record.SetFieldValue(FieldKeys.Link, "nicht-vorhanden.pdf");

        Assert.Null(SchachtFileTargetResolver.ResolveExplorerTarget(record, projectFile));
        Assert.Null(SchachtFileTargetResolver.ResolveExplorerTarget(record, null));
    }

    private sealed class TempDir : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "schacht-target-" + Guid.NewGuid().ToString("N"));

        public TempDir()
        {
            Directory.CreateDirectory(Path);
        }

        public string CreateDirectory(params string[] parts)
        {
            var path = System.IO.Path.Combine(new[] { Path }.Concat(parts).ToArray());
            Directory.CreateDirectory(path);
            return path;
        }

        public string CreateFile(params string[] parts)
        {
            var path = System.IO.Path.Combine(new[] { Path }.Concat(parts).ToArray());
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "x");
            return path;
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path))
                    Directory.Delete(Path, recursive: true);
            }
            catch
            {
                // best effort cleanup
            }
        }
    }
}
