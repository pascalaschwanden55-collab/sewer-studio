using System;
using System.IO;
using System.Linq;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import.WinCan;
using Microsoft.Data.Sqlite;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

/// <summary>
/// Feldabdeckung: der WinCan-.db3-Import muss Schacht oben/unten an der Haltung setzen,
/// aufgeloest aus OBJ_FromNode_REF / OBJ_ToNode_REF (Section) gegen die NODE-Schluessel.
/// </summary>
public sealed class WinCanDbImportSchachtTests
{
    // Legt eine minimale WinCan-db3 mit allen von den Loadern abgefragten Tabellen an:
    // SECTION (1 Haltung mit From/ToNode), NODE (2 Schaechte), SECINSP/SECOBS/SECOBSMM (leer).
    private static void ErzeugeMiniDb3(string db3Path, string? inspectionDir = null)
    {
        using var conn = new SqliteConnection($"Data Source={db3Path};");
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

        // Eine Haltung 06-001 mit FromNode=N1, ToNode=N2
        Exec(@"INSERT INTO SECTION(OBJ_PK, OBJ_Key, OBJ_FromNode_REF, OBJ_ToNode_REF)
               VALUES('SEC1', '06-001', 'N1', 'N2');");
        // Zwei Schaechte; N1 mit zusaetzlichen Stammdaten (Form/Groesse/Tiefe/Material)
        Exec(@"INSERT INTO NODE(OBJ_PK, OBJ_Key, OBJ_Shape, OBJ_Size1, OBJ_RimToInvert, OBJ_Material)
               VALUES('N1', 'S-865', 'rund', '1000', '2500', 'Beton');");
        Exec(@"INSERT INTO NODE(OBJ_PK, OBJ_Key) VALUES('N2', 'S-864');");

        // Optionale Inspektion mit Befahrungsrichtung (fuer Gegenbefahrungs-Fall)
        if (!string.IsNullOrWhiteSpace(inspectionDir))
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT INTO SECINSP(INS_PK, INS_Section_FK, INS_StartDate, INS_InspectionDir) VALUES('INS1', 'SEC1', '2024-01-15', $dir);";
            cmd.Parameters.AddWithValue("$dir", inspectionDir);
            cmd.ExecuteNonQuery();
        }
    }

    [Fact]
    public void WinCanImport_SetztSchachtObenUnten_AusFromToNodeRefs()
    {
        var root = Path.Combine(Path.GetTempPath(), $"wincan-schacht-{Guid.NewGuid():N}");
        var dbDir = Path.Combine(root, "DB");           // FindDb3 verlangt Pfad-Segment \DB\
        Directory.CreateDirectory(dbDir);
        var db3 = Path.Combine(dbDir, "projekt.db3");
        try
        {
            ErzeugeMiniDb3(db3);

            var project = new Project();
            var svc = new WinCanDbImportService();
            var result = svc.ImportWinCanExport(root, project);

            Assert.True(result.Ok, result.ErrorMessage);

            var rec = project.Data.FirstOrDefault(r =>
                string.Equals(r.GetFieldValue("Haltungsname"), "06-001", StringComparison.OrdinalIgnoreCase));
            Assert.NotNull(rec);
            Assert.Equal("S-865", rec!.GetFieldValue("Schacht_oben"));
            Assert.Equal("S-864", rec.GetFieldValue("Schacht_unten"));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }

    [Fact]
    public void WinCanImport_SetztSchachtStammdaten_AusNodeSpalten()
    {
        var root = Path.Combine(Path.GetTempPath(), $"wincan-node-{Guid.NewGuid():N}");
        var dbDir = Path.Combine(root, "DB");
        Directory.CreateDirectory(dbDir);
        var db3 = Path.Combine(dbDir, "projekt.db3");
        try
        {
            ErzeugeMiniDb3(db3);

            var project = new Project();
            var svc = new WinCanDbImportService();
            var result = svc.ImportWinCanExport(root, project);
            Assert.True(result.Ok, result.ErrorMessage);

            var schacht = project.SchaechteData.FirstOrDefault(s =>
                string.Equals(s.GetFieldValue("Schachtnummer"), "S-865", StringComparison.OrdinalIgnoreCase));
            Assert.NotNull(schacht);
            Assert.Equal("rund", schacht!.GetFieldValue("Schachtform"));
            Assert.Equal("1000", schacht.GetFieldValue("Durchmesser"));
            Assert.Equal("2500", schacht.GetFieldValue("Schachttiefe"));
            Assert.False(string.IsNullOrWhiteSpace(schacht.GetFieldValue("Material")));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }

    [Fact]
    public void WinCanImport_GegenBefahrung_VertauschtSchachtObenUnten()
    {
        // Bei Gegenbefahrung (INS_InspectionDir='U') faehrt die Kamera von ToNode nach FromNode:
        // Schacht_oben = Anfangsschacht der Befahrung = ToNode, Schacht_unten = FromNode.
        var root = Path.Combine(Path.GetTempPath(), $"wincan-rev-{Guid.NewGuid():N}");
        var dbDir = Path.Combine(root, "DB");
        Directory.CreateDirectory(dbDir);
        var db3 = Path.Combine(dbDir, "projekt.db3");
        try
        {
            ErzeugeMiniDb3(db3, inspectionDir: "U");

            var project = new Project();
            var svc = new WinCanDbImportService();
            var result = svc.ImportWinCanExport(root, project);
            Assert.True(result.Ok, result.ErrorMessage);

            var rec = project.Data.FirstOrDefault(r =>
                string.Equals(r.GetFieldValue("Haltungsname"), "06-001", StringComparison.OrdinalIgnoreCase));
            Assert.NotNull(rec);
            // getauscht gegenueber Standardbefahrung: oben=ToNode(S-864), unten=FromNode(S-865)
            Assert.Equal("S-864", rec!.GetFieldValue("Schacht_oben"));
            Assert.Equal("S-865", rec.GetFieldValue("Schacht_unten"));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }
}
