using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;

namespace AuswertungPro.Next.Infrastructure.Import.WinCan;

// WinCanDbImportService DB-Loaders + DTOs: Liest WinCan-DB3 (SQLite) Tabellen
// SECTION/INSPECTION/OBSERVATION/MEDIA/NODE in DTO-Records ein.
// Aus dem Hauptdatei extrahiert (Slice 19a).
public sealed partial class WinCanDbImportService
{
    private static List<DbSection> LoadSections(SqliteConnection conn)
    {
        var list = new List<DbSection>();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"SELECT OBJ_PK, OBJ_Key, OBJ_Street, OBJ_Material, OBJ_Size1, OBJ_PipeHeightOrDia, OBJ_Length,
                                   OBJ_RealLength, OBJ_PipeLength, OBJ_Usage, OBJ_Ownership, OBJ_ConstructionYearText,
                                   OBJ_ConstructionDate, OBJ_Memo, OBJ_FromNode_REF, OBJ_ToNode_REF
                            FROM SECTION WHERE OBJ_Key IS NOT NULL AND OBJ_Key <> ''";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            list.Add(new DbSection(
                Pk: r.GetString(0),
                Key: r.IsDBNull(1) ? "" : r.GetString(1),
                Street: r.IsDBNull(2) ? null : r.GetValue(2)?.ToString(),
                Material: r.IsDBNull(3) ? null : r.GetValue(3)?.ToString(),
                Size1: r.IsDBNull(4) ? null : r.GetValue(4)?.ToString(),
                PipeHeightOrDia: r.IsDBNull(5) ? null : r.GetValue(5)?.ToString(),
                Length: r.IsDBNull(6) ? null : r.GetValue(6)?.ToString(),
                RealLength: r.IsDBNull(7) ? null : r.GetValue(7)?.ToString(),
                PipeLength: r.IsDBNull(8) ? null : r.GetValue(8)?.ToString(),
                Usage: r.IsDBNull(9) ? null : r.GetValue(9)?.ToString(),
                Ownership: r.IsDBNull(10) ? null : r.GetValue(10)?.ToString(),
                ConstructionYearText: r.IsDBNull(11) ? null : r.GetValue(11)?.ToString(),
                ConstructionDate: r.IsDBNull(12) ? null : r.GetValue(12)?.ToString(),
                Memo: r.IsDBNull(13) ? null : r.GetValue(13)?.ToString(),
                FromNodeFk: r.IsDBNull(14) ? null : r.GetValue(14)?.ToString(),
                ToNodeFk: r.IsDBNull(15) ? null : r.GetValue(15)?.ToString()));
        }
        return list;
    }

    private static List<DbInspection> LoadInspections(SqliteConnection conn)
    {
        var list = new List<DbInspection>();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT INS_PK, INS_Section_FK, INS_StartDate, INS_StartTime, INS_TimeStamp, INS_InspectionDir FROM SECINSP";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            var pk = r.GetString(0);
            var sectionFk = r.GetString(1);
            var sortKey = ParseSqliteDate(r[2]) ?? ParseSqliteDate(r[3]) ?? ParseSqliteDate(r[4]) ?? DateTime.MinValue;
            var dir = r.IsDBNull(5) ? null : r.GetValue(5)?.ToString();
            list.Add(new DbInspection(pk, sectionFk, sortKey, dir));
        }
        return list;
    }

    private static Dictionary<string, List<DbObservation>> LoadObservations(SqliteConnection conn)
    {
        var dict = new Dictionary<string, List<DbObservation>>();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"SELECT OBS_PK, OBS_Inspection_FK, OBS_OpCode, OBS_Observation, OBS_Distance, OBS_ContDefectLength, OBS_TimeCtr, OBS_Q1_Value, OBS_Q2_Value, OBS_Q3_Value, OBS_U1_Value, OBS_U2_Value, OBS_U3_Value, OBS_Char1, OBS_Char2, OBS_C1_Value, OBS_C2_Value, OBS_ClockPos1, OBS_ClockPos2, OBS_SortOrder FROM SECOBS WHERE OBS_Deleted IS NULL";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            var obs = new DbObservation(
                Pk: r.GetString(0),
                InspectionFk: r.GetString(1),
                OpCode: r.IsDBNull(2) ? "" : r.GetString(2),
                Observation: r.IsDBNull(3) ? "" : r.GetString(3),
                Distance: SafeReadDouble(r, 4),
                ContDefectLength: SafeReadDouble(r, 5),
                TimeCtr: r.IsDBNull(6) ? null : r.GetString(6),
                Q1: r.IsDBNull(7) ? null : r.GetString(7),
                Q2: r.IsDBNull(8) ? null : r.GetString(8),
                Q3: r.IsDBNull(9) ? null : r.GetString(9),
                U1: r.IsDBNull(10) ? null : r.GetString(10),
                U2: r.IsDBNull(11) ? null : r.GetString(11),
                U3: r.IsDBNull(12) ? null : r.GetString(12),
                Char1: r.IsDBNull(13) ? null : r.GetString(13),
                Char2: r.IsDBNull(14) ? null : r.GetString(14),
                C1: r.IsDBNull(15) ? null : r.GetString(15),
                C2: r.IsDBNull(16) ? null : r.GetString(16),
                ClockPos1: r.IsDBNull(17) ? null : r.GetValue(17),
                ClockPos2: r.IsDBNull(18) ? null : r.GetValue(18),
                SortOrder: r.IsDBNull(19) ? 0 : r.GetInt32(19));

            if (!dict.TryGetValue(obs.InspectionFk, out var list))
            {
                list = new List<DbObservation>();
                dict[obs.InspectionFk] = list;
            }
            list.Add(obs);
        }
        return dict;
    }

    private static Dictionary<string, List<DbMedia>> LoadObservationMedia(SqliteConnection conn)
    {
        var dict = new Dictionary<string, List<DbMedia>>();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT OMM_Observation_FK, OMM_FileName, OMM_FileType FROM SECOBSMM WHERE OMM_Deleted IS NULL";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            var obsFk = r.IsDBNull(0) ? "" : r.GetString(0);
            if (string.IsNullOrWhiteSpace(obsFk))
                continue;

            var media = new DbMedia(
                ObservationFk: obsFk,
                FileName: r.IsDBNull(1) ? "" : r.GetString(1),
                FileType: r.IsDBNull(2) ? "" : r.GetString(2));

            if (!dict.TryGetValue(obsFk, out var list))
            {
                list = new List<DbMedia>();
                dict[obsFk] = list;
            }
            list.Add(media);
        }
        return dict;
    }

    private static List<DbNode> LoadNodes(SqliteConnection conn)
    {
        var list = new List<DbNode>();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"SELECT OBJ_PK, OBJ_Key, OBJ_Number, OBJ_Street, OBJ_Type, OBJ_NodeType, OBJ_Usage, OBJ_Material,
                                   OBJ_Shape, OBJ_Size1, OBJ_Size2, OBJ_DepthToInvert, OBJ_RimToInvert, OBJ_Condition,
                                   OBJ_Ownership, OBJ_LandOwner, OBJ_ConstructionYearText, OBJ_ConstructionDate, OBJ_Memo,
                                   OBJ_State, OBJ_CoversCount, OBJ_Accessible, OBJ_ConstructionStyle, OBJ_Locality
                            FROM NODE
                            WHERE (OBJ_Key IS NOT NULL AND OBJ_Key <> '') OR (OBJ_Number IS NOT NULL AND OBJ_Number <> '')";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            list.Add(new DbNode(
                Pk: r.GetString(0),
                Key: r.IsDBNull(1) ? null : r.GetValue(1)?.ToString(),
                Number: r.IsDBNull(2) ? null : r.GetValue(2)?.ToString(),
                Street: r.IsDBNull(3) ? null : r.GetValue(3)?.ToString(),
                Type: r.IsDBNull(4) ? null : r.GetValue(4)?.ToString(),
                NodeType: r.IsDBNull(5) ? null : r.GetValue(5)?.ToString(),
                Usage: r.IsDBNull(6) ? null : r.GetValue(6)?.ToString(),
                Material: r.IsDBNull(7) ? null : r.GetValue(7)?.ToString(),
                Shape: r.IsDBNull(8) ? null : r.GetValue(8)?.ToString(),
                Size1: r.IsDBNull(9) ? null : r.GetValue(9)?.ToString(),
                Size2: r.IsDBNull(10) ? null : r.GetValue(10)?.ToString(),
                DepthToInvert: r.IsDBNull(11) ? null : r.GetValue(11)?.ToString(),
                RimToInvert: r.IsDBNull(12) ? null : r.GetValue(12)?.ToString(),
                Condition: r.IsDBNull(13) ? null : r.GetValue(13)?.ToString(),
                Ownership: r.IsDBNull(14) ? null : r.GetValue(14)?.ToString(),
                LandOwner: r.IsDBNull(15) ? null : r.GetValue(15)?.ToString(),
                ConstructionYearText: r.IsDBNull(16) ? null : r.GetValue(16)?.ToString(),
                ConstructionDate: r.IsDBNull(17) ? null : r.GetValue(17)?.ToString(),
                Memo: r.IsDBNull(18) ? null : r.GetValue(18)?.ToString(),
                State: r.IsDBNull(19) ? null : r.GetValue(19)?.ToString(),
                CoversCount: r.IsDBNull(20) ? null : r.GetValue(20)?.ToString(),
                Accessible: r.IsDBNull(21) ? null : r.GetValue(21)?.ToString(),
                ConstructionStyle: r.IsDBNull(22) ? null : r.GetValue(22)?.ToString(),
                Locality: r.IsDBNull(23) ? null : r.GetValue(23)?.ToString()));
        }
        return list;
    }

    private static DateTime? ParseSqliteDate(object? raw)
    {
        if (raw is null)
            return null;
        var text = raw.ToString();
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var m = Regex.Match(text, @"Date\((\d+)\)");
        if (m.Success && long.TryParse(m.Groups[1].Value, out var ms))
            return DateTimeOffset.FromUnixTimeMilliseconds(ms).DateTime;

        // Try explicit European date formats first to avoid DD/MM swap
        var formats = new[] { "dd.MM.yyyy", "dd/MM/yyyy", "dd-MM-yyyy", "yyyy-MM-dd", "dd.MM.yyyy HH:mm:ss", "yyyy-MM-dd HH:mm:ss" };
        if (DateTime.TryParseExact(text, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dtExact))
            return dtExact;

        if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var dt))
            return dt;

        return null;
    }

    private sealed record DbSection(
        string Pk,
        string Key,
        string? Street,
        string? Material,
        string? Size1,
        string? PipeHeightOrDia,
        string? Length,
        string? RealLength,
        string? PipeLength,
        string? Usage,
        string? Ownership,
        string? ConstructionYearText,
        string? ConstructionDate,
        string? Memo,
        string? FromNodeFk,
        string? ToNodeFk);
    private sealed record DbInspection(string Pk, string SectionFk, DateTime SortKey, string? InspectionDir);
    private sealed record DbObservation(
        string Pk,
        string InspectionFk,
        string OpCode,
        string Observation,
        double? Distance,
        double? ContDefectLength,
        string? TimeCtr,
        string? Q1,
        string? Q2,
        string? Q3,
        string? U1,
        string? U2,
        string? U3,
        string? Char1,
        string? Char2,
        string? C1,
        string? C2,
        object? ClockPos1,
        object? ClockPos2,
        int SortOrder);

    private sealed record DbMedia(string ObservationFk, string FileName, string FileType);

    private sealed record DbNode(
        string Pk,
        string? Key,
        string? Number,
        string? Street,
        string? Type,
        string? NodeType,
        string? Usage,
        string? Material,
        string? Shape,
        string? Size1,
        string? Size2,
        string? DepthToInvert,
        string? RimToInvert,
        string? Condition,
        string? Ownership,
        string? LandOwner,
        string? ConstructionYearText,
        string? ConstructionDate,
        string? Memo,
        string? State,
        string? CoversCount,
        string? Accessible,
        string? ConstructionStyle,
        string? Locality);
}
