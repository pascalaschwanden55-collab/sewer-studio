using AuswertungPro.Next.UI.DataPage;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DataPageSaveStatusControllerTests
{
    [Fact]
    public void Show_ersetzt_leere_meldung_durch_default_und_startet_timer_neu()
    {
        var calls = new List<string>();
        var status = "";
        var visible = false;

        DataPageSaveStatusController.Show(
            "  ",
            value => status = value,
            value => visible = value,
            () => calls.Add("stop"),
            () => calls.Add("start"));

        Assert.Equal("Gespeichert", status);
        Assert.True(visible);
        Assert.Equal(new[] { "stop", "start" }, calls);
    }

    [Fact]
    public void Show_setzt_text_und_zeigt_banner()
    {
        var status = "";
        var visible = false;

        DataPageSaveStatusController.Show(
            "Automatisch gespeichert",
            value => status = value,
            value => visible = value,
            () => { },
            () => { });

        Assert.Equal("Automatisch gespeichert", status);
        Assert.True(visible);
    }

    [Fact]
    public void Hide_stoppt_timer_und_blendet_banner_aus()
    {
        var calls = new List<string>();
        var visible = true;

        DataPageSaveStatusController.Hide(
            () => calls.Add("stop"),
            value => visible = value);

        Assert.Equal(new[] { "stop" }, calls);
        Assert.False(visible);
    }
}
