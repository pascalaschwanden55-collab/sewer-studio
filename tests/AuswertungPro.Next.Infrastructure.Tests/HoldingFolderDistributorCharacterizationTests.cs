using System;
using System.Globalization;
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

    // ── Charge 2: DistributeTxtFiles Verhaltens-Tests (TXT-basiert) ────────
    //
    // Diese Tests erreichen DistributeTxtCore und damit den Match-/Move-Pfad,
    // OHNE pdftotext-Dependency. Jeder Test friert einen anderen
    // VideoMatchStatus / Side-Effect ein.

    [Fact]
    public void DistributeTxtFiles_VideoMissing_ReturnsNotFoundAndCreatesMissingMarker()
    {
        using var temp = new TempDir();
        var (txtPath, videosFolder, destFolder) = SetupKinsLayout(
            temp.Path,
            txtBody: "Schmutzwasser 100 -> 200 UV 300 @Datei=GHOST.MPG",
            infoBody: "Aufnahmen: 04.12.14 - 05.12.14",
            videosToCreate: Array.Empty<string>()); // kein Video -> NotFound

        var results = HoldingFolderDistributor.DistributeTxtFiles(
            txtFiles: new[] { txtPath },
            videoSourceFolder: videosFolder,
            destGemeindeFolder: destFolder,
            moveInsteadOfCopy: false,
            overwrite: false,
            recursiveVideoSearch: true,
            unmatchedFolderName: "__UNMATCHED",
            project: null,
            progress: null);

        var item = Assert.Single(results);
        Assert.True(item.Success, item.Message);
        Assert.Equal(HoldingFolderDistributor.VideoMatchStatus.NotFound, item.VideoStatus);
        Assert.Null(item.DestVideoPath);
        Assert.False(string.IsNullOrWhiteSpace(item.InfoPath),
            "NotFound muss eine VIDEO_MISSING.txt-Marker-Datei erzeugen");
        Assert.True(File.Exists(item.InfoPath!));
        Assert.Contains("VIDEO_MISSING", Path.GetFileName(item.InfoPath!), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DistributeTxtFiles_TwoVideosMatchSameHolding_ReturnsAmbiguousAndCreatesUnmatchedFolder()
    {
        using var temp = new TempDir();
        // Zwei Sub-Folder, jeweils mit AMB001.MPG -> recursive Search findet beide.
        var sub1 = Path.Combine(temp.Path, "videos", "sub1");
        var sub2 = Path.Combine(temp.Path, "videos", "sub2");
        Directory.CreateDirectory(sub1);
        Directory.CreateDirectory(sub2);
        File.WriteAllText(Path.Combine(sub1, "AMB001.MPG"), "video-a");
        File.WriteAllText(Path.Combine(sub2, "AMB001.MPG"), "video-b");

        var (txtPath, videosFolder, destFolder) = SetupKinsLayout(
            temp.Path,
            txtBody: "Schmutzwasser 300 -> 400 UV 500 @Datei=AMB001.MPG",
            infoBody: "Aufnahmen: 04.12.14 - 05.12.14",
            videosToCreate: Array.Empty<string>(),
            customVideosFolder: Path.Combine(temp.Path, "videos"));

        var results = HoldingFolderDistributor.DistributeTxtFiles(
            txtFiles: new[] { txtPath },
            videoSourceFolder: videosFolder,
            destGemeindeFolder: destFolder,
            moveInsteadOfCopy: false,
            overwrite: false,
            recursiveVideoSearch: true,
            unmatchedFolderName: "__UNMATCHED",
            project: null,
            progress: null);

        var item = Assert.Single(results);
        Assert.True(item.Success, item.Message);
        Assert.Equal(HoldingFolderDistributor.VideoMatchStatus.Ambiguous, item.VideoStatus);
        Assert.Null(item.DestVideoPath);
        Assert.False(string.IsNullOrWhiteSpace(item.InfoPath));
        Assert.Contains("AMBIGUOUS", Path.GetFileName(item.InfoPath!), StringComparison.OrdinalIgnoreCase);

        // __UNMATCHED-Folder muss existieren mit Kandidaten-Kopien
        var unmatchedRoot = Path.Combine(destFolder, "__UNMATCHED");
        Assert.True(Directory.Exists(unmatchedRoot),
            "Bei Ambiguous muss der __UNMATCHED-Folder mit Kandidaten-Kopien angelegt werden");
        Assert.NotEmpty(Directory.EnumerateFiles(unmatchedRoot, "*.MPG", SearchOption.AllDirectories));
    }

    [Fact]
    public void DistributeTxtFiles_DestTxtAlreadyExists_OverwriteFalse_DoesNotOverwriteOriginal()
    {
        using var temp = new TempDir();
        var (txtPath, videosFolder, destFolder) = SetupKinsLayout(
            temp.Path,
            txtBody: "Schmutzwasser 100 -> 200 UV 300 @Datei=ABC001.MPG",
            infoBody: "Aufnahmen: 04.12.14 - 05.12.14",
            videosToCreate: new[] { "ABC001.MPG" });

        // Existierende Ziel-Datei mit dem erwarteten Namen vorbereiten —
        // {yyyyMMdd}_{haltung}.txt = 20141204_100-200.txt
        const string sentinel = "ORIGINAL-INHALT-NICHT-UEBERSCHREIBEN";
        var dateStamp = new DateTime(2014, 12, 4).ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var holdingFolder = Path.Combine(destFolder, "100-200");
        Directory.CreateDirectory(holdingFolder);
        var existingTxt = Path.Combine(holdingFolder, $"{dateStamp}_100-200.txt");
        File.WriteAllText(existingTxt, sentinel);

        var results = HoldingFolderDistributor.DistributeTxtFiles(
            txtFiles: new[] { txtPath },
            videoSourceFolder: videosFolder,
            destGemeindeFolder: destFolder,
            moveInsteadOfCopy: false,
            overwrite: false,
            recursiveVideoSearch: true,
            unmatchedFolderName: "__UNMATCHED",
            project: null,
            progress: null);

        var item = Assert.Single(results);
        Assert.True(item.Success, item.Message);

        // Original muss unveraendert bleiben (EnsureUniquePath schuetzt
        // existierende Datei wenn overwrite=false).
        Assert.True(File.Exists(existingTxt), "Original-Datei wurde geloescht");
        Assert.Equal(sentinel, File.ReadAllText(existingTxt));

        // Eine zweite Datei wurde erzeugt (im gleichen Holding-Folder, anderer Name)
        var allTxtsInHolding = Directory.GetFiles(holdingFolder, "*.txt");
        Assert.True(allTxtsInHolding.Length >= 2,
            $"Erwartet >=2 TXT-Dateien (Original + Neue), gefunden: {allTxtsInHolding.Length}");
    }

    [Fact]
    public void DistributeTxtFiles_MoveInsteadOfCopy_RemovesSourceVideo()
    {
        using var temp = new TempDir();
        var (txtPath, videosFolder, destFolder) = SetupKinsLayout(
            temp.Path,
            txtBody: "Schmutzwasser 100 -> 200 UV 300 @Datei=MOV001.MPG",
            infoBody: "Aufnahmen: 04.12.14 - 05.12.14",
            videosToCreate: new[] { "MOV001.MPG" });
        var sourceVideoPath = Path.Combine(videosFolder, "MOV001.MPG");
        Assert.True(File.Exists(sourceVideoPath), "Pre-Bedingung: Source-Video muss existieren");

        var results = HoldingFolderDistributor.DistributeTxtFiles(
            txtFiles: new[] { txtPath },
            videoSourceFolder: videosFolder,
            destGemeindeFolder: destFolder,
            moveInsteadOfCopy: true, // <-- der Punkt
            overwrite: false,
            recursiveVideoSearch: true,
            unmatchedFolderName: "__UNMATCHED",
            project: null,
            progress: null);

        var item = Assert.Single(results);
        Assert.True(item.Success, item.Message);
        Assert.Equal(HoldingFolderDistributor.VideoMatchStatus.Matched, item.VideoStatus);

        // Move-Semantik: Source ist weg, Dest existiert.
        Assert.False(File.Exists(sourceVideoPath),
            "Bei moveInsteadOfCopy=true muss die Source-Datei nach Lauf geloescht sein");
        Assert.False(string.IsNullOrWhiteSpace(item.DestVideoPath));
        Assert.True(File.Exists(item.DestVideoPath!));
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Erstellt das KINS-Standard-Layout in temp:
    ///   {temp}/src/kiDVDaten.txt (mit txtBody)
    ///   {temp}/src/kiDVinfo.txt  (mit Datums-Header)
    ///   {temp}/videos/{...} (optionale Video-Dateien)
    ///   {temp}/dest/ (Ziel-Gemeinde-Folder)
    /// Gibt (txtPath, videosFolder, destFolder) zurueck.
    /// </summary>
    private static (string txtPath, string videosFolder, string destFolder) SetupKinsLayout(
        string root,
        string txtBody,
        string infoBody,
        string[] videosToCreate,
        string? customVideosFolder = null)
    {
        var src = Path.Combine(root, "src");
        var videos = customVideosFolder ?? Path.Combine(root, "videos");
        var dest = Path.Combine(root, "dest");
        Directory.CreateDirectory(src);
        Directory.CreateDirectory(videos);
        Directory.CreateDirectory(dest);

        var txtPath = Path.Combine(src, "kiDVDaten.txt");
        var infoPath = Path.Combine(src, "kiDVinfo.txt");
        File.WriteAllText(infoPath, infoBody);
        File.WriteAllText(txtPath, string.Join(Environment.NewLine, new[]
        {
            txtBody,
            "  0.0m Rohranfang  @Pos=0:00:00",
            "  1.0m Rohrende  @Pos=0:00:05"
        }));

        foreach (var video in videosToCreate)
            File.WriteAllText(Path.Combine(videos, video), $"video-content-{video}");

        return (txtPath, videos, dest);
    }

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
