using AuswertungPro.Next.UI.Views.Windows;

using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DossierExactPreviewStateTests
{
    [Fact]
    public void Fehlgeschlagene_Aktualisierung_sperrt_altes_Blatt_und_Uebernehmen()
    {
        var state = new DossierExactPreviewState();
        var ersteAusgabe = state.RequestOutputRefresh();
        Assert.True(state.TryCompleteOutput(ersteAusgabe, success: true));
        Assert.False(state.CanAccept);
        var ersteSeite = state.BeginPageRender();
        Assert.True(state.TryCompletePage(ersteSeite, success: true));
        Assert.True(state.CanAccept);
        Assert.True(state.CanInteractWithPage);

        var neueAusgabe = state.RequestOutputRefresh();

        Assert.False(state.CanAccept);
        Assert.False(state.CanInteractWithPage);
        Assert.True(state.TryCompleteOutput(neueAusgabe, success: false));
        Assert.False(state.CanAccept);
        Assert.False(state.CanInteractWithPage);
        Assert.False(state.NeedsOutputRefresh);
    }

    [Fact]
    public void Seitenwechsel_sperrt_alte_Klickziele_bis_die_neue_Seite_fertig_ist()
    {
        var state = new DossierExactPreviewState();
        var ausgabe = state.RequestOutputRefresh();
        Assert.True(state.TryCompleteOutput(ausgabe, success: true));
        var alteSeite = state.BeginPageRender();
        Assert.True(state.TryCompletePage(alteSeite, success: true));

        var ersteAnfrage = state.BeginPageRender();
        var letzteAnfrage = state.BeginPageRender();

        Assert.False(state.CanAccept);
        Assert.False(state.CanInteractWithPage);
        Assert.False(state.TryCompletePage(ersteAnfrage, success: true));
        Assert.False(state.CanInteractWithPage);
        Assert.True(state.TryCompletePage(letzteAnfrage, success: true));
        Assert.True(state.CanAccept);
        Assert.True(state.CanInteractWithPage);
    }

    [Fact]
    public void Rasterfehler_sperrt_Uebernehmen_und_Klickziele()
    {
        var state = new DossierExactPreviewState();
        var ausgabe = state.RequestOutputRefresh();
        Assert.True(state.TryCompleteOutput(ausgabe, success: true));
        var seite = state.BeginPageRender();

        Assert.True(state.TryCompletePage(seite, success: false));

        Assert.False(state.CanAccept);
        Assert.False(state.CanInteractWithPage);
    }

    [Fact]
    public void Verspaetetes_altes_Ausgabeergebnis_gibt_Vorschau_nicht_frei()
    {
        var state = new DossierExactPreviewState();
        var alteAusgabe = state.RequestOutputRefresh();
        var neueAusgabe = state.RequestOutputRefresh();

        Assert.False(state.TryCompleteOutput(alteAusgabe, success: true));
        Assert.False(state.CanAccept);
        Assert.True(state.NeedsOutputRefresh);

        Assert.True(state.TryCompleteOutput(neueAusgabe, success: true));
        Assert.False(state.CanAccept);
    }
}
