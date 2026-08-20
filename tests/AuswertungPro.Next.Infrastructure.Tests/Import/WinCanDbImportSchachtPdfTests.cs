using System;
using System.IO;
using System.Linq;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import.WinCan;
using Microsoft.Data.Sqlite;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

/// <summary>
/// Der WinCan-Import schrieb den Schacht-PDF-Pfad nur nach "Link". Die Verteilung
/// (SchachtProtocolApplier) setzt dagegen "Link" UND "PDF_Path"; der Rest des
/// Programms liest "PDF_Path". Beide Wege muessen dasselbe Feldpaar fuellen.
/// </summary>
public sealed class WinCanDbImportSchachtPdfTests : IDisposable
{
    private readonly string _wurzel = Path.Combine(
        Path.GetTempPath(), $"wincan-schachtpdf-{Guid.NewGuid():N}");

    private string ErzeugeExport()
    {
        var dbOrdner = Path.Combine(_wurzel, "DB");
        Directory.CreateDirectory(dbOrdner);
        var db3 = Path.Combine(dbOrdner, "projekt.db3");

        using (var conn = new SqliteConnection($"Data Source={db3};"))
        {
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

            Exec("INSERT INTO SECTION(OBJ_PK, OBJ_Key, OBJ_FromNode_REF, OBJ_ToNode_REF) VALUES('SEC1','80823-80872','N1','N2');");
            Exec("INSERT INTO NODE(OBJ_PK, OBJ_Key) VALUES('N1','80823');");
            Exec("INSERT INTO NODE(OBJ_PK, OBJ_Key) VALUES('N2','80872');");
        }

        // Schachtprotokoll wie im Kundenexport: reine Nummer als Dateiname.
        File.WriteAllText(Path.Combine(_wurzel, "80823.pdf"), "protokoll");
        return _wurzel;
    }

    [Fact]
    public void SchachtProtokoll_WirdInLinkUndPdfPathGeschrieben()
    {
        var export = ErzeugeExport();
        var project = new Project();

        var ergebnis = new WinCanDbImportService().ImportWinCanExport(export, project);
        Assert.True(ergebnis.Ok, ergebnis.ErrorMessage);

        var schacht = project.SchaechteData.Single(s =>
            string.Equals(s.GetFieldValue("Schachtnummer"), "80823", StringComparison.OrdinalIgnoreCase));

        var link = schacht.GetFieldValue("Link");
        var pdfPath = schacht.GetFieldValue("PDF_Path");

        Assert.False(string.IsNullOrWhiteSpace(link));
        Assert.False(string.IsNullOrWhiteSpace(pdfPath));
        Assert.Equal(link, pdfPath);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_wurzel, recursive: true); } catch { }
    }
}
