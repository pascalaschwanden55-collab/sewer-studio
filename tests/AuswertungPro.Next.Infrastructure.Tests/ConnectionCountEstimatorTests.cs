using System.Collections.Generic;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import.Pdf;
using AuswertungPro.Next.Infrastructure.Vsa;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class ConnectionCountEstimatorTests
{
    [Fact]
    public void EstimateFromRecord_UsesExplicitField_WhenPresent()
    {
        var record = new HaltungRecord();
        record.SetFieldValue("Anschluesse_verpressen", "4", FieldSource.Pdf, userEdited: false);
        record.SetFieldValue(
            "Primaere_Schaeden",
            "BCAEA @7.52m (Anschluss eingespitzt, bei 12 Uhr)",
            FieldSource.Pdf,
            userEdited: false);

        var result = ConnectionCountEstimator.EstimateFromRecord(record);

        Assert.Equal(4, result);
    }

    [Fact]
    public void EstimateFromRecord_UsesExplicitZero_WhenPresent()
    {
        var record = new HaltungRecord();
        record.SetFieldValue("Anschluesse_verpressen", "0", FieldSource.Pdf, userEdited: false);
        record.SetFieldValue(
            "Primaere_Schaeden",
            "BCAEA @7.52m (Anschluss eingespitzt, bei 12 Uhr)",
            FieldSource.Pdf,
            userEdited: false);

        var result = ConnectionCountEstimator.EstimateFromRecord(record);

        Assert.Equal(0, result);
    }

    [Fact]
    public void EstimateFromRecord_DeduplicatesConnectionFindings_ByLocationAndClock()
    {
        var record = new HaltungRecord
        {
            VsaFindings = new List<VsaFinding>
            {
                new()
                {
                    KanalSchadencode = "BCAEA",
                    SchadenlageAnfang = 7.52,
                    Raw = "Anschluss eingespitzt, bei 12 Uhr"
                },
                new()
                {
                    KanalSchadencode = "BAHC",
                    SchadenlageAnfang = 7.53,
                    Raw = "Anschluss unvollstaendig eingebunden, bei 12 Uhr"
                },
                new()
                {
                    KanalSchadencode = "BCAAA",
                    SchadenlageAnfang = 21.40,
                    Raw = "Anschluss mit Formstueck, offen bei 3 Uhr"
                }
            }
        };

        var result = ConnectionCountEstimator.EstimateFromRecord(record);

        Assert.Equal(2, result);
    }

    [Fact]
    public void EstimateFromRecord_UsesPrimaryDamageText_WhenNoFindings()
    {
        var record = new HaltungRecord();
        record.SetFieldValue(
            "Primaere_Schaeden",
            string.Join('\n', new[]
            {
                "BCAEA @2.20m (Anschluss eingespitzt, offen bei 12 Uhr, 120mm hoch)",
                "BAHC @2.20m (Anschluss unvollstaendig eingebunden bei 12 Uhr)",
                "BCAAA @10.56m (Anschluss mit Formstueck, offen bei 3 Uhr, 150mm hoch)"
            }),
            FieldSource.Pdf,
            userEdited: false);

        var result = ConnectionCountEstimator.EstimateFromRecord(record);

        Assert.Equal(2, result);
    }

    [Fact]
    public void EstimateFromRecord_RecognizesBagCodesAndSeiteneinlaufText()
    {
        var record = new HaltungRecord();
        record.SetFieldValue(
            "Primaere_Schaeden",
            string.Join('\n', new[]
            {
                "BAGA @3.40m (Anschluss einragend, bei 2 Uhr)",
                "BBZZ @11.20m (Seiteneinlauf offen, bei 4 Uhr)"
            }),
            FieldSource.Pdf,
            userEdited: false);

        var result = ConnectionCountEstimator.EstimateFromRecord(record);

        Assert.Equal(2, result);
    }

    [Fact]
    public void EstimateFromRecord_SameMeterDifferentConnectionSubcodes_CountsAsOne()
    {
        var record = new HaltungRecord();
        record.SetFieldValue(
            "Primaere_Schaeden",
            string.Join('\n', new[]
            {
                "BCAEA @7.85m (Anschluss eingespitzt, bei 12 Uhr)",
                "BAHC @7.85m (Anschluss unvollstaendig eingebunden, bei 12 Uhr)"
            }),
            FieldSource.Pdf,
            userEdited: false);

        var result = ConnectionCountEstimator.EstimateFromRecord(record);

        Assert.Equal(1, result);
    }

    [Fact]
    public void PdfParser_ExtractsAnschluesseField()
    {
        var parser = new PdfParser();
        var text = "Anschl\u00FCsse verpressen (Stk): 3";

        var fields = parser.ParseFields(text);

        Assert.True(fields.TryGetValue("Anschluesse_verpressen", out var value));
        Assert.Equal("3", value);
    }

    [Fact]
    public void PdfParser_ExtractsAnschluesseField_Zero()
    {
        var parser = new PdfParser();
        var text = "Anschl\u00FCsse verpressen (Stk): 0";

        var fields = parser.ParseFields(text);

        Assert.True(fields.TryGetValue("Anschluesse_verpressen", out var value));
        Assert.Equal("0", value);
    }
}
