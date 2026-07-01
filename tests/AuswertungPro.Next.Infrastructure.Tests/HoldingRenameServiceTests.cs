using System;
using System.IO;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Domain.Models;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class HoldingRenameServiceTests
{
    [Fact]
    public void Rename_RealDashUnderscoreGSchema_RenamesFolderVideoAndPdfFiles_AndUpdatesLink()
    {
        // Reales Verteilungs-Schema: JJJJMMTT-<Haltung>.mp4 (+ _G) und ....pdf (+ _G).
        // Frueher (enges Regex '_<Haltung>-g') wurden diese Dateien NICHT umbenannt und der
        // Link-Pfad behielt die alte Haltung im Dateinamen -> defekt.
        var oldH = "07.1026779-10750";
        var newH = "07.5555-4444";
        var oldSan = ProjectPathResolver.SanitizePathSegment(oldH);
        var newSan = ProjectPathResolver.SanitizePathSegment(newH);

        var root = Path.Combine(Path.GetTempPath(), $"holdrename-{Guid.NewGuid():N}");
        var projFile = Path.Combine(root, "projekt.json");
        var oldFolder = Path.Combine(root, "Haltungen", oldSan);
        Directory.CreateDirectory(oldFolder);

        var stdVideo = Path.Combine(oldFolder, $"20250310-{oldSan}.mp4");
        var gegVideo = Path.Combine(oldFolder, $"20250310-{oldSan}_G.mp4");
        var stdPdf = Path.Combine(oldFolder, $"20250310-{oldSan}.pdf");
        var gegPdf = Path.Combine(oldFolder, $"20250310-{oldSan}_G.pdf");
        foreach (var p in new[] { stdVideo, gegVideo, stdPdf, gegPdf })
            File.WriteAllText(p, "x");
        File.WriteAllText(projFile, "{}");

        try
        {
            var record = new HaltungRecord();
            record.SetFieldValue("Haltungsname", oldH, FieldSource.Xtf, userEdited: false);
            record.SetFieldValue("Link", stdVideo, FieldSource.Xtf, userEdited: false);

            var result = HoldingRenameService.Rename(record, oldH, newH, projFile);

            Assert.True(result.Success, result.ErrorMessage);
            Assert.True(result.FolderRenamed);

            var newFolder = Path.Combine(root, "Haltungen", newSan);
            Assert.True(Directory.Exists(newFolder));
            Assert.False(Directory.Exists(oldFolder));
            Assert.True(File.Exists(Path.Combine(newFolder, $"20250310-{newSan}.mp4")));
            Assert.True(File.Exists(Path.Combine(newFolder, $"20250310-{newSan}_G.mp4")));
            Assert.True(File.Exists(Path.Combine(newFolder, $"20250310-{newSan}.pdf")));
            Assert.True(File.Exists(Path.Combine(newFolder, $"20250310-{newSan}_G.pdf")));

            // Link zeigt auf das umbenannte Video im umbenannten Ordner (Ordner UND Dateiname).
            var link = record.GetFieldValue("Link") ?? "";
            Assert.Contains(newSan, link, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(oldSan, link, StringComparison.OrdinalIgnoreCase);
            Assert.True(File.Exists(link), $"Link zeigt auf nicht existierende Datei: {link}");
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }

    [Fact]
    public void Rename_NeueStruktur_ProjektjsonInProjektdateien_RelativerLink_BenenntVerteilteDateienUm()
    {
        // Neue Struktur: projekt.json unter Projektdateien\, verteilte Dateien in Haltungen_Verteilt\,
        // Link RELATIV zum Root. Frueher fand LocateHoldingFolder den Ordner nicht (relativ gegen
        // Projektdateien\ aufgeloest + Fallback suchte nur "Haltungen") -> Dateien blieben unbenannt,
        // Record-Pfade zeigten ins Leere.
        var oldH = "06-001";
        var newH = "06-999";
        var oldSan = ProjectPathResolver.SanitizePathSegment(oldH);
        var newSan = ProjectPathResolver.SanitizePathSegment(newH);

        var root = Path.Combine(Path.GetTempPath(), $"holdrename-neu-{Guid.NewGuid():N}");
        var projFile = Path.Combine(root, "Projektdateien", "projekt.json");
        Directory.CreateDirectory(Path.GetDirectoryName(projFile)!);
        var oldFolder = Path.Combine(root, "Haltungen_Verteilt", oldSan);
        Directory.CreateDirectory(oldFolder);

        var video = Path.Combine(oldFolder, $"20250310_{oldSan}.mpg");
        var pdf = Path.Combine(oldFolder, $"20250310_{oldSan}.pdf");
        File.WriteAllText(video, "x");
        File.WriteAllText(pdf, "x");
        File.WriteAllText(projFile, "{}");

        try
        {
            var relLink = Path.Combine("Haltungen_Verteilt", oldSan, $"20250310_{oldSan}.mpg");
            var relPdf = Path.Combine("Haltungen_Verteilt", oldSan, $"20250310_{oldSan}.pdf");
            var record = new HaltungRecord();
            record.SetFieldValue("Haltungsname", oldH, FieldSource.Xtf, userEdited: false);
            record.SetFieldValue("Link", relLink, FieldSource.Xtf, userEdited: false);
            record.SetFieldValue("PDF_Path", relPdf, FieldSource.Xtf, userEdited: false);

            var result = HoldingRenameService.Rename(record, oldH, newH, projFile);

            Assert.True(result.Success, result.ErrorMessage);
            Assert.True(result.FolderRenamed);

            var newFolder = Path.Combine(root, "Haltungen_Verteilt", newSan);
            Assert.True(Directory.Exists(newFolder), "Verteil-Ordner wurde nicht umbenannt");
            Assert.False(Directory.Exists(oldFolder));
            Assert.True(File.Exists(Path.Combine(newFolder, $"20250310_{newSan}.mpg")), "Video nicht umbenannt");
            Assert.True(File.Exists(Path.Combine(newFolder, $"20250310_{newSan}.pdf")), "PDF nicht umbenannt");

            // Link ist relativ aktualisiert und ueber den Root auf die existierende Datei aufloesbar.
            var link = record.GetFieldValue("Link") ?? "";
            Assert.Contains(newSan, link, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(oldSan, link, StringComparison.OrdinalIgnoreCase);
            Assert.False(Path.IsPathRooted(link), $"Link soll relativ bleiben: {link}");
            var resolved = ProjectPathResolver.ResolveFilePath(link, projFile);
            Assert.False(string.IsNullOrWhiteSpace(resolved), "Link nicht gegen Root aufloesbar");
            Assert.True(File.Exists(resolved!), $"aufgeloester Link existiert nicht: {resolved}");
        }
        finally { try { Directory.Delete(root, recursive: true); } catch { } }
    }

    [Fact]
    public void Rename_BenenntAuchDenFotosOrdner_UndDessenDateienUm()
    {
        // Fotos liegen in einem SEPARATEN Ordner (Fotos\Haltungen\<H>\), nicht ueber den Link
        // auffindbar. Regression: der Rename fasste ihn nicht an -> Fotos blieben unter altem Namen.
        var oldH = "3.01-3.04";
        var newH = "07.1085601-22152";
        var oldSan = ProjectPathResolver.SanitizePathSegment(oldH);
        var newSan = ProjectPathResolver.SanitizePathSegment(newH);

        var root = Path.Combine(Path.GetTempPath(), $"holdrename-fotos-{Guid.NewGuid():N}");
        var projFile = Path.Combine(root, "Projektdateien", "projekt.json");
        Directory.CreateDirectory(Path.GetDirectoryName(projFile)!);
        File.WriteAllText(projFile, "{}");

        // Verteil-Ordner mit Video (damit LocateHoldingFolder etwas findet)
        var verteiltOld = Path.Combine(root, "Haltungen_Verteilt", oldSan);
        Directory.CreateDirectory(verteiltOld);
        File.WriteAllText(Path.Combine(verteiltOld, $"20250310_{oldSan}.mpg"), "x");

        // Separater Fotos-Ordner mit haltungsbenannten Fotos
        var fotoOld = Path.Combine(root, "Fotos", "Haltungen", oldSan);
        Directory.CreateDirectory(fotoOld);
        File.WriteAllText(Path.Combine(fotoOld, $"H_{oldSan}_034.jpg"), "x");

        try
        {
            var record = new HaltungRecord();
            record.SetFieldValue("Haltungsname", oldH, FieldSource.Xtf, userEdited: false);
            record.SetFieldValue("Link", Path.Combine("Haltungen_Verteilt", oldSan, $"20250310_{oldSan}.mpg"),
                FieldSource.Xtf, userEdited: false);

            var result = HoldingRenameService.Rename(record, oldH, newH, projFile);
            Assert.True(result.Success, result.ErrorMessage);

            var fotoNew = Path.Combine(root, "Fotos", "Haltungen", newSan);
            Assert.True(Directory.Exists(fotoNew), "Fotos-Ordner wurde nicht umbenannt");
            Assert.False(Directory.Exists(fotoOld), "alter Fotos-Ordner existiert noch");
            Assert.True(File.Exists(Path.Combine(fotoNew, $"H_{newSan}_034.jpg")), "Foto-Datei nicht umbenannt");
        }
        finally { try { Directory.Delete(root, recursive: true); } catch { } }
    }

    [Fact]
    public void Rename_LegacyUnderscoreDashGSchema_StillRenamesFiles()
    {
        // Regression: altes Schema JJJJMMTT_<Haltung>-g.mp4 muss weiter umbenannt werden.
        var oldH = "06-001";
        var newH = "06-999";
        var oldSan = ProjectPathResolver.SanitizePathSegment(oldH);
        var newSan = ProjectPathResolver.SanitizePathSegment(newH);

        var root = Path.Combine(Path.GetTempPath(), $"holdrename-{Guid.NewGuid():N}");
        var projFile = Path.Combine(root, "projekt.json");
        var oldFolder = Path.Combine(root, "Haltungen", oldSan);
        Directory.CreateDirectory(oldFolder);

        var std = Path.Combine(oldFolder, $"20240630_{oldSan}.mp4");
        var geg = Path.Combine(oldFolder, $"20240630_{oldSan}-g.mp4");
        File.WriteAllText(std, "x");
        File.WriteAllText(geg, "x");
        File.WriteAllText(projFile, "{}");

        try
        {
            var record = new HaltungRecord();
            record.SetFieldValue("Link", std, FieldSource.Xtf, userEdited: false);

            var result = HoldingRenameService.Rename(record, oldH, newH, projFile);

            Assert.True(result.Success, result.ErrorMessage);
            var newFolder = Path.Combine(root, "Haltungen", newSan);
            Assert.True(File.Exists(Path.Combine(newFolder, $"20240630_{newSan}.mp4")));
            Assert.True(File.Exists(Path.Combine(newFolder, $"20240630_{newSan}-g.mp4")));
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }

    [Fact]
    public void Rename_TargetFolderExists_FailsWithoutChange()
    {
        // Kollisionsschutz: Zielname existiert bereits -> Abbruch, Quelle unveraendert.
        var oldH = "06-001";
        var newH = "06-999";
        var oldSan = ProjectPathResolver.SanitizePathSegment(oldH);
        var newSan = ProjectPathResolver.SanitizePathSegment(newH);

        var root = Path.Combine(Path.GetTempPath(), $"holdrename-{Guid.NewGuid():N}");
        var projFile = Path.Combine(root, "projekt.json");
        var oldFolder = Path.Combine(root, "Haltungen", oldSan);
        Directory.CreateDirectory(oldFolder);
        Directory.CreateDirectory(Path.Combine(root, "Haltungen", newSan)); // Kollision
        File.WriteAllText(projFile, "{}");
        var std = Path.Combine(oldFolder, $"20240630_{oldSan}.mp4");
        File.WriteAllText(std, "x");

        try
        {
            var record = new HaltungRecord();
            record.SetFieldValue("Link", std, FieldSource.Xtf, userEdited: false);

            var result = HoldingRenameService.Rename(record, oldH, newH, projFile);

            Assert.False(result.Success);
            Assert.True(Directory.Exists(oldFolder));
            Assert.True(File.Exists(std));
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }
}
