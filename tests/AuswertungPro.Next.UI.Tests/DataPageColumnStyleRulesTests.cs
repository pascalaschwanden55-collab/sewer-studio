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

    /// <summary>
    /// Nova-Etappe 2b, Task 1: alle im Brief genannten Zahlenspalten ueber die echten
    /// Feldnamen aus <see cref="FieldKeys"/>. "Tiefe_m" fehlt bewusst — es gibt weder in
    /// FieldKeys noch im FieldCatalog ein solches Feld (siehe Task-1-Bericht).
    /// </summary>
    [Theory]
    [InlineData(FieldKeys.NominalDiameterMm, true)]
    [InlineData(FieldKeys.ClearWidthMm, true)]
    [InlineData(FieldKeys.HoldingLengthMeters, true)]
    [InlineData(FieldKeys.ConstructionYear, true)]
    [InlineData("VSA_Zustandsnote_D", true)]
    [InlineData("VSA_Zustandsnote_S", true)]
    [InlineData("VSA_Zustandsnote_B", true)]
    [InlineData(FieldKeys.LinerRenovationMeters, true)]
    [InlineData(FieldKeys.ConnectionsToGrout, true)]
    [InlineData(FieldKeys.RepairSleeve, true)]
    [InlineData(FieldKeys.LinerEndSleeve, true)]
    [InlineData(FieldKeys.ShortLinerRepair, true)]
    [InlineData("Erneuerung_Neubau_m", true)]
    [InlineData(FieldKeys.Cost, true)]
    [InlineData(FieldKeys.ShaftDimension1Mm, true)]
    [InlineData(FieldKeys.ShaftDimension2Mm, true)]
    public void Alle_Brief_Zahlenspalten_der_Etappe_2b_sind_rechtsbuendig(string feld, bool erwartet)
        => Assert.Equal(erwartet, DataPageColumnStyleRules.IstZahlenspalte(feld));
}
