using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import.Xtf;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

public sealed class XtfPrimaryDamageFormatterTests
{
    [Fact]
    public void FormatLine_VerwendetUhrlageNichtAlsMeterstand()
    {
        var finding = new VsaFinding
        {
            KanalSchadencode = "BCA",
            MeterStart = null,
            SchadenlageAnfang = 9
        };

        var result = XtfPrimaryDamageFormatter.FormatLine(finding);

        Assert.StartsWith("BCA", result, StringComparison.Ordinal);
        Assert.DoesNotContain("9.00m", result, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatLines_DedupliziertOhneMeterUnabhaengigVonUhrlage()
    {
        var findings = new[]
        {
            new VsaFinding { KanalSchadencode = "BCA", SchadenlageAnfang = 9 },
            new VsaFinding { KanalSchadencode = "BCA", SchadenlageAnfang = 3 }
        };

        var result = XtfPrimaryDamageFormatter.FormatLines(findings);

        Assert.Single(result.Split('\n', StringSplitOptions.RemoveEmptyEntries));
        Assert.DoesNotContain("9.00m", result, StringComparison.Ordinal);
        Assert.DoesNotContain("3.00m", result, StringComparison.Ordinal);
    }
}
