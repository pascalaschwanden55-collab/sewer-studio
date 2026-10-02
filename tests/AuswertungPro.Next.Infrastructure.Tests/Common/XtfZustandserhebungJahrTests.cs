using AuswertungPro.Next.Application.Xtf;
using AuswertungPro.Next.Domain.Models;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Common;

/// <summary>
/// Zustandserhebung_Jahr im XTF-Export liest Datum_Jahr nach der gemeinsamen Leseregel
/// (Deepscan 02.10.2026, A4). Der Export bleibt vorsichtig: Ein Jahr aus Freitext wird nicht
/// geraten, nur ein ganzer Wert aus vier Ziffern zaehlt als reines Jahr.
/// </summary>
public sealed class XtfZustandserhebungJahrTests
{
    [Theory]
    [InlineData("2024", "2024")]
    [InlineData("05.03.2024", "2024")]
    [InlineData("5.3.2024", "2024")]
    [InlineData("2024-03-05", "2024")]
    [InlineData("24.09.25", "2025")]
    [InlineData("2024-03-05T10:00:00", "2024")]
    [InlineData("05.03.2024 14:30", "2024")]
    [InlineData("Aufnahmen 2024", null)]
    [InlineData("2024/2025", null)]
    [InlineData("unbekannt", null)]
    [InlineData("1700", null)]
    public void ErgaenzeGemeinsame_liest_Datum_Jahr_nach_der_gemeinsamen_Leseregel(string datum, string? erwartet)
    {
        var felder = new List<KeyValuePair<string, string>>();

        XtfBauwerkFelder.ErgaenzeGemeinsame(
            felder, key => key == FieldKeys.InspectionYear ? datum : null, "H1", []);

        var jahr = felder.Where(f => f.Key == "Zustandserhebung_Jahr").Select(f => f.Value).SingleOrDefault();
        Assert.Equal(erwartet, jahr);
    }
}
