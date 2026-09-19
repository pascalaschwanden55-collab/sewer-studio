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

    [Fact]
    public void Weitere_Leitungen_am_Schachtpunkt_kommen_ueber_den_Raumindex()
    {
        var pfad = Path.Combine(_ordner, "leitungen-raumindex.gpkg");
        SchreibeLeitungenMitRaumindex(pfad,
        [
            ("80409-80538", 250.0, "Beton_Normalbeton", [(2692630.471, 1192370.448), (2692593.472, 1192373.417)]),
            ("u-80409", 115.0, "Kunststoff_Hartpolyethylen", [(2692640.0, 1192380.0), (2692630.471, 1192370.448)]), // endet im Schacht
            ("07.999-1", 125.0, "Kunststoff", [(2692630.471, 1192370.448), (2692620.0, 1192360.0)]),              // beginnt im Schacht
            ("FERN", 200.0, "Beton", [(2692634.0, 1192370.448), (2692700.0, 1192370.448)]),                        // 3.5 m daneben
        ]);
        var leser = new QgisGpkgSchachtLageLeser(() => _schaechte, () => pfad);

        var lage = leser.Lies("80409", ["80409-80538"]);

        Assert.NotNull(lage);
        Assert.Single(lage!.AzimutJeHaltung);
        Assert.Equal(2, lage.WeitereLeitungen.Count);

        var hausanschluss = Assert.Single(lage.WeitereLeitungen, l => l.Name == "u-80409");
        Assert.True(hausanschluss.EndetImSchacht);
        Assert.Equal(115, hausanschluss.DnMm);
        Assert.False(string.IsNullOrWhiteSpace(hausanschluss.Material));
        Assert.InRange(hausanschluss.AzimutGrad, 40, 50);

        var abgang = Assert.Single(lage.WeitereLeitungen, l => l.Name == "07.999-1");
        Assert.False(abgang.EndetImSchacht);
        Assert.InRange(abgang.AzimutGrad, 220, 230);

        Assert.DoesNotContain(lage.WeitereLeitungen, l => l.Name is "FERN" or "80409-80538");
    }

    [Fact]
    public void Ohne_Raumindex_bleibt_die_Liste_der_weiteren_Leitungen_leer()
    {
        var leser = new QgisGpkgSchachtLageLeser(() => _schaechte, () => _leitungen);

        var lage = leser.Lies("80409", ["80409-80538"]);

        Assert.NotNull(lage);
        Assert.Empty(lage!.WeitereLeitungen);
    }

    /// <summary>
    /// Leitungen mit Sachspalten und Raumindex. Der echte Index ist eine R-Tree-Tabelle; fuer den
    /// Leser zaehlt nur ihr Inhalt (id = rowid, minx/maxx/miny/maxy), deshalb genuegt hier eine
    /// gewoehnliche Tabelle desselben Namens.
    /// </summary>
    private static void SchreibeLeitungenMitRaumindex(string pfad, (string Name, double Dn, string Material, (double X, double Y)[] Punkte)[] zeilen)
    {
        using var db = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = pfad }.ToString());
        db.Open();
        using (var befehl = db.CreateCommand())
        {
            befehl.CommandText =
                "CREATE TABLE gpkg_contents (table_name TEXT NOT NULL, data_type TEXT NOT NULL);" +
                "INSERT INTO gpkg_contents VALUES ('leitungen', 'features');" +
                "CREATE TABLE \"leitungen\" (fid INTEGER PRIMARY KEY, ne_bezeichnung TEXT, geom BLOB, ha_lichte_hoehe REAL, ha_material TEXT);" +
                "CREATE TABLE \"rtree_leitungen_geom\" (id INTEGER PRIMARY KEY, minx REAL, maxx REAL, miny REAL, maxy REAL);";
            befehl.ExecuteNonQuery();
        }

        var fid = 0;
        foreach (var (name, dn, material, punkte) in zeilen)
        {
            fid++;
            using (var befehl = db.CreateCommand())
            {
                befehl.CommandText = "INSERT INTO \"leitungen\" (fid, ne_bezeichnung, geom, ha_lichte_hoehe, ha_material) VALUES (@f, @n, @g, @d, @m)";
                befehl.Parameters.AddWithValue("@f", fid);
                befehl.Parameters.AddWithValue("@n", name);
                befehl.Parameters.AddWithValue("@g", Blob(Linie(punkte)));
                befehl.Parameters.AddWithValue("@d", dn);
                befehl.Parameters.AddWithValue("@m", material);
                befehl.ExecuteNonQuery();
            }

            using (var befehl = db.CreateCommand())
            {
                befehl.CommandText = "INSERT INTO \"rtree_leitungen_geom\" (id, minx, maxx, miny, maxy) VALUES (@f, @x0, @x1, @y0, @y1)";
                befehl.Parameters.AddWithValue("@f", fid);
                befehl.Parameters.AddWithValue("@x0", punkte.Min(p => p.X));
                befehl.Parameters.AddWithValue("@x1", punkte.Max(p => p.X));
                befehl.Parameters.AddWithValue("@y0", punkte.Min(p => p.Y));
                befehl.Parameters.AddWithValue("@y1", punkte.Max(p => p.Y));
                befehl.ExecuteNonQuery();
            }
        }
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
