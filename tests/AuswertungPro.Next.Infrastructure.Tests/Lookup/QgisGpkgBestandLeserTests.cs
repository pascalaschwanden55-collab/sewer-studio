using AuswertungPro.Next.Application.Lookup;
using AuswertungPro.Next.Infrastructure.Lookup;
using Microsoft.Data.Sqlite;

namespace AuswertungPro.Next.Infrastructure.Tests.Lookup;

/// <summary>
/// Der GeoPackage-Leser gegen eine im Test erzeugte Datei. Ein GeoPackage ist eine
/// SQLite-Datenbank; die Testdatei ist deshalb eine echte, keine Nachbildung.
/// </summary>
public sealed class QgisGpkgBestandLeserTests : IDisposable
{
    private readonly string _dir;

    public QgisGpkgBestandLeserTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"qgis-gpkg-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_dir, recursive: true); } catch { /* Aufraeumen ist Nebensache */ }
    }

    [Fact]
    public void Der_Leser_findet_die_Tabelle_ueber_die_Namensspalte()
    {
        var datei = Leitungen(
            ("80638-80631", "Steinzeug", "300"),
            ("80631-80551", "Beton_Normalbeton", "400"));

        var bestand = new QgisGpkgBestandLeser(_ => datei).Lies(BauteilArt.Haltung);

        Assert.Equal(2, bestand.GeleseneObjekte);
        Assert.Equal("Steinzeug", bestand.Finde("80638-80631")!.Werte["ha_material"]);
        Assert.Equal("400", bestand.Finde("80631-80551")!.Werte["ha_lichte_hoehe"]);
    }

    // Der Layername traegt in echten Ausgaben Umlaute und Zusaetze
    // ("Schaechte-Selektioniert-Ausfuehrung_durch"). Erkannt wird deshalb ueber die
    // Spalte, nicht ueber den Tabellennamen.
    [Fact]
    public void Ein_ungewoehnlicher_Tabellenname_stoert_nicht()
    {
        var datei = Path.Combine(_dir, "schaechte.gpkg");
        Erzeuge(datei, "Schächte-Selektioniert-Ausführung_durch",
            ["bw_bezeichnung", "ns_funktion", "ns_material"],
            [["80401", "Kontroll_Einsteigschacht", "Beton"]]);

        var bestand = new QgisGpkgBestandLeser(_ => datei).Lies(BauteilArt.Schacht);

        Assert.Equal("Kontroll_Einsteigschacht", bestand.Finde("80401")!.Werte["ns_funktion"]);
    }

    // 2574 Haltungsnamen tragen im echten Bestand mehr als ein Objekt. Beide
    // muessen unbrauchbar werden — auch das zuerst gelesene.
    [Fact]
    public void Ein_doppelter_Name_wird_ganz_verworfen()
    {
        var datei = Leitungen(
            ("u-u", "Steinzeug", "300"),
            ("80631-80551", "Beton_Normalbeton", "400"),
            ("u-u", "Zement", "500"));

        var bestand = new QgisGpkgBestandLeser(_ => datei).Lies(BauteilArt.Haltung);

        Assert.Null(bestand.Finde("u-u"));
        Assert.True(bestand.IstMehrdeutig("u-u"));
        Assert.NotNull(bestand.Finde("80631-80551"));
        Assert.Equal(3, bestand.GeleseneObjekte);
    }

    [Fact]
    public void Ein_leerer_Name_wird_uebersprungen()
    {
        var datei = Leitungen(("", "Steinzeug", "300"), ("80631-80551", "Zement", "400"));

        var bestand = new QgisGpkgBestandLeser(_ => datei).Lies(BauteilArt.Haltung);

        Assert.Single(bestand.JeName);
        Assert.Empty(bestand.Mehrdeutig);
    }

    // Verschiedene QGIS-Ausgaben fuehren verschiedene Spaltenmengen. Eine fehlende
    // Spalte ist kein Grund, den ganzen Lauf zu verweigern.
    [Fact]
    public void Eine_fehlende_Spalte_verhindert_den_Lauf_nicht()
    {
        var datei = Path.Combine(_dir, "knapp.gpkg");
        Erzeuge(datei, "leitungen", ["ne_bezeichnung", "ha_material"],
            [["80638-80631", "Steinzeug"]]);

        var bestand = new QgisGpkgBestandLeser(_ => datei).Lies(BauteilArt.Haltung);

        Assert.Equal("Steinzeug", bestand.Finde("80638-80631")!.Werte["ha_material"]);
        Assert.False(bestand.Finde("80638-80631")!.Werte.ContainsKey("ha_lichte_hoehe"));
    }

    // Eine fehlende Datei darf nie wie "nichts gefunden" aussehen — sonst haelt der
    // Benutzer eine Stoerung fuer eine Datenluecke.
    [Fact]
    public void Eine_fehlende_Datei_wirft()
    {
        var leser = new QgisGpkgBestandLeser(_ => Path.Combine(_dir, "gibtsnicht.gpkg"));

        Assert.Throws<FileNotFoundException>(() => leser.Lies(BauteilArt.Haltung));
    }

    [Fact]
    public void Eine_Datei_ohne_passende_Tabelle_wirft()
    {
        var datei = Path.Combine(_dir, "fremd.gpkg");
        Erzeuge(datei, "irgendwas", ["nummer", "text"], [["1", "a"]]);

        var leser = new QgisGpkgBestandLeser(_ => datei);

        Assert.Throws<InvalidDataException>(() => leser.Lies(BauteilArt.Haltung));
    }

    // Die Datei gehoert dem Benutzer und liegt ausserhalb des Projekts.
    [Fact]
    public void Die_Quelldatei_bleibt_bytegleich()
    {
        var datei = Leitungen(("80638-80631", "Steinzeug", "300"));
        var vorher = System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(datei));

        new QgisGpkgBestandLeser(_ => datei).Lies(BauteilArt.Haltung);
        SqliteConnection.ClearAllPools();

        Assert.Equal(vorher, System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(datei)));
    }

    private string Leitungen(params (string Name, string Material, string Hoehe)[] zeilen)
    {
        var datei = Path.Combine(_dir, "leitungen.gpkg");
        Erzeuge(datei, "Leitungen lokal",
            ["ne_bezeichnung", "ha_material", "ha_lichte_hoehe"],
            zeilen.Select(z => new[] { z.Name, z.Material, z.Hoehe }).ToList());
        return datei;
    }

    /// <summary>
    /// Baut eine kleine, echte GeoPackage-Datei: die Tabelle plus den Eintrag in
    /// <c>gpkg_contents</c>, ueber den der Leser sie findet.
    /// </summary>
    private static void Erzeuge(
        string datei, string tabelle, string[] spalten, IReadOnlyList<string[]> zeilen)
    {
        using var db = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = datei,
            Mode = SqliteOpenMode.ReadWriteCreate
        }.ToString());
        db.Open();

        Fuehre(db, "CREATE TABLE gpkg_contents (table_name TEXT, data_type TEXT)");
        Fuehre(db, $"CREATE TABLE \"{tabelle}\" ({string.Join(", ", spalten.Select(s => $"\"{s}\" TEXT"))})");
        Fuehre(db, $"INSERT INTO gpkg_contents VALUES ('{tabelle}', 'features')");

        foreach (var zeile in zeilen)
        {
            var werte = string.Join(", ", zeile.Select(w => $"'{w.Replace("'", "''")}'"));
            Fuehre(db, $"INSERT INTO \"{tabelle}\" VALUES ({werte})");
        }

        // Microsoft.Data.Sqlite haelt die Verbindung im Pool, auch nach Dispose.
        // Ohne das Leeren bleibt die Datei gesperrt und der Test kann sie nicht lesen.
        db.Close();
        SqliteConnection.ClearAllPools();
    }

    private static void Fuehre(SqliteConnection db, string sql)
    {
        using var befehl = db.CreateCommand();
        befehl.CommandText = sql;
        befehl.ExecuteNonQuery();
    }
}
