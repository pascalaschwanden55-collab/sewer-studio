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
/// Deepscan 02.10.2026 (A2): «Leere Felder aus QGIS ergänzen» muss auf beiden Seiten gleich
/// abschliessen — Ergebnis in der Statuszeile, Verlauf leeren und das Ereignis
/// <c>FelderExternErgaenzt</c>, an dem das offene Formular seine Importwerte neu zeichnet.
/// Vorher meldete die Schachtseite nichts und zeichnete nichts neu.
///
/// Die Seiten laufen echt (Shell, ServiceProvider, QGIS-Leser); die GeoPackage-Datei ist eine
/// kleine, im Test erzeugte SQLite-Datei.
/// </summary>
public sealed class SeitenUebernahmeQgisTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"seiten-qgis-{Guid.NewGuid():N}");
    private readonly ILoggerFactory _loggerFactory = LoggerFactory.Create(_ => { });
    private readonly AppSettings _settings = new() { EnableRestorePoints = false };
    private readonly ServiceProvider _services;
    private readonly ShellViewModel _shell;

    public SeitenUebernahmeQgisTests()
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
    public void Schachtseite_meldet_das_Ergebnis_leert_den_Verlauf_und_zeichnet_die_Anzeige_neu()
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

        seite.QgisFelderErgaenzenCommand.Execute(null);

        Assert.Equal("800", schacht.GetFieldValue(FieldKeys.ShaftDimension1Mm));
        Assert.Equal("1 leere Felder aus QGIS ergänzt. Nicht vergessen zu speichern.", seite.LastResult);
        Assert.Equal(1, neuGezeichnet);
        Assert.False(_services.DatenaenderungsVerlauf.KannRueckgaengig(DatenaenderungsBereich.Schaechte));
    }

    /// <summary>Das Vorbild: Die Haltungsseite tat das schon. Der Test haelt es fest.</summary>
    [Fact]
    public void Haltungsseite_meldet_das_Ergebnis_leert_den_Verlauf_und_zeichnet_die_Anzeige_neu()
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

        seite.QgisFelderErgaenzenCommand.Execute(null);

        Assert.Equal("300", haltung.GetFieldValue(FieldKeys.NominalDiameterMm));
        Assert.Equal("1 leere Felder aus QGIS ergänzt. Nicht vergessen zu speichern.", seite.SaveStatus);
        Assert.Equal(1, neuGezeichnet);
        Assert.False(_services.DatenaenderungsVerlauf.KannRueckgaengig(DatenaenderungsBereich.Haltungen));
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
