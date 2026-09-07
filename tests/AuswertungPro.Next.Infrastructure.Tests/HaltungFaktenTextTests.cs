using AuswertungPro.Next.Application.UseCases.Uebersicht;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Nova-Fixwelle B3: Ein leeres Eckdatenfeld zeigt einen Gedankenstrich, nie eine nackte
/// Einheit (" m") oder einen einsamen Trenner (" · ").
/// </summary>
public sealed class HaltungFaktenTextTests
{
    [Theory]
    [InlineData("Beton", null, "Beton")]
    [InlineData("30", "m", "30 m")]
    [InlineData("  30  ", "m", "30 m")]
    public void Ein_gefuellter_Wert_bleibt_stehen(string wert, string? einheit, string erwartet)
        => Assert.Equal(erwartet, HaltungFaktenText.Wert(wert, einheit));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Ein_leerer_Wert_wird_zum_Gedankenstrich(string? wert)
    {
        Assert.Equal(HaltungFaktenText.Leer, HaltungFaktenText.Wert(wert));
        Assert.Equal(HaltungFaktenText.Leer, HaltungFaktenText.Wert(wert, "m"));
    }

    [Fact]
    public void Mehrere_Teile_werden_verbunden()
        => Assert.Equal("300 · Kreisprofil", HaltungFaktenText.Zusammen(new[] { "300", "Kreisprofil" }));

    [Fact]
    public void Ein_leerer_Teil_faellt_weg()
        => Assert.Equal("300", HaltungFaktenText.Zusammen(new[] { "300", "  " }));

    [Fact]
    public void Alle_Teile_leer_ergibt_den_Gedankenstrich()
        => Assert.Equal(HaltungFaktenText.Leer, HaltungFaktenText.Zusammen(new string?[] { null, "" }));
}
