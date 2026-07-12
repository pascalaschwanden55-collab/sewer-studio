using System;
using AuswertungPro.Next.Infrastructure.Import.WinCan;
using Microsoft.Data.Sqlite;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

/// <summary>
/// Sichert das bisherige Einleseverhalten der WinCan-DB-Tabellen.
/// </summary>
public sealed class WinCanDbReaderTests
{
    [Fact]
    public void Read_LiestTabellenUndGruppiertBeobachtungenMitMedien()
    {
        using var connection = CreateDatabase();
        Execute(connection, @"
            INSERT INTO SECTION(OBJ_PK, OBJ_Key, OBJ_Street, OBJ_FromNode_REF, OBJ_ToNode_REF)
            VALUES('SEC1', '06-001', 'Gotthardstrasse', 'N1', 'N2');
            INSERT INTO SECINSP(INS_PK, INS_Section_FK, INS_StartDate, INS_StartTime, INS_TimeStamp, INS_InspectionDir)
            VALUES('INS1', 'SEC1', '2024-01-15', '2023-01-01', '2022-01-01', '2');
            INSERT INTO SECOBS(OBS_PK, OBS_Inspection_FK, OBS_OpCode, OBS_Observation, OBS_Distance,
                OBS_ContDefectLength, OBS_TimeCtr, OBS_Q1_Value, OBS_ClockPos1, OBS_SortOrder)
            VALUES('OBS1', 'INS1', 'BAA', 'Wurzeln', '3.4', '1.2', '00:01:30', '25', '03', 7);
            INSERT INTO SECOBSMM(OMM_Observation_FK, OMM_FileName, OMM_FileType)
            VALUES('OBS1', 'obs001.jpg', 'JPG');
            INSERT INTO NODE(OBJ_PK, OBJ_Key, OBJ_Material) VALUES('N1', 'S-865', 'Beton');");

        var result = WinCanDbReader.Read(connection);

        var section = Assert.Single(result.Sections);
        Assert.Equal("06-001", section.Key);
        Assert.Equal("N1", section.FromNodeFk);
        Assert.Equal("N2", section.ToNodeFk);

        var inspection = Assert.Single(result.Inspections);
        Assert.Equal(new DateTime(2024, 1, 15), inspection.SortKey);
        Assert.Equal("2", inspection.InspectionDir);

        var observation = Assert.Single(result.ObservationsByInspection["INS1"]);
        Assert.Equal(3.4, observation.Distance);
        Assert.Equal(1.2, observation.ContDefectLength);
        Assert.Equal("25", observation.Q1);
        Assert.Equal(7, observation.SortOrder);

        var media = Assert.Single(result.MediaByObservation["OBS1"]);
        Assert.Equal("obs001.jpg", media.FileName);
        Assert.Equal("JPG", media.FileType);

        var node = Assert.Single(result.Nodes);
        Assert.Equal("S-865", node.Key);
        Assert.Equal("Beton", node.Material);
    }

    [Fact]
    public void Read_IgnoriertGeloeschteBeobachtungenUndMedien()
    {
        using var connection = CreateDatabase();
        Execute(connection, @"
            INSERT INTO SECOBS(OBS_PK, OBS_Inspection_FK, OBS_OpCode, OBS_Observation, OBS_SortOrder, OBS_Deleted)
            VALUES('OBS_DELETED', 'INS1', 'BAA', 'geloescht', 1, '1');
            INSERT INTO SECOBSMM(OMM_Observation_FK, OMM_FileName, OMM_FileType, OMM_Deleted)
            VALUES('OBS_DELETED', 'alt.jpg', 'JPG', '1');");

        var result = WinCanDbReader.Read(connection);

        Assert.Empty(result.ObservationsByInspection);
        Assert.Empty(result.MediaByObservation);
    }

    private static SqliteConnection CreateDatabase()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        Execute(connection, @"
            CREATE TABLE SECTION(
                OBJ_PK TEXT, OBJ_Key TEXT, OBJ_Street TEXT, OBJ_Material TEXT, OBJ_Size1 TEXT,
                OBJ_PipeHeightOrDia TEXT, OBJ_Length TEXT, OBJ_RealLength TEXT, OBJ_PipeLength TEXT,
                OBJ_Usage TEXT, OBJ_Ownership TEXT, OBJ_ConstructionYearText TEXT, OBJ_ConstructionDate TEXT,
                OBJ_Memo TEXT, OBJ_FromNode_REF TEXT, OBJ_ToNode_REF TEXT);
            CREATE TABLE SECINSP(
                INS_PK TEXT, INS_Section_FK TEXT, INS_StartDate TEXT, INS_StartTime TEXT,
                INS_TimeStamp TEXT, INS_InspectionDir TEXT);
            CREATE TABLE SECOBS(
                OBS_PK TEXT, OBS_Inspection_FK TEXT, OBS_OpCode TEXT, OBS_Observation TEXT, OBS_Distance TEXT,
                OBS_ContDefectLength TEXT, OBS_TimeCtr TEXT, OBS_Q1_Value TEXT, OBS_Q2_Value TEXT, OBS_Q3_Value TEXT,
                OBS_U1_Value TEXT, OBS_U2_Value TEXT, OBS_U3_Value TEXT, OBS_Char1 TEXT, OBS_Char2 TEXT,
                OBS_C1_Value TEXT, OBS_C2_Value TEXT, OBS_ClockPos1 TEXT, OBS_ClockPos2 TEXT, OBS_SortOrder INTEGER,
                OBS_Deleted TEXT);
            CREATE TABLE SECOBSMM(
                OMM_Observation_FK TEXT, OMM_FileName TEXT, OMM_FileType TEXT, OMM_Deleted TEXT);
            CREATE TABLE NODE(
                OBJ_PK TEXT, OBJ_Key TEXT, OBJ_Number TEXT, OBJ_Street TEXT, OBJ_Type TEXT, OBJ_NodeType TEXT,
                OBJ_Usage TEXT, OBJ_Material TEXT, OBJ_Shape TEXT, OBJ_Size1 TEXT, OBJ_Size2 TEXT,
                OBJ_DepthToInvert TEXT, OBJ_RimToInvert TEXT, OBJ_Condition TEXT, OBJ_Ownership TEXT,
                OBJ_LandOwner TEXT, OBJ_ConstructionYearText TEXT, OBJ_ConstructionDate TEXT, OBJ_Memo TEXT,
                OBJ_State TEXT, OBJ_CoversCount TEXT, OBJ_Accessible TEXT, OBJ_ConstructionStyle TEXT, OBJ_Locality TEXT);");

        return connection;
    }

    private static void Execute(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
