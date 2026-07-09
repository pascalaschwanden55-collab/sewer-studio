using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.ViewModels.Pages;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SchachtSanierungPflichtfeldValidatorTests
{
    [Fact]
    public void MissingFields_meldet_beide_felder_wenn_leer()
    {
        var record = new SchachtRecord();

        var missing = SchachtSanierungPflichtfeldValidator.MissingFields(record);

        Assert.Equal(new[] { "Sanieren Ja/Nein", "Ausgefuehrt durch" }, missing);
    }

    [Fact]
    public void MissingFields_akzeptiert_aliasfelder_mit_umlaut()
    {
        var record = new SchachtRecord();
        record.SetFieldValue("Ja/Nein", "Ja");
        record.SetFieldValue("Ausgeführt durch", "Baumeister");

        var missing = SchachtSanierungPflichtfeldValidator.MissingFields(record);

        Assert.Empty(missing);
    }
}
