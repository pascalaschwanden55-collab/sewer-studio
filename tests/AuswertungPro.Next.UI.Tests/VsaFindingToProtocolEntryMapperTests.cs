using System;
using System.Linq;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.DataPage;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Sichert die aus DataPageViewModel extrahierte Abbildung VsaFinding -> ProtocolEntry ab.
/// Die Katalog-Titelaufloesung wird als reiner Resolver-Delegate hereingereicht.
/// </summary>
public sealed class VsaFindingToProtocolEntryMapperTests
{
    private static string? NoTitle(string code) => null;

    [Fact]
    public void BuildEntries_uebernimmt_kernfelder()
    {
        var finding = new VsaFinding
        {
            KanalSchadencode = "BABA",
            Raw = "Riss laengs",
            MeterStart = 1.0,
            MeterEnd = 5.0,
            MPEG = "00:01:30",
            FotoPath = "f.jpg",
        };

        var entry = Single(VsaFindingToProtocolEntryMapper.BuildEntries(new[] { finding }, NoTitle));

        Assert.Equal("BABA", entry.Code);
        Assert.Equal("Riss laengs", entry.Beschreibung);
        Assert.Equal(1.0, entry.MeterStart);
        Assert.Equal(5.0, entry.MeterEnd);
        Assert.True(entry.IsStreckenschaden);
        Assert.Equal("00:01:30", entry.Mpeg);
        Assert.Equal(new TimeSpan(0, 1, 30), entry.Zeit);
        Assert.Contains("f.jpg", entry.FotoPaths);
        Assert.Equal(ProtocolEntrySource.Imported, entry.Source);
    }

    [Fact]
    public void BuildEntries_faellt_auf_schadenlage_meter_zurueck()
    {
        var finding = new VsaFinding
        {
            KanalSchadencode = "BAB",
            Raw = "Riss",
            SchadenlageAnfang = 2.0,
            SchadenlageEnde = 8.0,
        };

        var entry = Single(VsaFindingToProtocolEntryMapper.BuildEntries(new[] { finding }, NoTitle));

        Assert.Equal(2.0, entry.MeterStart);
        Assert.Equal(8.0, entry.MeterEnd);
    }

    [Fact]
    public void BuildEntries_loest_titel_auf_wenn_beschreibung_leer_oder_kurz()
    {
        var finding = new VsaFinding { KanalSchadencode = "BAB", Raw = "BAB" };

        var entry = Single(VsaFindingToProtocolEntryMapper.BuildEntries(
            new[] { finding },
            code => code == "BAB" ? "Riss laengs (Katalog)" : null));

        Assert.Equal("Riss laengs (Katalog)", entry.Beschreibung);
    }

    [Fact]
    public void BuildEntries_nutzt_resolver_nicht_bei_aussagekraeftiger_beschreibung()
    {
        var finding = new VsaFinding { KanalSchadencode = "BAB", Raw = "Langer beschreibender Text" };

        var entry = Single(VsaFindingToProtocolEntryMapper.BuildEntries(
            new[] { finding },
            code => "SOLLTE_NICHT_VERWENDET_WERDEN"));

        Assert.Equal("Langer beschreibender Text", entry.Beschreibung);
    }

    [Fact]
    public void BuildEntries_schreibt_quantifizierung_in_codemeta()
    {
        var finding = new VsaFinding { KanalSchadencode = "BAB", Raw = "Riss", Quantifizierung1 = "10" };

        var entry = Single(VsaFindingToProtocolEntryMapper.BuildEntries(new[] { finding }, NoTitle));

        Assert.NotNull(entry.CodeMeta);
        Assert.Equal("10", entry.CodeMeta!.Parameters["Quantifizierung1"]);
        Assert.Equal("", entry.CodeMeta.Parameters["Quantifizierung2"]);
    }

    [Fact]
    public void BuildEntries_ohne_quantifizierung_setzt_keine_codemeta()
    {
        var finding = new VsaFinding { KanalSchadencode = "BAB", Raw = "Riss" };

        var entry = Single(VsaFindingToProtocolEntryMapper.BuildEntries(new[] { finding }, NoTitle));

        Assert.Null(entry.CodeMeta);
    }

    [Fact]
    public void BuildEntries_ist_kein_streckenschaden_wenn_nur_startmeter()
    {
        var finding = new VsaFinding { KanalSchadencode = "BAB", Raw = "Riss", MeterStart = 3.0 };

        var entry = Single(VsaFindingToProtocolEntryMapper.BuildEntries(new[] { finding }, NoTitle));

        Assert.False(entry.IsStreckenschaden);
    }

    [Fact]
    public void BuildEntries_nimmt_zeit_aus_timestamp_wenn_mpeg_leer()
    {
        var finding = new VsaFinding
        {
            KanalSchadencode = "BAB",
            Raw = "Riss",
            MPEG = null,
            Timestamp = new DateTime(2026, 5, 29, 8, 15, 0),
        };

        var entry = Single(VsaFindingToProtocolEntryMapper.BuildEntries(new[] { finding }, NoTitle));

        Assert.Equal(new TimeSpan(8, 15, 0), entry.Zeit);
    }

    private static ProtocolEntry Single(System.Collections.Generic.IReadOnlyList<ProtocolEntry> entries)
    {
        Assert.Single(entries);
        return entries[0];
    }
}
