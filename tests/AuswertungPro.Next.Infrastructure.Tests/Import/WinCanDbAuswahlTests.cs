using System;
using System.IO;
using System.Linq;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Application.UseCases.Import.Quellen;
using AuswertungPro.Next.Infrastructure.Import;
using AuswertungPro.Next.Infrastructure.Import.WinCan;
using Microsoft.Data.Sqlite;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

/// <summary>
/// Auswahl der richtigen WinCan-Datenbank im Exportordner.
///
/// Echter Fall (G:\Sanierung_Andermatt_GKS): WinCan VX legt neben der Datendatei
/// eine gleichnamige "*_Meta.db3" ab. Diese Metadatei ist oft ein Vielfaches
/// groesser (dort 6,8 MB gegen 1,2 MB) und enthaelt KEINE Tabellen SECTION,
/// SECINSP oder SECOBS. Wer die groesste Datei nimmt, liest die falsche Datenbank
/// und importiert null Haltungen.
///
/// Zusaetzlich muss ein gewaehlter Ordner mit MEHREREN WinCan-Projekten alle
/// Projekte einlesen, nicht nur eines davon.
/// </summary>
public sealed class WinCanDbAuswahlTests
{
    // Minimale, vom Leser vollstaendig abgefragte WinCan-Datenbank mit einer Haltung.
    private static void ErzeugeDatenDb3(
        string db3Path,
        string haltungsname,
        string schachtOben = "S-1",
        string schachtUnten = "S-2")
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

        using var insert = conn.CreateCommand();
        insert.CommandText = @"INSERT INTO SECTION(OBJ_PK, OBJ_Key, OBJ_Street, OBJ_FromNode_REF, OBJ_ToNode_REF)
                               VALUES('SEC1', $key, 'Bahnhofstrasse', 'N1', 'N2');";
        insert.Parameters.AddWithValue("$key", haltungsname);
        insert.ExecuteNonQuery();

        using var knotenOben = conn.CreateCommand();
        knotenOben.CommandText = "INSERT INTO NODE(OBJ_PK, OBJ_Key) VALUES('N1', $k);";
        knotenOben.Parameters.AddWithValue("$k", schachtOben);
        knotenOben.ExecuteNonQuery();

        using var knotenUnten = conn.CreateCommand();
        knotenUnten.CommandText = "INSERT INTO NODE(OBJ_PK, OBJ_Key) VALUES('N2', $k);";
        knotenUnten.Parameters.AddWithValue("$k", schachtUnten);
        knotenUnten.ExecuteNonQuery();
    }

    // WinCan-Metadatenbank: gueltiges SQLite, aber ohne die fachlichen Tabellen.
    // Wird absichtlich groesser gemacht als die Datendatei.
    private static void ErzeugeMetaDb3(string db3Path, long fuellGroesse)
    {
        using (var conn = new SqliteConnection($"Data Source={db3Path};"))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "CREATE TABLE METAOBJ(OBJ_PK TEXT, Fuellung BLOB);";
            cmd.ExecuteNonQuery();

            using var insert = conn.CreateCommand();
            insert.CommandText = "INSERT INTO METAOBJ(OBJ_PK, Fuellung) VALUES('x', zeroblob($n));";
            insert.Parameters.AddWithValue("$n", (int)fuellGroesse);
            insert.ExecuteNonQuery();
        }

        SqliteConnection.ClearAllPools();
    }

    private static string NeuerTempOrdner(string praefix)
    {
        var root = Path.Combine(Path.GetTempPath(), $"{praefix}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        return root;
    }

    private static void Aufraeumen(string root)
    {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(root, recursive: true); } catch { }
    }

    [Fact]
    public void GroessereMetaDatenbank_DarfDieDatendatenbankNichtVerdraengen()
    {
        var root = NeuerTempOrdner("wincan-meta");
        var dbDir = Path.Combine(root, "DB");
        Directory.CreateDirectory(dbDir);
        try
        {
            var datenPfad = Path.Combine(dbDir, "projekt.db3");
            ErzeugeDatenDb3(datenPfad, "H6");
            SqliteConnection.ClearAllPools();
            var datenGroesse = new FileInfo(datenPfad).Length;
            ErzeugeMetaDb3(Path.Combine(dbDir, "projekt_Meta.db3"), datenGroesse * 3);

            Assert.True(
                new FileInfo(Path.Combine(dbDir, "projekt_Meta.db3")).Length > datenGroesse,
                "Vorbedingung: die Metadatei muss fuer diesen Test groesser sein.");

            var project = new Project();
            var result = new WinCanDbImportService().ImportWinCanExport(root, project);

            Assert.True(result.Ok, result.ErrorMessage);
            Assert.Contains(project.Data, r =>
                string.Equals(r.GetFieldValue("Haltungsname"), "S-1-S-2", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Aufraeumen(root);
        }
    }

    [Fact]
    public void ImporterUndFormaterkennung_WaehlenDieselbeDatenbank()
    {
        var root = NeuerTempOrdner("wincan-einig");
        var dbDir = Path.Combine(root, "DB");
        Directory.CreateDirectory(dbDir);
        try
        {
            var datenPfad = Path.Combine(dbDir, "projekt.db3");
            ErzeugeDatenDb3(datenPfad, "H6");
            SqliteConnection.ClearAllPools();
            ErzeugeMetaDb3(Path.Combine(dbDir, "projekt_Meta.db3"), new FileInfo(datenPfad).Length * 3);

            var erkannt = new KanalExportDetectionService().Detect(root);
            Assert.Equal(KanalExportFormat.WinCan, erkannt.Format);
            Assert.NotNull(erkannt.Db3Path);

            var result = new WinCanDbImportService().ImportWinCanExport(root, new Project());

            Assert.True(result.Ok, result.ErrorMessage);
            // Der Importer nennt seine Quelle in den Meldungen. Sie muss zur Erkennung passen,
            // sonst laufen Erkennung und Import wieder auf verschiedene Dateien.
            Assert.Contains(
                result.Value!.Messages,
                m => m.Contains("Importquelle: WinCan DB3", StringComparison.Ordinal)
                     && m.Contains(Path.GetFileName(erkannt.Db3Path!), StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Aufraeumen(root);
        }
    }

    [Fact]
    public void MehrereProjekteUnterEinemOrdner_WerdenAlleEingelesen()
    {
        var root = NeuerTempOrdner("wincan-mehrere");
        try
        {
            // Zwei WinCan-Projekte nebeneinander, wie im echten Sanierungsordner.
            var a = Path.Combine(root, "2.26.045 Zone A", "Projects", "2.26.045 Zone A", "DB");
            var b = Path.Combine(root, "2.26.046 Zone B", "Projects", "2.26.046 Zone B", "DB");
            Directory.CreateDirectory(a);
            Directory.CreateDirectory(b);

            var datenA = Path.Combine(a, "zoneA.db3");
            ErzeugeDatenDb3(datenA, "H6", "A1", "A2");
            ErzeugeDatenDb3(Path.Combine(b, "zoneB.db3"), "H12", "B1", "B2");
            SqliteConnection.ClearAllPools();
            ErzeugeMetaDb3(Path.Combine(a, "zoneA_Meta.db3"), new FileInfo(datenA).Length * 3);

            var project = new Project();
            var result = new WinCanDbImportService().ImportWinCanExport(root, project);

            Assert.True(result.Ok, result.ErrorMessage);
            // Namen entstehen aus dem Schachtpaar, nicht aus der WinCan-Bezeichnung.
            Assert.Contains(project.Data, r =>
                string.Equals(r.GetFieldValue("Haltungsname"), "A1-A2", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(project.Data, r =>
                string.Equals(r.GetFieldValue("Haltungsname"), "B1-B2", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Aufraeumen(root);
        }
    }

    [Fact]
    public void GleicheWinCanBezeichnung_AberAndereSchaechte_ErgibtZweiEigeneNummern()
    {
        // Echter Fall: "H6" gibt es in Zone 2.03 (Schacht 955509 -> 4789) und in Zone 2.11
        // (2413 -> 327015). Weil die Haltungsnummer aus dem Schachtpaar gebildet wird,
        // kollidieren die beiden gar nicht mehr — ein Zonen-Zusatz im Namen ist unnoetig.
        var root = NeuerTempOrdner("wincan-zwei-h6");
        try
        {
            var a = Path.Combine(root, "2.26.045 Andermatt Zone 2.03_Bahnhofstrasse", "DB");
            var b = Path.Combine(root, "2.26.049 Andermatt Zone 2.11_Kirchgasse", "DB");
            Directory.CreateDirectory(a);
            Directory.CreateDirectory(b);

            ErzeugeDatenDb3(Path.Combine(a, "zoneA.db3"), "H6", "955509", "4789");
            ErzeugeDatenDb3(Path.Combine(b, "zoneB.db3"), "H6", "2413", "327015");
            SqliteConnection.ClearAllPools();

            var project = new Project();
            var result = new WinCanDbImportService().ImportWinCanExport(root, project);

            Assert.True(result.Ok, result.ErrorMessage);

            var erste = project.Data.SingleOrDefault(r =>
                string.Equals(r.GetFieldValue("Haltungsname"), "955509-4789", StringComparison.OrdinalIgnoreCase));
            var zweite = project.Data.SingleOrDefault(r =>
                string.Equals(r.GetFieldValue("Haltungsname"), "2413-327015", StringComparison.OrdinalIgnoreCase));

            Assert.NotNull(erste);
            Assert.NotNull(zweite);
            Assert.Equal("955509", erste!.GetFieldValue("Schacht_oben"));
            Assert.Equal("4789", erste.GetFieldValue("Schacht_unten"));
            Assert.Equal("2413", zweite!.GetFieldValue("Schacht_oben"));
            Assert.Equal("327015", zweite.GetFieldValue("Schacht_unten"));

            // Kein Zonen-Zusatz noetig.
            Assert.DoesNotContain(project.Data, r =>
                (r.GetFieldValue("Haltungsname") ?? "").Contains("(Zone", StringComparison.Ordinal));

            // Die WinCan-Bezeichnung bleibt im Bericht nachvollziehbar.
            Assert.Contains(result.Value!.Messages, m =>
                m.Contains("955509-4789", StringComparison.Ordinal)
                && m.Contains("WinCan-Bezeichnung H6", StringComparison.Ordinal));
        }
        finally
        {
            Aufraeumen(root);
        }
    }

    [Fact]
    public void OhneVollstaendigesSchachtpaar_BleibtDieWinCanBezeichnung()
    {
        // Rueckfall: fehlt ein Schacht, kann keine Haltungsnummer gebildet werden.
        // Dann bleibt die WinCan-Bezeichnung stehen und wird sichtbar gemeldet.
        var root = NeuerTempOrdner("wincan-ohne-schacht");
        var dbDir = Path.Combine(root, "DB");
        Directory.CreateDirectory(dbDir);
        try
        {
            ErzeugeDatenDb3(Path.Combine(dbDir, "projekt.db3"), "H6", "955509", "");
            SqliteConnection.ClearAllPools();

            var project = new Project();
            var result = new WinCanDbImportService().ImportWinCanExport(root, project);

            Assert.True(result.Ok, result.ErrorMessage);
            Assert.Contains(project.Data, r =>
                string.Equals(r.GetFieldValue("Haltungsname"), "H6", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(result.Value!.Messages, m =>
                m.Contains("Schacht oben/unten unvollstaendig", StringComparison.Ordinal));
        }
        finally
        {
            Aufraeumen(root);
        }
    }

    [Fact]
    public void GleicherName_UndGleicheSchaechte_BleibtEineHaltung()
    {
        var root = NeuerTempOrdner("wincan-selbe");
        try
        {
            // Dieselbe physische Haltung in zwei Projektordnern: darf NICHT verdoppelt werden.
            var a = Path.Combine(root, "2.26.046 Zone 2.08", "DB");
            var b = Path.Combine(root, "2.26.049 Zone 2.11", "DB");
            Directory.CreateDirectory(a);
            Directory.CreateDirectory(b);

            ErzeugeDatenDb3(Path.Combine(a, "zoneA.db3"), "H10", "7370", "7427");
            ErzeugeDatenDb3(Path.Combine(b, "zoneB.db3"), "H10", "7370", "7427");
            SqliteConnection.ClearAllPools();

            var project = new Project();
            var result = new WinCanDbImportService().ImportWinCanExport(root, project);

            Assert.True(result.Ok, result.ErrorMessage);
            Assert.Single(project.Data.Where(r =>
                string.Equals(r.GetFieldValue("Haltungsname"), "7370-7427", StringComparison.OrdinalIgnoreCase)));
        }
        finally
        {
            Aufraeumen(root);
        }
    }

    [Fact]
    public void HaltungOhneBefunde_ZaehltAlsBearbeitet()
    {
        // Ein sauberes Rohr ohne einen einzigen Befund ist vollstaendig importiert.
        // Wuerde es nicht mitgezaehlt, meldete das Plausibilitaetstor bei fast jedem
        // normalen Projekt Fehlalarm (real aufgefallen: 16 Quellhaltungen, nur 15
        // gezaehlt, weil eine keine Beobachtungen hatte).
        var root = NeuerTempOrdner("wincan-ohne-befunde");
        var dbDir = Path.Combine(root, "DB");
        Directory.CreateDirectory(dbDir);
        try
        {
            ErzeugeDatenDb3(Path.Combine(dbDir, "projekt.db3"), "H6");
            SqliteConnection.ClearAllPools();

            var result = new WinCanDbImportService().ImportWinCanExport(root, new Project());

            Assert.True(result.Ok, result.ErrorMessage);
            Assert.Equal(1, result.Value!.ErwarteteHaltungen);
            Assert.Equal(1, result.Value.BearbeiteteHaltungen);

            var urteil = ImportPlausibilitaetsTor.Beurteile(
                result.Value.Quellenprotokoll, result.Value.BearbeiteteHaltungen);
            Assert.Equal(PlausibilitaetsStufe.Gruen, urteil.Stufe);
        }
        finally
        {
            Aufraeumen(root);
        }
    }

    [Fact]
    public void Sammelordner_MeldetErwarteteUndBearbeiteteHaltungenGetrennt()
    {
        var root = NeuerTempOrdner("wincan-zahlen");
        try
        {
            var a = Path.Combine(root, "Zone A", "DB");
            var b = Path.Combine(root, "Zone B", "DB");
            Directory.CreateDirectory(a);
            Directory.CreateDirectory(b);
            ErzeugeDatenDb3(Path.Combine(a, "a.db3"), "H6", "S-1", "S-2");
            ErzeugeDatenDb3(Path.Combine(b, "b.db3"), "H12", "S-3", "S-4");
            SqliteConnection.ClearAllPools();

            var result = new WinCanDbImportService().ImportWinCanExport(root, new Project());

            Assert.True(result.Ok, result.ErrorMessage);
            Assert.Equal(2, result.Value!.ErwarteteHaltungen);
            Assert.Equal(2, result.Value.BearbeiteteHaltungen);
            // Found zaehlt zusaetzlich die Schaechte und taugt deshalb nicht als Pruefgroesse.
            Assert.True(result.Value.Found > result.Value.BearbeiteteHaltungen);

            // Das Protokoll nennt beide Projekte.
            Assert.Equal(2, result.Value.Quellenprotokoll!.AlleVersuche.Count);
        }
        finally
        {
            Aufraeumen(root);
        }
    }
}
