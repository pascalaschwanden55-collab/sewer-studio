using AuswertungPro.Next.Domain.Models.Dossiers;

using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Dossiers;

/// <summary>
/// Prueft die zentrale "ist eine Eigentuemerzeile leer"-Regel. Bisher gab es
/// diese Regel zweimal im Code (Editor-Fenster und Migration); jetzt gibt es
/// nur noch <see cref="DossierOwnerRow.HasContent"/>.
/// </summary>
public sealed class DossierOwnerRowTests
{
    [Fact]
    public void Eine_ganz_leere_Zeile_hat_keinen_Inhalt()
    {
        var row = new DossierOwnerRow();

        Assert.False(row.HasContent);
    }

    [Fact]
    public void Nur_Leerraum_in_allen_Feldern_zaehlt_nicht_als_Inhalt()
    {
        var row = new DossierOwnerRow
        {
            HouseNumber = "   ",
            ParcelNumber = "\t",
            Name = "  \n ",
            Phone = "",
            Mail = "   ",
            Occupancy = ""
        };

        Assert.False(row.HasContent);
    }

    [Theory]
    [InlineData("3", "", "", "", "", "")]
    [InlineData("", "170", "", "", "", "")]
    [InlineData("", "", "Martin Muster", "", "", "")]
    [InlineData("", "", "", "079 858 53 74", "", "")]
    [InlineData("", "", "", "", "markus@example.ch", "")]
    [InlineData("", "", "", "", "", "Einfamilienhaus")]
    public void Ein_gefuelltes_Feld_reicht_fuer_Inhalt(
        string houseNumber,
        string parcelNumber,
        string name,
        string phone,
        string mail,
        string occupancy)
    {
        var row = new DossierOwnerRow
        {
            HouseNumber = houseNumber,
            ParcelNumber = parcelNumber,
            Name = name,
            Phone = phone,
            Mail = mail,
            Occupancy = occupancy
        };

        Assert.True(row.HasContent);
    }
}
