using AuswertungPro.Next.Application.Lookup;
using AuswertungPro.Next.Infrastructure.Lookup;
using Microsoft.Data.Sqlite;

namespace AuswertungPro.Next.Infrastructure.Tests.Lookup;

/// <summary>
/// Der Leser der Kennungstabelle gegen eine im Test erzeugte SQLite-Datei mit
/// demselben Aufbau wie die aus der GEONIS-Kopie gebaute Datei.
/// </summary>
public sealed class KatasterKennungGpkgLeserTests : IDisposable
{
    private readonly string _dir;

    public KatasterKennungGpkgLeserTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"kataster-gpkg-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_dir, recursive: true); } catch { /* Aufraeumen ist Nebensache */ }
    }

    [Fact]
    public void Haltungen_werden_mit_allen_Verbundkennungen_gelesen()
    {
        var datei = Erzeuge(
            haltungen:
            [
                ["78998-79002", "Altdorf", "ch23h1a4uL3A2Sjp", "ch23h1a46oVbkGmT", "ch23h1a4CNjzeqBU", "A75394",
                 "ch23h1a44Op5RVY5", "E75394", "ch23h1a43obhLa8B", "0", "2024/05/27 14:37:28+00"]
            ],
            schaechte: []);

        var bestand = new KatasterKennungGpkgLeser(() => datei).Lies(BauteilArt.Haltung);

        Assert.Equal("GEONIS-Kopie 2024-12", bestand.Stand);
        var k = bestand.Finde("78998-79002")!;
        Assert.Equal(new DateTime(2024, 5, 27, 14, 37, 28, DateTimeKind.Utc), k.GeonisGeaendert);
        Assert.Equal("ch23h1a4uL3A2Sjp", k.Haltung);
        Assert.Equal("ch23h1a46oVbkGmT", k.Kanal);
        Assert.Equal("A75394", k.VonPunktBezeichnung);
        Assert.Equal("ch23h1a44Op5RVY5", k.NachPunkt);
        Assert.Equal("ch23h1a43obhLa8B", k.Rohrprofil);
        Assert.Equal("unbekannt", k.RohrprofilTyp);
    }

    [Fact]
    public void Schaechte_werden_mit_Knoten_und_Bauwerk_gelesen()
    {
        var datei = Erzeuge(
            haltungen: [],
            schaechte: [["78998", "Altdorf", "ch23h1a4ftlGdbHU", "ch23h1a4Umcgr2UF", "2018/12/19 00:00:00+00"]]);

        var bestand = new KatasterKennungGpkgLeser(() => datei).Lies(BauteilArt.Schacht);

        var k = bestand.Finde("78998")!;
        Assert.Equal("ch23h1a4ftlGdbHU", k.Knoten);
        Assert.Equal("ch23h1a4Umcgr2UF", k.Bauwerk);
    }

    // 389 echte Haltungsnamen tragen in der Kopie mehr als ein Objekt. Beide muessen
    // unbrauchbar werden — auch das zuerst gelesene.
    [Fact]
    public void Ein_doppelter_Name_wird_ganz_verworfen()
    {
        var datei = Erzeuge(
            haltungen:
            [
                ["84102-84102", "Altdorf", "ch23h1a4AAAAAAAA", null, null, null, null, null, null, null, null],
                ["84102-84102", "Altdorf", "ch23h1a4BBBBBBBB", null, null, null, null, null, null, null, null]
            ],
            schaechte: []);

        var bestand = new KatasterKennungGpkgLeser(() => datei).Lies(BauteilArt.Haltung);

        Assert.Null(bestand.Finde("84102-84102"));
        Assert.True(bestand.IstMehrdeutig("84102-84102"));
        Assert.Equal(2, bestand.GeleseneObjekte);
    }

    // 26 Haltungen der Kopie haben keine SIA405-Kennung; und die Kennung muss die
    // Form einer STANDARDOID haben, sonst kann der Export sie nicht schreiben.
    [Fact]
    public void Ohne_gueltige_Hauptkennung_gibt_es_kein_Bauteil()
    {
        var datei = Erzeuge(
            haltungen:
            [
                ["1-2", "Altdorf", null, null, null, null, null, null, null, null, null],
                ["3-4", "Altdorf", "12345678C5U3aV7n", null, null, null, null, null, null, null, null],
                ["5-6", "Altdorf", "ch23h1a4CCCCCCCC", "zu-kurz", null, null, null, null, null, null, "kein Datum"]
            ],
            schaechte: []);

        var bestand = new KatasterKennungGpkgLeser(() => datei).Lies(BauteilArt.Haltung);

        Assert.Null(bestand.Finde("1-2"));
        Assert.Null(bestand.Finde("3-4"));
        var k = bestand.Finde("5-6")!;
        Assert.Equal("ch23h1a4CCCCCCCC", k.Haltung);
        Assert.Null(k.Kanal);
        Assert.Null(k.GeonisGeaendert);
    }

    [Fact]
    public void Eine_fehlende_Datei_wirft_statt_leer_zu_liefern()
    {
        var leser = new KatasterKennungGpkgLeser(() => Path.Combine(_dir, "gibt-es-nicht.gpkg"));

        Assert.Throws<FileNotFoundException>(() => leser.Lies(BauteilArt.Haltung));
    }

    [Fact]
    public void Eine_Datei_mit_fremdem_Aufbau_wirft_verstaendlich()
    {
        var datei = Path.Combine(_dir, "fremd.gpkg");
        using (var db = Oeffne(datei))
            Fuehre(db, "CREATE TABLE irgendwas (a TEXT)");
        SqliteConnection.ClearAllPools();

        var ex = Assert.Throws<InvalidDataException>(
            () => new KatasterKennungGpkgLeser(() => datei).Lies(BauteilArt.Haltung));
        Assert.Contains("haltungen", ex.Message, StringComparison.Ordinal);
    }

    private string Erzeuge(IReadOnlyList<string?[]> haltungen, IReadOnlyList<string?[]> schaechte)
    {
        var datei = Path.Combine(_dir, $"{Guid.NewGuid():N}.gpkg");
        using (var db = Oeffne(datei))
        {
            Fuehre(db, "CREATE TABLE herkunft (schluessel TEXT PRIMARY KEY, wert TEXT)");
            Fuehre(db, "INSERT INTO herkunft VALUES ('stand', '2024-12')");
            Fuehre(db,
                "CREATE TABLE haltungen (bezeichnung TEXT, gemeinde TEXT, haltung_id TEXT, kanal_id TEXT, " +
                "vonpunkt_id TEXT, vonpunkt_bezeichnung TEXT, nachpunkt_id TEXT, nachpunkt_bezeichnung TEXT, " +
                "rohrprofil_id TEXT, profiltyp_code INTEGER, geonis_geaendert TEXT)");
            Fuehre(db, "CREATE TABLE schaechte (bezeichnung TEXT, gemeinde TEXT, knoten_id TEXT, bauwerk_id TEXT, geonis_geaendert TEXT)");
            foreach (var zeile in haltungen)
                Fuehre(db, $"INSERT INTO haltungen VALUES ({Werte(zeile)})");
            foreach (var zeile in schaechte)
                Fuehre(db, $"INSERT INTO schaechte VALUES ({Werte(zeile)})");
        }

        // Microsoft.Data.Sqlite haelt die Verbindung im Pool, auch nach Dispose.
        SqliteConnection.ClearAllPools();
        return datei;
    }

    private static string Werte(string?[] zeile)
        => string.Join(", ", zeile.Select(w => w is null ? "NULL" : $"'{w.Replace("'", "''")}'"));

    private static SqliteConnection Oeffne(string datei)
    {
        var db = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = datei,
            Mode = SqliteOpenMode.ReadWriteCreate
        }.ToString());
        db.Open();
        return db;
    }

    private static void Fuehre(SqliteConnection db, string sql)
    {
        using var befehl = db.CreateCommand();
        befehl.CommandText = sql;
        befehl.ExecuteNonQuery();
    }
}
