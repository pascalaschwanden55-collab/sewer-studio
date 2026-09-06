using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.DataPage;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DataPageColumnStyleRulesTests
{
    [Fact]
    public void Nur_der_Haltungsname_ist_die_fette_Namensspalte()
    {
        Assert.True(DataPageColumnStyleRules.IstNamensspalte(FieldKeys.HoldingName));
        Assert.False(DataPageColumnStyleRules.IstNamensspalte(FieldKeys.Street));
    }

    [Theory]
    [InlineData("DN_mm", true)]
    [InlineData("Haltungslaenge_m", true)]
    [InlineData("Kosten", true)]
    [InlineData("VSA_Zustandsnote_D", true)]
    [InlineData("Gefaelle_Promille", true)]
    [InlineData("Baujahr", true)]
    [InlineData("Strasse", false)]
    [InlineData("Zustandsklasse", false)]
    public void Zahlenspalten_sind_die_Mengen_Masse_und_Kosten(string feld, bool erwartet)
        => Assert.Equal(erwartet, DataPageColumnStyleRules.IstZahlenspalte(feld));
}
