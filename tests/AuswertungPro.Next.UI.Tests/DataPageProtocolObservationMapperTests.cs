using System.Globalization;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.DataPage;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DataPageProtocolObservationMapperTests
{
    [Fact]
    public void Build_deduplicates_primary_damage_lines_and_keeps_quantifications()
    {
        using var _ = new CultureScope(CultureInfo.InvariantCulture);
        var entries = new[]
        {
            new ProtocolEntry
            {
                Code = "BAJA",
                MeterStart = 1.23,
                Beschreibung = "  Versatz\r\nstark  ",
                CodeMeta = new ProtocolEntryCodeMeta
                {
                    Parameters =
                    {
                        ["Quantifizierung1"] = "20",
                        ["vsa.q2"] = "A"
                    }
                }
            },
            new ProtocolEntry
            {
                Code = "baja",
                MeterStart = 1.23,
                Beschreibung = "Doppelter Eintrag"
            }
        };

        var result = DataPageProtocolObservationMapper.Build(entries, existingFindings: null);

        Assert.Equal("1.23m BAJA Versatz stark Q1=20 Q2=A", result.PrimaryDamageText);
    }

    [Fact]
    public void BuildFindings_reuses_existing_media_scores_and_timing_for_matching_finding()
    {
        var timestamp = new DateTime(2026, 5, 29, 8, 30, 0);
        var existing = new[]
        {
            new VsaFinding
            {
                KanalSchadencode = "BAJ",
                MeterStart = 5.03,
                MPEG = "00:01:02",
                FotoPath = "old-photo.jpg",
                Timestamp = timestamp,
                LL = 1.5,
                EZD = 2,
                EZS = 3,
                EZB = 4
            }
        };
        var entries = new[]
        {
            new ProtocolEntry
            {
                Code = "BAJA",
                MeterStart = 5.0,
                Beschreibung = "Rohrverbindung",
                CodeMeta = new ProtocolEntryCodeMeta
                {
                    Parameters = { ["vsa.q1"] = "20" }
                }
            }
        };

        var finding = Assert.Single(DataPageProtocolObservationMapper.BuildFindings(entries, existing));

        Assert.Equal("BAJA", finding.KanalSchadencode);
        Assert.Equal("Rohrverbindung", finding.Raw);
        Assert.Equal(5.0, finding.MeterStart);
        Assert.Equal("20", finding.Quantifizierung1);
        Assert.Equal("00:01:02", finding.MPEG);
        Assert.Equal("old-photo.jpg", finding.FotoPath);
        Assert.Equal(timestamp, finding.Timestamp);
        Assert.Equal(1.5, finding.LL);
        Assert.Equal(2, finding.EZD);
        Assert.Equal(3, finding.EZS);
        Assert.Equal(4, finding.EZB);
    }

    [Fact]
    public void BuildFindings_calculates_length_for_streckenschaden()
    {
        var entries = new[]
        {
            new ProtocolEntry
            {
                Code = "BABBA",
                MeterStart = 2.0,
                MeterEnd = 3.25,
                IsStreckenschaden = true
            }
        };

        var finding = Assert.Single(DataPageProtocolObservationMapper.BuildFindings(entries, existingFindings: null));

        Assert.Equal(1.25, finding.LL);
    }

    private sealed class CultureScope : IDisposable
    {
        private readonly CultureInfo _originalCulture = CultureInfo.CurrentCulture;
        private readonly CultureInfo _originalUiCulture = CultureInfo.CurrentUICulture;

        public CultureScope(CultureInfo culture)
        {
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
        }

        public void Dispose()
        {
            CultureInfo.CurrentCulture = _originalCulture;
            CultureInfo.CurrentUICulture = _originalUiCulture;
        }
    }
}
