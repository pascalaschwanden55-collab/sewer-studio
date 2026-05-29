using System.Linq;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.DataPage;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Sichert die aus DataPageViewModel extrahierte Finding-Text-Normalisierung ab:
/// Meter/Zeit aus Roh-Text parsen und Findings deduplizieren. Der UI-State-Teil
/// (Dirty/Grid-Refresh) bleibt im ViewModel und ist hier nicht Gegenstand.
/// </summary>
public sealed class VsaFindingNormalizerTests
{
    private static HaltungRecord RecordWith(params VsaFinding[] findings)
        => new() { VsaFindings = findings.ToList() };

    [Fact]
    public void Normalize_parst_startmeter_aus_rohtext()
    {
        var record = RecordWith(new VsaFinding { KanalSchadencode = "BAB", Raw = "@12.5m" });

        var changed = VsaFindingNormalizer.Normalize(record);

        Assert.True(changed);
        Assert.Equal(12.5, record.VsaFindings[0].MeterStart);
    }

    [Fact]
    public void Normalize_parst_zweiten_meter_als_endmeter()
    {
        var record = RecordWith(new VsaFinding { KanalSchadencode = "BAB", Raw = "Riss 12.5m bis 18.0m" });

        VsaFindingNormalizer.Normalize(record);

        Assert.Equal(12.5, record.VsaFindings[0].MeterStart);
        Assert.Equal(18.0, record.VsaFindings[0].MeterEnd);
    }

    [Fact]
    public void Normalize_parst_zeit_in_mpeg()
    {
        var record = RecordWith(new VsaFinding { KanalSchadencode = "BAB", Raw = "Aufnahme 00:01:30" });

        VsaFindingNormalizer.Normalize(record);

        Assert.Equal("00:01:30", record.VsaFindings[0].MPEG);
    }

    [Fact]
    public void Normalize_dedupliziert_gleiche_findings()
    {
        var record = RecordWith(
            new VsaFinding { KanalSchadencode = "BAB", Raw = "@5.0m" },
            new VsaFinding { KanalSchadencode = "BAB", Raw = "@5.0m" });

        var changed = VsaFindingNormalizer.Normalize(record);

        Assert.True(changed);
        Assert.Single(record.VsaFindings);
    }

    [Fact]
    public void Normalize_liefert_false_ohne_findings()
    {
        Assert.False(VsaFindingNormalizer.Normalize(RecordWith()));
    }

    [Fact]
    public void Normalize_liefert_false_wenn_nichts_zu_aendern()
    {
        var record = RecordWith(new VsaFinding
        {
            KanalSchadencode = "BAB",
            Raw = "",
            MeterStart = 5.0,
            MPEG = "00:00:10",
        });

        Assert.False(VsaFindingNormalizer.Normalize(record));
    }
}
