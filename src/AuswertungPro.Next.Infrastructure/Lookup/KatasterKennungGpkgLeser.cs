using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using AuswertungPro.Next.Application.Lookup;
using Microsoft.Data.Sqlite;

namespace AuswertungPro.Next.Infrastructure.Lookup;

/// <summary>
/// Liest die Kennungstabelle aus einer GeoPackage-Datei.
///
/// Die Datei entsteht ausserhalb des Programms aus einer Kopie der
/// GEONIS-Datenbank (Stand Dezember 2024, siehe <c>herkunft</c>-Tabelle) und
/// fuehrt je Haltung und Schacht ausschliesslich Kennungen: die SIA405-Kennung
/// des Bauteils und die seiner Verbundobjekte. Fachwerte stehen bewusst nicht
/// darin — die Kopie ist alt, und dieser Weg uebernimmt nur Kennungen.
///
/// Die Tabellen heissen fest <c>haltungen</c> und <c>schaechte</c>; sie werden
/// nicht ueber Spalten gesucht wie beim QGIS-Bestand, weil diese Datei kein
/// fremder Export ist, sondern nach eigener Vorgabe gebaut wird.
///
/// Ausschliesslich lesend (<c>Mode=ReadOnly</c>).
/// </summary>
public sealed class KatasterKennungGpkgLeser : IKatasterKennungLeser
{
    private readonly Func<string> _pfad;

    public KatasterKennungGpkgLeser(Func<string> pfad)
        => _pfad = pfad ?? throw new ArgumentNullException(nameof(pfad));

    public string Quellpfad() => _pfad() ?? "";

    public KatasterKennungBestand Lies(BauteilArt art)
    {
        var datei = Quellpfad();
        if (string.IsNullOrWhiteSpace(datei))
            throw new InvalidOperationException("Es ist keine Kennungstabelle eingestellt.");

        if (!File.Exists(datei))
            throw new FileNotFoundException($"Die Kennungstabelle wurde nicht gefunden: {datei}", datei);

        using var db = new SqliteConnection(
            new SqliteConnectionStringBuilder
            {
                DataSource = datei,
                Mode = SqliteOpenMode.ReadOnly
            }.ToString());
        db.Open();

        var stand = LiesStand(db);
        return art == BauteilArt.Haltung ? LiesHaltungen(db, stand) : LiesSchaechte(db, stand);
    }

    private static string LiesStand(SqliteConnection db)
    {
        try
        {
            using var befehl = db.CreateCommand();
            befehl.CommandText = "SELECT wert FROM herkunft WHERE schluessel = 'stand'";
            var wert = befehl.ExecuteScalar();
            var text = wert is null || wert is DBNull ? "" : Convert.ToString(wert) ?? "";
            return text.Length > 0 ? $"GEONIS-Kopie {text}" : "GEONIS-Kopie";
        }
        catch (SqliteException)
        {
            // Ohne Herkunftstabelle bleibt der Stand unbekannt; die Kennungen sind
            // deshalb nicht weniger gueltig.
            return "GEONIS-Kopie";
        }
    }

    private static KatasterKennungBestand LiesHaltungen(SqliteConnection db, string stand)
    {
        var jeName = new Dictionary<string, KatasterKennung>(StringComparer.OrdinalIgnoreCase);
        var mehrdeutig = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var gelesen = 0;

        using var befehl = db.CreateCommand();
        befehl.CommandText =
            "SELECT bezeichnung, gemeinde, haltung_id, kanal_id, vonpunkt_id, vonpunkt_bezeichnung, " +
            "nachpunkt_id, nachpunkt_bezeichnung, rohrprofil_id, profiltyp_code, geonis_geaendert FROM haltungen";

        using var leser = Fuehre(befehl, "haltungen");
        while (leser.Read())
        {
            gelesen++;
            var name = Wert(leser, 0);
            var haltung = Wert(leser, 2);
            if (name.Length == 0 || !SiaObjektkennung.IstGueltig(haltung))
                continue;

            Nimm(jeName, mehrdeutig, name, () => KatasterKennung.FuerHaltung(
                name,
                Leer(Wert(leser, 1)),
                haltung,
                Kennung(Wert(leser, 3)),
                Kennung(Wert(leser, 4)),
                Leer(Wert(leser, 5)),
                Kennung(Wert(leser, 6)),
                Leer(Wert(leser, 7)),
                Kennung(Wert(leser, 8)),
                GeonisProfiltyp.NachNorm(Wert(leser, 9)),
                Zeitpunkt(Wert(leser, 10))));
        }

        foreach (var name in mehrdeutig)
            jeName.Remove(name);

        return new KatasterKennungBestand(BauteilArt.Haltung, jeName, mehrdeutig, gelesen, stand);
    }

    private static KatasterKennungBestand LiesSchaechte(SqliteConnection db, string stand)
    {
        var jeName = new Dictionary<string, KatasterKennung>(StringComparer.OrdinalIgnoreCase);
        var mehrdeutig = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var gelesen = 0;

        using var befehl = db.CreateCommand();
        befehl.CommandText = "SELECT bezeichnung, gemeinde, knoten_id, bauwerk_id, geonis_geaendert FROM schaechte";

        using var leser = Fuehre(befehl, "schaechte");
        while (leser.Read())
        {
            gelesen++;
            var name = Wert(leser, 0);
            var knoten = Wert(leser, 2);
            if (name.Length == 0 || !SiaObjektkennung.IstGueltig(knoten))
                continue;

            Nimm(jeName, mehrdeutig, name, () => KatasterKennung.FuerSchacht(
                name, Leer(Wert(leser, 1)), knoten, Kennung(Wert(leser, 3)), Zeitpunkt(Wert(leser, 4))));
        }

        foreach (var name in mehrdeutig)
            jeName.Remove(name);

        return new KatasterKennungBestand(BauteilArt.Schacht, jeName, mehrdeutig, gelesen, stand);
    }

    private static SqliteDataReader Fuehre(SqliteCommand befehl, string tabelle)
    {
        try
        {
            return befehl.ExecuteReader();
        }
        catch (SqliteException ex)
        {
            throw new InvalidDataException(
                $"Die Kennungstabelle hat nicht den erwarteten Aufbau (Tabelle '{tabelle}'): {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Traegt ein Bauteil ein. Ein zweites Objekt unter demselben Namen macht beide
    /// unbrauchbar: Welches gemeint ist, weiss die Tabelle nicht.
    /// </summary>
    private static void Nimm(
        Dictionary<string, KatasterKennung> jeName,
        HashSet<string> mehrdeutig,
        string name,
        Func<KatasterKennung> baue)
    {
        if (!jeName.ContainsKey(name))
        {
            jeName[name] = baue();
            return;
        }

        mehrdeutig.Add(name);
    }

    private static string Wert(IDataRecord satz, int spalte)
        => satz.IsDBNull(spalte)
            ? ""
            : (Convert.ToString(satz.GetValue(spalte), System.Globalization.CultureInfo.InvariantCulture) ?? "").Trim();

    private static string? Leer(string text) => text.Length == 0 ? null : text;

    /// <summary>Eine Nebenkennung nur, wenn sie die SIA405-Form hat; sonst nichts.</summary>
    private static string? Kennung(string text) => SiaObjektkennung.IstGueltig(text) ? text : null;

    /// <summary>
    /// Das GEONIS-Aenderungsdatum, wie ogr2ogr es ablegt ("2024/05/27 14:37:28+00" oder
    /// ISO). Unlesbar heisst unbekannt, nie ein erfundener Zeitpunkt.
    /// </summary>
    private static DateTime? Zeitpunkt(string text)
    {
        if (text.Length == 0)
            return null;

        var norm = text.Replace('/', '-');
        return DateTime.TryParse(
            norm,
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal,
            out var wert)
            ? wert
            : null;
    }
}
