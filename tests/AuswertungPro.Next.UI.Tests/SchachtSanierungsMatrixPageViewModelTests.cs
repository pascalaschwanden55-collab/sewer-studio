using System;
using System.IO;
using System.Linq;
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels;
using AuswertungPro.Next.UI.ViewModels.Pages;
using Microsoft.Extensions.Logging;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Kernlogik der Schacht-Matrix-Seite (ohne WPF/Dialoge — nur der Happy-Path, der keine
/// MessageBox ausloest). Nutzt den echten Katalog/Vorlagen aus Config (im Test-Output).
/// </summary>
public sealed class SchachtSanierungsMatrixPageViewModelTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory().FullName;
    private readonly ILoggerFactory _lf = LoggerFactory.Create(_ => { });

    private (ShellViewModel shell, ServiceProvider sp) CreateShell(
        params (string? nummer, string funktion, string resultat)[] schaechte)
    {
        var projectPath = Path.Combine(_dir, "Projekt.json");
        var settings = new AppSettings { LastProjectPath = projectPath };
        var sp = new ServiceProvider(settings, new DiagnosticsOptions(), _lf.CreateLogger("test"), _lf);
        var shell = new ShellViewModel(sp, new SystemMonitorService(enableHardwareSensorInit: false));
        foreach (var s in schaechte)
        {
            var rec = new SchachtRecord();
            if (s.nummer is not null)
                rec.SetFieldValue("Schachtnummer", s.nummer);
            rec.SetFieldValue("Funktion", s.funktion);
            rec.SetFieldValue("Pruefungsresultat", s.resultat);
            shell.Project.SchaechteData.Add(rec);
        }
        return (shell, sp);
    }

    [Fact]
    public void Zeilen_werden_je_Schachtnummer_gebaut_und_leere_uebersprungen()
    {
        var (shell, sp) = CreateShell(
            ("KS 1", "Kontrollschacht", "Zustandsklasse 2"),
            ("KS 2", "Einlaufschacht", ""),
            (null, "ohne Nummer", ""));

        var vm = new SchachtSanierungsMatrixPageViewModel(shell, sp);

        Assert.Equal(2, vm.Rows.Count);
        Assert.Equal("KS 1", vm.Rows[0].Schachtnummer);
        Assert.Equal("Kontrollschacht", vm.Rows[0].Funktion);
        Assert.Equal("Zustandsklasse 2", vm.Rows[0].Resultat);
        // Erste Option ist immer "— keine —" (Id null).
        Assert.Null(vm.MeasureOptions[0].Id);
        Assert.Contains(vm.MeasureOptions, o => o.Id == "SCHACHT_PAUSCHAL");
    }

    [Fact]
    public void Massnahme_speichern_legt_getrennte_Datei_an_und_neu_laden_stellt_Auswahl_wieder_her()
    {
        var (shell, sp) = CreateShell(("KS 60191", "Kontrollschacht", "ZK 3"));

        var vm = new SchachtSanierungsMatrixPageViewModel(shell, sp);
        var row = vm.Rows.Single();
        row.SelectedMeasure = vm.MeasureOptions.First(o => o.Id == "SCHACHT_PAUSCHAL");
        row.Menge = 1m;
        vm.SpeichernCommand.Execute(null);

        // Eigene Datei, kollidiert NICHT mit dem Haltungs-Store.
        Assert.True(File.Exists(Path.Combine(_dir, "costs", "schacht_costs.json")));
        Assert.False(File.Exists(Path.Combine(_dir, "costs", "costs.json")));

        // Frischer VM lädt den gespeicherten Stand.
        var vm2 = new SchachtSanierungsMatrixPageViewModel(shell, sp);
        var row2 = vm2.Rows.Single();
        Assert.Equal("SCHACHT_PAUSCHAL", row2.SelectedMeasure?.Id);
    }

    [Fact]
    public void Auswahl_keine_entfernt_den_Zeilen_Eintrag_wieder()
    {
        var (shell, sp) = CreateShell(("KS 1", "", ""));

        var vm = new SchachtSanierungsMatrixPageViewModel(shell, sp);
        var row = vm.Rows.Single();

        row.SelectedMeasure = vm.MeasureOptions.First(o => o.Id == "SCHACHT_PAUSCHAL");
        Assert.NotNull(row.SelectedMeasure?.Id);

        row.SelectedMeasure = vm.MeasureOptions.First(o => o.Id is null);
        Assert.Equal(0m, row.Total);
        Assert.Equal("", row.Hinweis);
    }

    [Fact]
    public void RahmenDeckel_Massnahme_setzt_AbdeckungStk_auf_1()
    {
        var (shell, sp) = CreateShell(("KS 1", "", ""));
        var vm = new SchachtSanierungsMatrixPageViewModel(shell, sp);
        var row = vm.Rows.Single();

        row.SelectedMeasure = vm.MeasureOptions.First(o => o.Id == "SCHACHT_RAHMEN_DECKEL");

        Assert.Equal("1", row.Record.GetFieldValue("Abdeckung Stk."));
    }

    public void Dispose()
    {
        _lf.Dispose();
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }
}
