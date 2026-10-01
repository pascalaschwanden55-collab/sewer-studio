using AuswertungPro.Next.Infrastructure.Ai.Training.PdfReview;
using AuswertungPro.Next.Infrastructure.Tests.Backup;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Ai.Training.PdfReview;

public sealed class TrainingPdfFolderDiscoveryServiceTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(
        Path.GetTempPath(),
        $"sewerstudio_pdf_folder_discovery_{Guid.NewGuid():N}");
    private readonly List<string> _createdDirectoryLinks = [];

    [Fact]
    public void Discover_ZweiRootsFindetPdfRekursivLaesstAndereDateienAusUndSortiertStabil()
    {
        var firstRoot = Path.Combine(_tempRoot, "z-root");
        var secondRoot = Path.Combine(_tempRoot, "a-root");
        var nested = Path.Combine(firstRoot, "unterordner");
        Directory.CreateDirectory(nested);
        Directory.CreateDirectory(secondRoot);

        var topPdf = WriteFile(firstRoot, "Zustand.PDF");
        var nestedPdf = WriteFile(nested, "Befund.pdf");
        var secondPdf = WriteFile(secondRoot, "Andere.pdf");
        WriteFile(firstRoot, "Hinweis.txt");
        WriteFile(nested, "Bild.jpg");

        var result = new TrainingPdfFolderDiscoveryService().Discover(
            [firstRoot, secondRoot],
            CancellationToken.None);

        var expected = new[] { topPdf, nestedPdf, secondPdf }
            .Select(Path.GetFullPath)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        Assert.Equal(expected, result.PdfPaths);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public void Discover_UeberlappendeRootsLiefertJedenVollpfadGenauEinmal()
    {
        var root = Path.Combine(_tempRoot, "root");
        var nested = Path.Combine(root, "unterordner");
        Directory.CreateDirectory(nested);

        var rootPdf = WriteFile(root, "oben.pdf");
        var nestedPdf = WriteFile(nested, "unten.PDF");

        var result = new TrainingPdfFolderDiscoveryService().Discover(
            [
                root,
                nested,
                root + Path.DirectorySeparatorChar,
                Path.Combine(root, "."),
            ],
            CancellationToken.None);

        var expected = new[] { rootPdf, nestedPdf }
            .Select(Path.GetFullPath)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        Assert.Equal(expected, result.PdfPaths);
        Assert.Equal(
            result.PdfPaths.Count,
            result.PdfPaths.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void Discover_FehlenderRootWirdAlsIssueGemeldetUndAndereRootsWerdenWeiterGelesen()
    {
        var missingRoot = Path.Combine(_tempRoot, "fehlt");
        var validRoot = Path.Combine(_tempRoot, "vorhanden");
        Directory.CreateDirectory(validRoot);
        var validPdf = WriteFile(validRoot, "haltung.pdf");

        var result = new TrainingPdfFolderDiscoveryService().Discover(
            [missingRoot, validRoot],
            CancellationToken.None);

        Assert.Equal([Path.GetFullPath(validPdf)], result.PdfPaths);
        Assert.Contains(
            result.Issues,
            issue => string.Equals(
                         issue.ReasonCode,
                         "root_missing",
                         StringComparison.Ordinal)
                     && PathsEqual(issue.Path, missingRoot));
    }

    [Fact]
    public void Discover_VerknuepfungInDerRootPfadketteWirdNichtDurchsucht()
    {
        var ancestor = Path.Combine(_tempRoot, "verknuepfter-vorfahr");
        var selectedRoot = Path.Combine(ancestor, "ausgewaehlt");
        Directory.CreateDirectory(selectedRoot);
        WriteFile(selectedRoot, "fremd.pdf");
        var service = new TrainingPdfFolderDiscoveryService(path =>
            PathsEqual(path, ancestor)
                ? FileAttributes.Directory | FileAttributes.ReparsePoint
                : File.GetAttributes(path));

        var result = service.Discover(
            [selectedRoot],
            CancellationToken.None);

        Assert.Empty(result.PdfPaths);
        Assert.Contains(
            result.Issues,
            issue => issue.ReasonCode == "reparse_point"
                     && PathsEqual(issue.Path, ancestor));
    }

    [Fact]
    public void Discover_VorDemLesenZumReparsePointGewordenerOrdnerWirdNichtBetreten()
    {
        var root = Path.Combine(_tempRoot, "root");
        var child = Path.Combine(root, "wechselt");
        Directory.CreateDirectory(child);
        WriteFile(child, "fremd.pdf");
        var childAttributeReads = 0;
        var service = new TrainingPdfFolderDiscoveryService(path =>
        {
            if (!PathsEqual(path, child))
                return File.GetAttributes(path);

            childAttributeReads++;
            return childAttributeReads == 1
                ? FileAttributes.Directory
                : FileAttributes.Directory | FileAttributes.ReparsePoint;
        });

        var result = service.Discover(
            [root],
            CancellationToken.None);

        Assert.Equal(2, childAttributeReads);
        Assert.Empty(result.PdfPaths);
        Assert.Contains(
            result.Issues,
            issue => issue.ReasonCode == "reparse_point"
                     && PathsEqual(issue.Path, child));
    }

    [JunctionFact]
    public void Discover_VerzeichnisverknuepfungWirdNichtBetreten()
    {
        var root = Path.Combine(_tempRoot, "root");
        var foreignRoot = Path.Combine(_tempRoot, "fremd");
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(foreignRoot);
        var ownPdf = WriteFile(root, "eigen.pdf");
        var foreignPdf = WriteFile(foreignRoot, "fremd.pdf");
        var link = Path.Combine(root, "verknuepfung");
        JunctionTestSupport.CreateDirectoryLink(link, foreignRoot);
        _createdDirectoryLinks.Add(link);

        var result = new TrainingPdfFolderDiscoveryService().Discover(
            [root],
            CancellationToken.None);

        Assert.Equal([Path.GetFullPath(ownPdf)], result.PdfPaths);
        Assert.DoesNotContain(
            result.PdfPaths,
            path => PathsEqual(path, foreignPdf));
        Assert.Contains(
            result.Issues,
            issue => PathsEqual(issue.Path, link));
    }

    public void Dispose()
    {
        foreach (var link in _createdDirectoryLinks.OrderByDescending(path => path.Length))
        {
            try
            {
                if (Directory.Exists(link))
                    Directory.Delete(link);
            }
            catch
            {
                // Nur Testaufraeumen.
            }
        }

        try
        {
            if (Directory.Exists(_tempRoot))
                Directory.Delete(_tempRoot, recursive: true);
        }
        catch
        {
            // Nur Testaufraeumen.
        }
    }

    private static string WriteFile(string folder, string fileName)
    {
        var path = Path.Combine(folder, fileName);
        File.WriteAllText(path, fileName);
        return path;
    }

    private static bool PathsEqual(string left, string right)
        => string.Equals(
            Path.GetFullPath(left),
            Path.GetFullPath(right),
            StringComparison.OrdinalIgnoreCase);
}
