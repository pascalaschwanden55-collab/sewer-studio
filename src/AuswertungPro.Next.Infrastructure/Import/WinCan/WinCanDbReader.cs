using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Data.Sqlite;

namespace AuswertungPro.Next.Infrastructure.Import.WinCan;

/// <summary>
/// Liest die fuer den WinCan-Import benoetigten Tabellen aus einer bereits geoeffneten Datenbank.
/// Die fachliche Zuordnung zu Projektfeldern bleibt im Import-Service.
/// </summary>
internal static class WinCanDbReader
{
    internal static WinCanDbSnapshot Read(SqliteConnection connection)
        => new(
            LoadSections(connection),
            LoadInspections(connection),
            LoadObservations(connection),
            LoadObservationMedia(connection),
            LoadNodes(connection));

    private static List<WinCanDbSection> LoadSections(SqliteConnection connection)
    {
        var list = new List<WinCanDbSection>();
        using var command = connection.CreateCommand();
        command.CommandText = @"SELECT OBJ_PK, OBJ_Key, OBJ_Street, OBJ_Material, OBJ_Size1, OBJ_PipeHeightOrDia, OBJ_Length,
                                      OBJ_RealLength, OBJ_PipeLength, OBJ_Usage, OBJ_Ownership, OBJ_ConstructionYearText,
                                      OBJ_ConstructionDate, OBJ_Memo, OBJ_FromNode_REF, OBJ_ToNode_REF
                               FROM SECTION WHERE OBJ_Key IS NOT NULL AND OBJ_Key <> ''";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new WinCanDbSection(
                Pk: reader.GetString(0),
                Key: reader.IsDBNull(1) ? "" : reader.GetString(1),
                Street: ReadText(reader, 2),
                Material: ReadText(reader, 3),
                Size1: ReadText(reader, 4),
                PipeHeightOrDia: ReadText(reader, 5),
                Length: ReadText(reader, 6),
                RealLength: ReadText(reader, 7),
                PipeLength: ReadText(reader, 8),
                Usage: ReadText(reader, 9),
                Ownership: ReadText(reader, 10),
                ConstructionYearText: ReadText(reader, 11),
                ConstructionDate: ReadText(reader, 12),
                Memo: ReadText(reader, 13),
                FromNodeFk: ReadText(reader, 14),
                ToNodeFk: ReadText(reader, 15)));
        }

        return list;
    }

    private static List<WinCanDbInspection> LoadInspections(SqliteConnection connection)
    {
        var list = new List<WinCanDbInspection>();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT INS_PK, INS_Section_FK, INS_StartDate, INS_StartTime, INS_TimeStamp, INS_InspectionDir FROM SECINSP";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            // Das Startdatum ist die erste Wahl, aber nur, wenn es glaubwuerdig ist. WinCan
            // traegt bei unvollstaendig erfassten Untersuchungen einen Platzhalter wie
            // 2007-12-31 ein; dann sagt der Zeitstempel des Datensatzes mehr. Real
            // aufgefallen in Seilergasse (07.638905-78998): Die Untersuchung mit 12 Befunden
            // trug das Platzhalterdatum und verlor gegen eine mit 4 Befunden. Der technische
            // Zeitstempel darf die Auswahl ordnen, wird aber nie zum Inspektionsdatum.
            var startDate = ReadText(reader, 2);
            var gelesenesStartdatum = WinCanValueNormalizer.ParseSqliteDate(startDate);
            var hatWinCanVorgabedatum = IstWinCanVorgabetag(gelesenesStartdatum);
            var glaubwuerdigesStartdatum = Glaubwuerdig(gelesenesStartdatum);
            var sortKey = glaubwuerdigesStartdatum
                          ?? Glaubwuerdig(WinCanValueNormalizer.ParseSqliteDate(reader[3]))
                          ?? WinCanValueNormalizer.ParseSqliteDate(reader[4])
                          ?? DateTime.MinValue;

            list.Add(new WinCanDbInspection(
                reader.GetString(0),
                reader.GetString(1),
                sortKey,
                ReadText(reader, 5),
                glaubwuerdigesStartdatum is null ? null : startDate,
                hatWinCanVorgabedatum));
        }

        return list;
    }

    /// <summary>
    /// Der Tag, den WinCan VX als Vorgabe in INS_StartDate eintraegt, solange niemand ein
    /// Datum erfasst hat ("2007-12-31 23:27:20", ohne Bruchteilsekunden). Gemessen in der
    /// Seilergasse-Datenbank; eine echte Aufnahme traegt dort Bruchteilsekunden.
    /// </summary>
    private static readonly DateOnly WinCanVorgabetag = new(2007, 12, 31);

    /// <summary>
    /// Ein Datum vor 1990 oder der WinCan-Vorgabetag ist ein Platzhalter, kein Aufnahmetag.
    /// </summary>
    private static DateTime? Glaubwuerdig(DateTime? datum)
        => datum is { Year: >= 1990 } d && DateOnly.FromDateTime(d) != WinCanVorgabetag ? datum : null;

    private static bool IstWinCanVorgabetag(DateTime? datum)
        => datum is { } d && DateOnly.FromDateTime(d) == WinCanVorgabetag;

    private static Dictionary<string, List<WinCanDbObservation>> LoadObservations(SqliteConnection connection)
    {
        var observationsByInspection = new Dictionary<string, List<WinCanDbObservation>>();
        using var command = connection.CreateCommand();
        command.CommandText = @"SELECT OBS_PK, OBS_Inspection_FK, OBS_OpCode, OBS_Observation, OBS_Distance, OBS_ContDefectLength, OBS_TimeCtr, OBS_Q1_Value, OBS_Q2_Value, OBS_Q3_Value, OBS_U1_Value, OBS_U2_Value, OBS_U3_Value, OBS_Char1, OBS_Char2, OBS_C1_Value, OBS_C2_Value, OBS_ClockPos1, OBS_ClockPos2, OBS_SortOrder FROM SECOBS WHERE OBS_Deleted IS NULL";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var observation = new WinCanDbObservation(
                Pk: reader.GetString(0),
                InspectionFk: reader.GetString(1),
                OpCode: reader.IsDBNull(2) ? "" : reader.GetString(2),
                Observation: reader.IsDBNull(3) ? "" : reader.GetString(3),
                Distance: ReadDouble(reader, 4),
                ContDefectLength: ReadDouble(reader, 5),
                TimeCtr: ReadString(reader, 6),
                Q1: ReadString(reader, 7),
                Q2: ReadString(reader, 8),
                Q3: ReadString(reader, 9),
                U1: ReadString(reader, 10),
                U2: ReadString(reader, 11),
                U3: ReadString(reader, 12),
                Char1: ReadString(reader, 13),
                Char2: ReadString(reader, 14),
                C1: ReadString(reader, 15),
                C2: ReadString(reader, 16),
                ClockPos1: reader.IsDBNull(17) ? null : reader.GetValue(17),
                ClockPos2: reader.IsDBNull(18) ? null : reader.GetValue(18),
                SortOrder: reader.IsDBNull(19) ? 0 : reader.GetInt32(19));

            if (!observationsByInspection.TryGetValue(observation.InspectionFk, out var list))
            {
                list = new List<WinCanDbObservation>();
                observationsByInspection[observation.InspectionFk] = list;
            }

            list.Add(observation);
        }

        return observationsByInspection;
    }

    private static Dictionary<string, List<WinCanDbMedia>> LoadObservationMedia(SqliteConnection connection)
    {
        var mediaByObservation = new Dictionary<string, List<WinCanDbMedia>>();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT OMM_Observation_FK, OMM_FileName, OMM_FileType FROM SECOBSMM WHERE OMM_Deleted IS NULL";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var observationFk = reader.IsDBNull(0) ? "" : reader.GetString(0);
            if (string.IsNullOrWhiteSpace(observationFk))
                continue;

            var media = new WinCanDbMedia(
                observationFk,
                reader.IsDBNull(1) ? "" : reader.GetString(1),
                reader.IsDBNull(2) ? "" : reader.GetString(2));

            if (!mediaByObservation.TryGetValue(observationFk, out var list))
            {
                list = new List<WinCanDbMedia>();
                mediaByObservation[observationFk] = list;
            }

            list.Add(media);
        }

        return mediaByObservation;
    }

    private static List<WinCanDbNode> LoadNodes(SqliteConnection connection)
    {
        var list = new List<WinCanDbNode>();
        using var command = connection.CreateCommand();
        command.CommandText = @"SELECT OBJ_PK, OBJ_Key, OBJ_Number, OBJ_Street, OBJ_Type, OBJ_NodeType, OBJ_Usage, OBJ_Material,
                                      OBJ_Shape, OBJ_Size1, OBJ_Size2, OBJ_DepthToInvert, OBJ_RimToInvert, OBJ_Condition,
                                      OBJ_Ownership, OBJ_LandOwner, OBJ_ConstructionYearText, OBJ_ConstructionDate, OBJ_Memo,
                                      OBJ_State, OBJ_CoversCount, OBJ_Accessible, OBJ_ConstructionStyle, OBJ_Locality
                               FROM NODE
                               WHERE (OBJ_Key IS NOT NULL AND OBJ_Key <> '') OR (OBJ_Number IS NOT NULL AND OBJ_Number <> '')";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new WinCanDbNode(
                Pk: reader.GetString(0),
                Key: ReadText(reader, 1),
                Number: ReadText(reader, 2),
                Street: ReadText(reader, 3),
                Type: ReadText(reader, 4),
                NodeType: ReadText(reader, 5),
                Usage: ReadText(reader, 6),
                Material: ReadText(reader, 7),
                Shape: ReadText(reader, 8),
                Size1: ReadText(reader, 9),
                Size2: ReadText(reader, 10),
                DepthToInvert: ReadText(reader, 11),
                RimToInvert: ReadText(reader, 12),
                Condition: ReadText(reader, 13),
                Ownership: ReadText(reader, 14),
                LandOwner: ReadText(reader, 15),
                ConstructionYearText: ReadText(reader, 16),
                ConstructionDate: ReadText(reader, 17),
                Memo: ReadText(reader, 18),
                State: ReadText(reader, 19),
                CoversCount: ReadText(reader, 20),
                Accessible: ReadText(reader, 21),
                ConstructionStyle: ReadText(reader, 22),
                Locality: ReadText(reader, 23)));
        }

        return list;
    }

    private static string? ReadText(SqliteDataReader reader, int column)
        => reader.IsDBNull(column) ? null : reader.GetValue(column)?.ToString();

    private static string? ReadString(SqliteDataReader reader, int column)
        => reader.IsDBNull(column) ? null : reader.GetString(column);

    private static double? ReadDouble(SqliteDataReader reader, int column)
    {
        if (reader.IsDBNull(column))
            return null;

        var value = reader.GetValue(column);
        if (value is double doubleValue) return doubleValue;
        if (value is decimal decimalValue) return (double)decimalValue;
        if (value is float floatValue) return floatValue;
        return double.TryParse(value?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }
}

internal sealed record WinCanDbSnapshot(
    List<WinCanDbSection> Sections,
    List<WinCanDbInspection> Inspections,
    Dictionary<string, List<WinCanDbObservation>> ObservationsByInspection,
    Dictionary<string, List<WinCanDbMedia>> MediaByObservation,
    List<WinCanDbNode> Nodes);

internal sealed record WinCanDbSection(
    string Pk, string Key, string? Street, string? Material, string? Size1, string? PipeHeightOrDia,
    string? Length, string? RealLength, string? PipeLength, string? Usage, string? Ownership,
    string? ConstructionYearText, string? ConstructionDate, string? Memo, string? FromNodeFk, string? ToNodeFk);

internal sealed record WinCanDbInspection(
    string Pk,
    string SectionFk,
    DateTime SortKey,
    string? InspectionDir,
    string? StartDate,
    bool HatWinCanVorgabedatum);

internal sealed record WinCanDbObservation(
    string Pk, string InspectionFk, string OpCode, string Observation, double? Distance,
    double? ContDefectLength, string? TimeCtr, string? Q1, string? Q2, string? Q3,
    string? U1, string? U2, string? U3, string? Char1, string? Char2, string? C1,
    string? C2, object? ClockPos1, object? ClockPos2, int SortOrder);

internal sealed record WinCanDbMedia(string ObservationFk, string FileName, string FileType);

internal sealed record WinCanDbNode(
    string Pk, string? Key, string? Number, string? Street, string? Type, string? NodeType,
    string? Usage, string? Material, string? Shape, string? Size1, string? Size2,
    string? DepthToInvert, string? RimToInvert, string? Condition, string? Ownership,
    string? LandOwner, string? ConstructionYearText, string? ConstructionDate, string? Memo,
    string? State, string? CoversCount, string? Accessible, string? ConstructionStyle, string? Locality);
