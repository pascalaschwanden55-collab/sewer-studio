using System;
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.UI;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels;
using Microsoft.Extensions.Logging;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Auditbefund 12, zweiter Teil (18.09.2026): Waehrend eines langsamen Projektladens
/// blieben Neu, Oeffnen und Projektwechsel bedienbar. Erst dadurch konnte ueberhaupt ein
/// Entwurf entstehen, den das verspaetete Ladeergebnis ersetzte.
/// </summary>
public sealed class ShellProjektladeSperreTests
{
    [Fact]
    public void Waehrend_des_Ladens_sind_Neu_Oeffnen_und_Wechsel_gesperrt()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var services = new ServiceProvider(
            new AppSettings { EnableRestorePoints = false },
            new DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory);
        using var shell = new ShellViewModel(
            services,
            new SystemMonitorService(enableHardwareSensorInit: false));

        Assert.True(shell.NewProjectCommand.CanExecute(null));
        Assert.True(shell.SwitchProjectCommand.CanExecute(null));
        Assert.True(shell.OpenProjectCommand.CanExecute(null));

        using (shell.BeginProjectLoadOperation())
        {
            Assert.False(shell.NewProjectCommand.CanExecute(null), "Neu muss waehrend des Ladens gesperrt sein.");
            Assert.False(shell.SwitchProjectCommand.CanExecute(null), "Projektwechsel muss gesperrt sein.");
            Assert.False(shell.OpenProjectCommand.CanExecute(null), "Oeffnen muss gesperrt sein.");
        }

        Assert.True(shell.NewProjectCommand.CanExecute(null), "Nach dem Laden wieder frei.");
        Assert.True(shell.SwitchProjectCommand.CanExecute(null));
        Assert.True(shell.OpenProjectCommand.CanExecute(null));
    }

    [Fact]
    public void Die_Sperre_wird_auch_bei_einem_Fehler_wieder_freigegeben()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var services = new ServiceProvider(
            new AppSettings { EnableRestorePoints = false },
            new DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory);
        using var shell = new ShellViewModel(
            services,
            new SystemMonitorService(enableHardwareSensorInit: false));

        try
        {
            using (shell.BeginProjectLoadOperation())
            {
                throw new InvalidOperationException("Ladefehler");
            }
        }
        catch (InvalidOperationException)
        {
            // erwartet
        }

        Assert.True(shell.NewProjectCommand.CanExecute(null), "Nach einem Fehler darf nichts gesperrt bleiben.");
    }
}
