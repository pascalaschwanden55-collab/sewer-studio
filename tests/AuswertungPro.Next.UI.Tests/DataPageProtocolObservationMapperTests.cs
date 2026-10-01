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

    [Fact]
    public void BuildFindings_haelt_meter_und_uhrlage_getrennt()
    {
        var entries = new[]
        {
            new ProtocolEntry
            {
                Code = "BCAAA",
                MeterStart = 2.62136,
                MeterEnd = 4.5,
                CodeMeta = new ProtocolEntryCodeMeta
                {
                    Parameters =
                    {
                        ["vsa.uhr.von"] = "2.62136",
                        ["ClockPos1"] = "9",
                        ["vsa.uhr.bis"] = "3"
                    }
                }
            }
        };

        var finding = Assert.Single(DataPageProtocolObservationMapper.BuildFindings(entries, existingFindings: null));

        Assert.Equal(2.62136, finding.MeterStart);
        Assert.Equal(4.5, finding.MeterEnd);
        Assert.Equal(9, finding.SchadenlageAnfang);
        Assert.Equal(3, finding.SchadenlageEnde);
    }

    [Fact]
    public void BuildFindings_verwendet_clockpos_statt_altem_ganzzahligen_meter_spiegel()
    {
        var entries = new[]
        {
            new ProtocolEntry
            {
                Code = "BCAAA",
                MeterStart = 2,
                MeterEnd = 6,
                CodeMeta = new ProtocolEntryCodeMeta
                {
                    Parameters =
                    {
                        ["vsa.uhr.von"] = "2",
                        ["vsa.uhr.bis"] = "6",
                        ["ClockPos1"] = "9",
                        ["ClockPos2"] = "3"
                    }
                }
            }
        };

        var finding = Assert.Single(DataPageProtocolObservationMapper.BuildFindings(entries, existingFindings: null));

        Assert.Equal(9, finding.SchadenlageAnfang);
        Assert.Equal(3, finding.SchadenlageEnde);
    }

    [Fact]
    public void BuildFindings_leitet_uhrlage_nur_aus_explizitem_befundtext_ab()
    {
        var entries = new[]
        {
            new ProtocolEntry
            {
                Code = "BAB",
                MeterStart = 5,
                Beschreibung = "Riss von 4 Uhr bis 8 Uhr",
                CodeMeta = new ProtocolEntryCodeMeta
                {
                    Parameters =
                    {
                        ["Quantifizierung1"] = "9",
                        ["Quantifizierung2"] = "3"
                    }
                }
            }
        };

        var finding = Assert.Single(DataPageProtocolObservationMapper.BuildFindings(entries, existingFindings: null));

        Assert.Equal(4, finding.SchadenlageAnfang);
        Assert.Equal(8, finding.SchadenlageEnde);
    }

    [Fact]
    public void BuildFindings_verwendet_schadenlage_nicht_fuer_den_metervergleich()
    {
        var existing = new[]
        {
            new VsaFinding
            {
                KanalSchadencode = "BAB",
                SchadenlageAnfang = 5,
                MPEG = "00:00:05"
            }
        };
        var entries = new[]
        {
            new ProtocolEntry
            {
                Code = "BAB",
                MeterStart = 5
            }
        };

        var finding = Assert.Single(DataPageProtocolObservationMapper.BuildFindings(entries, existing));

        Assert.Null(finding.MPEG);
    }

    [Fact]
    public void BuildFindings_uebernimmt_nur_eindeutige_template_uhrlage()
    {
        var existing = new[]
        {
            new VsaFinding
            {
                KanalSchadencode = "BAB",
                MeterStart = 2,
                MeterEnd = 6,
                SchadenlageAnfang = 2,
                SchadenlageEnde = 6
            }
        };
        var entries = new[]
        {
            new ProtocolEntry
            {
                Code = "BAB",
                MeterStart = 2,
                MeterEnd = 6
            }
        };

        var finding = Assert.Single(DataPageProtocolObservationMapper.BuildFindings(entries, existing));

        Assert.Null(finding.SchadenlageAnfang);
        Assert.Null(finding.SchadenlageEnde);
    }

    [Fact]
    public void BuildFindings_uebernimmt_gueltige_template_uhrlage()
    {
        var existing = new[]
        {
            new VsaFinding
            {
                KanalSchadencode = "BAB",
                MeterStart = 2,
                MeterEnd = 6,
                SchadenlageAnfang = 4,
                SchadenlageEnde = 9
            }
        };
        var entries = new[]
        {
            new ProtocolEntry
            {
                Code = "BAB",
                MeterStart = 2,
                MeterEnd = 6
            }
        };

        var finding = Assert.Single(DataPageProtocolObservationMapper.BuildFindings(entries, existing));

        Assert.Equal(4, finding.SchadenlageAnfang);
        Assert.Equal(9, finding.SchadenlageEnde);
    }

    [Theory]
    [InlineData("BCE")]
    [InlineData("BCD")]
    public void BuildFindings_leitet_beim_rohrende_keine_uhr_aus_text_ab(string code)
    {
        var entries = new[]
        {
            new ProtocolEntry
            {
                Code = code,
                MeterStart = 2,
                Beschreibung = "Rohrende mit Anschluss bei 9 Uhr"
            }
        };

        var finding = Assert.Single(DataPageProtocolObservationMapper.BuildFindings(entries, existingFindings: null));

        Assert.Null(finding.SchadenlageAnfang);
        Assert.Null(finding.SchadenlageEnde);
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
