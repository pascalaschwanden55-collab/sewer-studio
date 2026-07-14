using System.IO;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SchachtProtocolFolderImportPolicyTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        $"SewerStudio_FolderProtocol_{Guid.NewGuid():N}");

    [Fact]
    public void FindPdfFiles_DurchsuchtUnterordner_UndIgnoriertProjektziel()
    {
        var source = Path.Combine(_root, "Quelle");
        var nested = Path.Combine(source, "A", "B");
        var projectDistribution = Path.Combine(source, "Projekt", "Schaechte_Verteilt");
        Directory.CreateDirectory(nested);
        Directory.CreateDirectory(projectDistribution);
        File.WriteAllText(Path.Combine(source, "oben.pdf"), "test");
        File.WriteAllText(Path.Combine(nested, "unten.pdf"), "test");
        File.WriteAllText(Path.Combine(nested, "kein-pdf.txt"), "test");
        File.WriteAllText(Path.Combine(projectDistribution, "bereits-importiert.pdf"), "test");

        var files = SchachtProtocolFolderImportPolicy.FindPdfFiles(source, projectDistribution);

        Assert.Equal(2, files.Count);
        Assert.Contains(files, path => path.EndsWith("oben.pdf", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(files, path => path.EndsWith("unten.pdf", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(files, path => path.Contains("bereits-importiert", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void FindPdfFiles_LiestProjektziel_WennEsDirektAusgewaehltWurde()
    {
        var projectDistribution = Path.Combine(_root, "Projekt", "Schaechte_Verteilt");
        var shaftFolder = Path.Combine(projectDistribution, "80454");
        Directory.CreateDirectory(shaftFolder);
        var pdf = Path.Combine(shaftFolder, "20240920_80454.pdf");
        File.WriteAllText(pdf, "test");

        var files = SchachtProtocolFolderImportPolicy.FindPdfFiles(
            projectDistribution,
            Array.Empty<string>());

        Assert.Equal(new[] { pdf }, files);
        Assert.True(SchachtProtocolFolderImportPolicy.IsSameOrBelow(shaftFolder, projectDistribution));
    }

    [Fact]
    public void SelectCurrentPerShaft_NimmtDasNeuesteProtokolldatum()
    {
        var older = Candidate("S-23", "10.01.2024", "20240110_S-23.pdf");
        var newer = Candidate("S-23", "09.06.2026", "20260609_S-23.pdf");
        var other = Candidate("S-24", "01.01.2025", "20250101_S-24.pdf");

        var selected = SchachtProtocolFolderImportPolicy.SelectCurrentPerShaft(
            new[] { older, other, newer });

        Assert.Equal(2, selected.Count);
        Assert.Contains(newer, selected);
        Assert.DoesNotContain(older, selected);
        Assert.Contains(other, selected);
    }

    private SchachtProtocolFolderCandidate Candidate(string shaft, string date, string fileName)
        => new(
            Path.Combine(_root, fileName),
            new SchachtProtocolParseResult(
                true,
                shaft,
                date,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                Array.Empty<(string, string)>()));

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }
        catch
        {
            // Test-Aufraeumen ist Best-Effort.
        }
    }
}
