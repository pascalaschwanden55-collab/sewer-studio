using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class VsaCodeExplorerColumnLayoutPresenterTests
{
    [Fact]
    public void Build_blendet_char2_spalte_aus_wenn_keine_tiles_vorhanden_sind()
    {
        var presentation = VsaCodeExplorerColumnLayoutPresenter.Build(char2TileCount: 0);

        Assert.False(presentation.ShowChar2Column);
    }

    [Fact]
    public void Build_zeigt_char2_spalte_wenn_tiles_vorhanden_sind()
    {
        var presentation = VsaCodeExplorerColumnLayoutPresenter.Build(char2TileCount: 1);

        Assert.True(presentation.ShowChar2Column);
    }
}
