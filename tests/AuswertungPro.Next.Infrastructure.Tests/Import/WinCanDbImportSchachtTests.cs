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

        // Eine Haltung 06-001 mit FromNode=N1, ToNode=N2 und einem BAUJAHR (darf Datum_Jahr NICHT belegen).
        Exec(@"INSERT INTO SECTION(OBJ_PK, OBJ_Key, OBJ_FromNode_REF, OBJ_ToNode_REF, OBJ_ConstructionDate)
               VALUES('SEC1', '06-001', 'N1', 'N2', '1998-05-05');");
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

    private static void FuegeBeobachtungMitFotoEin(string db3Path)
    {
        using var conn = new SqliteConnection($"Data Source={db3Path};");
        conn.Open();

        void Exec(string sql)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.ExecuteNonQuery();
        }

        Exec(@"INSERT INTO SECOBS(
            OBS_PK, OBS_Inspection_FK, OBS_OpCode, OBS_Observation, OBS_Distance,
            OBS_ContDefectLength, OBS_TimeCtr, OBS_Q1_Value, OBS_ClockPos1, OBS_SortOrder)
            VALUES('OBS1', 'INS1', 'BAA', 'Wurzeln 25%', '3.4', '1.2', '00:01:30', '25', '03', '1');");

        Exec(@"INSERT INTO SECOBSMM(OMM_Observation_FK, OMM_FileName, OMM_FileType)
            VALUES('OBS1', 'obs001.jpg', 'JPG');");
    }

    private static void FuegeBeobachtungMitNullOptionalsEin(string db3Path)
    {
        using var conn = new SqliteConnection($"Data Source={db3Path};");
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"INSERT INTO SECOBS(
            OBS_PK, OBS_Inspection_FK, OBS_OpCode, OBS_Observation, OBS_Distance,
            OBS_ContDefectLength, OBS_TimeCtr, OBS_Q1_Value, OBS_Q2_Value, OBS_Q3_Value,
            OBS_U1_Value, OBS_U2_Value, OBS_U3_Value, OBS_Char1, OBS_Char2,
            OBS_C1_Value, OBS_C2_Value, OBS_ClockPos1, OBS_ClockPos2, OBS_SortOrder)
            VALUES('OBS_NULLS', 'INS1', 'BAA', NULL, NULL, NULL, NULL, NULL, NULL, NULL,
                   NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 1);";
        cmd.ExecuteNonQuery();
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
    public void WinCanImport_LaesstHoeherwertigenXtfWert_stehen_und_protokolliertKonflikt()
    {
        // Die Mini-DB3 liefert Schacht_oben "S-865" (FieldSource.Legacy). Der bestehende Record
        // traegt dort aber bereits einen hoeherwertigen XTF-Wert. U16-Fix: WinCan-Legacy (Prio 50)
        // ueberschreibt den XTF-Wert (Prio 80) NICHT mehr still, sondern protokolliert den Konflikt.
        var root = Path.Combine(Path.GetTempPath(), $"wincan-xtf-{Guid.NewGuid():N}");
        var dbDir = Path.Combine(root, "DB");
        Directory.CreateDirectory(dbDir);
        var db3 = Path.Combine(dbDir, "projekt.db3");
        try
        {
            ErzeugeMiniDb3(db3);

            var project = new Project();
            var existing = project.CreateNewRecord();
            existing.SetFieldValue("Haltungsname", "06-001", FieldSource.Pdf, userEdited: false);
            existing.SetFieldValue("Schacht_oben", "XTF-Schacht", FieldSource.Xtf, userEdited: false);
            project.AddRecord(existing);

            var result = new WinCanDbImportService().ImportWinCanExport(root, project);

            Assert.True(result.Ok, result.ErrorMessage);
            var rec = project.Data.First(r =>
                string.Equals(r.GetFieldValue("Haltungsname"), "06-001", StringComparison.OrdinalIgnoreCase));
            Assert.Equal("XTF-Schacht", rec.GetFieldValue("Schacht_oben"));
            Assert.Contains(project.Conflicts, c =>
                string.Equals(c["field"]?.ToString(), "Schacht_oben", StringComparison.Ordinal));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }

    [Fact]
    public void WinCanImport_FuehrtMitBoundaryPrefixHaltung_zusammen_statt_Duplikat()
    {
        // XTF hat die Haltung als "06-001-1" (Segment-Suffix) angelegt; WinCan liefert den
        // Basis-Namen "06-001". IBAK/KINS fuehren solche Faelle per Grenz-Praefix zusammen —
        // WinCan matchte bisher nur exakt und legte ein Duplikat "06-001" an.
        var root = Path.Combine(Path.GetTempPath(), $"wincan-prefix-{Guid.NewGuid():N}");
        var dbDir = Path.Combine(root, "DB");
        Directory.CreateDirectory(dbDir);
        var db3 = Path.Combine(dbDir, "projekt.db3");
        try
        {
            ErzeugeMiniDb3(db3);

            var project = new Project();
            var existing = project.CreateNewRecord();
            existing.SetFieldValue("Haltungsname", "06-001-1", FieldSource.Xtf, userEdited: false);
            project.AddRecord(existing);

            var result = new WinCanDbImportService().ImportWinCanExport(root, project);

            Assert.True(result.Ok, result.ErrorMessage);
            // Genau EIN Record (zusammengefuehrt), kein Duplikat "06-001".
            var rec = Assert.Single(project.Data);
            Assert.Equal("06-001-1", rec.GetFieldValue("Haltungsname"));   // Key nie umgeschrieben
            // WinCan-Schacht wurde in den bestehenden Record gemergt (Zusammenfuehrung).
            Assert.Equal("S-865", rec.GetFieldValue("Schacht_oben"));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }

    [Fact]
    public void WinCanImport_MehrereBoundaryPrefixKandidaten_ErzeugenEinenNeuenExaktenRecord()
    {
        var root = Path.Combine(Path.GetTempPath(), $"wincan-prefix-ambiguous-{Guid.NewGuid():N}");
        var dbDir = Path.Combine(root, "DB");
        Directory.CreateDirectory(dbDir);
        var db3 = Path.Combine(dbDir, "projekt.db3");
        try
        {
            ErzeugeMiniDb3(db3);

            var project = new Project();
            var first = project.CreateNewRecord();
            first.SetFieldValue("Haltungsname", "06-001-1", FieldSource.Xtf, userEdited: false);
            project.AddRecord(first);
            var second = project.CreateNewRecord();
            second.SetFieldValue("Haltungsname", "06-001-2", FieldSource.Xtf, userEdited: false);
            project.AddRecord(second);

            var result = new WinCanDbImportService().ImportWinCanExport(root, project);

            Assert.True(result.Ok, result.ErrorMessage);
            Assert.Equal(3, project.Data.Count);
            var exact = Assert.Single(project.Data.Where(record =>
                string.Equals(
                    record.GetFieldValue("Haltungsname"),
                    "06-001",
                    StringComparison.OrdinalIgnoreCase)));
            Assert.Equal("S-865", exact.GetFieldValue("Schacht_oben"));
            Assert.True(string.IsNullOrWhiteSpace(first.GetFieldValue("Schacht_oben")));
            Assert.True(string.IsNullOrWhiteSpace(second.GetFieldValue("Schacht_oben")));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }

    [Fact]
    public void WinCanImport_ImportiertBeobachtungUndFoto_AusDb3()
    {
        // Charakterisierung des Kernpfads SECTION -> SECINSP -> SECOBS -> SECOBSMM:
        // Eine WinCan-Beobachtung wird als Protokoll-Eintrag und VsaFinding mit Foto uebernommen.
        var root = Path.Combine(Path.GetTempPath(), $"wincan-observation-{Guid.NewGuid():N}");
        var dbDir = Path.Combine(root, "DB");
        var pictureDir = Path.Combine(root, "Picture");
        Directory.CreateDirectory(dbDir);
        Directory.CreateDirectory(pictureDir);
        var db3 = Path.Combine(dbDir, "projekt.db3");
        var photo = Path.Combine(pictureDir, "obs001.jpg");
        try
        {
            File.WriteAllText(photo, "bild");
            ErzeugeMiniDb3(db3, inspectionDir: "D");
            FuegeBeobachtungMitFotoEin(db3);

            var project = new Project();
            var svc = new WinCanDbImportService();
            var result = svc.ImportWinCanExport(root, project);
            Assert.True(result.Ok, result.ErrorMessage);

            var rec = project.Data.FirstOrDefault(r =>
                string.Equals(r.GetFieldValue("Haltungsname"), "06-001", StringComparison.OrdinalIgnoreCase));
            Assert.NotNull(rec);

            var entry = Assert.Single(rec!.Protocol!.Current.Entries);
            Assert.Equal("BAA", entry.Code);
            Assert.Equal("Wurzeln 25%", entry.Beschreibung);
            Assert.Equal(3.4, entry.MeterStart);
            Assert.Equal(4.6, entry.MeterEnd);
            Assert.True(entry.IsStreckenschaden);
            Assert.Equal("00:01:30", entry.Mpeg);
            Assert.Equal(TimeSpan.FromSeconds(90), entry.Zeit);
            Assert.Equal(photo, Assert.Single(entry.FotoPaths));
            Assert.Equal("25", entry.CodeMeta!.Parameters["Q1"]);
            Assert.Equal("03", entry.CodeMeta.Parameters["ClockPos1"]);

            var finding = Assert.Single(rec.VsaFindings);
            Assert.Equal("BAA", finding.KanalSchadencode);
            Assert.Equal(3.4, finding.MeterStart);
            Assert.Equal(4.6, finding.MeterEnd);
            Assert.Equal(photo, finding.FotoPath);
            Assert.Equal("25", finding.Quantifizierung1);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }

    [Fact]
    public void WinCanImport_DatumJahr_AusInspektionsdatum_NichtBaujahr()
    {
        // Datum_Jahr muss das Inspektionsdatum (INS_StartDate=2024-01-15) tragen, nicht das
        // Baujahr (OBJ_ConstructionDate=1998). Ohne stillen Baujahr-Fallback.
        var root = Path.Combine(Path.GetTempPath(), $"wincan-datum-{Guid.NewGuid():N}");
        var dbDir = Path.Combine(root, "DB");
        Directory.CreateDirectory(dbDir);
        var db3 = Path.Combine(dbDir, "projekt.db3");
        try
        {
            ErzeugeMiniDb3(db3, inspectionDir: "D");   // Inspektion vorhanden -> INS_StartDate 2024-01-15

            var project = new Project();
            var svc = new WinCanDbImportService();
            var result = svc.ImportWinCanExport(root, project);
            Assert.True(result.Ok, result.ErrorMessage);

            var rec = project.Data.FirstOrDefault(r =>
                string.Equals(r.GetFieldValue("Haltungsname"), "06-001", StringComparison.OrdinalIgnoreCase));
            Assert.NotNull(rec);
            var datum = rec!.GetFieldValue("Datum_Jahr") ?? "";
            Assert.Equal("15.01.2024", datum);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }

    [Fact]
    public void WinCanImport_BeobachtungMitDbNullOptionalfeldern_BleibtImportierbar()
    {
        // WinCan-.db3-Dateien enthalten in der Praxis oft NULL in optionalen Beobachtungsspalten.
        // Charakterisierung: Code bleibt erhalten, optionale Meter/Zeit/Parameter werden nicht kuenstlich gefuellt.
        var root = Path.Combine(Path.GetTempPath(), $"wincan-nullobs-{Guid.NewGuid():N}");
        var dbDir = Path.Combine(root, "DB");
        Directory.CreateDirectory(dbDir);
        var db3 = Path.Combine(dbDir, "projekt.db3");
        try
        {
            ErzeugeMiniDb3(db3, inspectionDir: "D");
            FuegeBeobachtungMitNullOptionalsEin(db3);

            var project = new Project();
            var svc = new WinCanDbImportService();
            var result = svc.ImportWinCanExport(root, project);
            Assert.True(result.Ok, result.ErrorMessage);

            var rec = project.Data.FirstOrDefault(r =>
                string.Equals(r.GetFieldValue("Haltungsname"), "06-001", StringComparison.OrdinalIgnoreCase));
            Assert.NotNull(rec);

            var entry = Assert.Single(rec!.Protocol!.Current.Entries);
            Assert.Equal("BAA", entry.Code);
            Assert.Equal("", entry.Beschreibung);
            Assert.Null(entry.MeterStart);
            Assert.Null(entry.MeterEnd);
            Assert.False(entry.IsStreckenschaden);
            Assert.Null(entry.Zeit);
            Assert.Null(entry.CodeMeta);

            var finding = Assert.Single(rec.VsaFindings);
            Assert.Equal("BAA", finding.KanalSchadencode);
            Assert.Null(finding.MeterStart);
            Assert.Null(finding.MeterEnd);
            Assert.Null(finding.FotoPath);
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
