using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.DataPage;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Nova-Fixwelle 2b (P3): Startbreiten der drei Textspalten aus dem Prototyp v2.
/// Reine Regel, kein WPF.
/// </summary>
public sealed class NovaSpaltenbreitenTests
{
    [Theory]
    [InlineData(FieldKeys.HoldingName, 150d)]
    [InlineData("Schachtnummer", 150d)]
    [InlineData(FieldKeys.Street, 120d)]
    [InlineData(FieldKeys.PipeMaterial, 100d)]
    [InlineData("Material", 100d)]
    public void Die_drei_Prototypspalten_haben_ihre_Startbreite(string feld, double erwartet)
        => Assert.Equal(erwartet, NovaSpaltenbreiten.Startbreite(feld));

    [Theory]
    [InlineData(FieldKeys.NominalDiameterMm)]
    [InlineData(FieldKeys.ConditionClass)]
    [InlineData("")]
    [InlineData(null)]
    public void Jede_andere_Spalte_behaelt_ihre_Kopfbreite(string? feld)
        => Assert.Null(NovaSpaltenbreiten.Startbreite(feld));

    /// <summary>
    /// Schachtfelder heissen nach der Kopfzeile der Excel-Vorlage; mit der Faltung findet die
    /// Regel sie auch in abweichender Schreibweise.
    /// </summary>
    [Theory]
    [InlineData("schachtnummer", 150d)]
    [InlineData("STRASSE", 120d)]
    [InlineData("material", 100d)]
    public void Gefaltet_gilt_dieselbe_Breite(string feld, double erwartet)
        => Assert.Equal(erwartet, NovaSpaltenbreiten.Startbreite(feld, SchachtFeldnamen.Falte));

    [Fact]
    public void Ohne_Faltung_gilt_der_reine_Namensvergleich()
        => Assert.Null(NovaSpaltenbreiten.Startbreite("schachtnummer"));
}
