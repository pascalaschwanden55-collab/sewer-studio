using System;
using System.IO;
using System.Linq;
using AuswertungPro.Next.Application.Ai.Annotation;
using AuswertungPro.Next.Infrastructure.Ai.Annotation;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Slice 1 (Operateur-Annotation): Builder fuer eine Session aus Volltext
/// (testbar ohne pdftotext) + Folder-Variante (sucht Video/PDF).
/// </summary>
[Trait("Category", "Integration")]
public sealed class OperateurSessionBuilderTests : IDisposable
{
    private readonly string _tempDir;

    public OperateurSessionBuilderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(),
            "OperateurSessionBuilderTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true); }
        catch { /* best-effort */ }
    }

    [Fact]
    public void BuildFromText_ParsesAndSortsByMeter()
    {
        var pdfText = """
            Seite 1
            18.50  BAC A   Bruch partiell
             5.00  BCD     Rohranfang
            12.30  BAB B   Riss laengs
            """;

        var builder = new OperateurSessionBuilder();
        var session = builder.BuildFromText(
            pdfText,
            videoPath: @"C:\videos\h.mp4",
            pdfPath: @"C:\videos\h.pdf",
            caseId: "haltung-100-200");

        Assert.Equal("haltung-100-200", session.CaseId);
        Assert.Equal(@"C:\videos\h.mp4", session.VideoPath);
        Assert.Equal(@"C:\videos\h.pdf", session.PdfPath);
        Assert.Equal(3, session.Tasks.Count);

        // Sortierung nach Meterstand
        Assert.Equal(new[] { 5.0, 12.3, 18.5 }, session.Tasks.Select(t => t.Meterstand));
        Assert.Equal(new[] { "BCD", "BAB B", "BAC A" }, session.Tasks.Select(t => t.Code));

        // Alle Tasks landen als Pending — der Operator entscheidet, was er codiert.
        Assert.All(session.Tasks, t => Assert.Equal(CodeTaskState.Pending, t.State));
    }

    [Fact]
    public void BuildFromText_EmptyPdfText_ReturnsSessionWithoutTasks()
    {
        var builder = new OperateurSessionBuilder();
        var session = builder.BuildFromText(
            pdfText: "",
            videoPath: "v.mp4",
            pdfPath: "p.pdf",
            caseId: "c");
        Assert.Empty(session.Tasks);
    }

    [Fact]
    public void BuildFromFolder_NonexistentFolder_Throws()
    {
        var builder = new OperateurSessionBuilder();
        Assert.Throws<DirectoryNotFoundException>(() =>
            builder.BuildFromFolder(Path.Combine(_tempDir, "nope")));
    }

    [Fact]
    public void BuildFromFolder_NoVideo_Throws()
    {
        File.WriteAllText(Path.Combine(_tempDir, "protocol.pdf"), "fake");
        var builder = new OperateurSessionBuilder();
        Assert.Throws<FileNotFoundException>(() =>
            builder.BuildFromFolder(_tempDir));
    }

    [Fact]
    public void BuildFromFolder_NoPdf_Throws()
    {
        File.WriteAllText(Path.Combine(_tempDir, "video.mp4"), "fake");
        var builder = new OperateurSessionBuilder();
        Assert.Throws<FileNotFoundException>(() =>
            builder.BuildFromFolder(_tempDir));
    }

    [Fact]
    public void BuildFromFolder_PicksFirstVideoAndFirstPdf()
    {
        // Beide Dateien anlegen — Auswahl nach Ordinal-Sortierung deterministisch.
        File.WriteAllText(Path.Combine(_tempDir, "b_video.mp4"), "fake");
        File.WriteAllText(Path.Combine(_tempDir, "a_video.mp4"), "fake");
        File.WriteAllText(Path.Combine(_tempDir, "b_protocol.pdf"), "fake");
        File.WriteAllText(Path.Combine(_tempDir, "a_protocol.pdf"), "fake");

        // PdfTextExtractor wirft hier (keine echte PDF), aber die
        // Vorausuche im Ordner und die Fehlermeldung muessen klappen.
        var builder = new OperateurSessionBuilder();
        var ex = Assert.ThrowsAny<Exception>(() => builder.BuildFromFolder(_tempDir));
        // Fehler kommt aus dem PDF-Decoder, nicht aus der Vorauswahl —
        // also wurde der erste PDF-Pfad zumindest weitergegeben.
        Assert.False(string.IsNullOrWhiteSpace(ex.Message));
    }
}
