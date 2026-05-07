using System;
using System.IO;
using System.Linq;
using AuswertungPro.Next.Infrastructure;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Characterization-Tests fuer HoldingFolderDistributor (Cluster 1, Charge 1):
/// Argument-Validation der Public-Eintrittspforten Distribute, DistributeFiles
/// und DistributeTxt. Friert das heutige Pre-Core-Verhalten (vor
/// DistributeCore-Aufruf, also vor pdftotext-Pfad) ein, damit das geplante
/// Aufteilen der 4.576-Zeilen-Klasse diese Edge-Cases nicht unbemerkt
/// verschiebt.
///
/// Coverage-Karte (Stand 2026-05-07):
///   Distribute              : 0 echte Tests vor dieser Datei
///   DistributeFiles         : 0 echte Tests vor dieser Datei
///   DistributeTxt           : 0 echte Tests vor dieser Datei
/// </summary>
public sealed class HoldingFolderDistributorCharacterizationTests
{
    [Fact]
    public void Distribute_PdfFolderDoesNotExist_ReturnsSingleNotCheckedResult()
    {
        using var temp = new TempDir();
        var nonExistent = Path.Combine(temp.Path, "does-not-exist");
        var dest = Path.Combine(temp.Path, "dest");

        var results = HoldingFolderDistributor.Distribute(
            pdfSourceFolder: nonExistent,
            videoSourceFolder: temp.Path,
            destGemeindeFolder: dest);

        var item = Assert.Single(results);
        Assert.False(item.Success);
        Assert.Equal(HoldingFolderDistributor.VideoMatchStatus.NotChecked, item.VideoStatus);
        Assert.Contains("not found", item.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(nonExistent, item.SourcePdfPath);
    }

    [Fact]
    public void Distribute_PdfFolderEmpty_ReturnsSingleNotCheckedResult()
    {
        using var temp = new TempDir();
        var pdfFolder = Path.Combine(temp.Path, "pdfs");
        Directory.CreateDirectory(pdfFolder);
        var dest = Path.Combine(temp.Path, "dest");

        var results = HoldingFolderDistributor.Distribute(
            pdfSourceFolder: pdfFolder,
            videoSourceFolder: temp.Path,
            destGemeindeFolder: dest);

        var item = Assert.Single(results);
        Assert.False(item.Success);
        Assert.Equal(HoldingFolderDistributor.VideoMatchStatus.NotChecked, item.VideoStatus);
        Assert.Contains("No PDF files found", item.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Distribute_OnlySplitPrefixedPdfs_ReturnsSingleNotCheckedResult()
    {
        // split_*.pdf werden im Distribute-Filter ausgeschlossen — wenn der Folder
        // nur solche enthaelt, verhaelt sich der Aufruf wie ein leerer Folder.
        using var temp = new TempDir();
        var pdfFolder = Path.Combine(temp.Path, "pdfs");
        Directory.CreateDirectory(pdfFolder);
        File.WriteAllText(Path.Combine(pdfFolder, "split_001.pdf"), "dummy");
        File.WriteAllText(Path.Combine(pdfFolder, "split_002.pdf"), "dummy");
        var dest = Path.Combine(temp.Path, "dest");

        var results = HoldingFolderDistributor.Distribute(
            pdfSourceFolder: pdfFolder,
            videoSourceFolder: temp.Path,
            destGemeindeFolder: dest);

        var item = Assert.Single(results);
        Assert.False(item.Success);
        Assert.Equal(HoldingFolderDistributor.VideoMatchStatus.NotChecked, item.VideoStatus);
        Assert.Contains("No PDF files found", item.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DistributeFiles_EmptyList_ReturnsSingleNotCheckedResult()
    {
        using var temp = new TempDir();
        var dest = Path.Combine(temp.Path, "dest");

        var results = HoldingFolderDistributor.DistributeFiles(
            pdfFiles: Array.Empty<string>(),
            videoSourceFolder: temp.Path,
            destGemeindeFolder: dest);

        var item = Assert.Single(results);
        Assert.False(item.Success);
        Assert.Equal(HoldingFolderDistributor.VideoMatchStatus.NotChecked, item.VideoStatus);
        Assert.Contains("No valid PDF files", item.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DistributeFiles_OnlyMissingFiles_ReturnsSingleNotCheckedResult()
    {
        using var temp = new TempDir();
        var missing = new[]
        {
            Path.Combine(temp.Path, "ghost1.pdf"),
            Path.Combine(temp.Path, "ghost2.pdf")
        };
        var dest = Path.Combine(temp.Path, "dest");

        var results = HoldingFolderDistributor.DistributeFiles(
            pdfFiles: missing,
            videoSourceFolder: temp.Path,
            destGemeindeFolder: dest);

        var item = Assert.Single(results);
        Assert.False(item.Success);
        Assert.Equal(HoldingFolderDistributor.VideoMatchStatus.NotChecked, item.VideoStatus);
        Assert.Contains("No valid PDF files", item.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DistributeFiles_OnlyNonPdfExtensions_ReturnsSingleNotCheckedResult()
    {
        using var temp = new TempDir();
        var notPdfs = new[]
        {
            CreateFile(temp.Path, "report.txt"),
            CreateFile(temp.Path, "data.csv"),
            CreateFile(temp.Path, "notes.md")
        };
        var dest = Path.Combine(temp.Path, "dest");

        var results = HoldingFolderDistributor.DistributeFiles(
            pdfFiles: notPdfs,
            videoSourceFolder: temp.Path,
            destGemeindeFolder: dest);

        var item = Assert.Single(results);
        Assert.False(item.Success);
        Assert.Equal(HoldingFolderDistributor.VideoMatchStatus.NotChecked, item.VideoStatus);
        Assert.Contains("No valid PDF files", item.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DistributeFiles_OnlySplitPrefixedPdfs_ReturnsSingleNotCheckedResult()
    {
        // Wie der Distribute-Filter, aber auf der DistributeFiles-Eintrittspforte:
        // split_*.pdf werden ebenfalls gefiltert — auch wenn sie als File existieren.
        using var temp = new TempDir();
        var splits = new[]
        {
            CreateFile(temp.Path, "split_001.pdf"),
            CreateFile(temp.Path, "split_002.pdf")
        };
        var dest = Path.Combine(temp.Path, "dest");

        var results = HoldingFolderDistributor.DistributeFiles(
            pdfFiles: splits,
            videoSourceFolder: temp.Path,
            destGemeindeFolder: dest);

        var item = Assert.Single(results);
        Assert.False(item.Success);
        Assert.Equal(HoldingFolderDistributor.VideoMatchStatus.NotChecked, item.VideoStatus);
        Assert.Contains("No valid PDF files", item.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DistributeTxt_TxtFolderDoesNotExist_ReturnsSingleNotCheckedResult()
    {
        using var temp = new TempDir();
        var nonExistent = Path.Combine(temp.Path, "missing-txt-folder");
        var dest = Path.Combine(temp.Path, "dest");

        var results = HoldingFolderDistributor.DistributeTxt(
            txtSourceFolder: nonExistent,
            videoSourceFolder: temp.Path,
            destGemeindeFolder: dest);

        var item = Assert.Single(results);
        Assert.False(item.Success);
        Assert.Equal(HoldingFolderDistributor.VideoMatchStatus.NotChecked, item.VideoStatus);
        Assert.Contains("not found", item.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(nonExistent, item.SourcePdfPath);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static string CreateFile(string folder, string fileName)
    {
        var path = Path.Combine(folder, fileName);
        File.WriteAllText(path, "dummy");
        return path;
    }

    private sealed class TempDir : IDisposable
    {
        public string Path { get; }

        public TempDir()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "hfd_chr_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
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
                // ignore cleanup failures
            }
        }
    }
}
