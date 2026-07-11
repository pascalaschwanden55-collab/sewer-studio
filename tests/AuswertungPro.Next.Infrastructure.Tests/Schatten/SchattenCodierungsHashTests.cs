using AuswertungPro.Next.Application.Schatten;
using AuswertungPro.Next.Domain.Models;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Schatten;

public sealed class SchattenCodierungsHashTests
{
    private static HaltungRecord MitFindings(params (string Code, string? Q1, double? Meter)[] findings)
    {
        var r = new HaltungRecord();
        r.SetFieldValue("Haltungslaenge_m", "57.00", FieldSource.Xtf, false);
        r.SetFieldValue("DN_mm", "300", FieldSource.Xtf, false);
        foreach (var (code, q1, meter) in findings)
            r.VsaFindings.Add(new VsaFinding { KanalSchadencode = code, Quantifizierung1 = q1, MeterStart = meter });
        return r;
    }

    [Fact]
    public void GleicheFindings_AndereReihenfolge_GleicherHash()
    {
        var a = MitFindings(("BAB", "5", 12.4), ("BBC", null, 3.0));
        var b = MitFindings(("BBC", null, 3.0), ("BAB", "5", 12.4));

        Assert.Equal(SchattenCodierungsHash.Compute(a), SchattenCodierungsHash.Compute(b));
    }

    [Theory]
    [InlineData("Code")]     // anderer Schadenscode
    [InlineData("Q1")]       // andere Quantifizierung
    [InlineData("Laenge")]   // anderer Kontext (fliesst in Bewertung ein)
    public void Aenderung_AendertHash(string was)
    {
        var basis = MitFindings(("BAB", "5", 12.4));
        var geaendert = MitFindings(("BAB", "5", 12.4));
        switch (was)
        {
            case "Code": geaendert.VsaFindings[0].KanalSchadencode = "BAC"; break;
            case "Q1": geaendert.VsaFindings[0].Quantifizierung1 = "9"; break;
            case "Laenge": geaendert.SetFieldValue("Haltungslaenge_m", "88.00", FieldSource.Xtf, false); break;
        }

        Assert.NotEqual(SchattenCodierungsHash.Compute(basis), SchattenCodierungsHash.Compute(geaendert));
    }
}
