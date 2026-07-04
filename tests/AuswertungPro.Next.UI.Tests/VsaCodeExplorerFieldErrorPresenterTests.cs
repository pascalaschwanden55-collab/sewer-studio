using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class VsaCodeExplorerFieldErrorPresenterTests
{
    [Fact]
    public void Build_versteckt_null_fehler_und_leert_text()
    {
        var presentation = VsaCodeExplorerFieldErrorPresenter.Build(null);

        Assert.Equal("", presentation.Text);
        Assert.False(presentation.Show);
    }

    [Fact]
    public void Build_zeigt_vorhandenen_fehlertext()
    {
        var presentation = VsaCodeExplorerFieldErrorPresenter.Build("Wert ist Pflicht");

        Assert.Equal("Wert ist Pflicht", presentation.Text);
        Assert.True(presentation.Show);
    }

    [Fact]
    public void Build_behält_leeren_fehler_als_sichtbaren_zustand()
    {
        var presentation = VsaCodeExplorerFieldErrorPresenter.Build("");

        Assert.Equal("", presentation.Text);
        Assert.True(presentation.Show);
    }
}
