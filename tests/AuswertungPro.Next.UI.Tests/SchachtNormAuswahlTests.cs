using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.DataPage;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Entscheid Pascal: Die Auswahlmenues duerfen exakt die AWU-Begriffe fuehren -
/// Haltungen und Schaechte. Funktion und Material des Schachts waren bis dahin
/// Freitext und konnten deshalb alles enthalten.
/// </summary>
public sealed class SchachtNormAuswahlTests
{
    [Fact]
    public void Die_Urner_Schachtformen_stehen_vollstaendig_im_Dropdown()
    {
        Assert.Equal(
            ["", "Unbekannt", "Rund", "Oval", "Quadratisch", "Rechteckig", "Vieleckig"],
            SchachtformVokabular.Auswahl);

        Assert.True(GridDropdownFieldPolicy.TryResolve(FieldKeys.ShaftShape, out var spec));
        Assert.Equal("SchachtformOptions", spec.ItemsSourcePath);
        Assert.False(spec.AllowFreeText);
    }

    [Theory]
    [InlineData("rund", "Rund")]
    [InlineData("circular", "Rund")]
    [InlineData("oval", "Oval")]
    [InlineData("square", "Quadratisch")]
    [InlineData("rectangular", "Rechteckig")]
    [InlineData("polygonal", "Vieleckig")]
    public void Importbegriffe_werden_auf_die_Urner_Schachtformen_angehoben(
        string roh, string erwartet)
        => Assert.Equal(erwartet, SchachtformVokabular.Normalisieren(roh));

    [Fact]
    public void Form_und_beide_Innenmasse_werden_auch_ohne_neue_Vorlage_ergänzt()
    {
        var spalten = new List<string> { "Schachtnummer", "Form", "Dimension 1 mm" };

        SchaechteColumnPolicy.ErgaenzeFormUndMasse(spalten);

        Assert.Single(spalten, s => s == "Form");
        Assert.DoesNotContain(FieldKeys.ShaftShape, spalten);
        Assert.Single(spalten, s => s == FieldKeys.ShaftDimension1Mm);
        Assert.Single(spalten, s => s == FieldKeys.ShaftDimension2Mm);
        Assert.Equal("Grösstes Innenmass mm", SchaechteColumnPolicy.GetDisplayHeader(FieldKeys.ShaftDimension1Mm));
        Assert.Equal("Kleinstes Innenmass mm", SchaechteColumnPolicy.GetDisplayHeader(FieldKeys.ShaftDimension2Mm));
    }

    [Theory]
    [InlineData("Funktion", "SchachtFunktionOptions")]
    [InlineData("Material", "SchachtMaterialOptions")]
    public void Funktion_und_Material_sind_gefuehrte_Auswahlfelder(string feld, string quelle)
    {
        Assert.True(GridDropdownFieldPolicy.TryResolve(feld, out var spec));
        Assert.Equal(quelle, spec.ItemsSourcePath);
        Assert.False(spec.AllowFreeText);   // sonst kaeme wieder alles hinein
        Assert.False(spec.Managed);         // die Norm ist nicht vom Benutzer erweiterbar
    }

    [Fact]
    public void Die_Funktionsliste_fuehrt_die_Begriffe_der_Norm()
    {
        var liste = SchachtFunktionVokabular.Auswahl;

        Assert.Contains("Kontrollschacht", liste);
        Assert.Contains("Schlammsammler", liste);
        Assert.Contains("Einlaufschacht", liste);
        Assert.Contains("Dachwasserschacht", liste);
        Assert.Contains("", liste);

        // Kein Eintrag darf einen Wert liefern, den die Modelldatei nicht kennt.
        var gueltig = new[]
        {
            "Absturzbauwerk","andere","Be_Entlueftung","Behandlungsanlage","Bodenablauf",
            "Dachwasserschacht","Einlaufschacht","Entwaesserungsrinne",
            "Entwaesserungsrinne_mit_Schlammsack","Fettabscheider","Geleiseschacht",
            "Kombischacht","Kontroll_Einsteigschacht","Oelabscheider","Pumpwerk",
            "Regenueberlauf","Schlammsammler","Schwimmstoffabscheider","Spuelschacht",
            "Trennbauwerk","unbekannt","Vorbehandlungsanlage"
        };
        foreach (var eintrag in liste.Where(a => a.Length > 0))
        {
            var norm = SchachtFunktionVokabular.NachNorm(eintrag);
            Assert.True(norm is not null && gueltig.Contains(norm),
                $"'{eintrag}' liefert '{norm}' - das ist kein Wert der Modelldatei.");
        }
    }

    [Fact]
    public void Die_Materialliste_fuehrt_nur_die_vier_Normwerte_als_Ziel()
    {
        var gueltig = new[] { "andere", "Beton", "Kunststoff", "unbekannt" };
        foreach (var eintrag in SchachtMaterialVokabular.Auswahl.Where(a => a.Length > 0))
        {
            var norm = SchachtMaterialVokabular.NachNorm(eintrag);
            Assert.True(norm is not null && gueltig.Contains(norm),
                $"'{eintrag}' liefert '{norm}'.");
        }
    }
}
