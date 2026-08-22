using System;
using System.Collections.Generic;
using System.IO;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.UseCases.Import.Quellen;
using Microsoft.Data.Sqlite;

namespace AuswertungPro.Next.Infrastructure.Import.WinCan;

/// <summary>
/// Kurzer Griff auf eine WinCan-Datenbank: Enthaelt sie ueberhaupt Haltungen?
///
/// Dies ist die EINZIGE Stelle, die entscheidet, welche ".db3" die fachliche Datendatei
/// ist. Formaterkennung (<see cref="KanalExportDetector"/>) und Import
/// (<see cref="WinCanDbImportService"/>) benutzen beide diesen Pruefer. Vorher lag die
/// Regel zweimal im Code, eine Kopie schloss "*_Meta.db3" aus und die andere nicht — die
/// Erkennung meldete die richtige Datei, der Importer oeffnete eine andere und las null
/// Haltungen (Andermatt, 2026-08-21).
///
/// Bewusst OHNE KI, Ollama oder Sidecar: Der Import muss auch bei ausgeschalteter KI und
/// auf einem Rechner mit kleiner Grafikkarte vollstaendig funktionieren. Ein Waechtertest
/// haelt das fest.
/// </summary>
public static class WinCanDb3Pruefer
{
    /// <summary>Dateiname-Vorsortierung: ".db3" unterhalb eines Ordners "DB".</summary>
    public static bool IstKandidat(string pfad)
    {
        if (string.IsNullOrWhiteSpace(pfad))
            return false;

        return Path.GetExtension(pfad).Equals(".db3", StringComparison.OrdinalIgnoreCase)
               && pfad.IndexOf(
                   Path.DirectorySeparatorChar + "DB" + Path.DirectorySeparatorChar,
                   StringComparison.OrdinalIgnoreCase) >= 0;
    }

    /// <summary>Alle Kandidaten unterhalb eines Ordners, ohne Verknuepfungen zu betreten.</summary>
    public static IEnumerable<string> FindeKandidaten(string wurzel)
    {
        foreach (var pfad in SafeFileEnumeration.EnumerateFilesSafe(wurzel, "*", recursive: true))
        {
            if (IstKandidat(pfad))
                yield return pfad;
        }
    }

    /// <summary>
    /// Schaut in die Datei hinein statt zu raten. Kostet nur einen Blick ins
    /// SQLite-Inhaltsverzeichnis und eine Zaehlabfrage.
    /// </summary>
    public static QuellenBefund Pruefe(string pfad)
    {
        // Die Namensregel bleibt als billige Vorsortierung erhalten, ist aber NICHT mehr
        // die letzte Verteidigungslinie: Selbst wenn der Hersteller die Metadatei morgen
        // anders benennt, entscheidet der Blick in die Datei.
        if (Path.GetFileNameWithoutExtension(pfad).EndsWith("_Meta", StringComparison.OrdinalIgnoreCase))
            return QuellenBefund.Untauglich("Metadatenbank (*_Meta.db3), keine Haltungsdaten");

        try
        {
            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = pfad,
                Mode = SqliteOpenMode.ReadOnly
            }.ToString();

            using var conn = new SqliteConnection(connectionString);
            conn.Open();

            if (!HatTabelle(conn, "SECTION"))
                return QuellenBefund.Untauglich("keine Haltungstabelle SECTION");

            var haltungen = ZaehleHaltungen(conn);
            return haltungen > 0
                ? QuellenBefund.Tauglich(haltungen, $"{haltungen} Haltung(en)")
                : QuellenBefund.Leer("lesbar, aber ohne Haltungen");
        }
        catch (Exception ex)
        {
            // "nicht lesbar" heisst NICHT "falsche Dateiart": Eine im LightViewer
            // geoeffnete und dadurch gesperrte Datenbank beweist weiterhin einen
            // WinCan-Export. Sonst wuerde ein offenes Projekt den Ordner als
            // "unbekanntes Format" erscheinen lassen.
            return QuellenBefund.NichtLesbar($"nicht lesbar: {ex.Message}");
        }
    }

    private static bool HatTabelle(SqliteConnection conn, string name)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND upper(name) = $n";
        cmd.Parameters.AddWithValue("$n", name.ToUpperInvariant());
        return Convert.ToInt64(cmd.ExecuteScalar() ?? 0L) > 0L;
    }

    private static int ZaehleHaltungen(SqliteConnection conn)
    {
        // Dieselbe Bedingung wie im Leser (WinCanDbReader.LoadSections), damit die
        // erwartete Menge zur tatsaechlich importierten passt.
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM SECTION WHERE OBJ_Key IS NOT NULL AND OBJ_Key <> ''";
        return checked((int)Convert.ToInt64(cmd.ExecuteScalar() ?? 0L));
    }
}
