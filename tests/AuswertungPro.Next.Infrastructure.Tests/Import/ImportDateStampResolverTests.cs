using System.Globalization;
using AuswertungPro.Next.Infrastructure.Import.Common;
using AuswertungPro.Next.Infrastructure.Tests.Common;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

/// <summary>
/// Der Dateistempel der Verteilung liest Datum_Jahr nach der gemeinsamen Leseregel
/// (Deepscan 02.10.2026, A4) und erst danach ein JJJJMMTT aus den Medienpfaden.
/// </summary>
public sealed class ImportDateStampResolverTests
{
    [Theory]
    [MemberData(nameof(HaltungFeldwerteTests.Datumsbeispiele), MemberType = typeof(HaltungFeldwerteTests))]
    public void Resolve_liest_Datum_Jahr_nach_der_gemeinsamen_Leseregel(string datum, string? erwartetIso, bool nurJahr)
    {
        _ = nurJahr;
        var erwartet = erwartetIso is null
            ? ImportDateStampResolver.Unbekannt
            : HaltungFeldwerteTests.Iso(erwartetIso).ToString("yyyyMMdd", CultureInfo.InvariantCulture);

        Assert.Equal(erwartet, ImportDateStampResolver.Resolve(datum));
    }

    [Theory]
    [InlineData("de-CH")]
    [InlineData("en-US")]
    public void Resolve_haengt_nicht_von_der_Windows_Kultur_ab(string kultur)
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(kultur);
            Assert.Equal("20240305", ImportDateStampResolver.Resolve("05.03.2024"));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void Resolve_nimmt_den_Pfadstempel_nur_ohne_lesbares_Datum()
    {
        Assert.Equal("20230915", ImportDateStampResolver.Resolve(null, @"C:\P\20230915_H1.mp4"));
        Assert.Equal("20240305", ImportDateStampResolver.Resolve("05.03.2024", @"C:\P\20230915_H1.mp4"));
        Assert.Equal(ImportDateStampResolver.Unbekannt, ImportDateStampResolver.Resolve("", @"C:\P\H1.mp4"));
    }
}
