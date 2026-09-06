using System;
using System.IO;
using System.Linq;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import.WinCan;
using AuswertungPro.Next.Infrastructure.Import;
using AuswertungPro.Next.Infrastructure.Projects;
using Microsoft.Data.Sqlite;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

/// <summary>
/// Arbeitspaket 5, Schritt 2: Eine WinCan-Haltung mit zwei Befahrungen.
///
/// Vorher (Stand c1021e76e) nahm der Import nur die neueste Untersuchung. Die uebrige
/// wurde gemeldet, ihre Befunde, Fotos und Videosekunden gingen aber verloren — in
/// Seilergasse waren das 12 Befunde, 9 Fotos und 1 Video bei "0 Fehler" im Bericht.
/// </summary>
public sealed class WinCanMehrereBefahrungenTests
{
    [Fact]
    public void ZweiteBefahrung_BleibtAlsRevisionErhalten()
    {
        MitDb(db =>
        {
            var projekt = new Project();
            var ergebnis = new WinCanDbImportService().ImportWinCanExport(db.Ordner, projekt);

            Assert.True(ergebnis.Ok, ergebnis.ErrorMessage);
            var haltung = Assert.Single(projekt.Data);

            // Die uebernommene Befahrung ist die Arbeitskopie …
            Assert.Equal(2, haltung.Protocol?.Current?.Entries.Count);
            // … die weitere liegt als Revision daneben und geht nicht verloren.
            var revision = Assert.Single(haltung.Protocol?.History ?? []);
            Assert.Equal(3, revision.Entries.Count);
            Assert.Contains("weitere", revision.Comment ?? "", StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public void BelegteGegenbefahrung_LandetInLinkG()
    {
        MitDb(db =>
        {
            // Explizite Gegenmarke plus Bezug zur selben SECTION. Die Kamerarichtung
            // für sich genügt nicht: auch eine normale Hauptfahrt darf upstream sein.
            File.Copy(Path.Combine(db.Ordner, "Video", "100-200_zurueck.mpg"), Path.Combine(db.Ordner, "Video", "100-200_G.mpg"));
            using (var conn = new SqliteConnection($"Data Source={Path.Combine(db.Ordner, "DB", "projekt.db3")}"))
            {
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "UPDATE SECOBSMM SET OMM_FileName='100-200_G.mpg' WHERE OMM_Observation_FK='O3'";
                cmd.ExecuteNonQuery();
            }
            var projekt = new Project();
            new WinCanDbImportService().ImportWinCanExport(db.Ordner, projekt);
            var haltung = Assert.Single(projekt.Data);

            // Die Richtung steht in INS_InspectionDir — nicht in der Dateireihenfolge.
            Assert.EndsWith("100-200_hin.mpg", haltung.GetFieldValue("Link"), StringComparison.OrdinalIgnoreCase);
            Assert.EndsWith("100-200_G.mpg", haltung.GetFieldValue("Link_G"), StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public void ZweiBefahrungenGleicherRichtung_ErfindenKeineGegeninspektion()
    {
        MitDb(db =>
        {
            var projekt = new Project();
            new WinCanDbImportService().ImportWinCanExport(db.Ordner, projekt);
            var haltung = Assert.Single(projekt.Data);

            Assert.True(string.IsNullOrWhiteSpace(haltung.GetFieldValue("Link_G")),
                "Zwei Aufnahmen derselben Richtung sind keine Gegeninspektion.");
        },
        richtungZweiteBefahrung: "D");
    }

    // ---------------------------------------------------------------------

    [Fact]
    public void NeuereRueckfahrt_BehaeltIhrVideoBeimAktivenProtokoll()
    {
        MitDb(db =>
        {
            using (var conn = new SqliteConnection($"Data Source={Path.Combine(db.Ordner, "DB", "projekt.db3")}"))
            {
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "UPDATE SECINSP SET INS_StartDate='2020-07-20' WHERE INS_PK='INS_ALT'";
                cmd.ExecuteNonQuery();
            }
            var p = new Project();
            var result = new WinCanDbImportService().ImportWinCanExport(db.Ordner, p);
            Assert.True(result.Ok, result.ErrorMessage);
            var h = Assert.Single(p.Data);
            Assert.Equal(new double?[] { 3, 7, 9 }, h.Protocol!.Current.Entries.Select(e => e.MeterStart));
            Assert.EndsWith("100-200_zurueck.mpg", h.GetFieldValue("Link"), StringComparison.OrdinalIgnoreCase);
            Assert.NotEqual(h.GetFieldValue("Link"), h.GetFieldValue("Link_G"));
        });
    }

    private sealed record Fixture(string Ordner);

    [Fact]
    public void FehlendesInDatenbankReferenziertesVideo_WirdAlsFehlerGemeldet()
    {
        MitDb(db =>
        {
            File.Delete(Path.Combine(db.Ordner, "Video", "100-200_hin.mpg"));
            var result = new WinCanDbImportService().ImportWinCanExport(db.Ordner, new Project());
            Assert.True(result.Ok, result.ErrorMessage);
            Assert.True(result.Value!.Errors > 0);
            Assert.Contains(result.Value.Messages, m => m.Contains("100-200_hin.mpg"));
        });
    }

    [Fact]
    public void WiederholterImport_BewahrtHandkorrekturImAktivenProtokoll()
    {
        MitDb(db =>
        {
            var p = new Project();
            var importer = new WinCanDbImportService();
            Assert.True(importer.ImportWinCanExport(db.Ordner, p).Ok);
            var h = Assert.Single(p.Data);
            h.Protocol!.Current.Entries[0].Beschreibung = "Handkorrektur";
            h.SetFieldValue("Link_G", "handwahl.mpg", FieldSource.Manual, true);
            Assert.True(importer.ImportWinCanExport(db.Ordner, p).Ok);
            Assert.Equal("Handkorrektur", h.Protocol.Current.Entries[0].Beschreibung);
            Assert.Equal("handwahl.mpg", h.GetFieldValue("Link_G"));
            Assert.Single(h.Protocol.History);
        });
    }

    [Fact]
    public void WeitereVideos_BleibenNachVerteilungUndNeuladenBeiIhrerUntersuchung()
    {
        MitDb(db =>
        {
            var p = new Project();
            var importer = new WinCanDbImportService();
            Assert.True(importer.ImportWinCanExport(db.Ordner, p).Ok);
            var ziel = Path.Combine(db.Ordner, "Ziel");
            Directory.CreateDirectory(ziel);
            var distributor = new KanalImportDistributionService();
            var result = distributor.Distribute(p, ziel, Path.Combine(ziel, "PDF"), db.Ordner, false);
            Assert.Equal(0, result.Errors);
            Assert.Equal(2, result.VideosDistributed);
            var h = Assert.Single(p.Data);
            Assert.True(string.IsNullOrWhiteSpace(h.GetFieldValue("Link_G")));
            var history = Assert.Single(h.Protocol!.History);
            var pfad = Assert.Single(history.ImportVideoPaths!);
            Assert.False(Path.IsPathRooted(pfad));
            Assert.Equal("Rueckfahrt", File.ReadAllText(Path.Combine(ziel, pfad)));

            var repo = new JsonProjectRepository();
            var json = Path.Combine(ziel, "projekt.json");
            Assert.True(repo.Save(p, json).Ok);
            var neu = repo.Load(json).Value!;
            Assert.Equal(pfad, Assert.Single(Assert.Single(neu.Data).Protocol!.History).ImportVideoPaths![0]);
            Assert.True(importer.ImportWinCanExport(db.Ordner, neu).Ok);
            Assert.Single(Assert.Single(neu.Data).Protocol!.History);
            var wiederholt = distributor.Distribute(neu, ziel, Path.Combine(ziel, "PDF"), db.Ordner, false);
            Assert.Equal(0, wiederholt.Errors);
            Assert.Equal(0, wiederholt.VideosDistributed);
        });
    }

    [Fact]
    public void EntfallenerGegenbeleg_LaesstKeinenAltenGegenlinkStehen()
    {
        MitDb(db =>
        {
            var p = new Project();
            var importer = new WinCanDbImportService();
            Assert.True(importer.ImportWinCanExport(db.Ordner, p).Ok);
            var h = Assert.Single(p.Data);
            h.SetFieldValue("Link_G", "veraltetes-video.mpg", FieldSource.Legacy, false);
            Assert.True(importer.ImportWinCanExport(db.Ordner, p).Ok);
            Assert.True(string.IsNullOrWhiteSpace(h.GetFieldValue("Link_G")));
        });
    }

    private static void MitDb(Action<Fixture> pruefung, string richtungZweiteBefahrung = "U")
    {
        var ordner = Path.Combine(Path.GetTempPath(), $"ap5-wincan-{Guid.NewGuid():N}");
        var dbOrdner = Path.Combine(ordner, "DB");
        Directory.CreateDirectory(dbOrdner);
        Directory.CreateDirectory(Path.Combine(ordner, "Video"));
        File.WriteAllText(Path.Combine(ordner, "Video", "100-200_hin.mpg"), "Hinfahrt");
        File.WriteAllText(Path.Combine(ordner, "Video", "100-200_zurueck.mpg"), "Rueckfahrt");

        ErzeugeDb3(Path.Combine(dbOrdner, "projekt.db3"), richtungZweiteBefahrung);

        try { pruefung(new Fixture(ordner)); }
        finally { try { Directory.Delete(ordner, recursive: true); } catch { } }
    }

    private static void ErzeugeDb3(string db3Path, string richtungZweiteBefahrung)
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

        Exec(@"INSERT INTO SECTION(OBJ_PK, OBJ_Key, OBJ_FromNode_REF, OBJ_ToNode_REF)
               VALUES('SEC1', '100-200', 'N1', 'N2');");
        Exec(@"INSERT INTO NODE(OBJ_PK, OBJ_Key) VALUES('N1', '100');");
        Exec(@"INSERT INTO NODE(OBJ_PK, OBJ_Key) VALUES('N2', '200');");

        // Die neuere Befahrung faehrt in Fliessrichtung, die aeltere zurueck.
        Exec(@"INSERT INTO SECINSP(INS_PK, INS_Section_FK, INS_StartDate, INS_InspectionDir)
               VALUES('INS_NEU', 'SEC1', '2020-07-10', 'D');");
        Exec($@"INSERT INTO SECINSP(INS_PK, INS_Section_FK, INS_StartDate, INS_InspectionDir)
               VALUES('INS_ALT', 'SEC1', '2020-07-02', '{richtungZweiteBefahrung}');");

        // Zwei Befunde in der neuen, drei in der alten Befahrung.
        Exec(@"INSERT INTO SECOBS(OBS_PK, OBS_Inspection_FK, OBS_OpCode, OBS_Distance, OBS_SortOrder)
               VALUES('O1', 'INS_NEU', 'BAB', '2.5', '1');");
        Exec(@"INSERT INTO SECOBS(OBS_PK, OBS_Inspection_FK, OBS_OpCode, OBS_Distance, OBS_SortOrder)
               VALUES('O2', 'INS_NEU', 'BAF', '5.0', '2');");
        Exec(@"INSERT INTO SECOBS(OBS_PK, OBS_Inspection_FK, OBS_OpCode, OBS_Distance, OBS_SortOrder)
               VALUES('O3', 'INS_ALT', 'BAB', '3.0', '1');");
        Exec(@"INSERT INTO SECOBS(OBS_PK, OBS_Inspection_FK, OBS_OpCode, OBS_Distance, OBS_SortOrder)
               VALUES('O4', 'INS_ALT', 'BBC', '7.0', '2');");
        Exec(@"INSERT INTO SECOBS(OBS_PK, OBS_Inspection_FK, OBS_OpCode, OBS_Distance, OBS_SortOrder)
               VALUES('O5', 'INS_ALT', 'BCA', '9.0', '3');");

        // Je Befahrung ein eigenes Video.
        Exec(@"INSERT INTO SECOBSMM(OMM_Observation_FK, OMM_FileName, OMM_FileType)
               VALUES('O1', '100-200_hin.mpg', 'MPG');");
        Exec(@"INSERT INTO SECOBSMM(OMM_Observation_FK, OMM_FileName, OMM_FileType)
               VALUES('O3', '100-200_zurueck.mpg', 'MPG');");
    }
}
