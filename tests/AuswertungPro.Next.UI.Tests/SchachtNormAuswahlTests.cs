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
