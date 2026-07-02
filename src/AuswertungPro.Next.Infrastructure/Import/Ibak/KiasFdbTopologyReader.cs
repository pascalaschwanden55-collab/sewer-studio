using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Globalization;
using System.IO;
using System.Linq;
using FirebirdSql.Data.FirebirdClient;
using AuswertungPro.Next.Infrastructure.Common;

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

        var connStr = IbakFdbConnectionOptions.CreateEmbedded(fdb).ToString();

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

        var connStr = IbakFdbConnectionOptions.CreateEmbedded(fdb).ToString();

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
            result = ReadStammdatenRows(r, messages);
        }
        catch (Exception ex)
        {
            messages?.Add($"KIAS-FDB: Stammdaten nicht lesbar ({ex.Message}).");
        }

        return result;
    }

    private static Dictionary<string, StammdatenEntry> ReadStammdatenRows(DbDataReader r, List<string>? messages)
    {
        var result = new Dictionary<string, StammdatenEntry>(StringComparer.OrdinalIgnoreCase);
        var badRows = 0;

        while (r.Read())
        {
            var name = ReadText(r, 0);
            if (string.IsNullOrWhiteSpace(name))
                continue;

            if (!TryReadNullableDouble(r, 2, out var len)
                || !TryReadNullableRoundedInt(r, 3, out var ph)
                || !TryReadNullableRoundedInt(r, 4, out var pw))
            {
                badRows++;
                continue;
            }

            var discrim = ReadText(r, 1) ?? string.Empty;
            var str3 = ReadText(r, 5);
            var str5 = ReadText(r, 6);

            var key = name.Replace(" ", "");
            if (!result.ContainsKey(key))
                result[key] = new StammdatenEntry(name, discrim, len, ph, pw, str3, str5);
        }

        if (badRows > 0)
            messages?.Add($"KIAS-FDB: {badRows} fehlerhafte Stammdaten-Zeile(n) uebersprungen.");

        return result;
    }

    private static string? ReadText(DbDataReader r, int ordinal)
    {
        if (r.IsDBNull(ordinal))
            return null;

        var text = (r.GetValue(ordinal)?.ToString() ?? string.Empty).Trim();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private static bool TryReadNullableRoundedInt(DbDataReader r, int ordinal, out int? value)
    {
        if (!TryReadNullableDouble(r, ordinal, out var number))
        {
            value = null;
            return false;
        }

        value = number.HasValue ? (int)Math.Round(number.Value) : null;
        return true;
    }

    private static bool TryReadNullableDouble(DbDataReader r, int ordinal, out double? value)
    {
        value = null;
        if (r.IsDBNull(ordinal))
            return true;

        var raw = r.GetValue(ordinal);
        if (raw is null)
            return true;

        switch (raw)
        {
            case double d:
                value = d;
                return true;
            case float f:
                value = f;
                return true;
            case decimal m:
                value = decimal.ToDouble(m);
                return true;
            case byte or sbyte or short or ushort or int or uint or long or ulong:
                value = Convert.ToDouble(raw, CultureInfo.InvariantCulture);
                return true;
            case string s:
                var normalized = s.Trim();
                if (string.IsNullOrWhiteSpace(normalized))
                    return true;
                if (normalized.Contains(',') && !normalized.Contains('.'))
                    normalized = normalized.Replace(',', '.');
                if (double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
                {
                    value = parsed;
                    return true;
                }
                return false;
            default:
                try
                {
                    value = Convert.ToDouble(raw, CultureInfo.InvariantCulture);
                    return true;
                }
                catch
                {
                    return false;
                }
        }
    }

    private static string? FindFdb(string exportRoot)
    {
        try
        {
            // Audit 2026-05-17 (Nachzieh): SafeFileEnumeration.
            var candidates = SafeFileEnumeration.EnumerateFilesSafe(exportRoot, "*.fdb", recursive: true)
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .ToList();
            // Bevorzuge Arizona.fdb (KIAS-Standard), sonst deterministisch ersten sortierten Kandidaten
            // (nicht dateisystem-abhaengig).
            return candidates.FirstOrDefault(p => string.Equals(Path.GetFileName(p), "Arizona.fdb", StringComparison.OrdinalIgnoreCase))
                   ?? candidates.FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }
}
