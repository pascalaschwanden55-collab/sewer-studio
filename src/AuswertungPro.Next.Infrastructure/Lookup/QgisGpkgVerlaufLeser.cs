using AuswertungPro.Next.Application.Lookup;
using AuswertungPro.Next.Application.Xtf;
using Microsoft.Data.Sqlite;

namespace AuswertungPro.Next.Infrastructure.Lookup;

/// <summary>
/// Holt die Linienzuege der Haltungen aus der lokalen QGIS-Kopie.
///
/// Bewusst getrennt vom <see cref="QgisGpkgBestandLeser"/>, der die Sachwerte fuer das
/// Nachfuellen leerer Felder liest: Das ist ein anderer, bereits abgenommener Weg, und
/// Geometrie ist eine andere Aufgabe als Text. Gemeinsam ist nur die Datei.
///
/// Ein mehrdeutiger Name liefert nichts. Im Abwassernetz des Kantons tragen 2574
/// Haltungsnamen mehr als ein Objekt; einen davon zu nehmen waere geraten — und eine
/// falsche Linie faellt in einer XTF nicht auf.
///
/// Ausschliesslich lesend; die GeoPackage-Datei bleibt unveraendert.
/// </summary>
public sealed class QgisGpkgVerlaufLeser : IXtfVerlaufQuelle
{
    private readonly Func<string?> _pfad;

    public QgisGpkgVerlaufLeser(Func<string?> haltungenGpkgPfad)
        => _pfad = haltungenGpkgPfad ?? throw new ArgumentNullException(nameof(haltungenGpkgPfad));

    public string? Quellpfad => _pfad();

    public IReadOnlyDictionary<string, XtfNeuGeometrie> Lies()
    {
        var pfad = (_pfad() ?? "").Trim();
        if (pfad.Length == 0 || !File.Exists(pfad))
            return new Dictionary<string, XtfNeuGeometrie>(StringComparer.OrdinalIgnoreCase);

        using var db = new SqliteConnection(
            new SqliteConnectionStringBuilder
            {
                DataSource = pfad,
                Mode = SqliteOpenMode.ReadOnly
            }.ToString());

        db.Open();

        var tabelle = FindeTabelle(db);
        return tabelle is null
            ? new Dictionary<string, XtfNeuGeometrie>(StringComparer.OrdinalIgnoreCase)
            : Lies(db, tabelle);
    }

    private static string? FindeTabelle(SqliteConnection db)
    {
        var namensspalte = QgisFeldKarte.Namensspalte(BauteilArt.Haltung);

        using var befehl = db.CreateCommand();
        befehl.CommandText =
            "SELECT table_name FROM gpkg_contents WHERE data_type = 'features'";

        var namen = new List<string>();
        try
        {
            using var leser = befehl.ExecuteReader();
            while (leser.Read())
                namen.Add(leser.GetString(0));
        }
        catch (SqliteException)
        {
            return null;
        }

        foreach (var tabelle in namen)
        {
            var spalten = Spalten(db, tabelle);
            if (spalten.Contains(namensspalte) && spalten.Contains("geom"))
                return tabelle;
        }

        return null;
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

    private static Dictionary<string, XtfNeuGeometrie> Lies(SqliteConnection db, string tabelle)
    {
        var namensspalte = QgisFeldKarte.Namensspalte(BauteilArt.Haltung);
        var jeName = new Dictionary<string, XtfNeuGeometrie>(StringComparer.OrdinalIgnoreCase);
        var mehrdeutig = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var gesehen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        using var befehl = db.CreateCommand();
        befehl.CommandText =
            $"SELECT {Zitiere(namensspalte)}, \"geom\" FROM {Zitiere(tabelle)} " +
            "WHERE \"geom\" IS NOT NULL";

        using var leser = befehl.ExecuteReader();
        while (leser.Read())
        {
            if (leser.IsDBNull(0))
                continue;

            var name = leser.GetString(0).Trim();
            if (name.Length == 0)
                continue;

            // Gesehene Namen getrennt fuehren: Wuerde die Mehrdeutigkeit am Ergebnis
            // gemessen, liesse ein Objekt mit unlesbarer Geometrie das zweite gleichen
            // Namens als eindeutig durchgehen.
            if (!gesehen.Add(name))
            {
                mehrdeutig.Add(name);
                continue;
            }

            var punkte = GpkgGeometrie.Linie(leser.GetFieldValue<byte[]>(1));
            if (punkte is { Count: > 1 })
                jeName[name] = new XtfNeuGeometrie("Verlauf", punkte);
        }

        foreach (var name in mehrdeutig)
            jeName.Remove(name);

        return jeName;
    }

    private static string Zitiere(string name)
        => $"\"{name.Replace("\"", "\"\"")}\"";
}
