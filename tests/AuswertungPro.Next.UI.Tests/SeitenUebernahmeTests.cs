using System.IO;
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Application.UseCases.Datenaenderungen;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels;
using AuswertungPro.Next.UI.ViewModels.Pages;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Deepscan 02.10.2026 (A2): Eine Uebernahme (QGIS, GeoShop, WebGIS) schliesst auf beiden Seiten
/// gleich ab — Ergebnis in der Statuszeile, Projekt als geaendert markiert, Verlauf leer und das
/// Ereignis <c>FelderExternErgaenzt</c>, an dem das offene Formular seine Importwerte neu zeichnet.
/// Vorher meldete die Schachtseite bei QGIS nichts und zeichnete nichts neu, und QGIS liess auf
/// beiden Seiten das Projekt «ungeaendert» (keine Rueckfrage beim Schliessen, kein Autosave).
///
/// Die Seiten laufen echt (Shell, ServiceProvider, QGIS-Leser); die GeoPackage-Datei ist eine
/// kleine, im Test erzeugte SQLite-Datei.
/// </summary>
public sealed class SeitenUebernahmeTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"seiten-qgis-{Guid.NewGuid():N}");
    private readonly ILoggerFactory _loggerFactory = LoggerFactory.Create(_ => { });
    private readonly AppSettings _settings = new() { EnableRestorePoints = false };
    private readonly ServiceProvider _services;
    private readonly ShellViewModel _shell;

    public SeitenUebernahmeTests()
    {
        Directory.CreateDirectory(_dir);
        _services = new ServiceProvider(_settings, new DiagnosticsOptions(),
            _loggerFactory.CreateLogger("test"), _loggerFactory)
        {
            Dialogs = new JaDialoge()
        };
        _shell = new ShellViewModel(_services, new SystemMonitorService(enableHardwareSensorInit: false));
    }

    public void Dispose()
    {
        _shell.Dispose();
        _loggerFactory.Dispose();
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { /* Temp-Rest stoert keinen Test */ }
    }

    [Fact]
    public void Qgis_auf_der_Schachtseite_meldet_markiert_leert_den_Verlauf_und_zeichnet_neu()
    {
        _settings.QgisSchaechteGpkgPath = Gpkg("schaechte.gpkg", "Schaechte",
            ["bw_bezeichnung", "ns_dimension1"], ["80401", "800"]);
        var schacht = new SchachtRecord();
        schacht.Fields["Schachtnummer"] = "80401";
        var projekt = new Project { Name = "QGIS" };
        projekt.SchaechteData.Add(schacht);
        OeffneProjekt(projekt);
        _shell.NavigateTo("Schaechte");
        var seite = Assert.IsType<SchaechtePageViewModel>(_shell.CurrentPage);
        using (_services.DatenaenderungsVerlauf.Erfasse(schacht, "Material"))
            schacht.SetFieldValue("Material", "Beton", FieldSource.Manual, true);
        Assert.True(_services.DatenaenderungsVerlauf.KannRueckgaengig(DatenaenderungsBereich.Schaechte));
        var neuGezeichnet = 0;
        seite.FelderExternErgaenzt += () => neuGezeichnet++;
        _shell.Project.Dirty = false;

        seite.QgisFelderErgaenzenCommand.Execute(null);

        // Das Projekt weiss, dass es geaendert ist: Titelmarke, Rueckfrage beim Schliessen, Autosave.
        Assert.True(_shell.Project.Dirty);

        Assert.Equal("800", schacht.GetFieldValue(FieldKeys.ShaftDimension1Mm));
        Assert.Equal("1 leere Felder aus QGIS ergänzt. Nicht vergessen zu speichern.", seite.LastResult);
        Assert.Equal(1, neuGezeichnet);
        Assert.False(_services.DatenaenderungsVerlauf.KannRueckgaengig(DatenaenderungsBereich.Schaechte));
    }

    /// <summary>Meldung, Verlauf und Ereignis hatte die Haltungsseite schon; neu ist die Aenderungsmarke.</summary>
    [Fact]
    public void Qgis_auf_der_Haltungsseite_meldet_markiert_leert_den_Verlauf_und_zeichnet_neu()
    {
        _settings.QgisHaltungenGpkgPath = Gpkg("leitungen.gpkg", "Leitungen",
            ["ne_bezeichnung", "ha_lichte_hoehe"], ["10001-10002", "300"]);
        var haltung = new HaltungRecord();
        haltung.Fields[FieldKeys.HoldingName] = "10001-10002";
        var projekt = new Project { Name = "QGIS" };
        projekt.Data.Add(haltung);
        OeffneProjekt(projekt);
        _shell.EnterWorkspaceOn("Haltungen");
        var seite = Assert.IsType<DataPageViewModel>(_shell.CurrentPage);
        using (_services.DatenaenderungsVerlauf.Erfasse(haltung, FieldKeys.PipeMaterial))
            haltung.SetFieldValue(FieldKeys.PipeMaterial, "PVC", FieldSource.Manual, true);
        Assert.True(_services.DatenaenderungsVerlauf.KannRueckgaengig(DatenaenderungsBereich.Haltungen));
        var neuGezeichnet = 0;
        seite.FelderExternErgaenzt += () => neuGezeichnet++;
        _shell.Project.Dirty = false;

        seite.QgisFelderErgaenzenCommand.Execute(null);

        // Das Projekt weiss, dass es geaendert ist: Titelmarke, Rueckfrage beim Schliessen, Autosave.
        Assert.True(_shell.Project.Dirty);

        Assert.Equal("300", haltung.GetFieldValue(FieldKeys.NominalDiameterMm));
        Assert.Equal("1 leere Felder aus QGIS ergänzt. Nicht vergessen zu speichern.", seite.SaveStatus);
        Assert.Equal(1, neuGezeichnet);
        Assert.False(_services.DatenaenderungsVerlauf.KannRueckgaengig(DatenaenderungsBereich.Haltungen));
    }

    [Fact]
    public void Abschliessen_markiert_plant_den_Autosave_leert_den_Verlauf_und_meldet_in_dieser_Reihenfolge()
    {
        var haltung = new HaltungRecord();
        haltung.Fields[FieldKeys.HoldingName] = "10001-10002";
        var projekt = new Project { Name = "Abschluss" };
        projekt.Data.Add(haltung);
        OeffneProjekt(projekt);
        using (_services.DatenaenderungsVerlauf.Erfasse(haltung, FieldKeys.PipeMaterial))
            haltung.SetFieldValue(FieldKeys.PipeMaterial, "PVC", FieldSource.Manual, true);
        _shell.Project.Dirty = false;
        var ablauf = new List<string>();

        SeitenUebernahme.Abschliessen(_shell,
            () => ablauf.Add($"autosave dirty={_shell.Project.Dirty}"),
            () => ablauf.Add($"neu zeichnen verlauf={_services.DatenaenderungsVerlauf.KannRueckgaengig(DatenaenderungsBereich.Haltungen)}"));

        Assert.Equal(["autosave dirty=True", "neu zeichnen verlauf=False"], ablauf);
    }

    /// <summary>
    /// Die Uebernahmen mit Fenster (GeoShop, WebGIS) laufen ohne Bildschirm nicht im Test. Deshalb haelt
    /// dieser Waechter fest: Jeder Uebernahmeweg beider Seiten endet in <c>MeldeUebernahme</c>, und
    /// nur der gemeinsame Abschluss markiert und leert dort. Ein eigener, halber Abschluss je Weg war
    /// genau der Weg, auf dem die Seiten auseinanderliefen.
    /// </summary>
    [Fact]
    public void Jeder_Uebernahmeweg_beider_Seiten_endet_im_gemeinsamen_Abschluss()
    {
        var vms = Path.Combine(TestRepoPaths.FindRepositoryRoot(), "src", "AuswertungPro.Next.UI", "ViewModels", "Pages");
        foreach (var seite in new[] { "DataPageViewModel", "SchaechtePageViewModel" })
        {
            foreach (var weg in new[] { "QgisNachfuellen", "KatasterKennungen", "WebGisHolen" })
            {
                var code = File.ReadAllText(Path.Combine(vms, $"{seite}.{weg}.cs"));
                Assert.Contains("MeldeUebernahme", code);
                Assert.DoesNotContain("Verlauf.Leere(", code);
                Assert.DoesNotContain("MarkProjectDirty", code);
            }

            var teile = Directory.GetFiles(vms, $"{seite}*.cs").Select(File.ReadAllText).ToArray();
            Assert.Single(teile, t => t.Contains(
                "SeitenUebernahme.Abschliessen(_shell, ScheduleAutoSave, FelderExternErgaenzt)", StringComparison.Ordinal));
            Assert.DoesNotContain(teile, t => t.Contains("GrundUebernahme", StringComparison.Ordinal));
        }
    }

    private void OeffneProjekt(Project projekt)
    {
        _shell.ReplaceProject(projekt);
        _shell.MarkProjectReady();
    }

    /// <summary>Eine kleine, echte GeoPackage-Datei mit einer Zeile (siehe QgisGpkgBestandLeserTests).</summary>
    private string Gpkg(string dateiname, string tabelle, string[] spalten, string[] zeile)
    {
        var datei = Path.Combine(_dir, dateiname);
        using (var db = new SqliteConnection(new SqliteConnectionStringBuilder
               { DataSource = datei, Mode = SqliteOpenMode.ReadWriteCreate }.ToString()))
        {
            db.Open();
            Fuehre(db, "CREATE TABLE gpkg_contents (table_name TEXT, data_type TEXT)");
            Fuehre(db, $"CREATE TABLE \"{tabelle}\" ({string.Join(", ", spalten.Select(s => $"\"{s}\" TEXT"))})");
            Fuehre(db, $"INSERT INTO gpkg_contents VALUES ('{tabelle}', 'features')");
            Fuehre(db, $"INSERT INTO \"{tabelle}\" VALUES ({string.Join(", ", zeile.Select(w => $"'{w}'"))})");
            db.Close();
        }

        // Microsoft.Data.Sqlite haelt die Verbindung sonst im Pool, und die Datei bleibt gesperrt.
        SqliteConnection.ClearAllPools();
        return datei;
    }

    private static void Fuehre(SqliteConnection db, string sql)
    {
        using var befehl = db.CreateCommand();
        befehl.CommandText = sql;
        befehl.ExecuteNonQuery();
    }

    /// <summary>Bestaetigt jede Rueckfrage; Meldungen bleiben still.</summary>
    private sealed class JaDialoge : IDialogService
    {
        public string? OpenFile(string title, string filter, string? initialDirectory = null) => null;
        public string? SaveFile(string title, string filter, string? defaultExt = null, string? defaultFileName = null) => null;
        public string[] OpenFiles(string title, string filter) => [];
        public string? SelectFolder(string title, string? initialPath = null) => null;
        public void Info(string message, string title = "Hinweis") { }
        public void Warn(string message, string title = "Warnung") { }
        public void Error(string message, string title = "Fehler") => throw new InvalidOperationException(message);
        public bool Confirm(string message, string title = "Bestätigung") => true;
        public bool ConfirmWarn(string message, string title = "Bestätigung", bool defaultNo = true) => true;
        public DialogConfirm ConfirmCancel(string message, string title = "Bestätigung") => DialogConfirm.Yes;
    }
}
