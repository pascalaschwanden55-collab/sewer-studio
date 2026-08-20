using System;
using System.IO;
using System.Linq;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import.WinCan;
using Microsoft.Data.Sqlite;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

/// <summary>
/// Ein Haltungsprotokoll traegt beide Schachtnummern im Namen
/// ("Section_4_892045-10.892870.pdf"). Es darf trotzdem NICHT als
/// Schachtprotokoll an einen dieser Schaechte gehaengt werden.
/// </summary>
public sealed class WinCanDbImportSchachtFremdPdfTests : IDisposable
{
    private readonly string _wurzel = Path.Combine(
        Path.GetTempPath(), $"wincan-fremdpdf-{Guid.NewGuid():N}");

    public WinCanDbImportSchachtFremdPdfTests()
    {
        Directory.CreateDirectory(_wurzel);
    }

    private void ErzeugeExport()
    {
        var dbOrdner = Path.Combine(_wurzel, "DB");
        Directory.CreateDirectory(dbOrdner);
        var db3 = Path.Combine(dbOrdner, "projekt.db3");

        using var conn = new SqliteConnection($"Data Source={db3};");
        conn.Open();
        void Exec(string sql)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.ExecuteNonQuery();
        }

        Exec(@"CREATE TABLE SECTION(
            OBJ_PK TEXT, OBJ_Key TEXT, OBJ_Street TEXT, OBJ_Material TEXT, OBJ_Size1 TEXT,
            OBJ_PipeHeightOrDia TEXT, OBJ_Length TEXT, OBJ_RealLength TEXT, OBJ_PipeLength TEXT,
            OBJ_Usage TEXT, OBJ_Ownership TEXT, OBJ_ConstructionYearText TEXT, OBJ_ConstructionDate TEXT,
            OBJ_Memo TEXT, OBJ_FromNode_REF TEXT, OBJ_ToNode_REF TEXT);");
        Exec(@"CREATE TABLE NODE(
            OBJ_PK TEXT, OBJ_Key TEXT, OBJ_Number TEXT, OBJ_Street TEXT, OBJ_Type TEXT, OBJ_NodeType TEXT,
            OBJ_Usage TEXT, OBJ_Material TEXT, OBJ_Shape TEXT, OBJ_Size1 TEXT, OBJ_Size2 TEXT,
            OBJ_DepthToInvert TEXT, OBJ_RimToInvert TEXT, OBJ_Condition TEXT, OBJ_Ownership TEXT,
            OBJ_LandOwner TEXT, OBJ_ConstructionYearText TEXT, OBJ_ConstructionDate TEXT, OBJ_Memo TEXT,
            OBJ_State TEXT, OBJ_CoversCount TEXT, OBJ_Accessible TEXT, OBJ_ConstructionStyle TEXT, OBJ_Locality TEXT);");
        Exec(@"CREATE TABLE SECINSP(
            INS_PK TEXT, INS_Section_FK TEXT, INS_StartDate TEXT, INS_StartTime TEXT,
            INS_TimeStamp TEXT, INS_InspectionDir TEXT);");
        Exec(@"CREATE TABLE SECOBS(
            OBS_PK TEXT, OBS_Inspection_FK TEXT, OBS_OpCode TEXT, OBS_Observation TEXT, OBS_Distance TEXT,
            OBS_ContDefectLength TEXT, OBS_TimeCtr TEXT, OBS_Q1_Value TEXT, OBS_Q2_Value TEXT, OBS_Q3_Value TEXT,
            OBS_U1_Value TEXT, OBS_U2_Value TEXT, OBS_U3_Value TEXT, OBS_Char1 TEXT, OBS_Char2 TEXT,
            OBS_C1_Value TEXT, OBS_C2_Value TEXT, OBS_ClockPos1 TEXT, OBS_ClockPos2 TEXT, OBS_SortOrder TEXT,
            OBS_Deleted TEXT);");
        Exec(@"CREATE TABLE SECOBSMM(
            OMM_Observation_FK TEXT, OMM_FileName TEXT, OMM_FileType TEXT, OMM_Deleted TEXT);");

        Exec("INSERT INTO SECTION(OBJ_PK, OBJ_Key, OBJ_FromNode_REF, OBJ_ToNode_REF) VALUES('SEC1','892045-10.892870','N1','N2');");
        Exec("INSERT INTO NODE(OBJ_PK, OBJ_Key) VALUES('N1','892045');");
        Exec("INSERT INTO NODE(OBJ_PK, OBJ_Key) VALUES('N2','10.892870');");
    }

    private SchachtRecord Importiere()
    {
        ErzeugeExport();
        var project = new Project();
        var res = new WinCanDbImportService().ImportWinCanExport(_wurzel, project);
        Assert.True(res.Ok, res.ErrorMessage);
        return project.SchaechteData.Single(s =>
            string.Equals(s.GetFieldValue("Schachtnummer"), "892045", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void HaltungsprotokollWirdNichtAnDenSchachtGehaengt()
    {
        File.WriteAllText(Path.Combine(_wurzel, "Section_4_892045-10.892870.pdf"), "haltungsprotokoll");

        var schacht = Importiere();

        Assert.True(string.IsNullOrWhiteSpace(schacht.GetFieldValue("PDF_Path")),
            $"Fremdes Haltungsprotokoll angehaengt: {schacht.GetFieldValue("PDF_Path")}");
        Assert.True(string.IsNullOrWhiteSpace(schacht.GetFieldValue("Link")),
            $"Fremdes Haltungsprotokoll angehaengt: {schacht.GetFieldValue("Link")}");
    }

    [Fact]
    public void EchtesSchachtprotokollWirdWeiterhinAngehaengt()
    {
        File.WriteAllText(Path.Combine(_wurzel, "Section_4_892045-10.892870.pdf"), "haltungsprotokoll");
        File.WriteAllText(Path.Combine(_wurzel, "892045.pdf"), "schachtprotokoll");

        var schacht = Importiere();

        Assert.Equal("892045.pdf", Path.GetFileName(schacht.GetFieldValue("PDF_Path")));
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_wurzel, recursive: true); } catch { }
    }
}
