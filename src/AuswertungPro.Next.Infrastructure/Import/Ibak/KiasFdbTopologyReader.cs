using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FirebirdSql.Data.FirebirdClient;

namespace AuswertungPro.Next.Infrastructure.Import.Ibak;

/// <summary>
/// Liest die KIAS-Netz-Topologie aus Arizona.fdb (GISOBJECT) und liefert die
/// gueltigen Haltungs-Paare (Cn = Conduit, Lt = Lateral). Wird vom IBAK-Importer
/// genutzt, um die aus Daten.txt geparsten Haltungsnamen gegen die FDB zu
/// validieren - Tippfehler / vertauschte Knoten fallen so frueh auf.
///
/// HINWEIS: KIAS speichert OBJ_LENGTH/PROFILE_HEIGHT/PROFILE_WIDTH durchgehend
/// NULL fuer Cn-Records, daher liefert dieser Reader bewusst KEINE DN/Material/
/// Laenge-Werte aus der FDB. Diese Stammdaten kommen aus dem PDF-Bericht.
/// </summary>
public static class KiasFdbTopologyReader
{
    public sealed record TopologyEntry(
        string ObjName,
        string Discrim,
        string? EndObjName);

    /// <summary>
    /// Stammdaten aus GISOBJECT - nur fuer Lt (Lateral/Anschluss) und Sc (Sonderbauwerk
    /// = "Hauptkanal-Haltung" in KIAS-Terminologie) sind die Felder gefuellt.
    /// Cn (Conduit/Knoten-Segment) und Mn (Manhole) haben durchgehend NULL.
    /// </summary>
    public sealed record StammdatenEntry(
        string ObjName,
        string Discrim,
        double? Laenge_m,
        int? ProfileHeight_mm,
        int? ProfileWidth_mm,
        string? Strasse,
        string? Ort);

    /// <summary>
    /// Liefert eine Map normalisierter Haltungs-Key (z.B. "36262-36275") -> Eintrag.
    /// Liefert leere Map wenn FDB nicht lesbar (kein Firebird-Client, keine Datei usw.).
    /// </summary>
    public static Dictionary<string, TopologyEntry> LoadHoldings(string exportRoot, List<string>? messages = null)
    {
        var result = new Dictionary<string, TopologyEntry>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(exportRoot) || !Directory.Exists(exportRoot))
            return result;

        var fdb = FindFdb(exportRoot);
        if (string.IsNullOrWhiteSpace(fdb))
            return result;

        // Embedded-Mode mit eingebettetem fbembed.dll - keine Server-Installation noetig.
        // SYSDBA/masterkey sind Firebird-Defaults, KIAS ueberschreibt diese nicht.
        var connStr = new FbConnectionStringBuilder
        {
            Database = fdb,
            UserID = Environment.GetEnvironmentVariable("IBAK_FDB_USER") ?? "SYSDBA",
            Password = Environment.GetEnvironmentVariable("IBAK_FDB_PASSWORD") ?? "masterkey",
            ServerType = FbServerType.Embedded,
            Charset = "NONE"
        }.ToString();

        try
        {
            using var conn = new FbConnection(connStr);
            conn.Open();

            // Erst alle Cn (Conduit) und ihre End-IDs einsammeln.
            var startById = new Dictionary<long, string>();
            using (var cmd = new FbCommand(
                "SELECT ID, OBJ_NAME, DISCRIM FROM GISOBJECT WHERE OBJ_NAME IS NOT NULL", conn))
            using (var r = cmd.ExecuteReader())
            {
                while (r.Read())
                {
                    var id = Convert.ToInt64(r.GetValue(0));
                    var name = (r.GetValue(1)?.ToString() ?? "").Trim();
                    if (!string.IsNullOrWhiteSpace(name))
                        startById[id] = name;
                }
            }

            using (var cmd = new FbCommand(
                "SELECT OBJ_NAME, DISCRIM, GISOBJECT_END FROM GISOBJECT WHERE DISCRIM IN ('Cn','Lt') AND OBJ_NAME IS NOT NULL", conn))
            using (var r = cmd.ExecuteReader())
            {
                while (r.Read())
                {
                    var startName = (r.GetValue(0)?.ToString() ?? "").Trim();
                    var discrim   = (r.GetValue(1)?.ToString() ?? "").Trim();
                    string? endName = null;
                    if (!r.IsDBNull(2))
                    {
                        var endId = Convert.ToInt64(r.GetValue(2));
                        startById.TryGetValue(endId, out endName);
                    }

                    if (string.IsNullOrWhiteSpace(startName) || string.IsNullOrWhiteSpace(endName))
                        continue;

                    var key = $"{startName}-{endName}".Replace(" ", "");
                    if (!result.ContainsKey(key))
                        result[key] = new TopologyEntry(startName, discrim, endName);
                }
            }
        }
        catch (Exception ex)
        {
            messages?.Add($"KIAS-FDB: Topologie nicht lesbar ({ex.Message}). Validierung uebersprungen.");
        }

        return result;
    }

    /// <summary>
    /// Liefert eine Map normalisierter Haltungs-Key (z.B. "36262-36275") -> Stammdaten-Eintrag.
    /// Nutzt OBJ_NAME (= bereits Pair-Form bei Lt/Sc), OBJ_LENGTH, PROFILE_HEIGHT/WIDTH,
    /// STR3 (Strasse), STR5 (Ort).
    /// </summary>
    public static Dictionary<string, StammdatenEntry> LoadStammdaten(string exportRoot, List<string>? messages = null)
    {
        var result = new Dictionary<string, StammdatenEntry>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(exportRoot) || !Directory.Exists(exportRoot))
            return result;

        var fdb = FindFdb(exportRoot);
        if (string.IsNullOrWhiteSpace(fdb))
            return result;

        var connStr = new FbConnectionStringBuilder
        {
            Database = fdb,
            UserID = Environment.GetEnvironmentVariable("IBAK_FDB_USER") ?? "SYSDBA",
            Password = Environment.GetEnvironmentVariable("IBAK_FDB_PASSWORD") ?? "masterkey",
            ServerType = FbServerType.Embedded,
            Charset = "NONE"
        }.ToString();

        try
        {
            using var conn = new FbConnection(connStr);
            conn.Open();

            // Nur Lt/Sc - hier sind die Stammdaten gepflegt.
            using var cmd = new FbCommand(
                @"SELECT OBJ_NAME, DISCRIM, OBJ_LENGTH, PROFILE_HEIGHT, PROFILE_WIDTH, STR3, STR5
                  FROM GISOBJECT
                  WHERE DISCRIM IN ('Lt','Sc') AND OBJ_NAME IS NOT NULL", conn);
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                var name = (r.GetValue(0)?.ToString() ?? "").Trim();
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                var discrim = (r.GetValue(1)?.ToString() ?? "").Trim();
                double? len = r.IsDBNull(2) ? null : Convert.ToDouble(r.GetValue(2));
                int? ph = r.IsDBNull(3) ? null : (int)Math.Round(Convert.ToDouble(r.GetValue(3)));
                int? pw = r.IsDBNull(4) ? null : (int)Math.Round(Convert.ToDouble(r.GetValue(4)));
                string? str3 = r.IsDBNull(5) ? null : (r.GetValue(5)?.ToString() ?? "").Trim();
                string? str5 = r.IsDBNull(6) ? null : (r.GetValue(6)?.ToString() ?? "").Trim();
                if (string.IsNullOrWhiteSpace(str3)) str3 = null;
                if (string.IsNullOrWhiteSpace(str5)) str5 = null;

                var key = name.Replace(" ", "");
                if (!result.ContainsKey(key))
                    result[key] = new StammdatenEntry(name, discrim, len, ph, pw, str3, str5);
            }
        }
        catch (Exception ex)
        {
            messages?.Add($"KIAS-FDB: Stammdaten nicht lesbar ({ex.Message}).");
        }

        return result;
    }

    private static string? FindFdb(string exportRoot)
    {
        try
        {
            var candidates = Directory.EnumerateFiles(exportRoot, "*.fdb", SearchOption.AllDirectories).ToList();
            // Bevorzuge Arizona.fdb (KIAS-Standard).
            return candidates.FirstOrDefault(p => string.Equals(Path.GetFileName(p), "Arizona.fdb", StringComparison.OrdinalIgnoreCase))
                   ?? candidates.FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }
}
