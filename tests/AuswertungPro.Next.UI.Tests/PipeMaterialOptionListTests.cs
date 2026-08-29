using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.DataPage;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PipeMaterialOptionListTests
{
    [Fact]
    public void Feste_Katalogwerte_bleiben_vollstaendig_und_stehen_zuerst()
    {
        var fest = FieldCatalog.GetComboItems(FieldKeys.PipeMaterial);

        var liste = PipeMaterialOptionList.Compose(new[] { "Spezialbetonrohr" });

        Assert.Equal(fest, liste.Take(fest.Count));
        Assert.Equal("Spezialbetonrohr", liste[^1]);
    }

    [Fact]
    public void Eigene_Werte_werden_angehaengt()
    {
        var liste = PipeMaterialOptionList.Compose(new[] { "Spezialbetonrohr", "Stahl" });

        Assert.Contains("Spezialbetonrohr", liste);
        Assert.Contains("Stahl", liste);
        Assert.Contains("Beton", liste);
    }

    [Fact]
    public void Ohne_eigene_Werte_bleibt_nur_der_Katalog()
    {
        var fest = FieldCatalog.GetComboItems(FieldKeys.PipeMaterial);

        Assert.Equal(fest, PipeMaterialOptionList.Compose(null));
        Assert.Equal(fest, PipeMaterialOptionList.Compose(Array.Empty<string>()));
    }

    [Theory]
    [InlineData("beton")]
    [InlineData("  Beton  ")]
    [InlineData("BETON")]
    public void Ein_bereits_fester_Wert_wird_nicht_doppelt_aufgenommen(string eingabe)
    {
        var liste = PipeMaterialOptionList.Compose(new[] { eingabe });

        Assert.Equal(FieldCatalog.GetComboItems(FieldKeys.PipeMaterial), liste);
    }

    [Fact]
    public void Doppelte_eigene_Werte_erscheinen_nur_einmal()
    {
        var liste = PipeMaterialOptionList.Compose(new[] { "Stahl", "stahl", " Stahl " });

        Assert.Single(liste.Where(x => string.Equals(x, "Stahl", StringComparison.OrdinalIgnoreCase)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Leere_eigene_Eintraege_werden_verworfen(string eingabe)
    {
        var liste = PipeMaterialOptionList.Compose(new[] { eingabe });

        Assert.Equal(FieldCatalog.GetComboItems(FieldKeys.PipeMaterial), liste);
    }

    [Fact]
    public void Eigene_Werte_werden_getrimmt_gespeichert()
    {
        var liste = PipeMaterialOptionList.Compose(new[] { "  Spezialbetonrohr  " });

        Assert.Contains("Spezialbetonrohr", liste);
    }

    [Fact]
    public void ExtractCustom_liefert_niemals_feste_Katalogwerte()
    {
        // "Stahl" war hier frueher das Beispiel eines eigenen Werts. Seit dem
        // MaterialVokabular ist es ein Normwert (im AWU-Kantonsexport 134 Haltungen)
        // und damit ein fester Katalogwert - deshalb jetzt "Blaustein".
        var alle = PipeMaterialOptionList.Compose(new[] { "Spezialbetonrohr", "Blaustein" });

        var eigene = PipeMaterialOptionList.ExtractCustom(alle);

        Assert.Equal(new[] { "Spezialbetonrohr", "Blaustein" }, eigene);
    }

    [Fact]
    public void ExtractCustom_behaelt_die_Reihenfolge_und_entdoppelt()
    {
        var eigene = PipeMaterialOptionList.ExtractCustom(
            new[] { "Blaustein", "Beton", "Spezialbetonrohr", "blaustein", "" });

        Assert.Equal(new[] { "Blaustein", "Spezialbetonrohr" }, eigene);
    }

    [Fact]
    public void ExtractCustom_ohne_Eingabe_ist_leer()
    {
        Assert.Empty(PipeMaterialOptionList.ExtractCustom(null));
    }

    [Theory]
    [InlineData("Beton", true)]
    [InlineData(" steinzeug ", true)]
    [InlineData("Spezialbetonrohr", false)]
    [InlineData(null, false)]
    public void IsFixed_erkennt_die_gesperrten_Katalogwerte(string? wert, bool erwartet)
    {
        Assert.Equal(erwartet, PipeMaterialOptionList.IsFixed(wert));
    }

    [Fact]
    public void Der_leere_Eintrag_bleibt_genau_einmal_erhalten()
    {
        var liste = PipeMaterialOptionList.Compose(new[] { "Stahl" });

        Assert.Single(liste.Where(string.IsNullOrEmpty));
    }
}
