using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class VsaCodeExplorerPhotoAssistantOpenPolicyTests
{
    [Fact]
    public void Resolve_erlaubt_existierendes_foto()
    {
        var decision = VsaCodeExplorerPhotoAssistantOpenPolicy.Resolve(
            ["foto1.png", "foto2.png"],
            photoIndex: 1,
            fileExists: path => path == "foto2.png");

        Assert.True(decision.CanOpen);
        Assert.Equal("foto2.png", decision.PhotoPath);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void Resolve_blockiert_fehlende_oder_nicht_existierende_fotos(int photoIndex)
    {
        var decision = VsaCodeExplorerPhotoAssistantOpenPolicy.Resolve(
            ["", "missing.png"],
            photoIndex,
            fileExists: _ => false);

        Assert.False(decision.CanOpen);
        Assert.Null(decision.PhotoPath);
        Assert.Equal("Kein Foto vorhanden. Bitte zuerst ein Foto aufnehmen.", decision.Message);
        Assert.Equal("PhotoAssistant", decision.Title);
    }
}
