using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using AuswertungPro.Next.Application.Lookup;
using Microsoft.Data.Sqlite;

namespace AuswertungPro.Next.Infrastructure.Lookup;

/// <summary>
/// Liest den QGIS-Bestand aus einer GeoPackage-Datei.
///
/// Ein GeoPackage ist eine SQLite-Datenbank — es braucht dafuer weder QGIS noch
/// einen Netzzugriff. Genau das ist der Grund fuer diesen Weg: Der Netzdienst des
/// Kantons drosselt, ein Sammellauf ueber ein ganzes Projekt waere dort hunderte
/// Einzelabfragen.
///
/// Die Datei wird ausschliesslich lesend geoeffnet (<c>Mode=ReadOnly</c>) und nie
/// veraendert. Sie gehoert dem Benutzer und liegt ausserhalb des Projekts.
///
/// Fehlt eine Spalte in der Datei, wird sie einfach nicht gelesen — verschiedene
/// QGIS-Ausgaben fuehren verschiedene Spaltenmengen, und ein fehlendes Feld ist
/// kein Grund, den ganzen Lauf zu verweigern.
/// </summary>
public sealed class QgisGpkgBestandLeser : IQgisBestandLeser
{
    private readonly Func<BauteilArt, string> _pfad;

    public QgisGpkgBestandLeser(Func<BauteilArt, string> pfad)
        => _pfad = pfad ?? throw new ArgumentNullException(nameof(pfad));

    public string Quellpfad(BauteilArt art) => _pfad(art) ?? "";

    public QgisBestand Lies(BauteilArt art)
    {
        var datei = Quellpfad(art);
        if (string.IsNullOrWhiteSpace(datei))
            throw new InvalidOperationException("Es ist keine QGIS-Datei eingestellt.");

        if (!File.Exists(datei))
            throw new FileNotFoundException($"Die QGIS-Datei wurde nicht gefunden: {datei}", datei);

        using var db = new SqliteConnection(
            new SqliteConnectionStringBuilder
            {
                DataSource = datei,
                Mode = SqliteOpenMode.ReadOnly
            }.ToString());
        db.Open();

        var tabelle = FindeTabelle(db, art)
            ?? throw new InvalidDataException(
                $"In {Path.GetFileName(datei)} wurde keine Tabelle mit der Spalte " +
                $"'{QgisFeldKarte.Namensspalte(art)}' gefunden.");

        return Lies(db, tabelle, art);
    }

    /// <summary>
    /// Die Tabelle, die zur Bauteilart passt: die erste des GeoPackage, welche die
    /// Namensspalte fuehrt. Der Tabellenname selbst taugt nicht als Merkmal — er
    /// traegt in echten Ausgaben den Layernamen samt Umlauten und Zusaetzen
    /// ("Schaechte-Selektioniert-Ausfuehrung_durch").
    /// </summary>
    private static string? FindeTabelle(SqliteConnection db, BauteilArt art)
    {
        var namensspalte = QgisFeldKarte.Namensspalte(art);

        foreach (var tabelle in Tabellen(db))
        {
            if (Spalten(db, tabelle).Contains(namensspalte, StringComparer.OrdinalIgnoreCase))
                return tabelle;
        }

        return null;
    }

    private static List<string> Tabellen(SqliteConnection db)
    {
        var namen = new List<string>();
        using var befehl = db.CreateCommand();

        // gpkg_contents fuehrt die Layer des GeoPackage. Fehlt sie, ist es keines —
        // dann bleibt die Liste leer und der Aufrufer bekommt eine klare Meldung.
        befehl.CommandText =
            "SELECT table_name FROM gpkg_contents WHERE data_type IN ('features','attributes')";
        try
        {
            using var leser = befehl.ExecuteReader();
            while (leser.Read())
                namen.Add(leser.GetString(0));
        }
        catch (SqliteException)
        {
            return namen;
        }

        return namen;
    }

    private static HashSet<string> Spalten(SqliteConnection db, string tabelle)
    {
        var spalten = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var befehl = db.CreateCommand();
        befehl.CommandText = $"PRAGMA table_info(\"{tabelle.Replace("\"", "\"\"")}\")";
        using var leser = befehl.ExecuteReader();
        while (leser.Read())
            spalten.Add(leser.GetString(1));

        return spalten;
    }

    private static QgisBestand Lies(SqliteConnection db, string tabelle, BauteilArt art)
    {
        var vorhanden = Spalten(db, tabelle);
        var namensspalte = QgisFeldKarte.Namensspalte(art);
        var gewuenscht = QgisFeldKarte.Spalten(art)
            .Where(vorhanden.Contains)
            .ToList();

        var jeName = new Dictionary<string, QgisBauteil>(StringComparer.OrdinalIgnoreCase);
        var mehrdeutig = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var gelesen = 0;

        using var befehl = db.CreateCommand();
        var felder = string.Join(", ", new[] { namensspalte }.Concat(gewuenscht).Select(Zitiere));
        befehl.CommandText = $"SELECT {felder} FROM {Zitiere(tabelle)}";

        using var leser = befehl.ExecuteReader();
        while (leser.Read())
        {
            gelesen++;

            var name = Wert(leser, 0);
            if (string.IsNullOrWhiteSpace(name))
                continue;

            name = name.Trim();

            // Ein zweites Objekt unter demselben Namen macht beide unbrauchbar:
            // Welches gemeint ist, weiss die Datei nicht.
            if (!jeName.ContainsKey(name))
            {
                var werte = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (var i = 0; i < gewuenscht.Count; i++)
                {
                    var text = Wert(leser, i + 1);
                    if (!string.IsNullOrWhiteSpace(text))
                        werte[gewuenscht[i]] = text.Trim();
                }

                jeName[name] = new QgisBauteil(name, werte);
                continue;
            }

            mehrdeutig.Add(name);
        }

        // Erst am Ende entfernen: Der zweite Treffer kann viele Zeilen spaeter kommen.
        foreach (var name in mehrdeutig)
            jeName.Remove(name);

        return new QgisBestand(jeName, mehrdeutig, gelesen);
    }

    /// <summary>
    /// Ein Zellwert als Text. Zahlen und Zeitpunkte stehen in GeoPackage-Dateien
    /// je nach Ausgabe als Text oder als Zahl; beides muss gleich ankommen.
    /// </summary>
    private static string Wert(IDataRecord satz, int spalte)
        => satz.IsDBNull(spalte)
            ? ""
            : Convert.ToString(satz.GetValue(spalte), System.Globalization.CultureInfo.InvariantCulture) ?? "";

    private static string Zitiere(string name)
        => $"\"{name.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
}
