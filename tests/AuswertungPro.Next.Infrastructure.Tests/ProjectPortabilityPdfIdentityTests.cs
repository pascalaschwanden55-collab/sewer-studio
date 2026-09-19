using System;
using System.IO;
using System.Linq;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Auditbefund 04 (18.09.2026): "Projekt portabel machen" bog mehrere verschiedene
/// PDF-Verweise auf EINE lokale Datei um. Ursache waren zwei Luecken:
/// Der vorhandene Inhaltsvergleich galt nur fuer Fotos (`copyExternalInto != null`),
/// und dieselbe Kandidatin durfte beliebig oft vergeben werden.
///
/// Der legitime Fall muss erhalten bleiben: Ein einzelner Verweis auf eine nicht mehr
/// vorhandene Quelle darf weiter auf die umbenannte Projektkopie zeigen — genau das
/// macht die Verteilung mit ihrem Datumspraefix.
/// </summary>
public sealed class ProjectPortabilityPdfIdentityTests
{
    [Fact]
    public void ZweiVorhandeneFremdePdfs_WerdenNichtAufDieselbeProjektdateiGelegt()
    {
        var root = NewDir();
        var extern1 = NewDir();
        try
        {
            var holding = "H-1";
            var holdingFolder = Path.Combine(root, "Verteilung", holding);
            Directory.CreateDirectory(holdingFolder);
            // Eine inhaltlich ANDERE PDF liegt im Haltungsordner.
            var lokal = Path.Combine(holdingFolder, "H-1.pdf");
            File.WriteAllText(lokal, "lokales Protokoll");

            var a = Path.Combine(extern1, "Protokoll_A.pdf");
            var b = Path.Combine(extern1, "Protokoll_B.pdf");
            File.WriteAllText(a, "Inhalt A");
            File.WriteAllText(b, "Inhalt B");

            var project = new Project();
            var rec = new HaltungRecord();
            rec.SetFieldValue("Haltungsname", holding, FieldSource.Xtf, userEdited: false);
            rec.SetFieldValue(FieldKeys.PdfPath, a, FieldSource.Xtf, userEdited: false);
            rec.SetFieldValue(FieldKeys.PdfAll, $"{a};{b}", FieldSource.Xtf, userEdited: false);
            project.AddRecord(rec);

            new ProjectPortabilityService().MakePortable(root, project);

            var pfad = rec.GetFieldValue(FieldKeys.PdfPath) ?? "";
            var alle = rec.GetFieldValue(FieldKeys.PdfAll) ?? "";
            Assert.DoesNotContain("H-1.pdf", pfad, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("H-1.pdf", alle, StringComparison.OrdinalIgnoreCase);
        }
        finally { TryDelete(root); TryDelete(extern1); }
    }

    [Fact]
    public void ZweiFehlendeFremdePdfs_TeilenSichNichtDieselbeKandidatin()
    {
        var root = NewDir();
        try
        {
            var holding = "H-2";
            var holdingFolder = Path.Combine(root, "Verteilung", holding);
            Directory.CreateDirectory(holdingFolder);
            var lokal = Path.Combine(holdingFolder, "20260918_H-2.pdf");
            File.WriteAllText(lokal, "das einzige Protokoll");

            var project = new Project();
            var rec = new HaltungRecord();
            rec.SetFieldValue("Haltungsname", holding, FieldSource.Xtf, userEdited: false);
            rec.SetFieldValue(FieldKeys.PdfAll,
                @"D:\Weg\Protokoll_A.pdf;D:\Weg\Protokoll_B.pdf", FieldSource.Xtf, userEdited: false);
            project.AddRecord(rec);

            new ProjectPortabilityService().MakePortable(root, project);

            var alle = rec.GetFieldValue(FieldKeys.PdfAll) ?? "";
            var treffer = alle.Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Count(teil => teil.Contains("20260918_H-2.pdf", StringComparison.OrdinalIgnoreCase));
            Assert.Equal(1, treffer);
            Assert.Contains("Protokoll_B.pdf", alle, StringComparison.OrdinalIgnoreCase);
        }
        finally { TryDelete(root); }
    }

    [Fact]
    public void EinzelnerVerweisAufFehlendeQuelle_ZeigtWeiterhinAufDieUmbenannteProjektkopie()
    {
        // Bestandsschutz: genau das macht die Verteilung mit ihrem Datumspraefix.
        var root = NewDir();
        try
        {
            var holding = "H-3";
            var holdingFolder = Path.Combine(root, "Verteilung", holding);
            Directory.CreateDirectory(holdingFolder);
            var lokal = Path.Combine(holdingFolder, "20260918_H-3.pdf");
            File.WriteAllText(lokal, "Protokoll");

            var project = new Project();
            var rec = new HaltungRecord();
            rec.SetFieldValue("Haltungsname", holding, FieldSource.Xtf, userEdited: false);
            rec.SetFieldValue(FieldKeys.PdfPath, @"D:\Weg\H_3_Protokoll.pdf", FieldSource.Xtf, userEdited: false);
            project.AddRecord(rec);

            new ProjectPortabilityService().MakePortable(root, project);

            var pfad = rec.GetFieldValue(FieldKeys.PdfPath) ?? "";
            Assert.False(Path.IsPathRooted(pfad), $"sollte relativ sein: {pfad}");
            var aufgeloest = ProjectPathResolver.ResolveFilePathFromProjectFolder(pfad, root);
            Assert.NotNull(aufgeloest);
            Assert.Equal(Path.GetFullPath(lokal), Path.GetFullPath(aufgeloest!), ignoreCase: true);
        }
        finally { TryDelete(root); }
    }

    [Fact]
    public void PdfAllAlsJsonListe_WirdAufgeloestUndNichtAlsEinPfadBehandelt()
    {
        var root = NewDir();
        try
        {
            var holding = "H-4";
            var holdingFolder = Path.Combine(root, "Verteilung", holding);
            Directory.CreateDirectory(holdingFolder);
            var lokal = Path.Combine(holdingFolder, "20260918_H-4.pdf");
            File.WriteAllText(lokal, "Protokoll");

            var project = new Project();
            var rec = new HaltungRecord();
            rec.SetFieldValue("Haltungsname", holding, FieldSource.Xtf, userEdited: false);
            rec.SetFieldValue(FieldKeys.PdfAll,
                "[\"D:\\\\Weg\\\\H_4_Protokoll.pdf\"]", FieldSource.Xtf, userEdited: false);
            project.AddRecord(rec);

            new ProjectPortabilityService().MakePortable(root, project);

            var alle = rec.GetFieldValue(FieldKeys.PdfAll) ?? "";
            Assert.Contains("20260918_H-4.pdf", alle, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("H_4_Protokoll.pdf", alle, StringComparison.OrdinalIgnoreCase);
            // Das gespeicherte Format bleibt eine JSON-Liste.
            Assert.StartsWith("[", alle.Trim(), StringComparison.Ordinal);
        }
        finally { TryDelete(root); }
    }

    private static string NewDir()
    {
        var d = Path.Combine(Path.GetTempPath(), $"port-id-{Guid.NewGuid():N}");
        Directory.CreateDirectory(d);
        return d;
    }

    private static void TryDelete(string d)
    {
        try { if (Directory.Exists(d)) Directory.Delete(d, recursive: true); } catch { }
    }
}
