using System.Globalization;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Views.Pages.Schachtansicht;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SchachtLaufnummerConverterTests
{
    [Theory]
    [InlineData("NR")]
    [InlineData("NR.")]
    [InlineData("Nr.")]
    public void Zeigt_die_laufende_Nummer_statt_der_Schachtbezeichnung(string feld)
    {
        var record = new SchachtRecord();
        record.SetFieldValue("Schachtnummer", "80421");
        record.SetFieldValue(feld, "12");

        var text = new SchachtLaufnummerConverter().Convert(
            [record, record.Fields], typeof(string), null!, CultureInfo.InvariantCulture);

        Assert.Equal("12", text);
    }

    [Fact]
    public void Ohne_laufende_Nummer_bleibt_die_Anzeige_leer()
    {
        var record = new SchachtRecord();
        record.SetFieldValue("Schachtnummer", "80421");

        var text = new SchachtLaufnummerConverter().Convert(
            [record, record.Fields], typeof(string), null!, CultureInfo.InvariantCulture);

        Assert.Equal(string.Empty, text);
    }
}
