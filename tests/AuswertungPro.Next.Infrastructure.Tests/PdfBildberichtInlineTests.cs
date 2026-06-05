using AuswertungPro.Next.Infrastructure.Ai.Training.Services;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Sichert den Parser fuer das IBAK-2025-"Haltungsbildbericht"-Layout:
/// 2-spaltig in einer Zeile, "Foto NNN Zustand CODE Entf. in Flie?r. METER m".
/// ("¦" = das vom Font-Encoding erzeugte Zeichen fuer ss/ß.)
/// </summary>
public sealed class PdfBildberichtInlineTests
{
    [Fact]
    public void ParseInlineBildbericht_extracts_codes_and_meters_two_columns()
    {
        var text =
            "Foto 082 Zustand BCD Entf. in Flie¦r. 0.00 m  Foto 083 Zustand BDB Entf. in Flie¦r. 0.00 m\n" +
            "Foto 084 Zustand BAF.B.E Entf. in Flie¦r. 2.10 m  Foto 089 Zustand BCE Entf. in Flie¦r. 36.10 m";

        var entries = PdfProtocolExtractor.ParseInlineBildbericht(text);

        Assert.Contains(entries, e => e.VsaCode == "BCD" && e.MeterStart == 0.0);
        Assert.Contains(entries, e => e.VsaCode == "BAFBE" && e.MeterStart == 2.1);   // Punkte entfernt
        Assert.Contains(entries, e => e.VsaCode == "BCE" && e.MeterStart == 36.1);
    }

    [Fact]
    public void ParseInlineBildbericht_handles_normal_fliessr_spelling()
    {
        var entries = PdfProtocolExtractor.ParseInlineBildbericht(
            "Foto 1 Zustand BCD Entf. in Fließr. 5.00 m");
        Assert.Contains(entries, e => e.VsaCode == "BCD" && e.MeterStart == 5.0);
    }

    [Fact]
    public void ParseInlineBildbericht_ignores_unknown_codes()
    {
        Assert.Empty(PdfProtocolExtractor.ParseInlineBildbericht(
            "Foto 1 Zustand XYZ Entf. in Flie¦r. 1.00 m"));
    }
}
