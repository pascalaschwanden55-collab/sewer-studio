using System.IO;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Infrastructure.Import;
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
    public void FindPdfFiles_SchliesstDirektGewaehltesProjektzielNichtAus()
    {
        var projectDistribution = Path.Combine(_root, "Projekt", "Schächte_Verteilt");
        var shaftFolder = Path.Combine(projectDistribution, "844859");
        Directory.CreateDirectory(shaftFolder);
        var pdf = Path.Combine(shaftFolder, "20260629_844859.pdf");
        File.WriteAllText(pdf, "test");

        var files = SchachtProtocolFolderImportPolicy.FindPdfFiles(
            projectDistribution,
            new[] { projectDistribution });

        Assert.Equal(new[] { pdf }, files);
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

    [Fact]
    public void BuildFolderImportSummary_preserves_base_line_order()
    {
        var summary = SchachtProtocolFolderImportPolicy.BuildFolderImportSummary(
            sourcePdfCount: 12,
            preparedPdfCount: 9,
            created: 2,
            updated: 7,
            archivedOlderProtocols: 0,
            skippedDirectoryCount: 0,
            failures: []);

        Assert.Equal(
            string.Join(
                Environment.NewLine,
                "Gefundene PDF-Dateien: 12",
                "Eingelesene Schachtprotokolle: 9",
                "Schaechte neu angelegt: 2",
                "Schaechte aktualisiert: 7"),
            summary);
    }

    [Fact]
    public void BuildFolderImportSummary_includes_optional_counts_and_limits_failure_details()
    {
        var failures = Enumerable.Range(1, 10)
            .Select(index => $"Fehler {index}")
            .ToArray();

        var summary = SchachtProtocolFolderImportPolicy.BuildFolderImportSummary(
            sourcePdfCount: 15,
            preparedPdfCount: 11,
            created: 3,
            updated: 8,
            archivedOlderProtocols: 4,
            skippedDirectoryCount: 2,
            failures: failures);

        Assert.Equal(
            string.Join(
                Environment.NewLine,
                "Gefundene PDF-Dateien: 15",
                "Eingelesene Schachtprotokolle: 11",
                "Schaechte neu angelegt: 3",
                "Schaechte aktualisiert: 8",
                "Aeltere Protokolle archiviert: 4 (Stammdaten stammen aus dem neuesten Protokoll)",
                "Nicht lesbare Unterordner uebersprungen: 2",
                "Fehler: 10",
                "- Fehler 1",
                "- Fehler 2",
                "- Fehler 3",
                "- Fehler 4",
                "- Fehler 5",
                "- Fehler 6",
                "- Fehler 7",
                "- Fehler 8",
                "- ... und 2 weitere"),
            summary);
    }

    [Fact]
    public void BuildFolderImportSummary_does_not_add_remainder_line_for_exactly_eight_failures()
    {
        var failures = Enumerable.Range(1, 8)
            .Select(index => $"Fehler {index}")
            .ToArray();

        var summary = SchachtProtocolFolderImportPolicy.BuildFolderImportSummary(
            sourcePdfCount: 8,
            preparedPdfCount: 0,
            created: 0,
            updated: 0,
            archivedOlderProtocols: 0,
            skippedDirectoryCount: 0,
            failures: failures);

        Assert.EndsWith("- Fehler 8", summary, StringComparison.Ordinal);
        Assert.DoesNotContain("weitere", summary, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(3, 0, true, false)]
    [InlineData(0, 2, false, true)]
    public void BuildFolderImportSummary_includes_archive_and_skipped_folder_lines_independently(
        int archivedOlderProtocols,
        int skippedDirectoryCount,
        bool expectsArchiveLine,
        bool expectsSkippedLine)
    {
        var summary = SchachtProtocolFolderImportPolicy.BuildFolderImportSummary(
            sourcePdfCount: 1,
            preparedPdfCount: 1,
            created: 0,
            updated: 1,
            archivedOlderProtocols: archivedOlderProtocols,
            skippedDirectoryCount: skippedDirectoryCount,
            failures: []);

        Assert.Equal(
            expectsArchiveLine,
            summary.Contains("Aeltere Protokolle archiviert:", StringComparison.Ordinal));
        Assert.Equal(
            expectsSkippedLine,
            summary.Contains("Nicht lesbare Unterordner uebersprungen:", StringComparison.Ordinal));
    }

    [Fact]
    public void ResolveCanonicalShaftFolder_uses_child_folder_but_not_distribution_root()
    {
        var distributionRoots = new[]
        {
            Path.Combine(_root, "Projekt", ProjectStructure.SchaechteVerteilt),
            Path.Combine(_root, "Projekt", "Schaechte_Verteilt")
        };

        foreach (var distributionRoot in distributionRoots)
        {
            var shaftFolder = Path.Combine(distributionRoot, "80454");
            var fromRoot = SchachtProtocolFolderImportPolicy.ResolveCanonicalShaftFolder(
                Path.Combine(distributionRoot, "protokoll.pdf"),
                distributionRoot.ToUpperInvariant() + Path.DirectorySeparatorChar);
            var fromShaft = SchachtProtocolFolderImportPolicy.ResolveCanonicalShaftFolder(
                Path.Combine(shaftFolder, "protokoll.pdf"),
                distributionRoot);

            Assert.Null(fromRoot);
            Assert.Equal("80454", fromShaft);
        }
    }

    [Fact]
    public void ResolveCanonicalShaftFolder_handles_missing_parent_and_invalid_root_safely()
    {
        var shaftFolder = Path.Combine(_root, "844859");

        var withoutParent = SchachtProtocolFolderImportPolicy.ResolveCanonicalShaftFolder(
            "protokoll.pdf",
            _root);
        var withInvalidRoot = SchachtProtocolFolderImportPolicy.ResolveCanonicalShaftFolder(
            Path.Combine(shaftFolder, "protokoll.pdf"),
            "\0");

        Assert.Null(withoutParent);
        Assert.Equal("844859", withInvalidRoot);
    }

    [Fact]
    public void ResolveCanonicalShaftFolder_checks_legacy_root_as_second_productive_root()
    {
        var modernRoot = Path.Combine(_root, "Projekt", ProjectStructure.SchaechteVerteilt);
        var legacyRoot = Path.Combine(_root, "Projekt", "Schaechte_Verteilt");
        var legacyShaftFolder = Path.Combine(legacyRoot, "99887");

        var directlyInLegacyRoot = SchachtProtocolFolderImportPolicy.ResolveCanonicalShaftFolder(
            Path.Combine(legacyRoot, "protokoll.pdf"),
            modernRoot,
            legacyRoot);
        var inLegacyShaftFolder = SchachtProtocolFolderImportPolicy.ResolveCanonicalShaftFolder(
            Path.Combine(legacyShaftFolder, "protokoll.pdf"),
            modernRoot,
            legacyRoot);

        Assert.Null(directlyInLegacyRoot);
        Assert.Equal("99887", inLegacyShaftFolder);
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
