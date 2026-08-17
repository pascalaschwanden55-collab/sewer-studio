using System;
using System.IO;
using AuswertungPro.Next.Infrastructure.Import;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Tests fuer KanalExportDetector — Format-Erkennung (WinCan / IKAS / Unknown / Ambiguous).
/// Alle Tests legen synthetische Temp-Fixtures an und raeumen danach auf.
/// </summary>
public sealed class KanalExportDetectorTests
{
    // -------------------------------------------------------------------------
    // Hilfsmethode: isoliertes Temp-Verzeichnis
    // -------------------------------------------------------------------------
    private sealed class TempDir : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "kanal-export-detector-tests",
            Guid.NewGuid().ToString("N"));

        public TempDir()
        {
            Directory.CreateDirectory(Path);
        }

        public void Dispose()
        {
            try { Directory.Delete(Path, recursive: true); } catch { }
        }
    }

    // -------------------------------------------------------------------------
    // WinCan-Erkennung
    // -------------------------------------------------------------------------

    [Fact]
    public void Detect_WinCan_GibtFormatWinCanUndDb3Path()
    {
        // Arrange: DB-Ordner mit echter .db3 und einer auszuschliessenden _Meta.db3
        using var tmp = new TempDir();
        var dbDir = System.IO.Path.Combine(tmp.Path, "DB");
        Directory.CreateDirectory(dbDir);
        File.WriteAllText(System.IO.Path.Combine(dbDir, "proj.db3"), "dummy-db3-content");
        File.WriteAllText(System.IO.Path.Combine(dbDir, "proj_Meta.db3"), "dummy-meta-content");

        // Act
        var result = KanalExportDetector.Detect(tmp.Path);

        // Assert
        Assert.Equal(KanalExportFormat.WinCan, result.Format);
        Assert.NotNull(result.Db3Path);
        Assert.Equal(System.IO.Path.Combine(dbDir, "proj.db3"), result.Db3Path);
        Assert.Null(result.VsaKekXtfPath);
        Assert.Null(result.Sia405XtfPath);
    }

    [Fact]
    public void Detect_WinCan_NurMetaDb3_GibtUnknown()
    {
        // Arrange: nur _Meta.db3 vorhanden — kein echter DB
        using var tmp = new TempDir();
        var dbDir = System.IO.Path.Combine(tmp.Path, "DB");
        Directory.CreateDirectory(dbDir);
        File.WriteAllText(System.IO.Path.Combine(dbDir, "proj_Meta.db3"), "meta-only");

        // Act
        var result = KanalExportDetector.Detect(tmp.Path);

        // Assert: _Meta.db3 wird ignoriert → kein WinCan → Unknown
        Assert.Equal(KanalExportFormat.Unknown, result.Format);
        Assert.Null(result.Db3Path);
    }

    [Fact]
    public void Detect_WinCan_Db3NurInDB_Ordner_NichtImRoot()
    {
        // Arrange: .db3 liegt direkt im Root (kein DB-Unterordner) — soll NICHT als WinCan erkannt werden
        using var tmp = new TempDir();
        File.WriteAllText(System.IO.Path.Combine(tmp.Path, "proj.db3"), "db-in-root");

        // Act
        var result = KanalExportDetector.Detect(tmp.Path);

        // Assert
        Assert.NotEqual(KanalExportFormat.WinCan, result.Format);
        Assert.Null(result.Db3Path);
    }

    [Fact]
    public void Detect_WinCan_GroessteDatenbank_WirdGewaehlt()
    {
        // Arrange: DB-Ordner mit zwei echten .db3 unterschiedlicher Groesse
        using var tmp = new TempDir();
        var dbDir = System.IO.Path.Combine(tmp.Path, "DB");
        Directory.CreateDirectory(dbDir);
        File.WriteAllText(System.IO.Path.Combine(dbDir, "small.db3"), "ab");
        File.WriteAllText(System.IO.Path.Combine(dbDir, "large.db3"), new string('X', 1000));

        // Act
        var result = KanalExportDetector.Detect(tmp.Path);

        // Assert
        Assert.Equal(KanalExportFormat.WinCan, result.Format);
        Assert.NotNull(result.Db3Path);
        Assert.EndsWith("large.db3", result.Db3Path, StringComparison.OrdinalIgnoreCase);
    }

    // -------------------------------------------------------------------------
    // IKAS-Erkennung ueber VSA_KEK-XTF
    // -------------------------------------------------------------------------

    [Fact]
    public void Detect_Ikas_VsaKekXtf_UndSia405Xtf_GibtFormatIkas()
    {
        // Arrange: VSA_KEK-XTF + SIA405-XTF + Arizona.fdb + Film-Ordner
        using var tmp = new TempDir();
        var dokuDir = System.IO.Path.Combine(tmp.Path, "Dokumente");
        Directory.CreateDirectory(dokuDir);

        // VSA_KEK-XTF (darf kein _SIA405 im Namen haben)
        File.WriteAllText(
            System.IO.Path.Combine(dokuDir, "x.xtf"),
            "<TRANSFER><HEADERSECTION><MODEL NAME=\"VSA_KEK_2020_LV95\" /></HEADERSECTION></TRANSFER>");

        // SIA405-XTF
        File.WriteAllText(
            System.IO.Path.Combine(dokuDir, "x_SIA405.xtf"),
            "<TRANSFER><MODEL NAME=\"SIA405_Abwasser_2015_LV95\" /></TRANSFER>");

        // Arizona.fdb (leer — Existenz genuegt fuer KIAS-Heuristik)
        var dataDir = System.IO.Path.Combine(tmp.Path, "Data");
        Directory.CreateDirectory(dataDir);
        File.WriteAllText(System.IO.Path.Combine(dataDir, "Arizona.fdb"), string.Empty);

        // Film-Ordner (fuer KIAS-Erkennung ueber KiasExportPattern)
        Directory.CreateDirectory(System.IO.Path.Combine(tmp.Path, "Film"));
        File.WriteAllText(System.IO.Path.Combine(tmp.Path, "Film", "Daten.txt"), "dummy");

        // Act
        var result = KanalExportDetector.Detect(tmp.Path);

        // Assert
        Assert.Equal(KanalExportFormat.Ikas, result.Format);
        Assert.NotNull(result.VsaKekXtfPath);
        Assert.NotNull(result.Sia405XtfPath);
        Assert.Null(result.Db3Path);
    }

    [Fact]
    public void Detect_Ikas_NurVsaKekXtf_OhneKiasPattern_GibtIkas()
    {
        // Arrange: nur VSA_KEK-XTF, kein KIAS-Pattern (kein .fdb/Film)
        using var tmp = new TempDir();
        File.WriteAllText(
            System.IO.Path.Combine(tmp.Path, "export.xtf"),
            "Irgendwas VSA_KEK_2020_LV95 noch mehr Text");

        // Act
        var result = KanalExportDetector.Detect(tmp.Path);

        // Assert: XTF-Inhalt erkennt IKAS
        Assert.Equal(KanalExportFormat.Ikas, result.Format);
        Assert.NotNull(result.VsaKekXtfPath);
    }

    [Fact]
    public void Detect_Ibak_KiasPatternOhneXtf_GibtIbak()
    {
        // Arrange: echtes KIAS-Pattern (Arizona.fdb + Film + Daten.txt), kein XTF
        using var tmp = new TempDir();
        File.WriteAllText(System.IO.Path.Combine(tmp.Path, "Arizona.fdb"), string.Empty);
        var filmDir = System.IO.Path.Combine(tmp.Path, "Film");
        Directory.CreateDirectory(filmDir);
        File.WriteAllText(System.IO.Path.Combine(filmDir, "Daten.txt"), "dummy-daten");

        // Act
        var result = KanalExportDetector.Detect(tmp.Path);

        // Assert
        Assert.Equal(KanalExportFormat.Ibak, result.Format);
        Assert.Null(result.VsaKekXtfPath);
        Assert.Null(result.Sia405XtfPath);
        Assert.Null(result.Db3Path);
    }

    // -------------------------------------------------------------------------
    // Leerer Ordner → Unknown
    // -------------------------------------------------------------------------

    [Fact]
    public void Detect_LeererOrdner_GibtUnknown()
    {
        // Arrange: leeres Verzeichnis
        using var tmp = new TempDir();

        // Act
        var result = KanalExportDetector.Detect(tmp.Path);

        // Assert
        Assert.Equal(KanalExportFormat.Unknown, result.Format);
        Assert.Null(result.Db3Path);
        Assert.Null(result.VsaKekXtfPath);
        Assert.Null(result.Sia405XtfPath);
    }

    [Fact]
    public void Detect_NichtExistenterPfad_GibtUnknown()
    {
        // Arrange: nicht-existentes Verzeichnis
        var nonExistent = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "DOES_NOT_EXIST_" + Guid.NewGuid().ToString("N"));

        // Act
        var result = KanalExportDetector.Detect(nonExistent);

        // Assert
        Assert.Equal(KanalExportFormat.Unknown, result.Format);
    }

    // -------------------------------------------------------------------------
    // Ambiguous: WinCan + IKAS gleichzeitig
    // -------------------------------------------------------------------------

    [Fact]
    public void Detect_WinCanUndIkas_GibtAmbiguous()
    {
        // Arrange: DB-Ordner mit .db3 UND VSA_KEK-XTF im gleichen Baum
        using var tmp = new TempDir();

        var dbDir = System.IO.Path.Combine(tmp.Path, "DB");
        Directory.CreateDirectory(dbDir);
        File.WriteAllText(System.IO.Path.Combine(dbDir, "proj.db3"), "wincan-data");

        File.WriteAllText(
            System.IO.Path.Combine(tmp.Path, "export.xtf"),
            "Inhalt mit VSA_KEK_2020_LV95 Referenz");

        // Act
        var result = KanalExportDetector.Detect(tmp.Path);

        // Assert
        Assert.Equal(KanalExportFormat.Ambiguous, result.Format);
        Assert.NotNull(result.Db3Path);
        Assert.NotNull(result.VsaKekXtfPath);
    }

    // -------------------------------------------------------------------------
    // Versteckte Ordner/Dateien (Audit 2026-08-17)
    //
    // Die Sucher benutzten die Standard-Aufzaehloptionen von .NET. Deren
    // AttributesToSkip ist "Hidden, System" — ein versteckter Ordner oder eine
    // versteckte Datei verschwand damit lautlos aus dem Import, und der Bericht
    // meldete nur eine kleinere Fundzahl. Nachgemessen auf .NET 10: 1 von 3
    // Dateien gefunden. Kundendaten von optischen Medien, aus Sicherungen oder
    // von Netzlaufwerken tragen diese Merker regelmaessig.
    // -------------------------------------------------------------------------

    [Fact]
    public void Detect_WinCanDb3_ImVerstecktenOrdner_WirdGefunden()
    {
        using var temp = new TempDir();
        var db = Path.Combine(temp.Path, "DB");
        Directory.CreateDirectory(db);
        File.WriteAllText(Path.Combine(db, "projekt.db3"), "x");
        new DirectoryInfo(db).Attributes |= FileAttributes.Hidden;

        var ergebnis = KanalExportDetector.Detect(temp.Path);

        Assert.Equal(KanalExportFormat.WinCan, ergebnis.Format);
        Assert.NotNull(ergebnis.Db3Path);
    }

    [Fact]
    public void Detect_VersteckteDb3Datei_WirdGefunden()
    {
        using var temp = new TempDir();
        var db = Path.Combine(temp.Path, "DB");
        Directory.CreateDirectory(db);
        var datei = Path.Combine(db, "projekt.db3");
        File.WriteAllText(datei, "x");
        File.SetAttributes(datei, FileAttributes.Hidden);

        var ergebnis = KanalExportDetector.Detect(temp.Path);

        Assert.Equal(KanalExportFormat.WinCan, ergebnis.Format);
        Assert.NotNull(ergebnis.Db3Path);
    }
}
