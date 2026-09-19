using System.Buffers.Binary;
using AuswertungPro.Next.Infrastructure.Lookup;
using Microsoft.Data.Sqlite;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Schachtgrafik Stammkarte: Schachtpunkt und Leitungsrichtungen aus kleinen, selbst
/// geschriebenen GeoPackage-Dateien (SQLite mit gpkg_contents und geom-Blobs).
/// </summary>
public sealed class QgisGpkgSchachtLageLeserTests : IDisposable
{
    private readonly string _ordner = Path.Combine(Path.GetTempPath(), "schachtlage-" + Guid.NewGuid().ToString("N"));
    private readonly string _schaechte;
    private readonly string _leitungen;

    public QgisGpkgSchachtLageLeserTests()
    {
        Directory.CreateDirectory(_ordner);
        _schaechte = Path.Combine(_ordner, "schaechte.gpkg");
        _leitungen = Path.Combine(_ordner, "leitungen.gpkg");

        SchreibeGpkg(_schaechte, "schaechte", "bw_bezeichnung",
        [
            ("80409", Blob(Punkt(2692630.471, 1192370.448))),
            ("80538", Blob(Punkt(2692593.472, 1192373.417))),
            ("DOPPELT", Blob(Punkt(1, 1))),
            ("DOPPELT", Blob(Punkt(2, 2))),
        ]);
        SchreibeGpkg(_leitungen, "leitungen", "ne_bezeichnung",
        [
            ("80409-80538", Blob(Linie((2692630.471, 1192370.448), (2692593.472, 1192373.417)))),
            ("80547-80409", Blob(Linie((2692653.023, 1192412.999), (2692630.471, 1192370.448)))),
            ("MEHRDEUTIG", Blob(Linie((2692630.471, 1192370.448), (2692630.471, 1192400.0)))),
            ("MEHRDEUTIG", Blob(Linie((2692630.471, 1192370.448), (2692660.0, 1192370.448)))),
            ("FERN", Blob(Linie((2692700.0, 1192370.448), (2692760.0, 1192370.448)))),
        ]);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_ordner, recursive: true); } catch (IOException) { }
    }

    [Fact]
    public void Liefert_Punkt_und_Azimute_nur_fuer_eindeutige_und_angeschlossene_Haltungen()
    {
        var leser = new QgisGpkgSchachtLageLeser(() => _schaechte, () => _leitungen);

        var lage = leser.Lies("80409", ["80409-80538", "80547-80409", "MEHRDEUTIG", "FERN", "UNBEKANNT"]);

        Assert.NotNull(lage);
        Assert.Equal(2692630.471, lage!.Schachtpunkt.Ost, 3);
        Assert.Equal(2, lage.AzimutJeHaltung.Count);
        Assert.InRange(lage.AzimutJeHaltung["80409-80538"], 274, 276);
        Assert.InRange(lage.AzimutJeHaltung["80547-80409"], 20, 40);
        Assert.False(lage.AzimutJeHaltung.ContainsKey("MEHRDEUTIG"));
        Assert.False(lage.AzimutJeHaltung.ContainsKey("FERN"));
    }

    [Fact]
    public void Ein_mehrdeutiger_oder_unbekannter_Schacht_liefert_nichts()
    {
        var leser = new QgisGpkgSchachtLageLeser(() => _schaechte, () => _leitungen);

        Assert.Null(leser.Lies("DOPPELT", ["80409-80538"]));
        Assert.Null(leser.Lies("99999", ["80409-80538"]));
        Assert.Null(leser.Lies("  ", ["80409-80538"]));
    }

    [Fact]
    public void Ohne_Leitungsdatei_bleibt_der_Punkt_ohne_Richtungen()
    {
        var leser = new QgisGpkgSchachtLageLeser(() => _schaechte, () => Path.Combine(_ordner, "fehlt.gpkg"));

        var lage = leser.Lies("80409", ["80409-80538"]);

        Assert.NotNull(lage);
        Assert.Empty(lage!.AzimutJeHaltung);
    }

    [Fact]
    public void Ohne_Schachtdatei_oder_mit_Muell_gibt_es_null_statt_einer_Ausnahme()
    {
        var muell = Path.Combine(_ordner, "muell.gpkg");
        File.WriteAllText(muell, "das ist keine Datenbank");

        Assert.Null(new QgisGpkgSchachtLageLeser(() => null, () => _leitungen).Lies("80409", []));
        Assert.Null(new QgisGpkgSchachtLageLeser(() => muell, () => _leitungen).Lies("80409", []));
    }

    private static void SchreibeGpkg(string pfad, string tabelle, string namensspalte, (string Name, byte[] Geom)[] zeilen)
    {
        using var db = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = pfad }.ToString());
        db.Open();
        using (var befehl = db.CreateCommand())
        {
            befehl.CommandText =
                "CREATE TABLE gpkg_contents (table_name TEXT NOT NULL, data_type TEXT NOT NULL);" +
                $"INSERT INTO gpkg_contents VALUES ('{tabelle}', 'features');" +
                $"CREATE TABLE \"{tabelle}\" (fid INTEGER PRIMARY KEY, \"{namensspalte}\" TEXT, geom BLOB);";
            befehl.ExecuteNonQuery();
        }

        foreach (var (name, geom) in zeilen)
        {
            using var befehl = db.CreateCommand();
            befehl.CommandText = $"INSERT INTO \"{tabelle}\" (\"{namensspalte}\", geom) VALUES (@n, @g)";
            befehl.Parameters.AddWithValue("@n", name);
            befehl.Parameters.AddWithValue("@g", geom);
            befehl.ExecuteNonQuery();
        }
    }

    private static byte[] Blob(byte[] wkb)
    {
        var kopf = new byte[8];
        kopf[0] = (byte)'G';
        kopf[1] = (byte)'P';
        kopf[3] = 0x01;
        BinaryPrimitives.WriteInt32LittleEndian(kopf.AsSpan(4, 4), 2056);
        return [.. kopf, .. wkb];
    }

    private static byte[] Punkt(double x, double y)
    {
        var b = new byte[21];
        b[0] = 1;
        BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(1, 4), 1);
        BinaryPrimitives.WriteDoubleLittleEndian(b.AsSpan(5, 8), x);
        BinaryPrimitives.WriteDoubleLittleEndian(b.AsSpan(13, 8), y);
        return b;
    }

    private static byte[] Linie(params (double X, double Y)[] punkte)
    {
        var b = new byte[9 + punkte.Length * 16];
        b[0] = 1;
        BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(1, 4), 2);
        BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(5, 4), (uint)punkte.Length);
        for (var i = 0; i < punkte.Length; i++)
        {
            BinaryPrimitives.WriteDoubleLittleEndian(b.AsSpan(9 + i * 16, 8), punkte[i].X);
            BinaryPrimitives.WriteDoubleLittleEndian(b.AsSpan(17 + i * 16, 8), punkte[i].Y);
        }

        return b;
    }
}
