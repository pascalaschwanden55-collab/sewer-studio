using System;
using System.IO;
using AuswertungPro.Next.Infrastructure.Import;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Tests fuer ImportSourceArchiver: Kopiert Quell-Rohdaten in die
/// Importdateien-Unterordner des Projekts — idempotent, ohne Videos.
/// </summary>
public sealed class ImportSourceArchiverTests
{
    /// <summary>
    /// Legt Quelldateien aller relevanten Endungen plus eine Video-Datei an.
    /// Prueft nach erstem Archive-Aufruf: Dateien in den richtigen Unterordnern,
    /// Video NICHT kopiert, Copied==4.
    /// Zweiter Aufruf: Copied==0, Reused==4, keine _1-Duplikate.
    /// </summary>
    [Fact]
    public void Archive_KopiertNachEndung_UndIstIdempotent()
    {
        var sourceFolder = Path.Combine(Path.GetTempPath(), "src_" + Guid.NewGuid().ToString("N"));
        var projectFolder = Path.Combine(Path.GetTempPath(), "proj_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(sourceFolder);
        Directory.CreateDirectory(projectFolder);

        try
        {
            // Quelldateien anlegen
            File.WriteAllText(Path.Combine(sourceFolder, "a.db3"), "db");
            File.WriteAllText(Path.Combine(sourceFolder, "b.xtf"), "xtf");
            File.WriteAllText(Path.Combine(sourceFolder, "c.pdf"), "pdf");
            File.WriteAllText(Path.Combine(sourceFolder, "Daten.txt"), "txt");
            File.WriteAllText(Path.Combine(sourceFolder, "film.mpg"), "video");

            // --- Erster Aufruf ---
            var r1 = ImportSourceArchiver.Archive(sourceFolder, projectFolder);

            Assert.Equal(4, r1.Copied);
            Assert.Equal(0, r1.Reused);

            // Zielordner pruefen
            var dbDir  = ProjectStructure.ImportdateienDir(projectFolder, ProjectStructure.Datenbanken);
            var xtfDir = ProjectStructure.ImportdateienDir(projectFolder, ProjectStructure.XtfDir);
            var pdfDir = ProjectStructure.ImportdateienDir(projectFolder, ProjectStructure.PdfDir);
            var txtDir = ProjectStructure.ImportdateienDir(projectFolder, ProjectStructure.TxtDir);

            Assert.True(File.Exists(Path.Combine(dbDir,  "a.db3")),    "a.db3 muss in Datenbanken liegen");
            Assert.True(File.Exists(Path.Combine(xtfDir, "b.xtf")),    "b.xtf muss in XTF liegen");
            Assert.True(File.Exists(Path.Combine(pdfDir, "c.pdf")),    "c.pdf muss in PDF liegen");
            Assert.True(File.Exists(Path.Combine(txtDir, "Daten.txt")), "Daten.txt muss in TXT liegen");

            // Video darf NIRGENDWO im Projekt landen
            Assert.False(File.Exists(Path.Combine(dbDir,  "film.mpg")), "Video darf nicht in Datenbanken sein");
            Assert.False(File.Exists(Path.Combine(xtfDir, "film.mpg")), "Video darf nicht in XTF sein");
            Assert.False(File.Exists(Path.Combine(pdfDir, "film.mpg")), "Video darf nicht in PDF sein");
            Assert.False(File.Exists(Path.Combine(txtDir, "film.mpg")), "Video darf nicht in TXT sein");

            // --- Zweiter Aufruf (Idempotenz) ---
            var r2 = ImportSourceArchiver.Archive(sourceFolder, projectFolder);

            Assert.Equal(0, r2.Copied);
            Assert.Equal(4, r2.Reused);

            // Keine _1-Duplikate
            Assert.False(File.Exists(Path.Combine(dbDir,  "a_1.db3")),     "Kein Duplikat a_1.db3");
            Assert.False(File.Exists(Path.Combine(xtfDir, "b_1.xtf")),     "Kein Duplikat b_1.xtf");
            Assert.False(File.Exists(Path.Combine(pdfDir, "c_1.pdf")),     "Kein Duplikat c_1.pdf");
            Assert.False(File.Exists(Path.Combine(txtDir, "Daten_1.txt")), "Kein Duplikat Daten_1.txt");
        }
        finally
        {
            try { Directory.Delete(sourceFolder,  true); } catch { }
            try { Directory.Delete(projectFolder, true); } catch { }
        }
    }

    /// <summary>
    /// Datei mit gleichem Namen aber abweichender Groesse bekommt einen kollisionssicheren Namen.
    /// Ursprungsdatei im Ziel bleibt unveraendert.
    /// </summary>
    [Fact]
    public void Archive_KollisionBeiAbweichenderGroesse_NeuerName()
    {
        var sourceFolder  = Path.Combine(Path.GetTempPath(), "srcC_" + Guid.NewGuid().ToString("N"));
        var projectFolder = Path.Combine(Path.GetTempPath(), "prjC_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(sourceFolder);
        Directory.CreateDirectory(projectFolder);

        try
        {
            // Zieldatei vorab anlegen (anderer Inhalt -> andere Groesse)
            var dbDir = ProjectStructure.ImportdateienDir(projectFolder, ProjectStructure.Datenbanken);
            Directory.CreateDirectory(dbDir);
            File.WriteAllText(Path.Combine(dbDir, "a.db3"), "ANDERS-INHALT");

            // Quelldatei mit abweichendem Inhalt
            File.WriteAllText(Path.Combine(sourceFolder, "a.db3"), "db");

            var r = ImportSourceArchiver.Archive(sourceFolder, projectFolder);

            // Original bleibt, neuer Name vergeben
            Assert.Equal(1, r.Copied);
            Assert.Equal(0, r.Reused);
            Assert.True(File.Exists(Path.Combine(dbDir, "a.db3")), "Originaldatei muss erhalten bleiben");
            // Mindestens eine Nachricht ueber die Kollision
            Assert.NotEmpty(r.Messages);
        }
        finally
        {
            try { Directory.Delete(sourceFolder,  true); } catch { }
            try { Directory.Delete(projectFolder, true); } catch { }
        }
    }

    /// <summary>
    /// .fdb-Dateien werden ebenfalls nach Datenbanken kopiert.
    /// </summary>
    [Fact]
    public void Archive_FdbWirdNachDatenbankenKopiert()
    {
        var sourceFolder  = Path.Combine(Path.GetTempPath(), "srcF_" + Guid.NewGuid().ToString("N"));
        var projectFolder = Path.Combine(Path.GetTempPath(), "prjF_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(sourceFolder);
        Directory.CreateDirectory(projectFolder);

        try
        {
            File.WriteAllText(Path.Combine(sourceFolder, "Arizona.fdb"), "fdb-inhalt");

            var r = ImportSourceArchiver.Archive(sourceFolder, projectFolder);

            Assert.Equal(1, r.Copied);
            var dbDir = ProjectStructure.ImportdateienDir(projectFolder, ProjectStructure.Datenbanken);
            Assert.True(File.Exists(Path.Combine(dbDir, "Arizona.fdb")), "Arizona.fdb muss in Datenbanken sein");
        }
        finally
        {
            try { Directory.Delete(sourceFolder,  true); } catch { }
            try { Directory.Delete(projectFolder, true); } catch { }
        }
    }

    /// <summary>
    /// Ignorierte Endungen (.mp4, .avi, .jpg usw.) fuehren zu Copied==0.
    /// </summary>
    [Fact]
    public void Archive_IgnoriertVideoUndBilddateien()
    {
        var sourceFolder  = Path.Combine(Path.GetTempPath(), "srcI_" + Guid.NewGuid().ToString("N"));
        var projectFolder = Path.Combine(Path.GetTempPath(), "prjI_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(sourceFolder);
        Directory.CreateDirectory(projectFolder);

        try
        {
            File.WriteAllText(Path.Combine(sourceFolder, "video.mp4"), "mp4");
            File.WriteAllText(Path.Combine(sourceFolder, "video.avi"), "avi");
            File.WriteAllText(Path.Combine(sourceFolder, "bild.jpg"),  "jpg");

            var r = ImportSourceArchiver.Archive(sourceFolder, projectFolder);

            Assert.Equal(0, r.Copied);
            Assert.Equal(0, r.Reused);
        }
        finally
        {
            try { Directory.Delete(sourceFolder,  true); } catch { }
            try { Directory.Delete(projectFolder, true); } catch { }
        }
    }

    /// <summary>
    /// Rekursive Enumeration: Dateien in Unterordnern des Quellordners werden ebenfalls erfasst.
    /// </summary>
    [Fact]
    public void Archive_ErfasstDateienRekursiv()
    {
        var sourceFolder  = Path.Combine(Path.GetTempPath(), "srcR_" + Guid.NewGuid().ToString("N"));
        var projectFolder = Path.Combine(Path.GetTempPath(), "prjR_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(sourceFolder);
        Directory.CreateDirectory(projectFolder);

        try
        {
            var sub = Path.Combine(sourceFolder, "Unterordner");
            Directory.CreateDirectory(sub);
            File.WriteAllText(Path.Combine(sub, "tief.pdf"), "pdf");

            var r = ImportSourceArchiver.Archive(sourceFolder, projectFolder);

            Assert.Equal(1, r.Copied);
            var pdfDir = ProjectStructure.ImportdateienDir(projectFolder, ProjectStructure.PdfDir);
            Assert.True(File.Exists(Path.Combine(pdfDir, "tief.pdf")), "tief.pdf muss in PDF sein");
        }
        finally
        {
            try { Directory.Delete(sourceFolder,  true); } catch { }
            try { Directory.Delete(projectFolder, true); } catch { }
        }
    }
}
