using System;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.UI.Views.Pages;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Ein Befehl mit "async void" fuehrt jede unbehandelte Ausnahme direkt in den
/// Programmabsturz. Das Gesamtaudit hat genau dieses Muster im Programm
/// bereits einmal beseitigt; hier darf es nicht neu entstehen.
/// </summary>
public sealed class EinfacherBefehlTests
{
    [Fact]
    public void Eine_Ausnahme_beendet_nicht_das_Programm_sondern_wird_gemeldet()
    {
        Exception? gemeldet = null;
        var befehl = new EinfacherBefehl(
            () => throw new InvalidOperationException("absichtlich"),
            ex => gemeldet = ex);

        befehl.Execute(null);

        Assert.NotNull(gemeldet);
        Assert.Equal("absichtlich", gemeldet!.Message);
    }

    [Fact]
    public void Nach_einer_Ausnahme_ist_der_Befehl_wieder_bedienbar()
    {
        var befehl = new EinfacherBefehl(
            () => throw new InvalidOperationException("absichtlich"),
            _ => { });

        befehl.Execute(null);

        Assert.True(befehl.CanExecute(null));
    }

    [Fact]
    public async Task Waehrend_ein_Nachschlag_laeuft_ist_auch_jeder_andere_gesperrt()
    {
        // Zwei Felder, zwei Befehle - aber nur eine Abfrage zur Zeit. Sonst
        // laufen zwei Grundbuchabfragen gleichzeitig und zaehlen doppelt
        // gegen die Drosselung des Kantons.
        var tor = new NachschlagTor();
        var laeuft = new TaskCompletionSource();
        var freigabe = new TaskCompletionSource();

        var erster = new EinfacherBefehl(
            async () => { laeuft.SetResult(); await freigabe.Task; }, _ => { }, tor);
        var zweiter = new EinfacherBefehl(() => Task.CompletedTask, _ => { }, tor);

        Assert.True(zweiter.CanExecute(null));

        erster.Execute(null);
        await laeuft.Task;

        Assert.False(zweiter.CanExecute(null));

        freigabe.SetResult();
        await Task.Delay(50);

        Assert.True(zweiter.CanExecute(null));
    }
}
