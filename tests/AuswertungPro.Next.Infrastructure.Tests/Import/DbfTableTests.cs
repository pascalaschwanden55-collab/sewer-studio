using System;
using System.IO;
using AuswertungPro.Next.Infrastructure.Import.Dbf;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

/// <summary>
/// Eigener FoxPro-/dBase-DBF-Leser (KINS-Stammdaten: haltung.DBF/schacht.DBF).
/// Die Fixtures werden byteweise gebaut (DbfTestFileBuilder) — exakt nach dem
/// Format der echten KINS-Dateien (Version 0x30, CP1252, Int32-Binaerfelder,
/// FPT-Memos).
/// </summary>
public sealed class DbfTableTests : IDisposable
{
    private readonly string _dir;

    public DbfTableTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "DbfTableTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    [Fact]
    public void Read_LiestZeichenUndZahlenfelder_MitUmlauten()
    {
        var pfad = Path.Combine(_dir, "haltung.DBF");
        new DbfTestFileBuilder()
            .Feld("STRASSE", 'C', 12)
            .Feld("HALTLAENGE", 'N', 7, 2)
            .Record(r => r.Text("Grünweg").Text("  30.40"))
            .Schreiben(pfad);

        var tabelle = DbfTable.Read(pfad);

        Assert.Equal(2, tabelle.Fields.Count);
        Assert.Equal("STRASSE", tabelle.Fields[0].Name);
        var row = Assert.Single(tabelle.Rows);
        Assert.Equal("Grünweg", row["STRASSE"]);
        Assert.Equal("30.40", row["HALTLAENGE"]);
    }

    [Fact]
    public void Read_LiestInt32Binaerfelder()
    {
        // KINS nutzt VFP-Typ 'I' (4 Bytes little-endian) fuer NR/S_O/S_U.
        var pfad = Path.Combine(_dir, "haltung.DBF");
        new DbfTestFileBuilder()
            .Feld("S_O", 'I', 4)
            .Feld("S_U", 'I', 4)
            .Record(r => r.Int32(58951).Int32(58950))
            .Schreiben(pfad);

        var row = Assert.Single(DbfTable.Read(pfad).Rows);
        Assert.Equal("58951", row["S_O"]);
        Assert.Equal("58950", row["S_U"]);
    }

    [Fact]
    public void Read_UeberspringtGeloeschteRecords()
    {
        var pfad = Path.Combine(_dir, "t.DBF");
        new DbfTestFileBuilder()
            .Feld("BEZ", 'C', 5)
            .Record(r => r.Text("aktiv"))
            .Record(r => r.Text("wegge"), geloescht: true)
            .Schreiben(pfad);

        var rows = DbfTable.Read(pfad).Rows;

        var row = Assert.Single(rows);
        Assert.Equal("aktiv", row["BEZ"]);
    }

    [Fact]
    public void Read_LiestMemoAusFptDatei()
    {
        var pfad = Path.Combine(_dir, "haltung.DBF");
        var blockNr = DbfTestFileBuilder.SchreibeFpt(Path.ChangeExtension(pfad, ".FPT"), "Bemerkung mit äöü");
        new DbfTestFileBuilder()
            .Feld("Z_BEM", 'M', 4)
            .Record(r => r.Int32(blockNr))
            .Schreiben(pfad);

        var row = Assert.Single(DbfTable.Read(pfad).Rows);
        Assert.Equal("Bemerkung mit äöü", row["Z_BEM"]);
    }

    [Fact]
    public void Read_MemoOhneFptDatei_BleibtLeer_OhneFehler()
    {
        var pfad = Path.Combine(_dir, "haltung.DBF");
        new DbfTestFileBuilder()
            .Feld("Z_BEM", 'M', 4)
            .Record(r => r.Int32(1))
            .Schreiben(pfad);

        var row = Assert.Single(DbfTable.Read(pfad).Rows);
        Assert.Equal("", row["Z_BEM"]);
    }

    [Fact]
    public void Read_LiestDatumsfeldRoh()
    {
        var pfad = Path.Combine(_dir, "t.DBF");
        new DbfTestFileBuilder()
            .Feld("DATUM", 'D', 8)
            .Record(r => r.Text("20260624"))
            .Schreiben(pfad);

        var row = Assert.Single(DbfTable.Read(pfad).Rows);
        Assert.Equal("20260624", row["DATUM"]);
    }

    [Fact]
    public void Read_LeereTabelle_LiefertKeineRows()
    {
        var pfad = Path.Combine(_dir, "leer.DBF");
        new DbfTestFileBuilder().Feld("BEZ", 'C', 5).Schreiben(pfad);

        var tabelle = DbfTable.Read(pfad);

        Assert.Empty(tabelle.Rows);
        Assert.Single(tabelle.Fields);
    }

    [Fact]
    public void Read_KaputteDatei_WirftVerstaendlicheAusnahme()
    {
        var pfad = Path.Combine(_dir, "kaputt.DBF");
        File.WriteAllBytes(pfad, new byte[] { 0x30, 0x01, 0x02 });

        Assert.Throws<InvalidDataException>(() => DbfTable.Read(pfad));
    }
}
