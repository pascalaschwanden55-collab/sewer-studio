using System.Data;
using AuswertungPro.Next.Application.Lookup;
using AuswertungPro.Next.Application.Reports;
using AuswertungPro.Next.Application.Xtf;
using Microsoft.Data.Sqlite;

namespace AuswertungPro.Next.Infrastructure.Lookup;

/// <summary>
/// Liest fuer EINEN Schacht den Punkt aus der QGIS-Kopie der Schaechte und die Richtungen
/// seiner Haltungen aus der QGIS-Kopie der Leitungen. Gezielte Abfragen je Name statt des
/// Volllaufs des <see cref="QgisGpkgVerlaufLeser"/> (der liest fuer den XTF-Export alle
/// Leitungen auf einmal; hier geht es um eine Handvoll bei jedem Auswahlwechsel).
///
/// Mehrdeutige Namen liefern nichts; Datei- oder Datenbankfehler ergeben <c>null</c> — die
/// Grafik ist eine Anzeige, sie zeigt dann «Richtung nicht erfasst» statt einer Fehlermeldung.
/// Ausschliesslich lesend.
/// </summary>
public sealed class QgisGpkgSchachtLageLeser : ISchachtLageQuelle
{
    private readonly Func<string?> _schaechtePfad;
    private readonly Func<string?> _haltungenPfad;

    public QgisGpkgSchachtLageLeser(Func<string?> schaechteGpkgPfad, Func<string?> haltungenGpkgPfad)
    {
        _schaechtePfad = schaechteGpkgPfad ?? throw new ArgumentNullException(nameof(schaechteGpkgPfad));
        _haltungenPfad = haltungenGpkgPfad ?? throw new ArgumentNullException(nameof(haltungenGpkgPfad));
    }

    public SchachtLage? Lies(string schachtnummer, IReadOnlyCollection<string> haltungsnamen)
    {
        var nummer = (schachtnummer ?? "").Trim();
        if (nummer.Length == 0)
            return null;

        try
        {
            var punkt = LiesSchachtpunkt(nummer);
            if (punkt is null)
                return null;

            var azimute = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            var namen = (haltungsnamen ?? [])
                .Select(n => (n ?? "").Trim())
                .Where(n => n.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (namen.Count > 0)
            {
                foreach (var (name, verlauf) in LiesVerlaeufe(namen))
                {
                    if (SchachtAnschlussRichtung.Azimut(punkt, verlauf) is { } azimut)
                        azimute[name] = azimut;
                }
            }

            return new SchachtLage(punkt, azimute);
        }
        catch (SqliteException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private XtfPunkt? LiesSchachtpunkt(string nummer)
    {
        using var db = Oeffne(_schaechtePfad());
        if (db is null)
            return null;

        var tabelle = FindeTabelle(db, QgisFeldKarte.Namensspalte(BauteilArt.Schacht));
        if (tabelle is null)
            return null;

        using var befehl = db.CreateCommand();
        befehl.CommandText =
            $"SELECT \"geom\" FROM {Zitiere(tabelle)} WHERE TRIM({Zitiere(QgisFeldKarte.Namensspalte(BauteilArt.Schacht))}) = @name";
        befehl.Parameters.AddWithValue("@name", nummer);

        XtfPunkt? punkt = null;
        var treffer = 0;
        using var leser = befehl.ExecuteReader();
        while (leser.Read())
        {
            treffer++;
            if (treffer > 1)
                return null; // mehrdeutig
            if (!leser.IsDBNull(0))
                punkt = GpkgGeometrie.Punkt(leser.GetFieldValue<byte[]>(0));
        }

        return punkt;
    }

    private IEnumerable<(string Name, IReadOnlyList<XtfPunkt> Verlauf)> LiesVerlaeufe(IReadOnlyList<string> namen)
    {
        using var db = Oeffne(_haltungenPfad());
        if (db is null)
            yield break;

        var spalte = QgisFeldKarte.Namensspalte(BauteilArt.Haltung);
        var tabelle = FindeTabelle(db, spalte);
        if (tabelle is null)
            yield break;

        foreach (var name in namen)
        {
            using var befehl = db.CreateCommand();
            befehl.CommandText = $"SELECT \"geom\" FROM {Zitiere(tabelle)} WHERE TRIM({Zitiere(spalte)}) = @name";
            befehl.Parameters.AddWithValue("@name", name);

            IReadOnlyList<XtfPunkt>? verlauf = null;
            var treffer = 0;
            using (var leser = befehl.ExecuteReader())
            {
                while (leser.Read())
                {
                    treffer++;
                    if (treffer > 1)
                        break;
                    if (!leser.IsDBNull(0))
                        verlauf = GpkgGeometrie.Linie(leser.GetFieldValue<byte[]>(0));
                }
            }

            if (treffer == 1 && verlauf is { Count: > 1 })
                yield return (name, verlauf);
        }
    }

    private static SqliteConnection? Oeffne(string? pfad)
    {
        var datei = (pfad ?? "").Trim();
        if (datei.Length == 0 || !File.Exists(datei))
            return null;

        var db = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = datei,
            Mode = SqliteOpenMode.ReadOnly
        }.ToString());
        db.Open();
        return db;
    }

    private static string? FindeTabelle(SqliteConnection db, string namensspalte)
    {
        var namen = new List<string>();
        using (var befehl = db.CreateCommand())
        {
            befehl.CommandText = "SELECT table_name FROM gpkg_contents WHERE data_type = 'features'";
            using var leser = befehl.ExecuteReader();
            while (leser.Read())
                namen.Add(leser.GetString(0));
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
        befehl.CommandText = $"PRAGMA table_info({Zitiere(tabelle)})";
        using var leser = befehl.ExecuteReader();
        while (leser.Read())
            spalten.Add(leser.GetString(1));
        return spalten;
    }

    private static string Zitiere(string name)
        => $"\"{name.Replace("\"", "\"\"")}\"";
}
