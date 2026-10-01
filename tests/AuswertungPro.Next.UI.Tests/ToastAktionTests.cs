using System.IO;
using AuswertungPro.Next.UI.Services;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Ein Erfolgs-Toast darf einen Link tragen ("Ordner öffnen"), der die Aktion ausloest.
/// Ohne Link verhaelt sich alles wie bisher.
/// </summary>
public sealed class ToastAktionTests
{
    [Fact]
    public void Show_mit_Aktion_traegt_Text_und_Aktion_am_sichtbaren_Toast()
    {
        var logik = new ToastQueueLogic();
        var ausgeloest = false;

        logik.Show("Haltungen exportiert", ToastSeverity.Success, nowMs: 0,
            aktionText: "Ordner öffnen", aktion: () => ausgeloest = true);

        var item = Assert.Single(logik.Visible);
        Assert.Equal("Ordner öffnen", item.AktionText);
        Assert.True(item.HatAktion);
        item.Aktion!();
        Assert.True(ausgeloest);
    }

    [Fact]
    public void Show_ohne_Aktion_bleibt_wie_bisher()
    {
        var logik = new ToastQueueLogic();
        logik.Show("Projekt gespeichert", ToastSeverity.Success, nowMs: 0);

        var item = Assert.Single(logik.Visible);
        Assert.Null(item.AktionText);
        Assert.False(item.HatAktion);
    }

    [Fact]
    public void Service_reicht_Aktion_an_die_Senke_weiter()
    {
        var dienst = new ToastService();
        string? gesehen = null;
        string? aktionText = null;
        dienst.AttachSink((message, _, aktion, _) =>
        {
            gesehen = message;
            aktionText = aktion;
        });

        dienst.Success("Schächte exportiert", "Ordner öffnen", () => { });

        Assert.Equal("Schächte exportiert", gesehen);
        Assert.Equal("Ordner öffnen", aktionText);
    }

    [Fact]
    public void Der_Toast_zeigt_den_Link_und_verdrahtet_den_Klick()
    {
        var xaml = File.ReadAllText(RepoFile("src", "AuswertungPro.Next.UI", "Controls", "ToastHost.xaml"));

        Assert.Contains("{Binding AktionText}", xaml, StringComparison.Ordinal);
        Assert.Contains("{Binding HatAktion, Converter={StaticResource BoolToVis}}", xaml, StringComparison.Ordinal);
        Assert.Contains("Click=\"ToastAktion_Click\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Style=\"{StaticResource LinkButtonStyle}\"", xaml, StringComparison.Ordinal);
    }
}
