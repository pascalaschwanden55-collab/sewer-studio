using System;
using System.IO;
using System.Text.Json;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.DataPage;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Sichert die aus DataPageViewModel extrahierte Protokoll-/PDF-Pfadaufloesung ab.
/// Reine Such-/Pfadlogik, deshalb gegen echte Temp-Verzeichnisse getestet.
/// </summary>
public sealed class DataPageProtocolPathResolverTests
{
    // --- BuildHoldingTokens ---

    [Fact]
    public void BuildHoldingTokens_liefert_leer_bei_fehlendem_namen()
    {
        var record = new HaltungRecord();

        Assert.Empty(DataPageProtocolPathResolver.BuildHoldingTokens(record));
    }

    [Fact]
    public void BuildHoldingTokens_liefert_einen_token_wenn_name_keine_sanitisierung_braucht()
    {
        var record = new HaltungRecord();
        record.SetFieldValue("Haltungsname", "12.034-12.035", FieldSource.Manual, userEdited: true);

        var tokens = DataPageProtocolPathResolver.BuildHoldingTokens(record);

        Assert.Equal(new[] { "12.034-12.035" }, tokens);
    }

    [Fact]
    public void BuildHoldingTokens_behaelt_sanitisiert_und_roh_bei_ungueltigen_zeichen()
    {
        var record = new HaltungRecord();
        record.SetFieldValue("Haltungsname", "A:B", FieldSource.Manual, userEdited: true);

        var tokens = DataPageProtocolPathResolver.BuildHoldingTokens(record);

        Assert.Equal(new[] { "A_B", "A:B" }, tokens);
    }

    // --- ParseStoredPathList ---

    [Fact]
    public void ParseStoredPathList_liest_json_array_und_trimmt()
    {
        var raw = JsonSerializer.Serialize(new[] { "a.pdf", "  b.pdf  " });

        var result = DataPageProtocolPathResolver.ParseStoredPathList(raw);

        Assert.Equal(new[] { "a.pdf", "b.pdf" }, result);
    }

    [Fact]
    public void ParseStoredPathList_faellt_auf_semikolon_split_zurueck()
    {
        var result = DataPageProtocolPathResolver.ParseStoredPathList("a.pdf; b.pdf;");

        Assert.Equal(new[] { "a.pdf", "b.pdf" }, result);
    }

    [Fact]
    public void ParseStoredPathList_liefert_leer_bei_leerstring()
    {
        Assert.Empty(DataPageProtocolPathResolver.ParseStoredPathList("   "));
    }

    // --- PickBestPdfCandidate ---

    [Fact]
    public void PickBestPdfCandidate_bevorzugt_token_suffix_treffer()
    {
        var candidates = new[]
        {
            @"C:\x\other.pdf",
            @"C:\x\Protokoll_12.034-12.035.pdf",
        };
        var tokens = new[] { "12.034-12.035" };

        var best = DataPageProtocolPathResolver.PickBestPdfCandidate(candidates, tokens);

        Assert.Equal(@"C:\x\Protokoll_12.034-12.035.pdf", best);
    }

    [Fact]
    public void PickBestPdfCandidate_faellt_auf_letzten_dateinamen_zurueck()
    {
        var candidates = new[] { @"C:\x\aaa.pdf", @"C:\x\zzz.pdf" };
        var tokens = new[] { "kein-treffer" };

        var best = DataPageProtocolPathResolver.PickBestPdfCandidate(candidates, tokens);

        Assert.Equal(@"C:\x\zzz.pdf", best);
    }

    [Fact]
    public void PickBestPdfCandidate_liefert_null_bei_leer()
    {
        Assert.Null(DataPageProtocolPathResolver.PickBestPdfCandidate(Array.Empty<string>(), new[] { "t" }));
    }

    // --- ResolveExistingPath ---

    [Fact]
    public void ResolveExistingPath_liefert_existierenden_absoluten_pfad()
    {
        using var temp = new TempDir();
        var file = temp.CreateFile("abs.pdf");

        var resolved = DataPageProtocolPathResolver.ResolveExistingPath(file, projectPath: null);

        Assert.Equal(file, resolved);
    }

    [Fact]
    public void ResolveExistingPath_loest_relativen_pfad_gegen_projektordner_auf()
    {
        using var temp = new TempDir();
        var projectPath = temp.CreateFile("project.json");
        var pdf = temp.CreateFile("sub.pdf");

        var resolved = DataPageProtocolPathResolver.ResolveExistingPath("sub.pdf", projectPath);

        Assert.Equal(Path.GetFullPath(pdf), resolved, ignoreCase: true);
    }

    [Fact]
    public void ResolveExistingPath_liefert_null_wenn_nichts_passt()
    {
        Assert.Null(DataPageProtocolPathResolver.ResolveExistingPath("gibt-es-nicht.pdf", projectPath: null));
    }

    // --- ResolveOriginalPdfPaths ---

    [Fact]
    public void ResolveOriginalPdfPaths_loest_pdf_path_und_pdf_all_ohne_duplikate()
    {
        using var temp = new TempDir();
        var x = temp.CreateFile("x.pdf");
        var y = temp.CreateFile("y.pdf");

        var record = new HaltungRecord();
        record.SetFieldValue("PDF_Path", "x.pdf", FieldSource.Manual, userEdited: true);
        record.SetFieldValue("PDF_All", "y.pdf;x.pdf", FieldSource.Manual, userEdited: true);

        var paths = DataPageProtocolPathResolver.ResolveOriginalPdfPaths(record, temp.Path);

        Assert.Equal(2, paths.Count);
        Assert.Contains(Path.GetFullPath(x), paths, StringComparer.OrdinalIgnoreCase);
        Assert.Contains(Path.GetFullPath(y), paths, StringComparer.OrdinalIgnoreCase);
    }

    // --- FindProtocolPath ---

    [Fact]
    public void FindProtocolPath_findet_pdf_im_haltungsordner_unter_initial_root()
    {
        using var temp = new TempDir();
        var holdingDir = Directory.CreateDirectory(Path.Combine(temp.Path, "12.034-12.035")).FullName;
        var pdf = Path.Combine(holdingDir, "Protokoll_12.034-12.035.pdf");
        File.WriteAllText(pdf, "x");

        var record = new HaltungRecord();
        record.SetFieldValue("Haltungsname", "12.034-12.035", FieldSource.Manual, userEdited: true);

        var found = DataPageProtocolPathResolver.FindProtocolPath(
            record,
            resolvedLink: null,
            initialFolder: temp.Path,
            projectPath: null,
            storedFilesRaw: null);

        Assert.NotNull(found);
        Assert.True(File.Exists(found));
        Assert.Equal("Protokoll_12.034-12.035.pdf", Path.GetFileName(found));
    }

    [Fact]
    public void FindProtocolPath_liefert_null_wenn_keine_quelle_passt()
    {
        using var temp = new TempDir();
        var record = new HaltungRecord();
        record.SetFieldValue("Haltungsname", "99.999", FieldSource.Manual, userEdited: true);

        var found = DataPageProtocolPathResolver.FindProtocolPath(
            record,
            resolvedLink: null,
            initialFolder: temp.Path,
            projectPath: null,
            storedFilesRaw: null);

        Assert.Null(found);
    }

    [Fact]
    public void FindProtocolPath_nutzt_aufgeloesten_link_direkt_wenn_pdf()
    {
        using var temp = new TempDir();
        var linkPdf = temp.CreateFile("irgendein_protokoll.pdf");

        var record = new HaltungRecord();
        record.SetFieldValue("Haltungsname", "12.034-12.035", FieldSource.Manual, userEdited: true);

        var found = DataPageProtocolPathResolver.FindProtocolPath(
            record,
            resolvedLink: linkPdf,
            initialFolder: null,
            projectPath: null,
            storedFilesRaw: null);

        Assert.Equal(linkPdf, found);
    }

    [Fact]
    public void FindProtocolPath_findet_pdf_ueber_gespeicherte_pdf_liste()
    {
        using var temp = new TempDir();
        var projectPath = temp.CreateFile("project.json");
        var pdf = temp.CreateFile("stored_12.034-12.035.pdf");
        var storedFilesRaw = JsonSerializer.Serialize(new[] { "stored_12.034-12.035.pdf" });

        var record = new HaltungRecord();
        record.SetFieldValue("Haltungsname", "12.034-12.035", FieldSource.Manual, userEdited: true);

        var found = DataPageProtocolPathResolver.FindProtocolPath(
            record,
            resolvedLink: null,
            initialFolder: null,
            projectPath: projectPath,
            storedFilesRaw: storedFilesRaw);

        Assert.Equal(Path.GetFullPath(pdf), found, ignoreCase: true);
    }

    private sealed class TempDir : IDisposable
    {
        public string Path { get; }

        public TempDir()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ssp_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string CreateFile(string name)
        {
            var full = System.IO.Path.Combine(Path, name);
            File.WriteAllText(full, "x");
            return full;
        }

        public void Dispose()
        {
            try { Directory.Delete(Path, recursive: true); }
            catch { /* Aufraeumen ist best effort */ }
        }
    }
}
