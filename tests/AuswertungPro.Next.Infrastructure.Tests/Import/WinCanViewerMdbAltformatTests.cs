using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import;
using AuswertungPro.Next.Infrastructure.Import.Xtf;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

/// <summary>
/// Alte WinCan-v8-Exporte (Projekt Seelisberg, 2017) legen die Daten als Access-Datei
/// <c>&lt;Projekt&gt;\DB\*_Viewer.mdb</c> ab, nicht als .db3. Zwei Stellen liessen diese
/// Exporte auflaufen:
///
/// 1. Die Formaterkennung suchte nur .db3 und meldete "Unknown" - der Ein-Knopf-Import
///    brach ab, obwohl der vorhandene MDB-Rueckfall die Datei lesen kann.
/// 2. Die Beobachtungen haengen in dieser Schema-Generation an <c>SO_Inspecs_ID</c>,
///    gesucht wurde <c>SO_Inspection_ID</c>. Real gemessen: 0 von 192 zugeordnet.
/// </summary>
public sealed class WinCanViewerMdbAltformatTests : IDisposable
{
    private readonly string _wurzel = Path.Combine(
        Path.GetTempPath(), "wcmdb_" + Guid.NewGuid().ToString("N"));

    public WinCanViewerMdbAltformatTests() => Directory.CreateDirectory(_wurzel);

    private string LegeDatei(string relativerPfad)
    {
        var pfad = Path.Combine(_wurzel, relativerPfad);
        Directory.CreateDirectory(Path.GetDirectoryName(pfad)!);
        File.WriteAllText(pfad, "x");
        return pfad;
    }

    // ---- Formaterkennung --------------------------------------------------

    [Fact]
    public void ViewerMdb_unter_DB_gilt_als_WinCan()
    {
        LegeDatei(Path.Combine("Projects", "P", "DB", "P_Viewer.mdb"));

        var erkennung = new KanalExportDetectionService().Detect(_wurzel);

        Assert.Equal(KanalExportFormat.WinCan, erkennung.Format);
        Assert.Contains("mdb", erkennung.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Db3_hat_weiterhin_vorrang_vor_der_mdb()
    {
        LegeDatei(Path.Combine("Projects", "P", "DB", "P_Viewer.mdb"));
        LegeDatei(Path.Combine("Projects", "P", "DB", "P.db3"));

        var erkennung = new KanalExportDetectionService().Detect(_wurzel);

        Assert.Equal(KanalExportFormat.WinCan, erkennung.Format);
        Assert.EndsWith("P.db3", erkennung.Db3Path, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Mdb_ausserhalb_eines_DB_ordners_macht_noch_kein_WinCan()
    {
        // Sonst wuerde irgendeine Access-Datei im Kundenordner den Import auf WinCan
        // umlenken. Die Einschraenkung auf DB\ bleibt.
        LegeDatei(Path.Combine("Dokumente", "Adressen.mdb"));

        var erkennung = new KanalExportDetectionService().Detect(_wurzel);

        Assert.Equal(KanalExportFormat.Unknown, erkennung.Format);
    }

    // ---- Beobachtungs-Zuordnung -------------------------------------------

    private static Dictionary<string, string> Zeile(
        string tabelle, params (string Spalte, string Wert)[] felder)
    {
        var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["__table"] = tabelle
        };
        foreach (var (spalte, wert) in felder)
            row[spalte] = wert;
        return row;
    }

    private static HaltungRecord Haltung(string name)
    {
        var r = new HaltungRecord();
        r.SetFieldValue("Haltungsname", name, FieldSource.Manual, false);
        return r;
    }

    [Fact]
    public void Beobachtung_mit_SO_Inspecs_ID_wird_zugeordnet()
    {
        var rows = new List<Dictionary<string, string>>
        {
            Zeile("S_T", ("S_ID", "S1"), ("S_StartNode", "51647"), ("S_EndNode", "51261"), ("S_SectionFlow", "D")),
            Zeile("SI_T", ("SI_ID", "I1"), ("SI_Section_ID", "S1"), ("SI_InspectionDir", "D")),
            Zeile("SO_T", ("SO_Inspecs_ID", "I1"), ("SO_OpCode", "BAFAD"), ("SO_Distance", "3.4"), ("SO_Counter", "3"))
        };
        var record = Haltung("51647-51261");
        var warnings = new List<string>();

        WinCanObservationAttacher.AttachFromRows(rows, [record], warnings);

        Assert.NotNull(record.Protocol);
        Assert.Single(record.Protocol!.Current.Entries);
        Assert.Equal("BAFAD", record.Protocol.Current.Entries[0].Code);
        Assert.DoesNotContain(warnings, w => w.Contains("ohne Inspektions-Zuordnung", StringComparison.Ordinal));
    }

    [Fact]
    public void Neuere_schreibweise_SO_Inspection_ID_bleibt_gueltig()
    {
        var rows = new List<Dictionary<string, string>>
        {
            Zeile("S_T", ("S_ID", "S1"), ("S_StartNode", "100"), ("S_EndNode", "200"), ("S_SectionFlow", "D")),
            Zeile("SI_T", ("SI_ID", "I1"), ("SI_Section_ID", "S1"), ("SI_InspectionDir", "D")),
            Zeile("SO_T", ("SO_Inspection_ID", "I1"), ("SO_OpCode", "BAB"))
        };
        var record = Haltung("100-200");

        WinCanObservationAttacher.AttachFromRows(rows, [record], new List<string>());

        Assert.Single(record.Protocol!.Current.Entries);
    }

    public void Dispose()
    {
        try { Directory.Delete(_wurzel, recursive: true); } catch { }
    }
}
