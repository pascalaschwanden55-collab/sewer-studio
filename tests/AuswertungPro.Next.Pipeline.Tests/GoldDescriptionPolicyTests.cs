using AuswertungPro.Next.Application.Ai.Training;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class GoldDescriptionPolicyTests
{
    [Theory]
    [InlineData("Rohranfang — Lage und Ausmass ergaenzen")]
    [InlineData("Rohranfang — Lage und Ausmass ergänzen")]
    [InlineData("Rohranfang — Lage und Ausmaß ergaenzen")]
    [InlineData("Rohranfang — Lage und Ausmaß ergänzen")]
    public void IsPlaceholder_erkennt_alle_unterstuetzten_Schreibweisen(string text)
        => Assert.True(GoldDescriptionPolicy.IsPlaceholder(text));

    [Fact]
    public void IsKnowledgeTextReady_erlaubt_fertigen_Fachtext()
        => Assert.True(GoldDescriptionPolicy.IsKnowledgeTextReady(
            "Offener Rohranfang bei 6 Uhr, vollstaendig sichtbar."));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("zu kurz")]
    [InlineData("Riss — Ausmass ergaenzen")]
    public void IsKnowledgeTextReady_sperrt_unfertigen_Text(string? text)
        => Assert.False(GoldDescriptionPolicy.IsKnowledgeTextReady(text));
}
